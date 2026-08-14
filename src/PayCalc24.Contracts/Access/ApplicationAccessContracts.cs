using PayCalc24.Contracts.Diagnostics;
using PayCalc24.Contracts.Identity;
using System.Text.Json;

namespace PayCalc24.Contracts.Access;

public enum PrincipalType { Human, AiAgent }
public enum AccessChannel { Desktop, AgentGateway, Web, Mobile }
public enum ProductEntitlementStatus { Active, GracePeriod, Expired, Blocked, Unavailable }
public enum ActionSensitivity { READ, EXECUTE, SENSITIVE, IRREVERSIBLE }

public sealed record ApplicationRequestContext(
    CompanyId CompanyId,
    UserId ActorUserId,
    PrincipalType PrincipalType,
    AccessChannel Channel,
    string CorrelationId,
    string? IdempotencyKey = null,
    string? AgentCode = null,
    UserId? RequestedByUserId = null);

public interface IApplicationPrincipal
{
    ApplicationRequestContext Context { get; }
    bool IsAuthenticated { get; }
    bool IsEnabled { get; }
    bool HasPermission(string permissionCode);
}

public sealed record ProductEntitlement(
    ProductEntitlementStatus Status,
    DateTimeOffset? ValidUntilUtc = null,
    IReadOnlySet<string>? Features = null);

/// <summary>Adapter over the existing online account/subscription source of truth.</summary>
public interface IProductEntitlementService
{
    ValueTask<ProductEntitlement> GetAsync(CompanyId companyId, CancellationToken cancellationToken = default);
}

public sealed record WorkflowAccess(bool Allowed, string? DiagnosticCode = null)
{
    public static WorkflowAccess Allow() => new(true);
    public static WorkflowAccess Deny(string diagnosticCode) => new(false, diagnosticCode);
}

public sealed record ApplicationAction(
    string Code,
    string PermissionCode,
    bool ProtectedMutation,
    ActionSensitivity Sensitivity,
    bool RequiresConfirmation = false,
    string? FeatureCode = null);

public sealed record ActionCapability(
    string Action,
    bool Allowed,
    string? DiagnosticCode,
    ActionSensitivity Sensitivity,
    bool RequiresConfirmation);

public sealed record AccessDecision(bool Allowed, Diagnostic? Diagnostic = null);

public interface IApplicationAccessGuard
{
    ValueTask<AccessDecision> EvaluateAsync(
        ApplicationAction action,
        WorkflowAccess workflow,
        CancellationToken cancellationToken = default);

    ValueTask EnsureAllowedAsync(
        ApplicationAction action,
        WorkflowAccess workflow,
        CancellationToken cancellationToken = default);
}

public interface ICapabilityService
{
    ValueTask<IReadOnlyList<ActionCapability>> GetAllowedActionsAsync(
        IReadOnlyList<(ApplicationAction Action, WorkflowAccess Workflow)> actions,
        CancellationToken cancellationToken = default);
}

public interface IWorkflowAccessResolver
{
    ValueTask<WorkflowAccess> ResolveAsync(
        string actionCode,
        string? resourceId,
        CancellationToken cancellationToken = default);
}

public sealed class ApplicationAccessDeniedException(Diagnostic diagnostic) : Exception(diagnostic.Code)
{
    public Diagnostic Diagnostic { get; } = diagnostic;
}

public sealed record ApiResponse<T>(
    bool Success,
    T? Data,
    IReadOnlyList<Diagnostic> Diagnostics,
    string CorrelationId,
    string? ResourceId = null,
    bool? IdempotentReplay = null);

public sealed record ApplicationOperationRequest(
    string Operation,
    CompanyId CompanyId,
    string? ResourceId,
    JsonElement Payload,
    string? IdempotencyKey = null);

public sealed record ApplicationOperationResult(
    JsonElement Data,
    string? ResourceId = null,
    bool IdempotentReplay = false);

/// <summary>
/// Canonical application dispatcher implemented by composition adapters over existing
/// module command/query contracts. It is shared by HTTP, Desktop and future clients.
/// </summary>
public interface IApplicationOperationDispatcher
{
    ValueTask<ApplicationOperationResult> DispatchAsync(
        ApplicationOperationRequest request,
        CancellationToken cancellationToken = default);
}

public interface IUnifiedApplicationService
{
    ValueTask<ApiResponse<JsonElement>> ExecuteAsync(
        ApplicationAction action,
        WorkflowAccess workflow,
        ApplicationOperationRequest request,
        CancellationToken cancellationToken = default);
}
