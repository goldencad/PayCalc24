using System.Security.Claims;
using PayCalc24.Contracts.Access;
using PayCalc24.Contracts.Diagnostics;
using PayCalc24.Contracts.Identity;
using PayCalc24.Contracts.Operations;

namespace PayCalc24.Api.Security;

public sealed class ClaimsApplicationPrincipal(IHttpContextAccessor accessor) : IApplicationPrincipal
{
    private ClaimsPrincipal User => accessor.HttpContext?.User ?? new ClaimsPrincipal();
    public bool IsAuthenticated => User.Identity?.IsAuthenticated == true;
    public bool IsEnabled => !string.Equals(User.FindFirstValue("account_status"), "disabled", StringComparison.OrdinalIgnoreCase);
    public bool HasPermission(string permissionCode) => User.FindAll("permission").Any(x => x.Value == permissionCode);

    public ApplicationRequestContext Context
    {
        get
        {
            var correlation = accessor.HttpContext?.Request.Headers["X-Correlation-Id"].FirstOrDefault();
            if (string.IsNullOrWhiteSpace(correlation)) correlation = accessor.HttpContext?.TraceIdentifier ?? Guid.NewGuid().ToString("D");
            return new(
                new CompanyId(ParseGuid("company_id")),
                new UserId(ParseGuid(ClaimTypes.NameIdentifier, "sub", "user_id")),
                ParseEnum(User.FindFirstValue("principal_type"), PrincipalType.Human),
                ParseEnum(User.FindFirstValue("channel"), AccessChannel.Web),
                correlation,
                accessor.HttpContext?.Request.Headers["Idempotency-Key"].FirstOrDefault(),
                User.FindFirstValue("agent_code"),
                TryUserId(User.FindFirstValue("requested_by_user_id")));
        }
    }

    private Guid ParseGuid(params string[] claimTypes)
    {
        foreach (var type in claimTypes)
            if (Guid.TryParse(User.FindFirstValue(type), out var value) && value != Guid.Empty) return value;
        return Guid.Empty;
    }

    private static UserId? TryUserId(string? value) => Guid.TryParse(value, out var id) && id != Guid.Empty ? UserId.From(id) : null;
    private static T ParseEnum<T>(string? value, T fallback) where T : struct => Enum.TryParse<T>(value, true, out var result) ? result : fallback;
}

public sealed class FailClosedEntitlementService : IProductEntitlementService
{
    public ValueTask<ProductEntitlement> GetAsync(CompanyId companyId, CancellationToken cancellationToken = default) =>
        ValueTask.FromResult(new ProductEntitlement(ProductEntitlementStatus.Unavailable));
}

public sealed class FailClosedWorkflowAccessResolver : IWorkflowAccessResolver
{
    public ValueTask<WorkflowAccess> ResolveAsync(string actionCode, string? resourceId, CancellationToken cancellationToken = default) =>
        ValueTask.FromResult(WorkflowAccess.Deny(DiagnosticCodes.ApiInvalidRequest));
}

public sealed class UnconfiguredOperationDispatcher : IApplicationOperationDispatcher
{
    public ValueTask<ApplicationOperationResult> DispatchAsync(ApplicationOperationRequest request, CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException("No application operation adapter is configured.");
}

public sealed class NullAuditWriter : IAuditWriter
{
    public ValueTask WriteAsync(AuditEntry entry, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
}

public sealed class ApiExceptionMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try { await next(context); }
        catch (ApplicationAccessDeniedException exception)
        {
            context.Response.StatusCode = exception.Diagnostic.Code == DiagnosticCodes.AuthUnauthenticated
                ? StatusCodes.Status401Unauthorized : StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(new { success = false, diagnostics = new[] { exception.Diagnostic }, correlationId = context.TraceIdentifier });
        }
        catch (Exception)
        {
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            await context.Response.WriteAsJsonAsync(new { success = false, diagnostics = new[] { new { code = "API.INTERNAL_ERROR", severity = "Error" } }, correlationId = context.TraceIdentifier });
        }
    }
}
