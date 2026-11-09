using OnlineLearning.Domain.Enums;

namespace OnlineLearning.Infrastructure.Persistence.Seed;

/// <summary>Dữ liệu demo (nội dung mẫu, có thể thay khi triển khai thật).</summary>
internal static class SampleData
{
    public const string SampleVideo = "https://commondatastorage.googleapis.com/gtv-videos-bucket/sample/BigBuckBunny.mp4";

    public sealed record InstructorSeed(string Email, string FullName, string Headline, string Bio);
    public sealed record StudentSeed(string Email, string FullName);
    public sealed record CourseSeed(
        string Title, string CategorySlug, string InstructorEmail, CourseLevel Level, decimal Price, decimal? DiscountPrice,
        bool Featured, string ShortDescription, string[] Outcomes, string[] Requirements, (string Title, string[] Lessons)[] Sections);

    public static readonly InstructorSeed[] Instructors =
    [
        new("giangvien@eduviet.local", "Nguyễn Minh Khoa", "Kỹ sư phần mềm, 10 năm kinh nghiệm .NET & React",
            "Từng làm việc tại các công ty sản phẩm và outsource lớn, chuyên kiến trúc hệ thống web quy mô vừa và lớn."),
        new("lethu.design@eduviet.local", "Lê Anh Thư", "UI/UX Designer, giảng viên thiết kế số",
            "Thiết kế sản phẩm cho fintech và edtech, đam mê giúp người mới bắt đầu có tư duy thiết kế bài bản."),
        new("phamquang.data@eduviet.local", "Phạm Quang Huy", "Data Analyst, chuyên gia Excel & Power BI",
            "Hơn 8 năm phân tích dữ liệu bán lẻ, đào tạo nội bộ cho hơn 2.000 nhân sự văn phòng."),
    ];

    public sealed record ExperienceSeed(ExperienceType Type, string Title, string? Organization, int? StartYear, int? EndYear, string? Description = null);
    /// <param name="AvatarUrl">Ảnh minh họa giảng viên ảo (CC0) trong src/frontend/online-learning-client/public/avatars/demo.</param>
    public sealed record InstructorProfileSeed(string Email, int Years, string[] Expertise, bool Verified, ExperienceSeed[] Experiences, string AvatarUrl);

    /// <summary>Hồ sơ giảng viên mẫu (tên đơn vị được để chung chung vì là dữ liệu minh họa).</summary>
    public static readonly InstructorProfileSeed[] InstructorProfiles =
    [
        new("giangvien@eduviet.local", 10, ["C#", "ASP.NET Core", "React", "TypeScript", "SQL Server", "Kiến trúc phần mềm"], true,
        [
            new(ExperienceType.Work, "Kiến trúc sư phần mềm", "Công ty phần mềm sản phẩm thương mại điện tử", 2020, null,
                "Thiết kế hệ thống đặt hàng phục vụ hàng trăm nghìn người dùng, dẫn dắt nhóm 8 kỹ sư."),
            new(ExperienceType.Work, "Kỹ sư phần mềm cao cấp", "Công ty outsource phần mềm", 2015, 2020,
                "Phát triển ứng dụng web .NET cho khách hàng Nhật Bản và Singapore."),
            new(ExperienceType.Education, "Kỹ sư Công nghệ thông tin", "Trường đại học kỹ thuật", 2010, 2015),
            new(ExperienceType.Certification, "Microsoft Certified: Azure Developer Associate", "Microsoft", 2022, null),
        ], "/avatars/demo/nguyen-minh-khoa.svg"),
        new("lethu.design@eduviet.local", 6, ["UI/UX", "Figma", "Design System", "Nghiên cứu người dùng"], false,
        [
            new(ExperienceType.Work, "Product Designer", "Công ty công nghệ tài chính", 2021, null,
                "Thiết kế ứng dụng ví điện tử và hệ thống design system dùng chung cho 5 sản phẩm."),
            new(ExperienceType.Work, "UI Designer", "Công ty edtech", 2018, 2021),
            new(ExperienceType.Education, "Cử nhân Thiết kế đồ họa", "Trường đại học mỹ thuật", 2014, 2018),
        ], "/avatars/demo/le-anh-thu.svg"),
        new("phamquang.data@eduviet.local", 8, ["Excel", "Power BI", "SQL", "Phân tích dữ liệu", "Dashboard"], true,
        [
            new(ExperienceType.Work, "Trưởng nhóm phân tích dữ liệu", "Chuỗi bán lẻ", 2019, null,
                "Xây dựng hệ thống báo cáo Power BI cho 300 cửa hàng, đào tạo nội bộ cho nhân sự văn phòng."),
            new(ExperienceType.Work, "Chuyên viên phân tích kinh doanh", "Công ty phân phối hàng tiêu dùng", 2016, 2019),
            new(ExperienceType.Education, "Cử nhân Hệ thống thông tin quản lý", "Trường đại học kinh tế", 2012, 2016),
            new(ExperienceType.Certification, "Microsoft Certified: Power BI Data Analyst Associate", "Microsoft", 2021, null),
        ], "/avatars/demo/pham-quang-huy.svg"),
    ];

