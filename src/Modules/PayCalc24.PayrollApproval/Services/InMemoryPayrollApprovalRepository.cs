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
    public PayrollApprovalCaseDto? GetCase(CompanyId companyId,PayrollApprovalCaseId id){lock(gate)return cases.GetValueOrDefault((companyId,id));}
    public PayrollApprovalCaseDto? GetByIdempotency(CompanyId companyId,string operation,string key){lock(gate)return caseKeys.TryGetValue((companyId,operation,key),out var existing)?cases[(companyId,existing.Id)]:null;}
    public void Add(PayrollApprovalCaseDto value,string operation,string key,string fingerprint){lock(gate){if(caseKeys.TryGetValue((value.CompanyId,operation,key),out var old)&&old.Fingerprint!=fingerprint)Conflict();cases.Add((value.CompanyId,value.Id),value);caseKeys[(value.CompanyId,operation,key)]=(fingerprint,value.Id);}}
    public void Update(PayrollApprovalCaseDto value,long expectedRevision,string operation,string key,string fingerprint){lock(gate){var old=cases[(value.CompanyId,value.Id)];if(old.Revision!=expectedRevision)Concurrency();if(caseKeys.TryGetValue((value.CompanyId,operation,key),out var prior)){if(prior.Fingerprint!=fingerprint)Conflict();return;}cases[(value.CompanyId,value.Id)]=value;caseKeys[(value.CompanyId,operation,key)]=(fingerprint,value.Id);}}
    public IReadOnlyList<PayrollApprovalCaseDto> List(CompanyId companyId,PayrollPeriodId periodId){lock(gate)return cases.Values.Where(x=>x.CompanyId==companyId&&x.PayrollPeriodId==periodId).ToArray();}
    public void Append(PayrollApprovalEventDto value){lock(gate){if(!events.Any(x=>x.CompanyId==value.CompanyId&&x.ApprovalCaseId==value.ApprovalCaseId&&x.IdempotencyKey==value.IdempotencyKey&&x.ToStatus==value.ToStatus))events.Add(value);}}
    public IReadOnlyList<PayrollApprovalEventDto> History(CompanyId companyId,PayrollApprovalCaseId id){lock(gate)return events.Where(x=>x.CompanyId==companyId&&x.ApprovalCaseId==id).OrderBy(x=>x.OccurredAt).ToArray();}
    public PayrollAdjustmentRequestDto? GetAdjustment(CompanyId companyId,PayrollAdjustmentRequestId id){lock(gate)return adjustments.GetValueOrDefault((companyId,id));}
    public PayrollAdjustmentRequestDto? GetAdjustmentByIdempotency(CompanyId companyId,string operation,string key){lock(gate)return adjustmentKeys.TryGetValue((companyId,operation,key),out var existing)?adjustments[(companyId,existing.Id)]:null;}
    public void AddAdjustment(PayrollAdjustmentRequestDto value,string operation,string key,string fingerprint){lock(gate){if(adjustmentKeys.TryGetValue((value.CompanyId,operation,key),out var old)&&old.Fingerprint!=fingerprint)Conflict();adjustments.Add((value.CompanyId,value.Id),value);adjustmentKeys[(value.CompanyId,operation,key)]=(fingerprint,value.Id);}}
    public void UpdateAdjustment(PayrollAdjustmentRequestDto value,long expectedRevision,string operation,string key,string fingerprint){lock(gate){var old=adjustments[(value.CompanyId,value.Id)];if(old.Revision!=expectedRevision)Concurrency();if(adjustmentKeys.TryGetValue((value.CompanyId,operation,key),out var prior)){if(prior.Fingerprint!=fingerprint)Conflict();return;}adjustments[(value.CompanyId,value.Id)]=value;adjustmentKeys[(value.CompanyId,operation,key)]=(fingerprint,value.Id);}}
    private static void Conflict()=>throw new PayrollApprovalException(new(PayCalc24.Contracts.Diagnostics.DiagnosticCodes.PayrollApprovalIdempotencyConflict,PayCalc24.Contracts.Diagnostics.DiagnosticSeverity.Error,new Dictionary<string,object?>()));
    private static void Concurrency()=>throw new PayrollApprovalException(new(PayCalc24.Contracts.Diagnostics.DiagnosticCodes.PayrollApprovalConcurrencyConflict,PayCalc24.Contracts.Diagnostics.DiagnosticSeverity.Error,new Dictionary<string,object?>()));
}
