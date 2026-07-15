using VmPortal.Core.Services;

namespace VmPortal.Api.Middleware;

/// <summary>
/// Übersetzt <see cref="VirtualizationException"/> (z. B. eine fehlgeschlagene WinRM-Verbindung
/// zum Hyper-V-Host) in eine sprechende HTTP-502-Antwort, statt einen generischen 500 zu liefern.
/// </summary>
public class VirtualizationExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<VirtualizationExceptionMiddleware> _logger;

    public VirtualizationExceptionMiddleware(RequestDelegate next, ILogger<VirtualizationExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (VirtualizationException ex)
        {
            _logger.LogError(ex, "Hyper-V-Operation fehlgeschlagen");
            context.Response.StatusCode = StatusCodes.Status502BadGateway;
            await context.Response.WriteAsJsonAsync(new { message = ex.Message });
        }
    }
}
