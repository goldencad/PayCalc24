using System.Text.Json;
using PayCalc24.Contracts.Access;
using PayCalc24.Contracts.Diagnostics;
using PayCalc24.Contracts.Operations;

namespace PayCalc24.Application.Access;

public sealed class UnifiedApplicationService(
    IApplicationPrincipal principal,
    IApplicationAccessGuard accessGuard,
    IApplicationOperationDispatcher dispatcher,
    IAuditWriter auditWriter,
    TimeProvider timeProvider) : IUnifiedApplicationService
{
    public async ValueTask<ApiResponse<JsonElement>> ExecuteAsync(
        ApplicationAction action,
        WorkflowAccess workflow,
        ApplicationOperationRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.CompanyId != principal.Context.CompanyId)
            throw Denied(DiagnosticCodes.CompanyScopeMismatch);

        await accessGuard.EnsureAllowedAsync(action, workflow, cancellationToken);

        try
        {
            var result = await dispatcher.DispatchAsync(request, cancellationToken);
            await AuditAsync(action.Code, request.ResourceId, "SUCCEEDED", cancellationToken);
            return new(
                true,
                result.Data,
                [],
                principal.Context.CorrelationId,
                result.ResourceId,
                result.IdempotentReplay);
        }
        catch
        {
            await AuditAsync(action.Code, request.ResourceId, "FAILED", cancellationToken);
            throw;
        }
    }

    private ValueTask AuditAsync(string action, string? resourceId, string outcome, CancellationToken ct) =>
        auditWriter.WriteAsync(new(
            principal.Context.CompanyId,
            principal.Context.ActorUserId,
            action,
            "ApplicationOperation",
            resourceId,
            principal.Context.CorrelationId,
            timeProvider.GetUtcNow(),
            new Dictionary<string, object?>
            {
                ["principalType"] = principal.Context.PrincipalType.ToString(),
                ["channel"] = principal.Context.Channel.ToString(),
                ["agentCode"] = principal.Context.AgentCode,
                ["requestedByUserId"] = principal.Context.RequestedByUserId?.Value,
                ["idempotencyKey"] = principal.Context.IdempotencyKey,
                ["outcome"] = outcome
            }), ct);

    private static ApplicationAccessDeniedException Denied(string code) =>
        new(new Diagnostic(code, DiagnosticSeverity.Error, new Dictionary<string, object?>()));
}
