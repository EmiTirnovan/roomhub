namespace RoomHub.Api.Common;

public enum ErrorType
{
    Failure = 0,
    Validation = 1,   // HTTP 400 Bad Request
    NotFound = 2,     // HTTP 404 Not Found
    Conflict = 3      // HTTP 409 Conflict
}

public sealed record Error(string Code, string Message, ErrorType Type = ErrorType.Failure)
{
    public static readonly Error None = new(string.Empty, string.Empty);

    public static Error Validation(string code, string message) => new(code, message, ErrorType.Validation);
    public static Error NotFound(string code, string message) => new(code, message, ErrorType.NotFound);
    public static Error Conflict(string code, string message) => new(code, message, ErrorType.Conflict);
    public static Error Failure(string code, string message) => new(code, message, ErrorType.Failure);
}
