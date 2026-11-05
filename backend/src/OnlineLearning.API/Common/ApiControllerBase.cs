using Microsoft.AspNetCore.Mvc;
using OnlineLearning.Application.Common.Models;

namespace OnlineLearning.API.Common;

[ApiController]
[Produces("application/json")]
public abstract class ApiControllerBase : ControllerBase
{
    protected IActionResult FromResult(Result result) =>
        result.IsSuccess ? NoContent() : Problem(result.Error!);

    protected IActionResult FromResult<T>(Result<T> result) =>
        result.IsSuccess ? Ok(result.Value) : Problem(result.Error!);

    protected IActionResult Created<T>(Result<T> result, string actionName, Func<T, object> routeValues) =>
        result.IsSuccess ? CreatedAtAction(actionName, routeValues(result.Value!), result.Value) : Problem(result.Error!);

    protected IActionResult Problem(Error error)
    {
        var status = error.Type switch
        {
            ErrorType.Validation => StatusCodes.Status400BadRequest,
            ErrorType.NotFound => StatusCodes.Status404NotFound,
            ErrorType.Conflict => StatusCodes.Status409Conflict,
            ErrorType.Forbidden => StatusCodes.Status403Forbidden,
            ErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
            _ => StatusCodes.Status400BadRequest,
        };

        var problem = ProblemDetailsFactory.CreateProblemDetails(HttpContext, status, title: error.Message, detail: error.Message);
        problem.Extensions["code"] = error.Code;
        return new ObjectResult(problem) { StatusCode = status };
    }
}
