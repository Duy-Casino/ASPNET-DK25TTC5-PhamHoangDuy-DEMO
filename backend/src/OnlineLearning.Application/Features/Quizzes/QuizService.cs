using Microsoft.EntityFrameworkCore;
using OnlineLearning.Application.Common.Interfaces;
using OnlineLearning.Application.Common.Models;
using OnlineLearning.Application.Features.Learning;
using OnlineLearning.Domain.Constants;
using OnlineLearning.Domain.Entities;
using OnlineLearning.Domain.Enums;

namespace OnlineLearning.Application.Features.Quizzes;

public interface IQuizAuthoringService
{
    Task<Result<IReadOnlyList<QuizListItemDto>>> ListAsync(Guid courseId, CancellationToken ct = default);
    Task<Result<QuizEditDto>> GetAsync(Guid quizId, CancellationToken ct = default);
    Task<Result<QuizEditDto>> CreateAsync(Guid courseId, SaveQuizRequest request, CancellationToken ct = default);
    Task<Result<QuizEditDto>> UpdateAsync(Guid quizId, SaveQuizRequest request, CancellationToken ct = default);
    Task<Result> DeleteAsync(Guid quizId, CancellationToken ct = default);
}

/// <summary>Giảng viên soạn bài kiểm tra trắc nghiệm (FR-QUIZ-01..03).</summary>
public sealed class QuizAuthoringService(IAppDbContext db, ICurrentUser currentUser) : IQuizAuthoringService
{
    private static readonly Error CourseNotFound = Error.NotFound("Không tìm thấy khóa học.");
    private static readonly Error QuizNotFound = Error.NotFound("Không tìm thấy bài kiểm tra.");
    private static readonly Error Locked = Error.Conflict("Khóa học đang chờ duyệt hoặc đã xuất bản, hãy chuyển về bản nháp để sửa bài kiểm tra.");

    private bool CanManage(Course c) => currentUser.IsInRole(Roles.Admin) || c.InstructorId == currentUser.UserId;

    public async Task<Result<IReadOnlyList<QuizListItemDto>>> ListAsync(Guid courseId, CancellationToken ct = default)
    {
        var course = await db.Courses.AsNoTracking().FirstOrDefaultAsync(c => c.Id == courseId && !c.IsDeleted, ct);
        if (course is null || !CanManage(course)) return CourseNotFound;

        var list = await db.Quizzes.AsNoTracking().Where(q => q.CourseId == courseId).OrderBy(q => q.SortOrder)
            .Select(q => new QuizListItemDto(q.Id, q.Title, q.SectionId, q.Section != null ? q.Section.Title : null, q.Questions.Count,
                q.PassingScore, q.MaxAttempts, q.IsRequired, q.IsFinalExam, q.Attempts.Count))
            .ToListAsync(ct);
        return list;
    }

    public async Task<Result<QuizEditDto>> GetAsync(Guid quizId, CancellationToken ct = default)
    {
        var quiz = await LoadAsync(quizId, ct, tracking: false);
        if (quiz is null || !CanManage(quiz.Course)) return QuizNotFound;
        return ToDto(quiz, await db.QuizAttempts.CountAsync(a => a.QuizId == quizId, ct));
    }

    public async Task<Result<QuizEditDto>> CreateAsync(Guid courseId, SaveQuizRequest request, CancellationToken ct = default)
    {
        var course = await db.Courses.FirstOrDefaultAsync(c => c.Id == courseId && !c.IsDeleted, ct);
        if (course is null || !CanManage(course)) return CourseNotFound;
        if (!course.IsEditable) return Locked;
        if (request.SectionId is { } sid && !await db.CourseSections.AnyAsync(s => s.Id == sid && s.CourseId == courseId, ct))
            return Error.Validation("Chương không thuộc khóa học này.");

        var maxOrder = await db.Quizzes.Where(q => q.CourseId == courseId).Select(q => (int?)q.SortOrder).MaxAsync(ct) ?? -1;
        var quiz = new Quiz { CourseId = courseId, SortOrder = maxOrder + 1 };
        Apply(quiz, request);
        ReplaceQuestions(quiz, request);
        db.Quizzes.Add(quiz);
        await db.SaveChangesAsync(ct);
        return await GetAsync(quiz.Id, ct);
    }

