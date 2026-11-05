using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OnlineLearning.Application.Common.Helpers;
using OnlineLearning.Domain.Constants;
using OnlineLearning.Domain.Entities;
using OnlineLearning.Domain.Enums;

namespace OnlineLearning.Infrastructure.Persistence.Seed;

/// <summary>
/// Áp dụng migration và tạo dữ liệu: vai trò, danh mục, tài khoản demo, khóa học mẫu, đơn hàng, đánh giá, mã giảm giá.
/// Mật khẩu demo đọc từ cấu hình "Seed:DemoPassword" (User Secrets) — không hard-code.
/// </summary>
public static class DbSeeder
{
    public static async Task SeedAsync(IServiceProvider services, CancellationToken ct = default, bool migrate = true)
    {
        using var scope = services.CreateScope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<AppDbContext>();
        var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger(nameof(DbSeeder));
        var config = sp.GetRequiredService<IConfiguration>();

        if (DependencyInjection.IsSqliteDemo(config)) await db.Database.EnsureCreatedAsync(ct);
        else if (migrate) await db.Database.MigrateAsync(ct);

        await SeedRolesAsync(sp.GetRequiredService<RoleManager<AppRole>>());
        await SeedCategoriesAsync(db, ct);

        var demoPassword = config["Seed:DemoPassword"];
        if (string.IsNullOrWhiteSpace(demoPassword))
        {
            logger.LogWarning("Bỏ qua tài khoản và khóa học demo: chưa cấu hình Seed:DemoPassword.");
            return;
        }

        var userManager = sp.GetRequiredService<UserManager<AppUser>>();
        await EnsureUserAsync(userManager, "admin@eduviet.local", "Quản trị viên", demoPassword, [Roles.Admin], logger);
        foreach (var i in SampleData.Instructors)
        {
            var u = await EnsureUserAsync(userManager, i.Email, i.FullName, demoPassword, [Roles.Instructor, Roles.Student], logger);
            if (u is not null && u.Headline is null)
            {
                u.Headline = i.Headline;
                u.Bio = i.Bio;
                await userManager.UpdateAsync(u);
            }
        }
        foreach (var s in SampleData.Students)
            await EnsureUserAsync(userManager, s.Email, s.FullName, demoPassword, [Roles.Student], logger);

        if (!await db.Courses.AnyAsync(ct))
        {
            await SeedCoursesAsync(db, ct);
            await SeedOrdersAndReviewsAsync(db, ct);
            await SeedCouponsAsync(db, ct);
            logger.LogInformation("Đã tạo {Count} khóa học mẫu.", SampleData.Courses.Length);
        }

        if (!await db.Quizzes.AnyAsync(ct) && await db.Courses.AnyAsync(ct))
        {
            await SeedQuizzesAndProgressAsync(db, ct);
            logger.LogInformation("Đã tạo bài kiểm tra và tiến độ học mẫu.");
        }

        logger.LogInformation("Seed dữ liệu hoàn tất.");
    }

