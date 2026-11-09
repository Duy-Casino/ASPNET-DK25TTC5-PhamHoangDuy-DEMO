namespace OnlineLearning.Application.Common.Models;

public enum ErrorType { Validation, NotFound, Conflict, Forbidden, Unauthorized, Failure }

public sealed record Error(string Code, string Message, ErrorType Type = ErrorType.Failure)
{
    public static Error NotFound(string message) => new("not_found", message, ErrorType.NotFound);
    public static Error Conflict(string message) => new("conflict", message, ErrorType.Conflict);
    public static Error Forbidden(string message = "Bạn không có quyền thực hiện thao tác này.") => new("forbidden", message, ErrorType.Forbidden);
    public static Error Validation(string message) => new("validation", message, ErrorType.Validation);
}

public class Result
{
    protected Result(bool isSuccess, Error? error)
    {
        IsSuccess = isSuccess;
        Error = error;
    }

    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;
    public Error? Error { get; }

    public static Result Success() => new(true, null);
    public static Result Failure(Error error) => new(false, error);
    public static Result<T> Success<T>(T value) => new(value, true, null);
    public static Result<T> Failure<T>(Error error) => new(default, false, error);
}

public sealed class Result<T> : Result
{
    internal Result(T? value, bool isSuccess, Error? error) : base(isSuccess, error)
    {
        Value = value;
    }

    public T? Value { get; }

    public static implicit operator Result<T>(T value) => Success(value);
    public static implicit operator Result<T>(Error error) => Failure<T>(error);
}

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount)
{
    public int TotalPages => PageSize == 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
}
