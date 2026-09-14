using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace EquipmentManagementBackend.Services;

public class ServiceException(int statusCode, string message) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
}

public sealed class ServiceExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken ct)
    {
        var error = exception switch
        {
            ServiceException e => (e.StatusCode, e.Message),
            DbUpdateException { InnerException: PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } }
                => (409, "This record or active reservation already exists. Refresh and retry."),
            DbUpdateException { InnerException: PostgresException { SqlState: PostgresErrorCodes.ForeignKeyViolation } }
                => (400, "A referenced record does not exist or is still in use."),
            _ => (0, "")
        };
        if (error.Item1 == 0) return false;
        context.Response.StatusCode = error.Item1;
        await context.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Status = error.Item1, Title = error.Item2
        }, ct);
        return true;
    }
}
