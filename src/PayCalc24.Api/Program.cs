using System.Text.Json;
using PayCalc24.Api.Security;
using PayCalc24.Application.Access;
using PayCalc24.Contracts.Access;
using PayCalc24.Contracts.Operations;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IApplicationPrincipal, ClaimsApplicationPrincipal>();
builder.Services.AddScoped<IApplicationAccessGuard, ApplicationAccessGuard>();
builder.Services.AddScoped<ICapabilityService>(services => (ApplicationAccessGuard)services.GetRequiredService<IApplicationAccessGuard>());
builder.Services.AddScoped<IUnifiedApplicationService, UnifiedApplicationService>();
builder.Services.AddSingleton<IProductEntitlementService, FailClosedEntitlementService>();
builder.Services.AddSingleton<IWorkflowAccessResolver, FailClosedWorkflowAccessResolver>();
builder.Services.AddSingleton<IApplicationOperationDispatcher, UnconfiguredOperationDispatcher>();
builder.Services.AddSingleton<IAuditWriter, NullAuditWriter>();
builder.Services.AddSingleton(TimeProvider.System);

var app = builder.Build();
app.UseMiddleware<ApiExceptionMiddleware>();
app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));

var api = app.MapGroup("/api/v1");
api.MapGet("/metadata/actions", () => Results.Ok(ApplicationActionCatalog.All.Values.Select(x => new
{
    x.Code, x.PermissionCode, x.Sensitivity, x.RequiresConfirmation, x.FeatureCode
})));
api.MapGet("/capabilities", async (string? resourceId, ICapabilityService capabilities, IWorkflowAccessResolver workflow, CancellationToken ct) =>
{
    var candidates = new List<(ApplicationAction, WorkflowAccess)>();
    foreach (var action in ApplicationActionCatalog.All.Values)
        candidates.Add((action, await workflow.ResolveAsync(action.Code, resourceId, ct)));
    return Results.Ok(await capabilities.GetAllowedActionsAsync(candidates, ct));
});

MapQuery(api, "/payroll/{resourceId}/period", "PAYROLL.PERIOD.READ");
MapQuery(api, "/payroll/{resourceId}/subjects", "PAYROLL.SUBJECTS.READ");
MapQuery(api, "/payroll/{resourceId}/validation", "PAYROLL.VALIDATION.READ");
MapQuery(api, "/payroll/{resourceId}/explain", "PAYROLL.EXPLAIN.READ");
MapQuery(api, "/payroll/{resourceId}/fund", "PAYROLL.FUND.READ");
MapQuery(api, "/scenarios/{resourceId}/result", "SCENARIO.RESULT.READ");
MapQuery(api, "/approval/{resourceId}/status", "APPROVAL.STATUS.READ");
MapQuery(api, "/settlements/{resourceId}", "SETTLEMENT.READ");
MapQuery(api, "/reports/{resourceId}", "REPORT.RESULT.READ");

MapOperation(api, "/payroll/{resourceId}/prepare", "PAYROLL.PREPARE");
MapOperation(api, "/payroll/{resourceId}/freeze", "PAYROLL.FREEZE");
MapOperation(api, "/payroll/{resourceId}/calculate", "PAYROLL.CALCULATE");
MapOperation(api, "/payroll-input/{resourceId}/submit", "PAYROLL.INPUT.SUBMIT");
MapOperation(api, "/attendance/{resourceId}/preview", "ATTENDANCE.PREVIEW");
MapOperation(api, "/attendance/{resourceId}/commit", "ATTENDANCE.COMMIT");
MapOperation(api, "/kpi/{resourceId}/validate-batch", "PERFORMANCE.KPI.VALIDATE_BATCH");
MapOperation(api, "/kpi/{resourceId}/commit-batch", "PERFORMANCE.KPI.COMMIT_BATCH");
MapOperation(api, "/scenarios/{resourceId}/run", "SCENARIO.RUN");
MapOperation(api, "/approval/{resourceId}/submit", "APPROVAL.SUBMIT");
MapOperation(api, "/approval/{resourceId}/approve", "APPROVAL.APPROVE");
MapOperation(api, "/approval/{resourceId}/reject", "APPROVAL.REJECT");
MapOperation(api, "/approval/{resourceId}/lock", "APPROVAL.LOCK");
MapOperation(api, "/approval/{resourceId}/request-adjustment", "APPROVAL.REQUEST_ADJUSTMENT");
MapOperation(api, "/reports/{resourceId}/generate", "REPORT.GENERATE");
MapOperation(api, "/accounting/{resourceId}/generate", "ACCOUNTING.GENERATE");
MapOperation(api, "/accounting/{resourceId}/publish", "ACCOUNTING.PUBLISH");

app.Run();

static void MapOperation(RouteGroupBuilder api, string route, string operation)
{
    api.MapPost(route, async (string resourceId, JsonElement payload, HttpContext http, IApplicationPrincipal principal,
        IWorkflowAccessResolver workflow, IUnifiedApplicationService service, CancellationToken ct) =>
    {
        var action = ApplicationActionCatalog.All[operation];
        var state = await workflow.ResolveAsync(operation, resourceId, ct);
        var request = new ApplicationOperationRequest(operation, principal.Context.CompanyId, resourceId, payload,
            http.Request.Headers["Idempotency-Key"].FirstOrDefault());
        return Results.Ok(await service.ExecuteAsync(action, state, request, ct));
    });
}

static void MapQuery(RouteGroupBuilder api, string route, string operation)
{
    api.MapGet(route, async (string resourceId, IApplicationPrincipal principal, IWorkflowAccessResolver workflow,
        IUnifiedApplicationService service, CancellationToken ct) =>
    {
        var action = ApplicationActionCatalog.All[operation];
        var state = await workflow.ResolveAsync(operation, resourceId, ct);
        var request = new ApplicationOperationRequest(operation, principal.Context.CompanyId, resourceId,
            JsonSerializer.SerializeToElement(new { }));
        return Results.Ok(await service.ExecuteAsync(action, state, request, ct));
    });
}

public partial class Program;
