using System.Text.Json;
using PayCalc24.Application.Access;
using PayCalc24.Contracts.Access;
using PayCalc24.Contracts.Diagnostics;
using PayCalc24.Contracts.Identity;
using PayCalc24.Contracts.Operations;

namespace PayCalc24.ApplicationTests;

public sealed class Task20AApplicationAccessTests
{
    private static readonly CompanyId CompanyA = CompanyId.From(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"));
    private static readonly CompanyId CompanyB = CompanyId.From(Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"));
    private static readonly UserId Human = UserId.From(Guid.Parse("11111111-1111-1111-1111-111111111111"));
    private static readonly UserId Agent = UserId.From(Guid.Parse("22222222-2222-2222-2222-222222222222"));
    private static readonly ApplicationAction Calculate = ApplicationActionCatalog.All["PAYROLL.CALCULATE"];

    [Theory]
    [InlineData(PrincipalType.Human, AccessChannel.Desktop)]
    [InlineData(PrincipalType.AiAgent, AccessChannel.AgentGateway)]
    public async Task HumanAndAgentUseSameCanonicalHandler(PrincipalType type, AccessChannel channel)
    {
        var dispatcher = new Dispatcher();
        var service = Service(Principal(type == PrincipalType.Human ? Human : Agent, type, channel, "PAYROLL.CALCULATE"),
            ProductEntitlementStatus.Active, dispatcher);

        var response = await service.ExecuteAsync(Calculate, WorkflowAccess.Allow(), Request(CompanyA));

        Assert.True(response.Success);
        Assert.Equal("PAYROLL.CALCULATE", dispatcher.LastOperation);
        Assert.Equal(1, dispatcher.CallCount);
    }

    [Theory]
    [InlineData(ProductEntitlementStatus.Expired, DiagnosticCodes.LicenseExpired)]
    [InlineData(ProductEntitlementStatus.Blocked, DiagnosticCodes.LicenseBlocked)]
    [InlineData(ProductEntitlementStatus.Unavailable, DiagnosticCodes.LicenseEntitlementUnavailable)]
    public async Task EntitlementDenialIsBelowUiAndNeverDispatches(ProductEntitlementStatus status, string code)
    {
        var dispatcher = new Dispatcher();
        var service = Service(Principal(Human, PrincipalType.Human, AccessChannel.Desktop, "PAYROLL.CALCULATE"), status, dispatcher);
        var exception = await Assert.ThrowsAsync<ApplicationAccessDeniedException>(
            async () => await service.ExecuteAsync(Calculate, WorkflowAccess.Allow(), Request(CompanyA)));
        Assert.Equal(code, exception.Diagnostic.Code);
        Assert.Equal(0, dispatcher.CallCount);
    }

    [Fact]
    public async Task PermissionAndEntitlementAreIndependent()
    {
        var dispatcher = new Dispatcher();
        var service = Service(Principal(Agent, PrincipalType.AiAgent, AccessChannel.AgentGateway), ProductEntitlementStatus.Active, dispatcher);
        var exception = await Assert.ThrowsAsync<ApplicationAccessDeniedException>(
            async () => await service.ExecuteAsync(Calculate, WorkflowAccess.Allow(), Request(CompanyA)));
        Assert.Equal(DiagnosticCodes.AuthPermissionDenied, exception.Diagnostic.Code);
        Assert.Equal(0, dispatcher.CallCount);
    }

    [Fact]
    public async Task DisabledAgentUsesNormalAccountDenial()
    {
        var dispatcher = new Dispatcher();
        var principal = Principal(Agent, PrincipalType.AiAgent, AccessChannel.AgentGateway, "PAYROLL.CALCULATE") with { Enabled = false };
        var service = Service(principal, ProductEntitlementStatus.Active, dispatcher);
        var exception = await Assert.ThrowsAsync<ApplicationAccessDeniedException>(
            async () => await service.ExecuteAsync(Calculate, WorkflowAccess.Allow(), Request(CompanyA)));
        Assert.Equal(DiagnosticCodes.AuthPrincipalDisabled, exception.Diagnostic.Code);
    }

    [Fact]
    public async Task CrossCompanyRequestNeverDispatches()
    {
        var dispatcher = new Dispatcher();
        var service = Service(Principal(Agent, PrincipalType.AiAgent, AccessChannel.AgentGateway, "PAYROLL.CALCULATE"),
            ProductEntitlementStatus.Active, dispatcher);
        var exception = await Assert.ThrowsAsync<ApplicationAccessDeniedException>(
            async () => await service.ExecuteAsync(Calculate, WorkflowAccess.Allow(), Request(CompanyB)));
        Assert.Equal(DiagnosticCodes.CompanyScopeMismatch, exception.Diagnostic.Code);
        Assert.Equal(0, dispatcher.CallCount);
    }

    [Fact]
    public async Task CapabilityUsesExactExecutionGuardDiagnostic()
    {
        var principal = Principal(Agent, PrincipalType.AiAgent, AccessChannel.AgentGateway, "PAYROLL.CALCULATE");
        var guard = new ApplicationAccessGuard(principal, new Entitlements(ProductEntitlementStatus.Expired));
        var capabilities = await guard.GetAllowedActionsAsync([(Calculate, WorkflowAccess.Allow())]);
        var execution = await guard.EvaluateAsync(Calculate, WorkflowAccess.Allow());
        Assert.False(capabilities.Single().Allowed);
        Assert.Equal(execution.Diagnostic?.Code, capabilities.Single().DiagnosticCode);
    }

    [Fact]
    public async Task WorkflowDenialPreventsExecution()
    {
        var dispatcher = new Dispatcher();
        var service = Service(Principal(Human, PrincipalType.Human, AccessChannel.Desktop, "PAYROLL.CALCULATE"),
            ProductEntitlementStatus.Active, dispatcher);
        var exception = await Assert.ThrowsAsync<ApplicationAccessDeniedException>(async () =>
            await service.ExecuteAsync(Calculate, WorkflowAccess.Deny(DiagnosticCodes.PayrollCalculationSnapshotNotFrozen), Request(CompanyA)));
        Assert.Equal(DiagnosticCodes.PayrollCalculationSnapshotNotFrozen, exception.Diagnostic.Code);
        Assert.Equal(0, dispatcher.CallCount);
    }

    private static UnifiedApplicationService Service(TestPrincipal principal, ProductEntitlementStatus status, Dispatcher dispatcher)
    {
        var guard = new ApplicationAccessGuard(principal, new Entitlements(status));
        return new UnifiedApplicationService(principal, guard, dispatcher, new Audit(), TimeProvider.System);
    }

    private static TestPrincipal Principal(UserId id, PrincipalType type, AccessChannel channel, params string[] permissions) =>
        new(new(CompanyA, id, type, channel, "correlation-20a", "idempotency-20a", type == PrincipalType.AiAgent ? "AI_HR1" : null), true, true, permissions.ToHashSet());
    private static ApplicationOperationRequest Request(CompanyId company) =>
        new("PAYROLL.CALCULATE", company, "period-1", JsonSerializer.SerializeToElement(new { period = "2026-08" }), "idempotency-20a");

    private sealed record TestPrincipal(ApplicationRequestContext Context, bool Authenticated, bool Enabled, HashSet<string> Permissions) : IApplicationPrincipal
    {
        public bool IsAuthenticated => Authenticated;
        public bool IsEnabled => Enabled;
        public bool HasPermission(string permissionCode) => Permissions.Contains(permissionCode);
    }
    private sealed class Entitlements(ProductEntitlementStatus status) : IProductEntitlementService
    {
        public ValueTask<ProductEntitlement> GetAsync(CompanyId companyId, CancellationToken cancellationToken = default) => ValueTask.FromResult(new ProductEntitlement(status));
    }
    private sealed class Dispatcher : IApplicationOperationDispatcher
    {
        public int CallCount { get; private set; }
        public string? LastOperation { get; private set; }
        public ValueTask<ApplicationOperationResult> DispatchAsync(ApplicationOperationRequest request, CancellationToken cancellationToken = default)
        {
            CallCount++; LastOperation = request.Operation;
            return ValueTask.FromResult(new ApplicationOperationResult(JsonSerializer.SerializeToElement(new { prepared = true }), request.ResourceId));
        }
    }
    private sealed class Audit : IAuditWriter
    {
        public ValueTask WriteAsync(AuditEntry entry, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
    }
}
