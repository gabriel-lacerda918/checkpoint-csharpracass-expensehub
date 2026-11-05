using System;
using System.Linq;
using ExpenseHub.Api.Application.Exceptions;
using ExpenseHub.Api.Domain;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace ExpenseHub.Api.Http;

/// <summary>
/// Translates application exceptions to a consistent ProblemDetails contract.
/// </summary>
internal static class ApiExceptionMapper
{
    public static ProblemDetails? Map(Exception exception)
    {
        return exception switch
        {
            ValidationFailedException validation => new HttpValidationProblemDetails(
                validation.Errors.ToDictionary(pair => pair.Key, pair => pair.Value))
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Bad Request",
                Type = "https://tools.ietf.org/html/rfc9110#section-15.5.1",
                Detail = validation.Message,
            },
            AuthenticationFailedException => Create(StatusCodes.Status401Unauthorized, "Unauthorized", "15.5.2", exception.Message),
            ForbiddenException => Create(StatusCodes.Status403Forbidden, "Forbidden", "15.5.4", exception.Message),
            NotFoundException => Create(StatusCodes.Status404NotFound, "Not Found", "15.5.5", exception.Message),
            StateConflictException => Create(StatusCodes.Status409Conflict, "Conflict", "15.5.10", exception.Message),
            ReportPublishException publish => CreateUnavailable(publish),
            _ => null,
        };
    }

    private static ProblemDetails CreateUnavailable(ReportPublishException exception)
    {
        ProblemDetails problem = Create(
            StatusCodes.Status503ServiceUnavailable,
            "Service Unavailable",
            "15.6.4",
            "The report was registered but could not be queued for processing. Query it by id; a new request creates a different report.");
        problem.Extensions["reportId"] = exception.ReportId;

        return problem;
    }

    private static ProblemDetails Create(int status, string title, string section, string detail)
    {
        return new ProblemDetails
        {
            Status = status,
            Title = title,
            Type = "https://tools.ietf.org/html/rfc9110#section-" + section,
            Detail = detail,
        };
    }
}