    public async Task<Result<QuizEditDto>> UpdateAsync(Guid quizId, SaveQuizRequest request, CancellationToken ct = default)
    {
        var quiz = await LoadAsync(quizId, ct, tracking: true);
        if (quiz is null || !CanManage(quiz.Course)) return QuizNotFound;
        if (!quiz.Course.IsEditable) return Locked;
        if (request.SectionId is { } sid && !await db.CourseSections.AnyAsync(s => s.Id == sid && s.CourseId == quiz.CourseId, ct))
            return Error.Validation("Chương không thuộc khóa học này.");

        Apply(quiz, request);

        var hasAttempts = await db.QuizAttempts.AnyAsync(a => a.QuizId == quizId, ct);
        if (hasAttempts)
        {
            // Giữ nguyên câu hỏi để lịch sử bài làm không bị sai lệch
            if (!SameQuestions(quiz, request))
                return Error.Conflict("Bài kiểm tra đã có học viên làm, chỉ được sửa thông tin chung (không sửa câu hỏi).");
        }
        else
        {
            foreach (var q in quiz.Questions.ToList()) db.Questions.Remove(q);
            quiz.Questions.Clear();
            ReplaceQuestions(quiz, request);
        }

        await db.SaveChangesAsync(ct);
        return await GetAsync(quiz.Id, ct);
    }

    public async Task<Result> DeleteAsync(Guid quizId, CancellationToken ct = default)
    {
        var quiz = await db.Quizzes.Include(q => q.Course).FirstOrDefaultAsync(q => q.Id == quizId, ct);
        if (quiz is null || !CanManage(quiz.Course)) return Result.Failure(QuizNotFound);
        if (!quiz.Course.IsEditable) return Result.Failure(Locked);
        if (await db.QuizAttempts.AnyAsync(a => a.QuizId == quizId, ct))
            return Result.Failure(Error.Conflict("Bài kiểm tra đã có học viên làm, không thể xóa."));

        db.Quizzes.Remove(quiz);
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }

    private Task<Quiz?> LoadAsync(Guid quizId, CancellationToken ct, bool tracking)
    {
        IQueryable<Quiz> q = db.Quizzes.Include(x => x.Course)
            .Include(x => x.Questions.OrderBy(qq => qq.SortOrder)).ThenInclude(qq => qq.Options.OrderBy(o => o.SortOrder))
            .AsSplitQuery();
        if (!tracking) q = q.AsNoTracking();
        return q.FirstOrDefaultAsync(x => x.Id == quizId, ct);
    }

    private static void Apply(Quiz quiz, SaveQuizRequest r)
    {
        quiz.Title = r.Title.Trim();
        quiz.Description = r.Description?.Trim();
        quiz.SectionId = r.SectionId;
        quiz.PassingScore = r.PassingScore;
        quiz.MaxAttempts = r.MaxAttempts;
        quiz.TimeLimitMinutes = r.TimeLimitMinutes;
        quiz.IsRequired = r.IsRequired;
        quiz.IsFinalExam = r.IsFinalExam;
        quiz.ShuffleQuestions = r.ShuffleQuestions;
    }

    private static void ReplaceQuestions(Quiz quiz, SaveQuizRequest r)
    {
        var order = 0;
        foreach (var q in r.Questions)
        {
            var question = new Question
            {
                Content = q.Content.Trim(), Type = q.Type, Points = q.Points, Explanation = q.Explanation?.Trim(), SortOrder = order++,
            };
            var optOrder = 0;
            foreach (var o in q.Options)
                question.Options.Add(new AnswerOption { Content = o.Content.Trim(), IsCorrect = o.IsCorrect, SortOrder = optOrder++ });
            quiz.Questions.Add(question);
        }
    }

    private static bool SameQuestions(Quiz quiz, SaveQuizRequest r)
    {
        var existing = quiz.Questions.OrderBy(q => q.SortOrder).ToList();
        if (existing.Count != r.Questions.Count) return false;
        for (var i = 0; i < existing.Count; i++)
        {
            var a = existing[i];
            var b = r.Questions[i];
            if (a.Content != b.Content.Trim() || a.Type != b.Type || a.Points != b.Points) return false;
            var ao = a.Options.OrderBy(o => o.SortOrder).ToList();
            if (ao.Count != b.Options.Count) return false;
            for (var j = 0; j < ao.Count; j++)
                if (ao[j].Content != b.Options[j].Content.Trim() || ao[j].IsCorrect != b.Options[j].IsCorrect) return false;
            a.Explanation = b.Explanation?.Trim(); // Lời giải thích được phép sửa
        }
        return true;
    }

