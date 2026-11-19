using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using MyApp.Domain.Common;

namespace MyApp.Features.Common;

public static class ProblemDetailsMappingExtensions
{
    /// <summary>
    /// Translates Domain Error to RFC 7807 ProblemHttpResult.
    /// Maps ErrorType to appropriate HTTP status codes without throwing exceptions.
    /// </summary>
    public static ProblemHttpResult ToProblemDetails(this Error error)
    {
        var (statusCode, title) = error.Type switch
        {
            ErrorType.Validation => (StatusCodes.Status400BadRequest, "Validation Error"),
            ErrorType.NotFound => (StatusCodes.Status404NotFound, "Resource Not Found"),
            ErrorType.Conflict => (StatusCodes.Status409Conflict, "Conflict"),
            ErrorType.Unauthorized => (StatusCodes.Status401Unauthorized, "Unauthorized"),
            _ => (StatusCodes.Status500InternalServerError, "Internal Server Error")
        };

        return TypedResults.Problem(
            statusCode: statusCode,
            title: title,
            detail: error.Message,
            extensions: new Dictionary<string, object?>
            {
                ["errorCode"] = error.Code,
                ["errorType"] = error.Type.ToString()
            });
    }

    /// <summary>
    /// Translates a failed Result to ProblemHttpResult.
    /// </summary>
    public static ProblemHttpResult ToProblemDetails(this Result result)
    {
        if (result.IsSuccess)
        {
            throw new InvalidOperationException("Successful result cannot be mapped to ProblemDetails.");
        }

        return result.Error.ToProblemDetails();
    }
}
