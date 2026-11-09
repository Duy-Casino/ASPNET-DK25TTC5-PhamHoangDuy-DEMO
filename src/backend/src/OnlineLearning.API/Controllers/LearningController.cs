using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OnlineLearning.API.Common;
using OnlineLearning.Application.Common.Models;
using OnlineLearning.Application.Features.Dashboards;
using OnlineLearning.Application.Features.Learning;
using OnlineLearning.Application.Features.Quizzes;
using OnlineLearning.Domain.Constants;

namespace OnlineLearning.API.Controllers;

/// <summary>Học viên: khóa học của tôi, trình học, tiến độ, bài kiểm tra, kết quả, chứng chỉ.</summary>
[Route("api/learning")]
[Authorize]
public sealed class LearningController(ILearningService learning, IQuizTakingService quizzes, IDashboardService dashboards) : ApiControllerBase
{
    [HttpGet("summary")]
    public async Task<ActionResult<StudentSummaryDto>> Summary(CancellationToken ct) =>
        Ok(await dashboards.GetStudentSummaryAsync(User.GetUserId(), ct));

    [HttpGet("courses")]
    public async Task<ActionResult<PagedResult<MyCourseDto>>> MyCourses([FromQuery] MyCoursesQuery query, CancellationToken ct) =>
        Ok(await learning.GetMyCoursesAsync(User.GetUserId(), query, ct));

    /// <summary>Đề cương kèm tiến độ; trả bài nên học tiếp (FR-LEARN-04).</summary>
    [HttpGet("courses/{slug}")]
    public async Task<IActionResult> Course(string slug, CancellationToken ct) =>
        FromResult(await learning.GetCourseAsync(User.GetUserId(), slug, ct));

    /// <summary>Nội dung bài học (video, bài viết, tài liệu) — chỉ cho người sở hữu khóa học.</summary>
    [HttpGet("lessons/{lessonId:guid}")]
    public async Task<IActionResult> Lesson(Guid lessonId, CancellationToken ct) =>
        FromResult(await learning.GetLessonAsync(User.GetUserId(), lessonId, ct));

    [HttpPost("lessons/{lessonId:guid}/complete")]
    public async Task<IActionResult> Complete(Guid lessonId, CancellationToken ct) =>
        FromResult(await learning.SetCompletedAsync(User.GetUserId(), lessonId, true, ct));

    [HttpDelete("lessons/{lessonId:guid}/complete")]
    public async Task<IActionResult> Uncomplete(Guid lessonId, CancellationToken ct) =>
        FromResult(await learning.SetCompletedAsync(User.GetUserId(), lessonId, false, ct));

    [HttpPut("lessons/{lessonId:guid}/position")]
    public async Task<IActionResult> SavePosition(Guid lessonId, SavePositionRequest request, CancellationToken ct) =>
        FromResult(await learning.SavePositionAsync(User.GetUserId(), lessonId, request.PositionSeconds, ct));

    // ---------- Bài kiểm tra ----------
    [HttpGet("quizzes/{quizId:guid}")]
    public async Task<IActionResult> QuizIntro(Guid quizId, CancellationToken ct) =>
        FromResult(await quizzes.GetIntroAsync(User.GetUserId(), quizId, ct));

    /// <summary>Bắt đầu (hoặc tiếp tục) một lượt làm bài. Không trả đáp án đúng.</summary>
    [HttpPost("quizzes/{quizId:guid}/attempts")]
    public async Task<IActionResult> StartAttempt(Guid quizId, CancellationToken ct) =>
        FromResult(await quizzes.StartAsync(User.GetUserId(), quizId, ct));

    [HttpPost("attempts/{attemptId:guid}/submit")]
    public async Task<IActionResult> Submit(Guid attemptId, SubmitAttemptRequest request, CancellationToken ct) =>
        FromResult(await quizzes.SubmitAsync(User.GetUserId(), attemptId, request, ct));

    [HttpGet("attempts/{attemptId:guid}")]
    public async Task<IActionResult> AttemptResult(Guid attemptId, CancellationToken ct) =>
        FromResult(await quizzes.GetResultAsync(User.GetUserId(), attemptId, ct));

    [HttpGet("quiz-results")]
    public async Task<ActionResult<PagedResult<MyQuizResultDto>>> QuizResults([FromQuery] PagedQuery query, CancellationToken ct) =>
        Ok(await quizzes.GetMyResultsAsync(User.GetUserId(), query, ct));

    [HttpGet("certificates")]
    public async Task<ActionResult<IReadOnlyList<CertificateListItemDto>>> Certificates(CancellationToken ct) =>
        Ok(await dashboards.GetMyCertificatesAsync(User.GetUserId(), ct));
}

/// <summary>Giảng viên: thống kê, học viên, soạn bài kiểm tra.</summary>
[Route("api/instructor")]
[Authorize(Roles = $"{Roles.Instructor},{Roles.Admin}")]
public sealed class InstructorInsightsController(IDashboardService dashboards, IQuizAuthoringService quizzes) : ApiControllerBase
{
    [HttpGet("overview")]
    public async Task<ActionResult<InstructorOverviewDto>> Overview(CancellationToken ct) =>
        Ok(await dashboards.GetInstructorOverviewAsync(User.GetUserId(), ct));

    [HttpGet("students")]
    public async Task<ActionResult<PagedResult<InstructorStudentDto>>> Students([FromQuery] InstructorStudentQuery query, CancellationToken ct) =>
        Ok(await dashboards.GetInstructorStudentsAsync(User.GetUserId(), query, ct));

    [HttpGet("courses/{courseId:guid}/quizzes")]
    public async Task<IActionResult> Quizzes(Guid courseId, CancellationToken ct) => FromResult(await quizzes.ListAsync(courseId, ct));

    [HttpPost("courses/{courseId:guid}/quizzes")]
    public async Task<IActionResult> CreateQuiz(Guid courseId, SaveQuizRequest request, CancellationToken ct) =>
        FromResult(await quizzes.CreateAsync(courseId, request, ct));

    [HttpGet("quizzes/{quizId:guid}")]
    public async Task<IActionResult> GetQuiz(Guid quizId, CancellationToken ct) => FromResult(await quizzes.GetAsync(quizId, ct));

    [HttpPut("quizzes/{quizId:guid}")]
    public async Task<IActionResult> UpdateQuiz(Guid quizId, SaveQuizRequest request, CancellationToken ct) =>
        FromResult(await quizzes.UpdateAsync(quizId, request, ct));

    [HttpDelete("quizzes/{quizId:guid}")]
    public async Task<IActionResult> DeleteQuiz(Guid quizId, CancellationToken ct) => FromResult(await quizzes.DeleteAsync(quizId, ct));
}