    /// <summary>Mỗi khóa học có một bài kiểm tra cuối khóa; học viên mẫu đã học một phần để dashboard có dữ liệu.</summary>
    private static async Task SeedQuizzesAndProgressAsync(AppDbContext db, CancellationToken ct)
    {
        var courses = await db.Courses.Where(c => c.Status == CourseStatus.Published)
            .Include(c => c.Sections).ThenInclude(s => s.Lessons)
            .ToListAsync(ct);

        foreach (var course in courses)
        {
            var lessonTitles = course.Sections.OrderBy(s => s.SortOrder).SelectMany(s => s.Lessons.OrderBy(l => l.SortOrder)).Select(l => l.Title).ToList();
            var quiz = new Quiz
            {
                CourseId = course.Id,
                Title = "Bài kiểm tra cuối khóa",
                Description = $"Kiểm tra kiến thức tổng hợp của khóa \"{course.Title}\". Cần đạt 70% để hoàn thành khóa học.",
                PassingScore = 70,
                MaxAttempts = 3,
                TimeLimitMinutes = 15,
                IsRequired = true,
                IsFinalExam = true,
            };
            var order = 0;
            foreach (var title in lessonTitles.Take(4))
            {
                var wrong = lessonTitles.Where(t => t != title).Take(3).ToList();
                var options = new List<(string, bool)> { (title, true) };
                options.AddRange(wrong.Select(w => (w, false)));
                // Đưa đáp án đúng vào vị trí khác nhau
                options = options.Skip(order % options.Count).Concat(options.Take(order % options.Count)).ToList();
                var question = new Question
                {
                    Content = $"Bài học nào thuộc chương \"{course.Sections.First(s => s.Lessons.Any(l => l.Title == title)).Title}\" và có tên đúng như sau: \"{title}\"?",
                    Type = QuestionType.SingleChoice,
                    Points = 1,
                    Explanation = $"Đáp án đúng là \"{title}\".",
                    SortOrder = order,
                };
                var o = 0;
                foreach (var (content, correct) in options)
                    question.Options.Add(new AnswerOption { Content = content, IsCorrect = correct, SortOrder = o++ });
                quiz.Questions.Add(question);
                order++;
            }
            quiz.Questions.Add(new Question
            {
                Content = "Khóa học có cấp chứng chỉ khi hoàn thành bài học bắt buộc và đạt bài kiểm tra cuối khóa.",
                Type = QuestionType.TrueFalse,
                Points = 1,
                Explanation = "Chứng chỉ được cấp khi hoàn thành 100% bài học bắt buộc và đạt các bài kiểm tra bắt buộc.",
                SortOrder = order,
                Options = { new AnswerOption { Content = "Đúng", IsCorrect = true, SortOrder = 0 }, new AnswerOption { Content = "Sai", SortOrder = 1 } },
            });
            db.Quizzes.Add(quiz);
        }

        // Tiến độ học mẫu: mỗi lượt ghi danh hoàn thành một phần bài học
        var enrollments = await db.Enrollments.Include(e => e.Course).ThenInclude(c => c.Sections).ThenInclude(s => s.Lessons).ToListAsync(ct);
        var now = DateTime.UtcNow;
        var seq = 0;
        foreach (var e in enrollments)
        {
            var lessons = e.Course.Sections.OrderBy(s => s.SortOrder).SelectMany(s => s.Lessons.OrderBy(l => l.SortOrder)).ToList();
            var done = (seq * 3) % (lessons.Count + 1);
            foreach (var lesson in lessons.Take(done))
                db.LessonProgress.Add(new LessonProgress { EnrollmentId = e.Id, LessonId = lesson.Id, IsCompleted = true, CompletedAt = now.AddDays(-seq % 20), UpdatedAt = now });
            e.ProgressPercent = lessons.Count == 0 ? 0 : Math.Round(done * 100m / lessons.Count, 2);
            e.LastLessonId = done > 0 ? lessons[Math.Min(done, lessons.Count - 1)].Id : null;
            e.LastAccessedAt = done > 0 ? now.AddDays(-(seq % 10)) : null;
            seq++;
        }

        await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Production: chỉ tạo vai trò và danh mục mặc định (không có tài khoản/khóa học demo).
    /// Tài khoản Admin đầu tiên tạo bằng cấu hình Seed:AdminEmail + Seed:AdminPassword (nếu có).
    /// </summary>
    public static async Task SeedRolesAndCategoriesAsync(IServiceProvider services, CancellationToken ct = default)
    {
        using var scope = services.CreateScope();
        var sp = scope.ServiceProvider;
        await SeedRolesAsync(sp.GetRequiredService<RoleManager<AppRole>>());
        await SeedCategoriesAsync(sp.GetRequiredService<AppDbContext>(), ct);

        var config = sp.GetRequiredService<IConfiguration>();
        var adminEmail = config["Seed:AdminEmail"];
        var adminPassword = config["Seed:AdminPassword"];
        if (!string.IsNullOrWhiteSpace(adminEmail) && !string.IsNullOrWhiteSpace(adminPassword))
        {
            var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger(nameof(DbSeeder));
            await EnsureUserAsync(sp.GetRequiredService<UserManager<AppUser>>(), adminEmail, "Quản trị viên", adminPassword, [Roles.Admin], logger);
        }
    }

    private static async Task SeedRolesAsync(RoleManager<AppRole> roleManager)
    {
        var roles = new Dictionary<string, string>
        {
            [Roles.Admin] = "Quản trị hệ thống",
            [Roles.Instructor] = "Giảng viên",
            [Roles.Student] = "Học viên",
        };
        foreach (var (name, description) in roles)
        {
            if (!await roleManager.RoleExistsAsync(name))
                await roleManager.CreateAsync(new AppRole(name, description));
        }
    }

    private static async Task SeedCategoriesAsync(AppDbContext db, CancellationToken ct)
    {
        if (await db.Categories.AnyAsync(ct)) return;

        var categories = new (string Name, string Slug, string Description)[]
        {
            ("Lập trình", "lap-trinh", "Web, mobile, backend và các ngôn ngữ lập trình phổ biến."),
            ("Thiết kế", "thiet-ke", "UI/UX, đồ họa và công cụ thiết kế."),
            ("Kinh doanh", "kinh-doanh", "Khởi nghiệp, quản trị và bán hàng."),
            ("Marketing", "marketing", "Digital marketing, nội dung và thương hiệu."),
            ("Ngoại ngữ", "ngoai-ngu", "Tiếng Anh, tiếng Nhật, tiếng Hàn."),
            ("Tin học văn phòng", "tin-hoc-van-phong", "Excel, Word, PowerPoint."),
            ("Kỹ năng mềm", "ky-nang-mem", "Giao tiếp, thuyết trình, quản lý thời gian."),
            ("Dữ liệu & AI", "du-lieu-ai", "Phân tích dữ liệu, học máy và trí tuệ nhân tạo."),
        };
        var order = 0;
        foreach (var (name, slug, description) in categories)
            db.Categories.Add(new Category { Name = name, Slug = slug, Description = description, DisplayOrder = order++ });
        await db.SaveChangesAsync(ct);
    }

    private static async Task SeedCoursesAsync(AppDbContext db, CancellationToken ct)
    {
        var categories = await db.Categories.ToDictionaryAsync(c => c.Slug, ct);
        var users = await db.Users.ToDictionaryAsync(u => u.Email!, ct);
        var now = DateTime.UtcNow;
        var index = 0;

        foreach (var s in SampleData.Courses)
        {
            var slug = SlugHelper.Slugify(s.Title);
            var course = new Course
            {
                Title = s.Title,
                Slug = slug,
                CategoryId = categories[s.CategorySlug].Id,
                InstructorId = users[s.InstructorEmail].Id,
                ShortDescription = s.ShortDescription,
                Description = $"<p>{s.ShortDescription}</p><p>Khóa học được thiết kế theo lộ trình từ dễ đến khó, " +
                              "mỗi chương đều có bài tập thực hành và tài liệu tải về.</p>",
                Outcomes = JsonSerializer.Serialize(s.Outcomes),
                Requirements = JsonSerializer.Serialize(s.Requirements),
                ThumbnailUrl = $"https://picsum.photos/seed/{slug}/800/450",
                PreviewVideoUrl = SampleData.SampleVideo,
                Level = s.Level,
                Price = s.Price,
                DiscountPrice = s.DiscountPrice,
                IsFeatured = s.Featured,
                Status = CourseStatus.Published,
                SubmittedAt = now.AddDays(-200 + index * 15),
                PublishedAt = now.AddDays(-198 + index * 15),
                CreatedAt = now.AddDays(-210 + index * 15),
            };

            var sectionOrder = 0;
            foreach (var (sectionTitle, lessons) in s.Sections)
            {
                var section = new CourseSection { Title = sectionTitle, SortOrder = sectionOrder };
                var lessonOrder = 0;
                foreach (var lessonTitle in lessons)
                {
                    var lesson = new Lesson
                    {
                        Title = lessonTitle,
                        Type = LessonType.Video,
                        VideoUrl = SampleData.SampleVideo,
                        DurationSeconds = 420 + (lessonOrder * 137 + sectionOrder * 211) % 900,
                        Content = $"<p>Ghi chú bài học: {lessonTitle}.</p>",
                        SortOrder = lessonOrder,
                        IsPreview = sectionOrder == 0 && lessonOrder < 2,
                        IsRequired = true,
                    };
                    if (lessonOrder == 0)
                    {
                        lesson.Resources.Add(new LessonResource
                        {
                            FileName = $"tai-lieu-{SlugHelper.Slugify(sectionTitle)}.pdf",
                            FileUrl = "https://www.w3.org/WAI/ER/tests/xhtml/testfiles/resources/pdf/dummy.pdf",
                            ContentType = "application/pdf",
                            FileSize = 13264,
                        });
                    }
                    section.Lessons.Add(lesson);
                    lessonOrder++;
                }
                course.Sections.Add(section);
                sectionOrder++;
            }

            course.TotalLessons = course.Sections.Sum(x => x.Lessons.Count);
            course.TotalDurationSeconds = course.Sections.Sum(x => x.Lessons.Sum(l => l.DurationSeconds));
            db.Courses.Add(course);
            index++;
        }

        // Một khóa học đang chờ duyệt để demo màn hình kiểm duyệt của Admin
        var pending = new Course
        {
            Title = "Docker và Kubernetes cho Developer",
            Slug = "docker-va-kubernetes-cho-developer",
            CategoryId = categories["lap-trinh"].Id,
            InstructorId = users["giangvien@eduviet.local"].Id,
            ShortDescription = "Đóng gói và vận hành ứng dụng với container.",
            Description = "<p>Đóng gói ứng dụng .NET và React bằng Docker, triển khai với Kubernetes.</p>",
            ThumbnailUrl = "https://picsum.photos/seed/docker-k8s/800/450",
            Level = CourseLevel.Advanced,
            Price = 1_590_000,
            Status = CourseStatus.Pending,
            SubmittedAt = now.AddDays(-1),
            Sections =
            {
                new CourseSection
                {
                    Title = "Docker cơ bản", SortOrder = 0,
                    Lessons = { new Lesson { Title = "Container là gì", Type = LessonType.Video, VideoUrl = SampleData.SampleVideo, DurationSeconds = 600 } },
                },
            },
            TotalLessons = 1,
            TotalDurationSeconds = 600,
        };
        db.Courses.Add(pending);

        await db.SaveChangesAsync(ct);
    }

    private static async Task SeedOrdersAndReviewsAsync(AppDbContext db, CancellationToken ct)
    {
        var courses = await db.Courses.Where(c => c.Status == CourseStatus.Published).OrderBy(c => c.CreatedAt).ToListAsync(ct);
        var studentEmails = SampleData.Students.Select(s => s.Email).ToList();
        var students = await db.Users.Where(u => studentEmails.Contains(u.Email!)).ToListAsync(ct);
        students = studentEmails.Select(e => students.First(s => s.Email == e)).ToList();
        var now = DateTime.UtcNow;
        var seq = 0;

        for (var ci = 0; ci < courses.Count; ci++)
        {
            var course = courses[ci];
            // Mỗi khóa có 2–6 học viên đã mua, rải đều trong 10 tháng gần đây để biểu đồ doanh thu có dữ liệu
            var buyers = students.Skip(ci % 3).Take(2 + ci % 5).ToList();
            for (var bi = 0; bi < buyers.Count; bi++)
            {
                var student = buyers[bi];
                var paidAt = now.AddDays(-(seq * 37 % 300) - 1);
                var price = course.EffectivePrice;
                var order = new Order
                {
                    OrderCode = $"EDV{paidAt:yyMMdd}S{seq:D5}",
                    UserId = student.Id,
                    SubTotal = price,
                    DiscountAmount = 0,
                    TotalAmount = price,
                    Status = OrderStatus.Paid,
                    CreatedAt = paidAt.AddMinutes(-3),
                    ExpiresAt = paidAt.AddMinutes(27),
                    PaidAt = paidAt,
                    Items =
                    {
                        new OrderItem
                        {
                            CourseId = course.Id, CourseTitle = course.Title, InstructorId = course.InstructorId,
                            OriginalPrice = course.Price, Price = price, DiscountAllocated = 0, FinalPrice = price,
                        },
                    },
                    Payments =
                    {
                        new Payment
                        {
                            Provider = price == 0 ? PaymentProvider.Free : PaymentProvider.VNPay,
                            Amount = price,
                            Status = PaymentStatus.Success,
                            TransactionRef = $"SEED{seq:D6}",
                            ProviderTransactionId = price == 0 ? null : $"1400{seq:D6}",
                            BankCode = price == 0 ? null : "NCB",
                            ResponseCode = "00",
                            PaidAt = paidAt,
                            CreatedAt = paidAt.AddMinutes(-2),
                        },
                    },
                };
                db.Orders.Add(order);
                db.Enrollments.Add(new Enrollment
                {
                    UserId = student.Id, CourseId = course.Id, Order = order, EnrolledAt = paidAt, Status = EnrollmentStatus.Active,
                });
                course.EnrollmentCount++;

                if (bi < 4)
                {
                    var (comment, rating) = SampleData.ReviewTexts[(ci + bi) % SampleData.ReviewTexts.Length];
                    db.Reviews.Add(new Review
                    {
                        CourseId = course.Id, UserId = student.Id, Rating = rating, Comment = comment,
                        CreatedAt = paidAt.AddDays(5),
                        InstructorReply = bi == 0 ? "Cảm ơn bạn đã tin tưởng khóa học! Chúc bạn học tốt." : null,
                        RepliedAt = bi == 0 ? paidAt.AddDays(6) : null,
                    });
                }
                seq++;
            }
        }

        await db.SaveChangesAsync(ct);

        // Cập nhật điểm đánh giá trung bình
        var stats = await db.Reviews.GroupBy(r => r.CourseId)
            .Select(g => new { CourseId = g.Key, Count = g.Count(), Ratings = g.Select(r => (int)r.Rating).ToList() })
            .ToListAsync(ct);
        foreach (var c in courses)
        {
            var s = stats.FirstOrDefault(x => x.CourseId == c.Id);
            c.ReviewCount = s?.Count ?? 0;
            c.AverageRating = s is null ? 0 : Math.Round((decimal)s.Ratings.Average(), 2);
        }
        await db.SaveChangesAsync(ct);
    }

    private static async Task SeedCouponsAsync(AppDbContext db, CancellationToken ct)
    {
        if (await db.Coupons.AnyAsync(ct)) return;
        var now = DateTime.UtcNow;
        db.Coupons.AddRange(
            new Coupon
            {
                Code = "CHAOBANMOI", Description = "Giảm 10% cho học viên mới, tối đa 100.000đ",
                DiscountType = DiscountType.Percentage, DiscountValue = 10, MaxDiscountAmount = 100_000,
                PerUserLimit = 1, StartsAt = now.AddDays(-30), ExpiresAt = now.AddYears(1), IsActive = true,
            },
            new Coupon
            {
                Code = "GIAM50K", Description = "Giảm 50.000đ cho đơn từ 300.000đ",
                DiscountType = DiscountType.FixedAmount, DiscountValue = 50_000, MinOrderAmount = 300_000,
                UsageLimit = 500, PerUserLimit = 2, StartsAt = now.AddDays(-30), ExpiresAt = now.AddMonths(6), IsActive = true,
            },
            new Coupon
            {
                Code = "MIENPHI100", Description = "Giảm 100% — dùng để demo đơn 0đ không qua cổng thanh toán",
                DiscountType = DiscountType.Percentage, DiscountValue = 100,
                UsageLimit = 100, PerUserLimit = 1, StartsAt = now.AddDays(-1), ExpiresAt = now.AddMonths(3), IsActive = true,
            });
        await db.SaveChangesAsync(ct);
    }

    private static async Task<AppUser?> EnsureUserAsync(
        UserManager<AppUser> userManager, string email, string fullName, string password, string[] roles, ILogger logger)
    {
        var user = await userManager.FindByEmailAsync(email);
        if (user is null)
        {
            user = new AppUser
            {
                UserName = email, Email = email, EmailConfirmed = true, FullName = fullName, CreatedAt = DateTime.UtcNow,
            };
            var result = await userManager.CreateAsync(user, password);
            if (!result.Succeeded)
            {
                logger.LogError("Không tạo được {Email}: {Errors}", email, string.Join("; ", result.Errors.Select(e => e.Description)));
                return null;
            }
        }

        foreach (var role in roles)
        {
            if (!await userManager.IsInRoleAsync(user, role))
                await userManager.AddToRoleAsync(user, role);
        }
        return user;
    }
}
