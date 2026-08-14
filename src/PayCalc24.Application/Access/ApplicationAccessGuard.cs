using PayCalc24.Contracts.Access;
using PayCalc24.Contracts.Diagnostics;

namespace PayCalc24.Application.Access;

public sealed class ApplicationAccessGuard(
    IApplicationPrincipal principal,
    IProductEntitlementService entitlements) : IApplicationAccessGuard, ICapabilityService
{
    public async ValueTask<AccessDecision> EvaluateAsync(
        ApplicationAction action,
        WorkflowAccess workflow,
        CancellationToken cancellationToken = default)
    {
        if (!principal.IsAuthenticated)
            return Deny(DiagnosticCodes.AuthUnauthenticated);
        if (!principal.IsEnabled)
            return Deny(DiagnosticCodes.AuthPrincipalDisabled);
        if (principal.Context.CompanyId.Value == Guid.Empty)
            return Deny(DiagnosticCodes.CompanyScopeMismatch);
        if (!principal.HasPermission(action.PermissionCode))
            return Deny(DiagnosticCodes.AuthPermissionDenied, ("permission", action.PermissionCode));

        var entitlement = await entitlements.GetAsync(principal.Context.CompanyId, cancellationToken);
        if (action.ProtectedMutation)
        {
            var entitlementDenial = EntitlementDenial(entitlement, action.FeatureCode);
            if (entitlementDenial is not null)
                return Deny(entitlementDenial);
        }

        if (!workflow.Allowed)
            return Deny(workflow.DiagnosticCode ?? DiagnosticCodes.ApiInvalidRequest);

        return new(true);
    }

    public async ValueTask EnsureAllowedAsync(
        ApplicationAction action,
        WorkflowAccess workflow,
        CancellationToken cancellationToken = default)
    {
        var decision = await EvaluateAsync(action, workflow, cancellationToken);
        if (!decision.Allowed)
            throw new ApplicationAccessDeniedException(decision.Diagnostic!);
    }

    public async ValueTask<IReadOnlyList<ActionCapability>> GetAllowedActionsAsync(
        IReadOnlyList<(ApplicationAction Action, WorkflowAccess Workflow)> actions,
        CancellationToken cancellationToken = default)
    {
        var result = new List<ActionCapability>(actions.Count);
        foreach (var item in actions)
        {
            var decision = await EvaluateAsync(item.Action, item.Workflow, cancellationToken);
            result.Add(new(
                item.Action.Code,
                decision.Allowed,
                decision.Diagnostic?.Code,
                item.Action.Sensitivity,
                item.Action.RequiresConfirmation));
        }

        return result;
    }

    private static string? EntitlementDenial(ProductEntitlement entitlement, string? featureCode) =>
        entitlement.Status switch
        {
            ProductEntitlementStatus.Expired => DiagnosticCodes.LicenseExpired,
            ProductEntitlementStatus.Blocked => DiagnosticCodes.LicenseBlocked,
            ProductEntitlementStatus.Unavailable => DiagnosticCodes.LicenseEntitlementUnavailable,
            _ when featureCode is not null && entitlement.Features is not null &&
                !entitlement.Features.Contains(featureCode) => DiagnosticCodes.LicenseFeatureUnavailable,
            _ => null
        };

    private static AccessDecision Deny(string code, params (string Key, object? Value)[] details) =>
        new(false, new Diagnostic(
            code,
            DiagnosticSeverity.Error,
            details.ToDictionary(x => x.Key, x => x.Value)));
}
