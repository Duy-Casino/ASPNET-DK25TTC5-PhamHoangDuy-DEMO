using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OnlineLearning.API.Common;
using OnlineLearning.Application.Common.Models;
using OnlineLearning.Application.Features.Learning;
using OnlineLearning.Domain.Constants;

namespace OnlineLearning.API.Controllers;

/// <summary>Hỏi đáp và ghi chú trong bài học (học viên đã ghi danh, giảng viên của khóa, Admin).</summary>
[Route("api/learning")]
[Authorize]
public sealed class LessonToolsController(ILessonQaService qa) : ApiControllerBase
{
    [HttpGet("lessons/{lessonId:guid}/questions")]
    public async Task<IActionResult> Questions(Guid lessonId, [FromQuery] PagedQuery query, CancellationToken ct) =>
        FromResult(await qa.ListAsync(User.GetUserId(), lessonId, query, ct));

    [HttpPost("lessons/{lessonId:guid}/questions")]
    public async Task<IActionResult> Ask(Guid lessonId, PostContentRequest request, CancellationToken ct) =>
        FromResult(await qa.AskAsync(User.GetUserId(), lessonId, request, ct));

    [HttpPost("questions/{questionId:guid}/answers")]
    public async Task<IActionResult> Answer(Guid questionId, PostContentRequest request, CancellationToken ct) =>
        FromResult(await qa.AnswerAsync(User.GetUserId(), questionId, request, ct));

    [HttpPut("questions/{questionId:guid}/resolved")]
    public async Task<IActionResult> Resolve(Guid questionId, SetResolvedRequest request, CancellationToken ct) =>
        FromResult(await qa.SetResolvedAsync(User.GetUserId(), questionId, request.IsResolved, ct));

    [HttpDelete("questions/{questionId:guid}")]
    public async Task<IActionResult> DeleteQuestion(Guid questionId, CancellationToken ct) =>
        FromResult(await qa.DeleteQuestionAsync(User.GetUserId(), questionId, ct));

    [HttpDelete("answers/{answerId:guid}")]
    public async Task<IActionResult> DeleteAnswer(Guid answerId, CancellationToken ct) =>
        FromResult(await qa.DeleteAnswerAsync(User.GetUserId(), answerId, ct));

    [HttpGet("lessons/{lessonId:guid}/notes")]
    public async Task<IActionResult> Notes(Guid lessonId, CancellationToken ct) =>
        FromResult(await qa.ListNotesAsync(User.GetUserId(), lessonId, ct));

    [HttpPost("lessons/{lessonId:guid}/notes")]
    public async Task<IActionResult> AddNote(Guid lessonId, SaveNoteRequest request, CancellationToken ct) =>
        FromResult(await qa.AddNoteAsync(User.GetUserId(), lessonId, request, ct));

    [HttpPut("notes/{noteId:guid}")]
    public async Task<IActionResult> UpdateNote(Guid noteId, SaveNoteRequest request, CancellationToken ct) =>
        FromResult(await qa.UpdateNoteAsync(User.GetUserId(), noteId, request, ct));

    [HttpDelete("notes/{noteId:guid}")]
    public async Task<IActionResult> DeleteNote(Guid noteId, CancellationToken ct) =>
        FromResult(await qa.DeleteNoteAsync(User.GetUserId(), noteId, ct));
}

/// <summary>Hộp câu hỏi của giảng viên trên mọi khóa học của mình.</summary>
[Route("api/instructor/questions")]
[Authorize(Roles = $"{Roles.Instructor},{Roles.Admin}")]
public sealed class InstructorQuestionsController(ILessonQaService qa) : ApiControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedResult<InstructorQuestionDto>>> List([FromQuery] InstructorQuestionQuery query, CancellationToken ct) =>
        Ok(await qa.ListForInstructorAsync(User.GetUserId(), User.IsInRole(Roles.Admin) && !User.IsInRole(Roles.Instructor), query, ct));
}

/// <summary>Cài đặt nhận email nhắc nhở (nhắc học, nhắc giỏ hàng).</summary>
[Route("api/account/reminder-settings")]
[Authorize]
public sealed class ReminderSettingsController(IStudyReminderService reminders) : ApiControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ReminderSettingsDto>> Get(CancellationToken ct) => Ok(await reminders.GetSettingsAsync(User.GetUserId(), ct));

    [HttpPut]
    public async Task<ActionResult<ReminderSettingsDto>> Set(ReminderSettingsDto request, CancellationToken ct) =>
        Ok(await reminders.SetSettingsAsync(User.GetUserId(), request.ReminderEmailsEnabled, ct));
}
