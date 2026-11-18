using System.Security.Claims;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SmartERP.Application.Common;

namespace SmartERP.Api.Infrastructure;

/// <summary>Maps domain/application exceptions to RFC 7807 problem responses.</summary>
public class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> log, IProblemDetailsService problems) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext ctx, Exception ex, CancellationToken ct)
    {
        var (status, title, detail) = ex switch
        {
            NotFoundException => (404, "Not found", ex.Message),
            BusinessRuleException => (400, "Business rule violation", ex.Message),
            UnauthorizedException => (401, "Unauthorized", ex.Message),
            DbUpdateConcurrencyException => (409, "Concurrency conflict", "The record was changed by someone else. Reload and try again."),
            DbUpdateException { InnerException: SqlException { Number: 2601 or 2627 } } => (409, "Duplicate", "A record with the same unique value already exists."),
            DbUpdateException { InnerException: SqlException { Number: 547 } } => (400, "Constraint violation", "The operation violates a data integrity rule (e.g. stock cannot go negative)."),
            HttpRequestException => (502, "AI provider error", ex.Message),
            OperationCanceledException => (499, "Request cancelled", "The request was cancelled."),
            _ => (500, "Server error", "An unexpected error occurred.")
        };
        if (status >= 500) log.LogError(ex, "Unhandled exception");
        else log.LogInformation("{Title}: {Message}", title, ex.Message);

        ctx.Response.StatusCode = status;
        return await problems.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = ctx,
            ProblemDetails = new ProblemDetails { Status = status, Title = title, Detail = detail },
            Exception = ex
        });
    }
}

public static class ClaimsExtensions
{
    public static int UserId(this ClaimsPrincipal user) =>
        int.TryParse(user.FindFirstValue("sub"), out var id) ? id : throw new UnauthorizedException("Invalid token.");

    public static string UserName(this ClaimsPrincipal user) => user.FindFirstValue("unique_name") ?? "unknown";
}
