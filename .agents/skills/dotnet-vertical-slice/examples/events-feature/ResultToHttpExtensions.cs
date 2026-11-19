using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using MyApp.Domain.Common;

namespace MyApp.Features.Common;

public static class ResultToHttpExtensions
{
    public static ProblemHttpResult ToProblemHttpResult(this Result result)
    {
        if (result.IsSuccess)
        {
            throw new InvalidOperationException("Cannot convert a successful result to a problem HTTP result.");
        }

        return result.Error.Type switch
        {
            ErrorType.Validation => TypedResults.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Validation Error",
                detail: result.Error.Message,
                extensions: new Dictionary<string, object?> { ["errorCode"] = result.Error.Code }),

            ErrorType.NotFound => TypedResults.Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "Not Found",
                detail: result.Error.Message,
                extensions: new Dictionary<string, object?> { ["errorCode"] = result.Error.Code }),

            ErrorType.Conflict => TypedResults.Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Conflict",
                detail: result.Error.Message,
                extensions: new Dictionary<string, object?> { ["errorCode"] = result.Error.Code }),

            ErrorType.Unauthorized => TypedResults.Problem(
                statusCode: StatusCodes.Status401Unauthorized,
                title: "Unauthorized",
                detail: result.Error.Message,
                extensions: new Dictionary<string, object?> { ["errorCode"] = result.Error.Code }),

            _ => TypedResults.Problem(
                statusCode: StatusCodes.Status500InternalServerError,
                title: "Internal Error",
                detail: result.Error.Message,
                extensions: new Dictionary<string, object?> { ["errorCode"] = result.Error.Code })
        };
    }
}
