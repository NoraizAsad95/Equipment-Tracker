using EquipmentTracker.Api.Services;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EquipmentTracker.Api.Middleware;

public class DomainExceptionHandler(IProblemDetailsService problemDetails) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken ct)
    {
        var (status, detail) = exception switch
        {
            DomainException d => (d.StatusCode, d.Message),
            // Someone else changed the same equipment first (AD-5).
            DbUpdateConcurrencyException => (StatusCodes.Status409Conflict,
                "Someone else just changed this equipment. Please refresh and try again."),
            _ => (0, "")
        };
        if (status == 0) return false;

        context.Response.StatusCode = status;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = context,
            Exception = exception,
            ProblemDetails = new ProblemDetails { Status = status, Detail = detail }
        });
    }
}