    private static QuizEditDto ToDto(Quiz q, int attempts) => new(
        q.Id, q.CourseId, q.SectionId, q.Title, q.Description, q.PassingScore, q.MaxAttempts, q.TimeLimitMinutes,
        q.IsRequired, q.IsFinalExam, q.ShuffleQuestions, attempts,
        q.Questions.OrderBy(x => x.SortOrder).Select(x => new QuestionEditDto(x.Id, x.Content, x.Type, x.Points, x.Explanation, x.SortOrder,
            x.Options.OrderBy(o => o.SortOrder).Select(o => new OptionEditDto(o.Id, o.Content, o.IsCorrect, o.SortOrder)).ToList())).ToList());
}

// =====================================================================

public interface IQuizTakingService
{
    Task<Result<QuizIntroDto>> GetIntroAsync(Guid userId, Guid quizId, CancellationToken ct = default);
    Task<Result<AttemptTakeDto>> StartAsync(Guid userId, Guid quizId, CancellationToken ct = default);
    Task<Result<AttemptResultDto>> SubmitAsync(Guid userId, Guid attemptId, SubmitAttemptRequest request, CancellationToken ct = default);
    Task<Result<AttemptResultDto>> GetResultAsync(Guid userId, Guid attemptId, CancellationToken ct = default);
    Task<PagedResult<MyQuizResultDto>> GetMyResultsAsync(Guid userId, PagedQuery query, CancellationToken ct = default);
}

/// <summary>
/// Học viên làm bài: đáp án đúng không bao giờ gửi xuống trước khi nộp (FR-QUIZ-04); chấm điểm ở server;
/// giới hạn số lần làm (BR-10); có hạn giờ thì chấm theo hạn (cộng 60 giây trễ mạng).
/// </summary>
public sealed class QuizTakingService(IAppDbContext db, IDateTimeProvider clock, ILearningService learning) : IQuizTakingService
{
    private static readonly TimeSpan Grace = TimeSpan.FromSeconds(60);
    private static readonly Error NotFound = Error.NotFound("Không tìm thấy bài kiểm tra.");

    public async Task<Result<QuizIntroDto>> GetIntroAsync(Guid userId, Guid quizId, CancellationToken ct = default)
    {
        var quiz = await db.Quizzes.AsNoTracking().Include(q => q.Course).Include(q => q.Questions)
            .FirstOrDefaultAsync(q => q.Id == quizId, ct);
        if (quiz is null) return NotFound;
        if (!await IsEnrolledAsync(userId, quiz.CourseId, ct)) return Error.Forbidden("Bạn chưa sở hữu khóa học này.");

        var attempts = await db.QuizAttempts.AsNoTracking().Where(a => a.QuizId == quizId && a.UserId == userId)
            .OrderByDescending(a => a.AttemptNumber).ToListAsync(ct);
        var now = clock.UtcNow;
        var inProgress = attempts.FirstOrDefault(a => a.SubmittedAt is null && !IsExpired(quiz, a, now));
        var submitted = attempts.Where(a => a.SubmittedAt is not null).ToList();
        var canAttempt = inProgress is not null || quiz.MaxAttempts is null || attempts.Count < quiz.MaxAttempts;

        return new QuizIntroDto(quiz.Id, quiz.CourseId, quiz.Course.Slug, quiz.Title, quiz.Description, quiz.PassingScore, quiz.MaxAttempts,
            quiz.TimeLimitMinutes, quiz.Questions.Count, quiz.Questions.Sum(q => q.Points), quiz.IsRequired, attempts.Count,
            submitted.Count == 0 ? null : submitted.Max(a => a.ScorePercent), submitted.Any(a => a.IsPassed), canAttempt, inProgress?.Id,
            submitted.Select(a => new AttemptSummaryDto(a.Id, a.AttemptNumber, a.StartedAt, a.SubmittedAt, a.ScorePercent, a.IsPassed)).ToList());
    }

