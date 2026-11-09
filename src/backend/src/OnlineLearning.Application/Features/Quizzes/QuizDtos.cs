using FluentValidation;
using OnlineLearning.Domain.Enums;

namespace OnlineLearning.Application.Features.Quizzes;

// ---------- Giảng viên soạn quiz ----------

public sealed record SaveOptionRequest(string Content, bool IsCorrect);
public sealed record SaveQuestionRequest(string Content, QuestionType Type, decimal Points, string? Explanation, IReadOnlyList<SaveOptionRequest> Options);
public sealed record SaveQuizRequest(
    string Title, string? Description, Guid? SectionId, decimal PassingScore, int? MaxAttempts, int? TimeLimitMinutes,
    bool IsRequired, bool IsFinalExam, bool ShuffleQuestions, IReadOnlyList<SaveQuestionRequest> Questions);

public sealed record OptionEditDto(Guid Id, string Content, bool IsCorrect, int SortOrder);
public sealed record QuestionEditDto(Guid Id, string Content, QuestionType Type, decimal Points, string? Explanation, int SortOrder, IReadOnlyList<OptionEditDto> Options);
public sealed record QuizEditDto(
    Guid Id, Guid CourseId, Guid? SectionId, string Title, string? Description, decimal PassingScore, int? MaxAttempts,
    int? TimeLimitMinutes, bool IsRequired, bool IsFinalExam, bool ShuffleQuestions, int AttemptCount,
    IReadOnlyList<QuestionEditDto> Questions);
public sealed record QuizListItemDto(Guid Id, string Title, Guid? SectionId, string? SectionTitle, int QuestionCount, decimal PassingScore,
    int? MaxAttempts, bool IsRequired, bool IsFinalExam, int AttemptCount);

// ---------- Học viên làm bài ----------

public sealed record AttemptSummaryDto(Guid Id, int AttemptNumber, DateTime StartedAt, DateTime? SubmittedAt, decimal ScorePercent, bool IsPassed);

public sealed record QuizIntroDto(
    Guid Id, Guid CourseId, string CourseSlug, string Title, string? Description, decimal PassingScore, int? MaxAttempts,
    int? TimeLimitMinutes, int QuestionCount, decimal TotalPoints, bool IsRequired, int AttemptsUsed, decimal? BestScore,
    bool IsPassed, bool CanAttempt, Guid? InProgressAttemptId, IReadOnlyList<AttemptSummaryDto> Attempts);

public sealed record TakeOptionDto(Guid Id, string Content);
public sealed record TakeQuestionDto(Guid Id, string Content, QuestionType Type, decimal Points, IReadOnlyList<TakeOptionDto> Options);
public sealed record AttemptTakeDto(Guid AttemptId, Guid QuizId, string QuizTitle, int AttemptNumber, DateTime StartedAt, DateTime? Deadline,
    IReadOnlyList<TakeQuestionDto> Questions);

public sealed record SubmitAnswer(Guid QuestionId, IReadOnlyList<Guid> OptionIds);
public sealed record SubmitAttemptRequest(IReadOnlyList<SubmitAnswer> Answers);

public sealed record QuestionResultDto(Guid QuestionId, string Content, QuestionType Type, decimal Points, decimal PointsAwarded, bool IsCorrect,
    string? Explanation, IReadOnlyList<ResultOptionDto> Options);
public sealed record ResultOptionDto(Guid Id, string Content, bool IsCorrect, bool Selected);
public sealed record AttemptResultDto(Guid AttemptId, Guid QuizId, string QuizTitle, Guid CourseId, string CourseSlug, int AttemptNumber,
    DateTime StartedAt, DateTime SubmittedAt, decimal Score, decimal MaxScore, decimal ScorePercent, decimal PassingScore, bool IsPassed,
    bool CourseCompleted, IReadOnlyList<QuestionResultDto> Questions, string? CertificateCode = null);

public sealed record MyQuizResultDto(Guid AttemptId, Guid QuizId, string QuizTitle, string CourseTitle, string CourseSlug, int AttemptNumber,
    DateTime SubmittedAt, decimal ScorePercent, decimal PassingScore, bool IsPassed);

// ---------- Validators ----------

public sealed class SaveQuizRequestValidator : AbstractValidator<SaveQuizRequest>
{
    public SaveQuizRequestValidator()
    {
        RuleFor(x => x.Title).NotEmpty().WithMessage("Vui lòng nhập tên bài kiểm tra.").MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(1000);
        RuleFor(x => x.PassingScore).InclusiveBetween(0, 100).WithMessage("Điểm đạt từ 0 đến 100%.");
        RuleFor(x => x.MaxAttempts).InclusiveBetween(1, 100).When(x => x.MaxAttempts.HasValue).WithMessage("Số lần làm từ 1 đến 100.");
        RuleFor(x => x.TimeLimitMinutes).InclusiveBetween(1, 600).When(x => x.TimeLimitMinutes.HasValue).WithMessage("Thời gian từ 1 đến 600 phút.");
        RuleFor(x => x.Questions).NotEmpty().WithMessage("Bài kiểm tra cần ít nhất một câu hỏi.").Must(q => q.Count <= 200);
        RuleForEach(x => x.Questions).SetValidator(new SaveQuestionRequestValidator());
    }
}

public sealed class SaveQuestionRequestValidator : AbstractValidator<SaveQuestionRequest>
{
    public SaveQuestionRequestValidator()
    {
        RuleFor(x => x.Content).NotEmpty().WithMessage("Câu hỏi không được để trống.").MaximumLength(2000);
        RuleFor(x => x.Type).IsInEnum();
        RuleFor(x => x.Points).GreaterThan(0).LessThanOrEqualTo(100);
        RuleFor(x => x.Explanation).MaximumLength(2000);
        RuleFor(x => x.Options).Must(o => o.Count is >= 2 and <= 10).WithMessage("Mỗi câu hỏi cần từ 2 đến 10 đáp án.");
        RuleForEach(x => x.Options).ChildRules(o => o.RuleFor(x => x.Content).NotEmpty().WithMessage("Đáp án không được để trống.").MaximumLength(1000));
        RuleFor(x => x.Options).Must(o => o.Count(x => x.IsCorrect) >= 1).WithMessage("Mỗi câu hỏi cần ít nhất một đáp án đúng.");
        RuleFor(x => x.Options).Must(o => o.Count(x => x.IsCorrect) == 1)
            .When(x => x.Type is QuestionType.SingleChoice or QuestionType.TrueFalse)
            .WithMessage("Câu một lựa chọn / đúng-sai chỉ có đúng một đáp án đúng.");
        RuleFor(x => x.Options).Must(o => o.Count == 2).When(x => x.Type == QuestionType.TrueFalse)
            .WithMessage("Câu đúng-sai có đúng 2 đáp án.");
    }
}

public sealed class SubmitAttemptRequestValidator : AbstractValidator<SubmitAttemptRequest>
{
    public SubmitAttemptRequestValidator() => RuleFor(x => x.Answers).NotNull().Must(a => a.Count <= 500);
}
