using System.Security.Claims;
using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using OnlineLearning.Application.Common.Interfaces;

namespace OnlineLearning.API.Common;

/// <summary>Đọc thông tin người dùng hiện tại từ JWT (claim "sub", "role").</summary>
public sealed class CurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    private ClaimsPrincipal? User => accessor.HttpContext?.User;

    public Guid? UserId =>
        Guid.TryParse(User?.FindFirstValue("sub"), out var id) ? id : null;

    public bool IsAuthenticated => User?.Identity?.IsAuthenticated == true;

    public bool IsInRole(string role) => User?.IsInRole(role) == true;

    public string? IpAddress => accessor.HttpContext?.Connection.RemoteIpAddress?.ToString();
}

public static class ClaimsPrincipalExtensions
{
    public static Guid GetUserId(this ClaimsPrincipal user) =>
        Guid.TryParse(user.FindFirstValue("sub"), out var id)
            ? id
            : throw new UnauthorizedAccessException("Thiếu claim sub.");
}

/// <summary>Chạy FluentValidation cho mọi tham số action có validator; trả 400 ValidationProblem.</summary>
public sealed class ValidationFilter(IServiceProvider services) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        foreach (var argument in context.ActionArguments.Values)
        {
            if (argument is null) continue;
            var validatorType = typeof(IValidator<>).MakeGenericType(argument.GetType());
            if (services.GetService(validatorType) is not IValidator validator) continue;

            var result = await validator.ValidateAsync(new ValidationContext<object>(argument), context.HttpContext.RequestAborted);
            if (result.IsValid) continue;

            foreach (var error in result.Errors)
                context.ModelState.AddModelError(error.PropertyName, error.ErrorMessage);

            var problem = new ValidationProblemDetails(context.ModelState)
            {
                Title = "Dữ liệu không hợp lệ.",
                Status = StatusCodes.Status400BadRequest,
            };
            context.Result = new BadRequestObjectResult(problem);
            return;
        }

        await next();
    }
}

/// <summary>Bắt lỗi chưa xử lý → ProblemDetails, không lộ stack trace ra ngoài.</summary>
public sealed class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger, IProblemDetailsService problemDetails) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var (status, title) = exception switch
        {
            UnauthorizedAccessException => (StatusCodes.Status401Unauthorized, "Bạn cần đăng nhập."),
            Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException => (StatusCodes.Status409Conflict, "Dữ liệu vừa được thay đổi bởi thao tác khác. Vui lòng thử lại."),
            Microsoft.EntityFrameworkCore.DbUpdateException => (StatusCodes.Status409Conflict, "Không thể lưu dữ liệu do xung đột ràng buộc."),
            BadHttpRequestException => (StatusCodes.Status400BadRequest, "Yêu cầu không hợp lệ."),
            _ => (StatusCodes.Status500InternalServerError, "Đã xảy ra lỗi hệ thống. Vui lòng thử lại sau."),
        };

        if (status == StatusCodes.Status500InternalServerError)
            logger.LogError(exception, "Lỗi chưa xử lý: {Path}", httpContext.Request.Path);
        else
            logger.LogWarning(exception, "Yêu cầu thất bại {Status}: {Path}", status, httpContext.Request.Path);

        httpContext.Response.StatusCode = status;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = { Status = status, Title = title },
        });
    }
}