    public async Task<Result<AttemptTakeDto>> StartAsync(Guid userId, Guid quizId, CancellationToken ct = default)
    {
        var quiz = await db.Quizzes.AsNoTracking()
            .Include(q => q.Questions).ThenInclude(q => q.Options)
            .AsSplitQuery()
            .FirstOrDefaultAsync(q => q.Id == quizId, ct);
        if (quiz is null) return NotFound;
        if (!await IsEnrolledAsync(userId, quiz.CourseId, ct)) return Error.Forbidden("Bạn chưa sở hữu khóa học này.");
        if (quiz.Questions.Count == 0) return Error.Conflict("Bài kiểm tra chưa có câu hỏi.");

        var now = clock.UtcNow;
        var attempts = await db.QuizAttempts.Where(a => a.QuizId == quizId && a.UserId == userId).ToListAsync(ct);

        // Tiếp tục lượt đang làm dở (nếu chưa hết giờ)
        var attempt = attempts.FirstOrDefault(a => a.SubmittedAt is null && !IsExpired(quiz, a, now));
        if (attempt is null)
        {
            if (quiz.MaxAttempts is { } max && attempts.Count >= max)
                return Error.Conflict($"Bạn đã dùng hết {max} lượt làm bài.");

            attempt = new QuizAttempt
            {
                QuizId = quizId, UserId = userId, AttemptNumber = attempts.Count + 1, StartedAt = now,
                MaxScore = quiz.Questions.Sum(q => q.Points),
            };
            db.QuizAttempts.Add(attempt);
            await db.SaveChangesAsync(ct);
        }

        var questions = quiz.Questions.OrderBy(q => q.SortOrder).ToList();
        if (quiz.ShuffleQuestions)
        {
            // Trộn ổn định theo lượt làm để tải lại trang không đổi thứ tự
            var rng = new Random(attempt.Id.GetHashCode());
            questions = questions.OrderBy(_ => rng.Next()).ToList();
        }

        return new AttemptTakeDto(attempt.Id, quiz.Id, quiz.Title, attempt.AttemptNumber, attempt.StartedAt, Deadline(quiz, attempt),
            questions.Select(q => new TakeQuestionDto(q.Id, q.Content, q.Type, q.Points,
                q.Options.OrderBy(o => o.SortOrder).Select(o => new TakeOptionDto(o.Id, o.Content)).ToList())).ToList());
    }

    public async Task<Result<AttemptResultDto>> SubmitAsync(Guid userId, Guid attemptId, SubmitAttemptRequest request, CancellationToken ct = default)
    {
        var attempt = await db.QuizAttempts
            .Include(a => a.Quiz).ThenInclude(q => q.Questions).ThenInclude(q => q.Options)
            .AsSplitQuery()
            .FirstOrDefaultAsync(a => a.Id == attemptId && a.UserId == userId, ct);
        if (attempt is null) return Error.NotFound("Không tìm thấy lượt làm bài.");
        if (attempt.SubmittedAt is not null) return await GetResultAsync(userId, attemptId, ct); // nộp lặp → trả kết quả cũ

        var quiz = attempt.Quiz;
        var questions = quiz.Questions.ToDictionary(q => q.Id);
        // Nộp sau hạn giờ (quá thời gian ân hạn) → không chấp nhận đáp án gửi lên, chấm 0 điểm
        var answers = IsExpired(quiz, attempt, clock.UtcNow) ? [] : request.Answers;
        var selectedByQuestion = answers
            .Where(a => questions.ContainsKey(a.QuestionId))
            .GroupBy(a => a.QuestionId)
            .ToDictionary(g => g.Key, g => g.SelectMany(a => a.OptionIds).ToHashSet());

        decimal score = 0;
        foreach (var question in questions.Values)
        {
            var validOptionIds = question.Options.Select(o => o.Id).ToHashSet();
            var selected = selectedByQuestion.GetValueOrDefault(question.Id) ?? [];
            selected.IntersectWith(validOptionIds);
            if (question.Type != QuestionType.MultipleChoice && selected.Count > 1) selected = [selected.First()];

            var correct = question.Options.Where(o => o.IsCorrect).Select(o => o.Id).ToHashSet();
            var isCorrect = selected.SetEquals(correct);
            if (isCorrect) score += question.Points;

            foreach (var optionId in selected)
                attempt.Answers.Add(new QuizAnswer { QuestionId = question.Id, AnswerOptionId = optionId, IsCorrect = correct.Contains(optionId) });
        }

        var now = clock.UtcNow;
        attempt.SubmittedAt = now;
        attempt.Score = score;
        attempt.MaxScore = questions.Values.Sum(q => q.Points);
        attempt.ScorePercent = attempt.MaxScore == 0 ? 0 : Math.Round(score * 100 / attempt.MaxScore, 2);
        attempt.IsPassed = attempt.ScorePercent >= quiz.PassingScore;
        await db.SaveChangesAsync(ct);

        var enrollment = await db.Enrollments.FirstOrDefaultAsync(e => e.UserId == userId && e.CourseId == quiz.CourseId, ct);
        if (enrollment is not null) await learning.RecalculateAsync(enrollment.Id, ct);

        return await GetResultAsync(userId, attemptId, ct);
    }

