using OnlineLearning.Domain.Common;
using OnlineLearning.Domain.Enums;

namespace OnlineLearning.Domain.Entities;

public class Enrollment : BaseEntity
{
    public Guid UserId { get; set; }
    public AppUser User { get; set; } = null!;

    public Guid CourseId { get; set; }
    public Course Course { get; set; } = null!;

    /// <summary>Null khi Admin cấp quyền thủ công.</summary>
    public Guid? OrderId { get; set; }
    public Order? Order { get; set; }

    public Guid? LastLessonId { get; set; }
    public Lesson? LastLesson { get; set; }

    public EnrollmentStatus Status { get; set; } = EnrollmentStatus.Active;
    public decimal ProgressPercent { get; set; }
    public DateTime EnrolledAt { get; set; }
    public DateTime? LastAccessedAt { get; set; }
    public DateTime? CompletedAt { get; set; }

    public ICollection<LessonProgress> LessonProgresses { get; set; } = [];
    public Certificate? Certificate { get; set; }
}

public class LessonProgress : BaseEntity
{
    public Guid EnrollmentId { get; set; }
    public Enrollment Enrollment { get; set; } = null!;

    public Guid LessonId { get; set; }
    public Lesson Lesson { get; set; } = null!;

    public bool IsCompleted { get; set; }
    public DateTime? CompletedAt { get; set; }
    public int LastPositionSeconds { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class Quiz : AuditableEntity
{
    public Guid CourseId { get; set; }
    public Course Course { get; set; } = null!;

    public Guid? SectionId { get; set; }
    public CourseSection? Section { get; set; }

    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    /// <summary>Điểm đạt theo phần trăm (0–100).</summary>
    public decimal PassingScore { get; set; } = 70;
    /// <summary>Null = không giới hạn.</summary>
    public int? MaxAttempts { get; set; }
    public int? TimeLimitMinutes { get; set; }
    public bool IsRequired { get; set; } = true;
    public bool IsFinalExam { get; set; }
    public bool ShuffleQuestions { get; set; }
    public int SortOrder { get; set; }

    public ICollection<Question> Questions { get; set; } = [];
    public ICollection<QuizAttempt> Attempts { get; set; } = [];
}

public class Question : AuditableEntity
{
    public Guid QuizId { get; set; }
    public Quiz Quiz { get; set; } = null!;

    public string Content { get; set; } = string.Empty;
    public QuestionType Type { get; set; } = QuestionType.SingleChoice;
    public decimal Points { get; set; } = 1;
    public string? Explanation { get; set; }
    public int SortOrder { get; set; }

    public ICollection<AnswerOption> Options { get; set; } = [];
}

public class AnswerOption : BaseEntity
{
    public Guid QuestionId { get; set; }
    public Question Question { get; set; } = null!;

    public string Content { get; set; } = string.Empty;
    public bool IsCorrect { get; set; }
    public int SortOrder { get; set; }
}

public class QuizAttempt : BaseEntity
{
    public Guid QuizId { get; set; }
    public Quiz Quiz { get; set; } = null!;

    public Guid UserId { get; set; }
    public AppUser User { get; set; } = null!;

    public int AttemptNumber { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public decimal Score { get; set; }
    public decimal MaxScore { get; set; }
    public decimal ScorePercent { get; set; }
    public bool IsPassed { get; set; }

    public ICollection<QuizAnswer> Answers { get; set; } = [];
}

/// <summary>Mỗi đáp án học viên chọn là một dòng.</summary>
public class QuizAnswer : BaseEntity
{
    public Guid AttemptId { get; set; }
    public QuizAttempt Attempt { get; set; } = null!;

    public Guid QuestionId { get; set; }
    public Question Question { get; set; } = null!;

    public Guid AnswerOptionId { get; set; }
    public AnswerOption AnswerOption { get; set; } = null!;

    public bool IsCorrect { get; set; }
}

public class Certificate : BaseEntity
{
    public string CertificateCode { get; set; } = string.Empty;

    public Guid UserId { get; set; }
    public AppUser User { get; set; } = null!;

    public Guid CourseId { get; set; }
    public Course Course { get; set; } = null!;

    public Guid EnrollmentId { get; set; }
    public Enrollment Enrollment { get; set; } = null!;

    // Ảnh chụp tại thời điểm cấp
    public string StudentName { get; set; } = string.Empty;
    public string CourseTitle { get; set; } = string.Empty;
    public string InstructorName { get; set; } = string.Empty;

    public decimal? FinalScore { get; set; }
    public string? PdfUrl { get; set; }
    public DateTime IssuedAt { get; set; }
    public bool IsRevoked { get; set; }
    public DateTime? RevokedAt { get; set; }
    public string? RevokedReason { get; set; }
}