    public static readonly StudentSeed[] Students =
    [
        new("hocvien@eduviet.local", "Trần Thu Hà"),
        new("hoangnam@eduviet.local", "Hoàng Văn Nam"),
        new("ngoclan@eduviet.local", "Đỗ Ngọc Lan"),
        new("minhtuan@eduviet.local", "Vũ Minh Tuấn"),
        new("baoanh@eduviet.local", "Ngô Bảo Anh"),
        new("ducthinh@eduviet.local", "Bùi Đức Thịnh"),
    ];

    public static readonly CourseSeed[] Courses =
    [
        new("Lập trình C# từ cơ bản đến nâng cao", "lap-trinh", "giangvien@eduviet.local", CourseLevel.Beginner, 899_000, 499_000, true,
            "Nắm vững C# và lập trình hướng đối tượng qua 40+ bài tập thực tế.",
            ["Viết chương trình C# sạch, dễ bảo trì", "Hiểu sâu OOP, LINQ, async/await", "Tự xây dựng ứng dụng console quản lý"],
            ["Máy tính Windows/macOS", "Không cần kinh nghiệm lập trình"],
            [("Làm quen với C#", ["Cài đặt Visual Studio và .NET", "Chương trình Hello World", "Biến và kiểu dữ liệu"]),
             ("Lập trình hướng đối tượng", ["Class và Object", "Kế thừa và đa hình", "Interface"]),
             ("C# hiện đại", ["LINQ", "Async/await", "Record và pattern matching"])]),
        new("ASP.NET Core Web API thực chiến", "lap-trinh", "giangvien@eduviet.local", CourseLevel.Intermediate, 1_290_000, 790_000, true,
            "Xây dựng REST API chuẩn doanh nghiệp với Clean Architecture, EF Core và JWT.",
            ["Thiết kế API theo Clean Architecture", "Xác thực JWT, refresh token", "Triển khai lên IIS và Azure"],
            ["Biết C# cơ bản", "Hiểu HTTP ở mức cơ bản"],
            [("Nền tảng ASP.NET Core", ["Middleware và pipeline", "Dependency Injection", "Cấu hình và Options"]),
             ("Dữ liệu với EF Core", ["Code First và Migration", "Quan hệ và truy vấn", "Tối ưu hiệu năng"]),
             ("Bảo mật", ["Identity", "JWT và refresh token", "Phân quyền theo vai trò"])]),
        new("ReactJS và TypeScript cho người mới", "lap-trinh", "giangvien@eduviet.local", CourseLevel.Beginner, 990_000, null, false,
            "Học React hiện đại với Hooks, TypeScript và React Router qua dự án thật.",
            ["Xây dựng SPA với React + TypeScript", "Quản lý state với Hooks và Context", "Gọi API với Axios"],
            ["HTML, CSS, JavaScript cơ bản"],
            [("Khởi động", ["Vite và cấu trúc dự án", "JSX và component", "Props và state"]),
             ("Hooks", ["useState và useEffect", "Custom hook", "Context API"]),
             ("Dự án", ["React Router", "Kết nối API", "Triển khai lên Vercel"])]),
        new("SQL Server cho lập trình viên", "lap-trinh", "giangvien@eduviet.local", CourseLevel.Intermediate, 690_000, 390_000, false,
            "Thiết kế CSDL chuẩn hóa, viết truy vấn hiệu quả và tối ưu chỉ mục.",
            ["Thiết kế CSDL 3NF", "Viết truy vấn JOIN, CTE, Window function", "Đọc execution plan và tạo index"],
            ["Biết sử dụng máy tính", "Có tư duy logic"],
            [("Thiết kế", ["Mô hình ERD", "Chuẩn hóa dữ liệu", "Khóa và ràng buộc"]),
             ("Truy vấn", ["SELECT và JOIN", "GROUP BY và HAVING", "CTE và Window function"]),
             ("Hiệu năng", ["Index", "Execution plan", "Transaction và khóa"])]),
        new("Thiết kế UI/UX với Figma", "thiet-ke", "lethu.design@eduviet.local", CourseLevel.Beginner, 1_190_000, 690_000, true,
            "Từ wireframe đến prototype hoàn chỉnh, xây dựng design system đầu tiên của bạn.",
            ["Sử dụng thành thạo Figma", "Hiểu quy trình UX research", "Tạo design system và prototype"],
            ["Không cần kinh nghiệm thiết kế"],
            [("Làm quen Figma", ["Giao diện và công cụ", "Frame, Auto Layout", "Component và Variant"]),
             ("Tư duy UX", ["Nghiên cứu người dùng", "User flow", "Wireframe"]),
             ("Hoàn thiện", ["Typography và màu sắc", "Design system", "Prototype và bàn giao"])]),
        new("Photoshop cơ bản cho người đi làm", "thiet-ke", "lethu.design@eduviet.local", CourseLevel.Beginner, 590_000, 299_000, false,
            "Chỉnh sửa ảnh, thiết kế banner mạng xã hội nhanh và đẹp.",
            ["Chỉnh sửa và phục hồi ảnh", "Thiết kế banner, poster", "Xuất file đúng chuẩn"],
            ["Máy tính cài Photoshop"],
            [("Cơ bản", ["Layer và mask", "Công cụ chọn vùng", "Chỉnh màu"]),
             ("Thực hành", ["Banner Facebook", "Poster sự kiện", "Ảnh sản phẩm"])]),
        new("Excel từ cơ bản đến nâng cao", "tin-hoc-van-phong", "phamquang.data@eduviet.local", CourseLevel.Beginner, 499_000, 249_000, true,
            "Thành thạo hàm, PivotTable và Dashboard để tăng tốc công việc văn phòng.",
            ["Sử dụng 50+ hàm thông dụng", "Phân tích dữ liệu với PivotTable", "Tạo dashboard báo cáo"],
            ["Máy tính cài Microsoft Excel"],
            [("Nền tảng", ["Định dạng và nhập liệu", "Hàm cơ bản", "Tham chiếu ô"]),
             ("Hàm nâng cao", ["VLOOKUP, XLOOKUP", "IF lồng và IFS", "SUMIFS, COUNTIFS"]),
             ("Phân tích", ["PivotTable", "Biểu đồ", "Dashboard"])]),
        new("Power BI phân tích dữ liệu kinh doanh", "du-lieu-ai", "phamquang.data@eduviet.local", CourseLevel.Intermediate, 1_490_000, 990_000, true,
            "Biến dữ liệu thô thành báo cáo trực quan, ra quyết định nhanh hơn.",
            ["Kết nối và làm sạch dữ liệu với Power Query", "Viết DAX cơ bản", "Xuất bản báo cáo"],
            ["Biết Excel cơ bản"],
            [("Power Query", ["Kết nối nguồn dữ liệu", "Làm sạch dữ liệu", "Gộp bảng"]),
             ("Mô hình hóa", ["Quan hệ bảng", "DAX cơ bản", "Measure và Column"]),
             ("Trực quan hóa", ["Chọn biểu đồ", "Bộ lọc và slicer", "Xuất bản và chia sẻ"])]),
        new("Kỹ năng thuyết trình chuyên nghiệp", "ky-nang-mem", "lethu.design@eduviet.local", CourseLevel.Beginner, 0, null, false,
            "Khóa học miễn phí giúp bạn tự tin nói trước đám đông.",
            ["Xây dựng cấu trúc bài nói", "Kiểm soát giọng nói và ngôn ngữ cơ thể", "Thiết kế slide rõ ràng"],
            ["Không yêu cầu"],
            [("Chuẩn bị", ["Xác định mục tiêu", "Cấu trúc bài nói"]),
             ("Trình bày", ["Giọng nói", "Ngôn ngữ cơ thể", "Xử lý câu hỏi"])]),
        new("Digital Marketing tổng quan", "marketing", "phamquang.data@eduviet.local", CourseLevel.Beginner, 790_000, 450_000, false,
            "Lập kế hoạch marketing online: SEO, quảng cáo Facebook, Google và email.",
            ["Lập kế hoạch marketing 3 tháng", "Chạy quảng cáo Facebook cơ bản", "Đo lường bằng Google Analytics"],
            ["Không yêu cầu"],
            [("Chiến lược", ["Chân dung khách hàng", "Kênh marketing", "Ngân sách"]),
             ("Thực thi", ["SEO cơ bản", "Quảng cáo Facebook", "Email marketing"]),
             ("Đo lường", ["Google Analytics", "Chỉ số quan trọng", "Tối ưu chiến dịch"])]),
    ];

    public static readonly (string Comment, byte Rating)[] ReviewTexts =
    [
        ("Khóa học rất dễ hiểu, giảng viên giải thích kỹ từng bước và có bài tập thực hành đi kèm.", 5),
        ("Nội dung thực tế, áp dụng được ngay vào công việc. Mình đã giới thiệu cho đồng nghiệp.", 5),
        ("Bài giảng chất lượng, video rõ nét. Mong có thêm phần nâng cao trong thời gian tới.", 4),
        ("Giảng viên nhiệt tình, trả lời câu hỏi nhanh. Đáng đồng tiền bát gạo.", 5),
        ("Phần đầu hơi chậm nhưng các chương sau rất hay, nhiều kiến thức hữu ích.", 4),
        ("Ổn, phù hợp người mới bắt đầu.", 4),
    ];
}
