using PayCalc24.Contracts.Identity;
using PayCalc24.Contracts.PayrollApproval;
using PayCalc24.Contracts.PayrollCalculation;
using PayCalc24.Contracts.PayrollInput;

namespace PayCalc24.PayrollApproval.Services;

/// <summary>Reference store for tests/hosts; production persistence implements the same atomic contract.</summary>
public sealed class InMemoryPayrollApprovalRepository : IPayrollApprovalRepository
{
    private readonly object gate=new();
    private readonly Dictionary<(CompanyId,PayrollApprovalCaseId),PayrollApprovalCaseDto> cases=[];
    private readonly Dictionary<(CompanyId,string,string),(string Fingerprint,PayrollApprovalCaseId Id)> caseKeys=[];
    private readonly List<PayrollApprovalEventDto> events=[];
    private readonly Dictionary<(CompanyId,PayrollAdjustmentRequestId),PayrollAdjustmentRequestDto> adjustments=[];
    private readonly Dictionary<(CompanyId,string,string),(string Fingerprint,PayrollAdjustmentRequestId Id)> adjustmentKeys=[];
    public PayrollApprovalCaseDto? Get(CompanyId c,PayrollApprovalCaseId id){lock(gate)return cases.GetValueOrDefault((c,id));}
    public PayrollApprovalCaseDto? GetByIdempotency(CompanyId c,string op,string key){lock(gate)return caseKeys.TryGetValue((c,op,key),out var x)?cases[(c,x.Id)]:null;}
    public void Add(PayrollApprovalCaseDto v,string op,string key,string fp){lock(gate){if(caseKeys.TryGetValue((v.CompanyId,op,key),out var old)&&old.Fingerprint!=fp)Conflict();cases.Add((v.CompanyId,v.Id),v);caseKeys[(v.CompanyId,op,key)]=(fp,v.Id);}}
    public void Update(PayrollApprovalCaseDto v,long expected,string op,string key,string fp){lock(gate){var old=cases[(v.CompanyId,v.Id)];if(old.Revision!=expected)Concurrency();if(caseKeys.TryGetValue((v.CompanyId,op,key),out var prior)){if(prior.Fingerprint!=fp)Conflict();return;}cases[(v.CompanyId,v.Id)]=v;caseKeys[(v.CompanyId,op,key)]=(fp,v.Id);}}
    public IReadOnlyList<PayrollApprovalCaseDto> List(CompanyId c,PayrollPeriodId p){lock(gate)return cases.Values.Where(x=>x.CompanyId==c&&x.PayrollPeriodId==p).ToArray();}
    public void Append(PayrollApprovalEventDto v){lock(gate){if(!events.Any(x=>x.CompanyId==v.CompanyId&&x.ApprovalCaseId==v.ApprovalCaseId&&x.IdempotencyKey==v.IdempotencyKey&&x.ToStatus==v.ToStatus))events.Add(v);}}
    public IReadOnlyList<PayrollApprovalEventDto> History(CompanyId c,PayrollApprovalCaseId id){lock(gate)return events.Where(x=>x.CompanyId==c&&x.ApprovalCaseId==id).OrderBy(x=>x.OccurredAt).ToArray();}
    public PayrollAdjustmentRequestDto? GetAdjustment(CompanyId c,PayrollAdjustmentRequestId id){lock(gate)return adjustments.GetValueOrDefault((c,id));}
    public PayrollAdjustmentRequestDto? GetAdjustmentByIdempotency(CompanyId c,string op,string key){lock(gate)return adjustmentKeys.TryGetValue((c,op,key),out var x)?adjustments[(c,x.Id)]:null;}
    public void AddAdjustment(PayrollAdjustmentRequestDto v,string op,string key,string fp){lock(gate){if(adjustmentKeys.TryGetValue((v.CompanyId,op,key),out var old)&&old.Fingerprint!=fp)Conflict();adjustments.Add((v.CompanyId,v.Id),v);adjustmentKeys[(v.CompanyId,op,key)]=(fp,v.Id);}}
    public void UpdateAdjustment(PayrollAdjustmentRequestDto v,long expected,string op,string key,string fp){lock(gate){var old=adjustments[(v.CompanyId,v.Id)];if(old.Revision!=expected)Concurrency();if(adjustmentKeys.TryGetValue((v.CompanyId,op,key),out var prior)){if(prior.Fingerprint!=fp)Conflict();return;}adjustments[(v.CompanyId,v.Id)]=v;adjustmentKeys[(v.CompanyId,op,key)]=(fp,v.Id);}}
    private static void Conflict()=>throw new PayrollApprovalException(new(PayCalc24.Contracts.Diagnostics.DiagnosticCodes.PayrollApprovalIdempotencyConflict,PayCalc24.Contracts.Diagnostics.DiagnosticSeverity.Error,new Dictionary<string,object?>()));
    private static void Concurrency()=>throw new PayrollApprovalException(new(PayCalc24.Contracts.Diagnostics.DiagnosticCodes.PayrollApprovalConcurrencyConflict,PayCalc24.Contracts.Diagnostics.DiagnosticSeverity.Error,new Dictionary<string,object?>()));
}
