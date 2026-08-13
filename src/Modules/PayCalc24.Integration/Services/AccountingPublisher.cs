using PayCalc24.Contracts.Diagnostics;
using PayCalc24.Contracts.Integration;
using PayCalc24.Contracts.PayrollApproval;
using PayCalc24.Contracts.PayrollCalculation;
using PayCalc24.Contracts.Identity;

namespace PayCalc24.Integration.Services;

public sealed class AccountingPublisher(IEnumerable<IExternalAccountingAdapter> adapters,
    IAccountingDeliveryRepository deliveries,Func<PayrollApprovalCaseId,PayrollApprovalCaseDto> approvalSource)
    : IPayrollAccountingPublisher
{
    private readonly IReadOnlyList<IExternalAccountingAdapter> adapters=adapters.ToArray();
    public AccountingPublishResult Publish(AccountingPublishRequest request)
    {
        var document=request.Document;
        if(document.TotalDebit!=document.TotalCredit||document.Lines.Sum(x=>x.Side==AccountingSide.Debit?x.Amount:-x.Amount)!=0m)
            throw Error(DiagnosticCodes.AccountingDocumentUnbalanced);
        if(document.ExecutionMode!=PayrollExecutionMode.Production)
            throw Error(DiagnosticCodes.PayrollApprovalNonProductionResult);
        var approval=approvalSource(document.ApprovalCaseId);
        if(approval.CompanyId!=document.CompanyId||approval.SnapshotId!=document.SnapshotId||
           approval.SnapshotRevision!=document.SnapshotRevision||approval.CalculationRunId!=document.CalculationRunId||
           approval.Status!=PayrollApprovalStatus.Locked)throw Error(DiagnosticCodes.PayrollApprovalLockBlocked);
        var fingerprint=CanonicalHash.Create(document.CompanyId.Value,document.PayrollPeriodId.Value,
            document.SnapshotRevision,document.CalculationRunId.Value,document.DocumentVersion,request.TargetSystem,document.ResultHash);
        var existing=deliveries.Find(document.CompanyId,request.TargetSystem,request.IdempotencyKey,out var prior);
        if(existing is not null)
        { if(prior!=fingerprint)throw Error(DiagnosticCodes.AccountingIdempotencyConflict);return existing; }
        var adapter=adapters.SingleOrDefault(x=>StringComparer.Ordinal.Equals(x.TargetSystem,request.TargetSystem))??
            throw Error(DiagnosticCodes.IntegrationTargetNotConfigured);
        var result=adapter.Publish(document,request.CorrelationId);
        deliveries.Add(document.CompanyId,request.TargetSystem,request.IdempotencyKey,fingerprint,result);
        return result;
    }
    private static StatutoryIntegrationException Error(string code)=>new(new(code,DiagnosticSeverity.Error,new Dictionary<string,object?>()));
}

public sealed class InMemoryAccountingDeliveryRepository : IAccountingDeliveryRepository
{
    private readonly object gate=new();
    private readonly Dictionary<(CompanyId,string,string),(string Fingerprint,AccountingPublishResult Result)> values=[];
    public AccountingPublishResult? Find(CompanyId companyId,string targetSystem,string idempotencyKey,out string? requestFingerprint)
    { lock(gate){if(values.TryGetValue((companyId,targetSystem,idempotencyKey),out var value)){requestFingerprint=value.Fingerprint;return value.Result;}requestFingerprint=null;return null;} }
    public void Add(CompanyId companyId,string targetSystem,string idempotencyKey,string requestFingerprint,AccountingPublishResult result)
    { lock(gate)values.Add((companyId,targetSystem,idempotencyKey),(requestFingerprint,result)); }
}