    public async Task<Result<AttemptResultDto>> GetResultAsync(Guid userId, Guid attemptId, CancellationToken ct = default)
    {
        var attempt = await db.QuizAttempts.AsNoTracking()
            .Include(a => a.Answers)
            .Include(a => a.Quiz).ThenInclude(q => q.Course)
            .Include(a => a.Quiz).ThenInclude(q => q.Questions).ThenInclude(q => q.Options)
            .AsSplitQuery()
            .FirstOrDefaultAsync(a => a.Id == attemptId && a.UserId == userId, ct);
        if (attempt is null || attempt.SubmittedAt is null) return Error.NotFound("Không tìm thấy kết quả.");

        var selected = attempt.Answers.Select(a => a.AnswerOptionId).ToHashSet();
        var completed = await db.Enrollments.AnyAsync(e => e.UserId == userId && e.CourseId == attempt.Quiz.CourseId && e.Status == EnrollmentStatus.Completed, ct);
        var certificateCode = await db.Certificates.Where(c => c.UserId == userId && c.CourseId == attempt.Quiz.CourseId && !c.IsRevoked)
            .Select(c => c.CertificateCode).FirstOrDefaultAsync(ct);

        var questions = attempt.Quiz.Questions.OrderBy(q => q.SortOrder).Select(q =>
        {
            var correctIds = q.Options.Where(o => o.IsCorrect).Select(o => o.Id).ToHashSet();
            var chosen = q.Options.Where(o => selected.Contains(o.Id)).Select(o => o.Id).ToHashSet();
            var ok = chosen.SetEquals(correctIds);
            return new QuestionResultDto(q.Id, q.Content, q.Type, q.Points, ok ? q.Points : 0, ok, q.Explanation,
                q.Options.OrderBy(o => o.SortOrder).Select(o => new ResultOptionDto(o.Id, o.Content, o.IsCorrect, chosen.Contains(o.Id))).ToList());
        }).ToList();

        return new AttemptResultDto(attempt.Id, attempt.QuizId, attempt.Quiz.Title, attempt.Quiz.CourseId, attempt.Quiz.Course.Slug,
            attempt.AttemptNumber, attempt.StartedAt, attempt.SubmittedAt.Value, attempt.Score, attempt.MaxScore, attempt.ScorePercent,
            attempt.Quiz.PassingScore, attempt.IsPassed, completed, questions, certificateCode);
    }

    public Task<PagedResult<MyQuizResultDto>> GetMyResultsAsync(Guid userId, PagedQuery query, CancellationToken ct = default) =>
        db.QuizAttempts.AsNoTracking()
            .Where(a => a.UserId == userId && a.SubmittedAt != null)
            .OrderByDescending(a => a.SubmittedAt)
            .Select(a => new MyQuizResultDto(a.Id, a.QuizId, a.Quiz.Title, a.Quiz.Course.Title, a.Quiz.Course.Slug, a.AttemptNumber,
                a.SubmittedAt!.Value, a.ScorePercent, a.Quiz.PassingScore, a.IsPassed))
            .ToPagedResultAsync(query.Page, query.PageSize, ct);

    private Task<bool> IsEnrolledAsync(Guid userId, Guid courseId, CancellationToken ct) =>
        db.Enrollments.AnyAsync(e => e.UserId == userId && e.CourseId == courseId && e.Status != EnrollmentStatus.Revoked, ct);

    private static DateTime? Deadline(Quiz quiz, QuizAttempt a) =>
        quiz.TimeLimitMinutes is { } m ? a.StartedAt.AddMinutes(m) : null;

    private static bool IsExpired(Quiz quiz, QuizAttempt a, DateTime now) =>
        Deadline(quiz, a) is { } d && now > d + Grace;
}
