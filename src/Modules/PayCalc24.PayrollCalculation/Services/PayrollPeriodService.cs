using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using PayCalc24.Contracts.Diagnostics;
using PayCalc24.Contracts.Identity;
using PayCalc24.Contracts.Operations;
using PayCalc24.Contracts.PayrollCalculation;
using PayCalc24.Contracts.PayrollInput;
using PayCalc24.PayrollCalculation.Model;

namespace PayCalc24.PayrollCalculation.Services;

public sealed class PayrollPeriodService(ICompanyContext companyContext, ICurrentUser currentUser,
    ICorrelationContext correlationContext, IAuditWriter auditWriter, TimeProvider timeProvider,
    IPayrollSnapshotResolver resolver) : IPayrollPeriodService, IPayrollSnapshotQueryService
{
    private readonly List<PayrollPeriod> periods=[];
    private readonly List<PayrollPeriodLifecycleEventDto> events=[];
    private readonly List<PayrollCalculationSnapshotDto> snapshots=[];
    private readonly Dictionary<PayrollPeriodId,PayrollSnapshotCandidate> prepared=[];
    private readonly Dictionary<(CompanyId,string),(string,PayrollCalculationSnapshotId)> freezeKeys=[];
    private readonly Lock gate=new();

    public async ValueTask<PayrollPeriodDto> CreateAsync(CreatePayrollPeriod command,CancellationToken token=default)
    {
        Scope(command.CompanyId);var code=PayrollPeriod.NormalizeCode(command.Code);PayrollPeriod period;
        lock(gate)
        {
            if(periods.Any(x=>x.CompanyId==command.CompanyId&&(StringComparer.OrdinalIgnoreCase.Equals(x.Code,code)||(x.PeriodStart==command.PeriodStart&&x.PeriodEnd==command.PeriodEnd))))
                PayrollPeriod.Throw(DiagnosticCodes.DuplicatePayrollPeriodCode,new(){["code"]=code});
            period=new(PayrollPeriodId.From(Guid.NewGuid()),command.CompanyId,code,command.Name,command.PeriodStart,command.PeriodEnd,command.BusinessDate,command.PaymentDate,timeProvider.GetUtcNow(),currentUser.UserId);
            periods.Add(period);Event(period,null,PayrollPeriodStatus.DRAFT,null,null);
        }
        await Audit(period,PayrollAuditActions.Created,token);return period.ToDto();
    }
    public async ValueTask<PayrollPeriodDto> UpdateDraftAsync(UpdatePayrollPeriodDraft command,CancellationToken token=default)
    {
        Scope(command.CompanyId);var period=Find(command.CompanyId,command.PayrollPeriodId);
        lock(gate){period.UpdateDraft(command.ExpectedRevision,command.Name,command.PeriodStart,command.PeriodEnd,command.BusinessDate,command.PaymentDate,timeProvider.GetUtcNow(),currentUser.UserId);}
        await Audit(period,PayrollAuditActions.DraftUpdated,token);return period.ToDto();
    }
    public PayrollPeriodDto GetById(CompanyId companyId,PayrollPeriodId periodId){Scope(companyId);return Find(companyId,periodId).ToDto();}
    public IReadOnlyList<PayrollPeriodDto> Search(CompanyId companyId,PayrollPeriodSearch search){Scope(companyId);return periods.Where(x=>x.CompanyId==companyId&&(search.Status is null||x.LifecycleStatus==search.Status)&&(search.From is null||x.PeriodEnd>=search.From)&&(search.To is null||x.PeriodStart<=search.To)).OrderByDescending(x=>x.PeriodStart).ThenBy(x=>x.Code,StringComparer.Ordinal).Select(x=>x.ToDto()).ToArray();}

    public async ValueTask<PayrollPeriodDto> PrepareAsync(CompanyId companyId,PayrollPeriodId periodId,long expectedRevision,CancellationToken token=default)
    {
        Scope(companyId);var period=Find(companyId,periodId);PayrollPeriodStatus from;
        lock(gate)
        {
            period.Expect(expectedRevision);if(period.LifecycleStatus is not (PayrollPeriodStatus.DRAFT or PayrollPeriodStatus.REOPENED))PayrollPeriod.Throw(DiagnosticCodes.InvalidPayrollPeriodTransition,[]);
            var candidate=resolver.Resolve(companyId,periodId,period.BusinessDate);ValidateCandidate(companyId,candidate);prepared[periodId]=candidate;
            from=period.LifecycleStatus;period.Transition(expectedRevision,PayrollPeriodStatus.PREPARED,timeProvider.GetUtcNow(),currentUser.UserId);Event(period,from,PayrollPeriodStatus.PREPARED,null,null);
        }
        await Audit(period,PayrollAuditActions.Prepared,token);return period.ToDto();
    }
    public IReadOnlyList<PreparationDiagnostic> GetPreparationDiagnostics(CompanyId companyId,PayrollPeriodId periodId){Scope(companyId);Find(companyId,periodId);return prepared.TryGetValue(periodId,out var value)?value.Diagnostics.ToArray():[];}
    public async ValueTask<PayrollPeriodDto> ResetPreparationAsync(CompanyId companyId,PayrollPeriodId periodId,long expectedRevision,CancellationToken token=default)
    {
        Scope(companyId);var period=Find(companyId,periodId);lock(gate){period.Transition(expectedRevision,PayrollPeriodStatus.DRAFT,timeProvider.GetUtcNow(),currentUser.UserId);prepared.Remove(periodId);Event(period,PayrollPeriodStatus.PREPARED,PayrollPeriodStatus.DRAFT,null,null);}await Audit(period,PayrollAuditActions.PreparationReset,token);return period.ToDto();
    }
    public async ValueTask<PayrollCalculationSnapshotDto> FreezeAsync(CompanyId companyId,PayrollPeriodId periodId,long expectedRevision,string idempotencyKey,CancellationToken token=default)
    {
        Scope(companyId);if(string.IsNullOrWhiteSpace(idempotencyKey))throw new ArgumentException("Idempotency key is required.",nameof(idempotencyKey));
        PayrollCalculationSnapshotDto snapshot;PayrollPeriod period;
        lock(gate)
        {
            period=Find(companyId,periodId);var fingerprint=$"{periodId.Value:D}|{expectedRevision.ToString(CultureInfo.InvariantCulture)}";
            if(freezeKeys.TryGetValue((companyId,idempotencyKey),out var prior))
            {if(prior.Item1!=fingerprint)PayrollPeriod.Throw(DiagnosticCodes.IdempotencyConflict,new(){["idempotencyKey"]=idempotencyKey});return GetSnapshotById(companyId,prior.Item2);}
            period.Expect(expectedRevision);if(period.LifecycleStatus!=PayrollPeriodStatus.PREPARED)PayrollPeriod.Throw(period.LifecycleStatus==PayrollPeriodStatus.FROZEN?DiagnosticCodes.PayrollSnapshotAlreadyFrozen:DiagnosticCodes.InvalidPayrollPeriodTransition,[]);
            var candidate=resolver.Resolve(companyId,periodId,period.BusinessDate);ValidateCandidate(companyId,candidate);
            if(candidate.Diagnostics.Any(x=>x.Severity==DiagnosticSeverity.Error))PayrollPeriod.Throw(DiagnosticCodes.PayrollPreparationBlockingErrors,new(){["errorCount"]=candidate.Diagnostics.Count(x=>x.Severity==DiagnosticSeverity.Error)});
            var revision=snapshots.Where(x=>x.PayrollPeriodId==periodId).Select(x=>x.SnapshotRevision).DefaultIfEmpty().Max()+1;
            var hashes=SnapshotHasher.Hash(companyId,periodId,revision,period.BusinessDate,candidate);
            var now=timeProvider.GetUtcNow();snapshot=new(PayrollCalculationSnapshotId.From(Guid.NewGuid()),companyId,periodId,revision,PayrollExecutionMode.Production,period.BusinessDate,now,currentUser.UserId,now,currentUser.UserId,hashes.Population,hashes.Input,hashes.Configuration,hashes.Snapshot,candidate.HistoricalFacts,candidate.PolicyConfiguration);
            snapshots.Add(snapshot);period.Transition(expectedRevision,PayrollPeriodStatus.FROZEN,now,currentUser.UserId);prepared.Remove(periodId);freezeKeys[(companyId,idempotencyKey)]=(fingerprint,snapshot.Id);Event(period,PayrollPeriodStatus.PREPARED,PayrollPeriodStatus.FROZEN,revision,null);
        }
        await Audit(period,PayrollAuditActions.Frozen,token);await AuditSnapshot(snapshot,token);return snapshot;
    }
    public ValueTask<PayrollPeriodDto> MarkCalculatedAsync(CompanyId companyId,PayrollPeriodId periodId,long expectedRevision,CancellationToken token=default)=>Transition(companyId,periodId,expectedRevision,PayrollPeriodStatus.CALCULATED,PayrollAuditActions.Calculated,null,token);
    public ValueTask<PayrollPeriodDto> CloseAsync(CompanyId companyId,PayrollPeriodId periodId,long expectedRevision,CancellationToken token=default)=>Transition(companyId,periodId,expectedRevision,PayrollPeriodStatus.CLOSED,PayrollAuditActions.Closed,null,token);
    public async ValueTask<PayrollPeriodDto> ReopenAsync(CompanyId companyId,PayrollPeriodId periodId,long expectedRevision,string reason,CancellationToken token=default)
    {if(string.IsNullOrWhiteSpace(reason))PayrollPeriod.Throw(DiagnosticCodes.PayrollPeriodReopenReasonRequired,[]);return await Transition(companyId,periodId,expectedRevision,PayrollPeriodStatus.REOPENED,PayrollAuditActions.Reopened,reason.Trim(),token);}
    private async ValueTask<PayrollPeriodDto> Transition(CompanyId companyId,PayrollPeriodId periodId,long expected,PayrollPeriodStatus target,string action,string? reason,CancellationToken token)
    {Scope(companyId);var period=Find(companyId,periodId);PayrollPeriodStatus from;lock(gate){from=period.LifecycleStatus;period.Transition(expected,target,timeProvider.GetUtcNow(),currentUser.UserId);Event(period,from,target,snapshots.Where(x=>x.PayrollPeriodId==periodId).Select(x=>(int?)x.SnapshotRevision).Max(),reason);}await Audit(period,action,token,reason);return period.ToDto();}
    public IReadOnlyList<PayrollPeriodLifecycleEventDto> GetLifecycleHistory(CompanyId companyId,PayrollPeriodId periodId){Scope(companyId);Find(companyId,periodId);return events.Where(x=>x.CompanyId==companyId&&x.PayrollPeriodId==periodId).OrderBy(x=>x.OccurredAt).ThenBy(x=>x.PeriodRevision).ToArray();}

    public PayrollCalculationSnapshotDto GetAuthoritative(CompanyId companyId,PayrollPeriodId periodId){Scope(companyId);Find(companyId,periodId);return snapshots.Where(x=>x.CompanyId==companyId&&x.PayrollPeriodId==periodId).OrderByDescending(x=>x.SnapshotRevision).FirstOrDefault()??throw PayrollPeriod.Error(DiagnosticCodes.PayrollSnapshotNotFound,[]);}
    public PayrollCalculationSnapshotDto GetSnapshotById(CompanyId companyId,PayrollCalculationSnapshotId snapshotId){Scope(companyId);var any=snapshots.SingleOrDefault(x=>x.Id==snapshotId);if(any is null||any.CompanyId!=companyId)throw PayrollPeriod.Error(DiagnosticCodes.PayrollSnapshotNotFound,[]);return any;}
    public PayrollCalculationSnapshotDto GetByRevision(CompanyId companyId,PayrollPeriodId periodId,int revision){Scope(companyId);return snapshots.SingleOrDefault(x=>x.CompanyId==companyId&&x.PayrollPeriodId==periodId&&x.SnapshotRevision==revision)??throw PayrollPeriod.Error(DiagnosticCodes.PayrollSnapshotInvalidRevision,new(){["revision"]=revision});}
    public IReadOnlyList<PayrollCalculationSnapshotDto> ListRevisions(CompanyId companyId,PayrollPeriodId periodId){Scope(companyId);Find(companyId,periodId);return snapshots.Where(x=>x.CompanyId==companyId&&x.PayrollPeriodId==periodId).OrderBy(x=>x.SnapshotRevision).ToArray();}
    public IReadOnlyList<SnapshotSubjectFact> GetSubjects(CompanyId companyId,PayrollCalculationSnapshotId snapshotId)=>GetSnapshotById(companyId,snapshotId).HistoricalFacts.Subjects;
    public IReadOnlyList<SnapshotResolvedInput> GetSubjectInputs(CompanyId companyId,PayrollCalculationSnapshotId snapshotId,PayCalc24.Contracts.Organization.PayrollSubjectId subjectId)=>GetSnapshotById(companyId,snapshotId).HistoricalFacts.Inputs.Where(x=>x.PayrollSubjectId==subjectId).ToArray();
    public IReadOnlyList<SnapshotCompensationVersion> GetCompensationVersions(CompanyId companyId,PayrollCalculationSnapshotId snapshotId)=>GetSnapshotById(companyId,snapshotId).PolicyConfiguration.CompensationVersions;
    public IReadOnlyList<SnapshotFormulaVersion> GetFormulaVersions(CompanyId companyId,PayrollCalculationSnapshotId snapshotId)=>GetSnapshotById(companyId,snapshotId).PolicyConfiguration.FormulaVersions;
    public IReadOnlyList<SnapshotParameterVersion> GetParameterVersions(CompanyId companyId,PayrollCalculationSnapshotId snapshotId)=>GetSnapshotById(companyId,snapshotId).PolicyConfiguration.ParameterVersions;
    public IReadOnlyList<SnapshotLookupVersion> GetLookupVersions(CompanyId companyId,PayrollCalculationSnapshotId snapshotId)=>GetSnapshotById(companyId,snapshotId).PolicyConfiguration.LookupVersions;
    public IReadOnlyList<SnapshotRuleSetVersion> GetRuleSetVersions(CompanyId companyId,PayrollCalculationSnapshotId snapshotId)=>GetSnapshotById(companyId,snapshotId).PolicyConfiguration.RuleSetVersions;

    private static void ValidateCandidate(CompanyId companyId,PayrollSnapshotCandidate candidate)
    {
        if(candidate.CompanyId!=companyId)PayrollPeriod.Throw(DiagnosticCodes.PayrollPreparationCrossCompanyReference,[]);
        if(candidate.HistoricalFacts.Subjects.GroupBy(x=>x.PayrollSubjectId).Any(x=>x.Count()!=1)||candidate.HistoricalFacts.Inputs.Any(x=>!candidate.HistoricalFacts.Subjects.Any(s=>s.PayrollSubjectId==x.PayrollSubjectId)))PayrollPeriod.Throw(DiagnosticCodes.PayrollPreparationCrossCompanyReference,[]);
        if(candidate.PolicyConfiguration.FormulaVersions.Any(x=>string.IsNullOrWhiteSpace(x.Checksum)))PayrollPeriod.Throw(DiagnosticCodes.PayrollPreparationFormulaVersionMissing,[]);
    }
    private PayrollPeriod Find(CompanyId c,PayrollPeriodId p){var any=periods.SingleOrDefault(x=>x.Id==p);if(any is null||any.CompanyId!=c)throw PayrollPeriod.Error(DiagnosticCodes.PayrollPeriodNotFound,new(){["payrollPeriodId"]=p.Value});return any;}
    private void Scope(CompanyId c){if(c!=companyContext.CompanyId)PayrollPeriod.Throw(DiagnosticCodes.CompanyScopeMismatch,[]);}
    private void Event(PayrollPeriod p,PayrollPeriodStatus? from,PayrollPeriodStatus to,int? snapshotRevision,string? reason)=>events.Add(new(Guid.NewGuid(),p.CompanyId,p.Id,from,to,p.Revision,snapshotRevision,reason,timeProvider.GetUtcNow(),currentUser.UserId,correlationContext.CorrelationId));
    private ValueTask Audit(PayrollPeriod p,string action,CancellationToken token,string? reason=null)=>auditWriter.WriteAsync(new(p.CompanyId,currentUser.UserId,action,"PayrollPeriod",p.Id.Value.ToString("D"),correlationContext.CorrelationId,timeProvider.GetUtcNow(),new Dictionary<string,object?>{{"status",p.LifecycleStatus},{"revision",p.Revision},{"reason",reason}}),token);
    private ValueTask AuditSnapshot(PayrollCalculationSnapshotDto s,CancellationToken token)=>auditWriter.WriteAsync(new(s.CompanyId,currentUser.UserId,PayrollAuditActions.SnapshotCreated,"PayrollCalculationSnapshot",s.Id.Value.ToString("D"),correlationContext.CorrelationId,timeProvider.GetUtcNow(),new Dictionary<string,object?>{{"payrollPeriodId",s.PayrollPeriodId.Value},{"snapshotRevision",s.SnapshotRevision},{"snapshotHash",s.SnapshotHash}}),token);
}

internal static class SnapshotHasher
{
    internal sealed record Hashes(string Population,string Input,string Configuration,string Snapshot);
    internal static Hashes Hash(CompanyId company,PayrollPeriodId period,int revision,DateOnly businessDate,PayrollSnapshotCandidate candidate)
    {
        var population=Digest(string.Join('\n',candidate.HistoricalFacts.Subjects.OrderBy(x=>x.PayrollSubjectId.Value).Select(x=>$"{x.PayrollSubjectId.Value:D}|{x.EmployeeCode}|{x.PayrollAssignmentId.Value:D}|{x.OrganizationUnitId.Value:D}|{x.PositionId?.Value:D}|{x.JobGradeId?.Value:D}|{x.CompensationSchemeId?.Value:D}|{x.AssignmentEffectiveFrom:yyyy-MM-dd}|{x.AssignmentEffectiveTo:yyyy-MM-dd}|{x.EligibleDependentCount.ToString(CultureInfo.InvariantCulture)}|{string.Join(',',x.EligibleDependentIds.OrderBy(y=>y.Value).Select(y=>y.Value.ToString("D")))}")));
        var input=Digest(string.Join('\n',candidate.HistoricalFacts.Inputs.OrderBy(x=>x.PayrollSubjectId.Value).ThenBy(x=>x.Code,StringComparer.Ordinal).ThenBy(x=>x.PayrollInputDefinitionId.Value).Select(x=>$"{x.PayrollSubjectId.Value:D}|{x.PayrollInputDefinitionId.Value:D}|{x.DefinitionRevision.ToString(CultureInfo.InvariantCulture)}|{x.Code}|{Value(x.ResolvedValue)}|{string.Join(',',x.ContributingLedgerEntryIds.OrderBy(y=>y.Value).Select(y=>y.Value.ToString("D")))}")));
        var cfg=Digest(string.Join('\n',new[]{string.Join(';',candidate.PolicyConfiguration.CompensationVersions.OrderBy(x=>x.CompensationSchemeId.Value).Select(x=>$"C:{x.CompensationSchemeId.Value:D}:{x.SchemeVersion}:{string.Join(',',x.Components.OrderBy(y=>y.Sequence).ThenBy(y=>y.PayComponentId.Value).Select(y=>$"{y.PayComponentId.Value:D}:{y.Version}:{y.Sequence}"))}")),string.Join(';',candidate.PolicyConfiguration.FormulaVersions.OrderBy(x=>x.FormulaDefinitionId.Value).Select(x=>$"F:{x.FormulaDefinitionId.Value:D}:{x.FormulaVersionId.Value:D}:{x.Revision}:{x.Checksum}")),string.Join(';',candidate.PolicyConfiguration.ParameterVersions.OrderBy(x=>x.Code,StringComparer.Ordinal).Select(x=>$"P:{x.ParameterSetVersionId.Value:D}:{x.Revision}")),string.Join(';',candidate.PolicyConfiguration.LookupVersions.OrderBy(x=>x.Code,StringComparer.Ordinal).Select(x=>$"L:{x.LookupTableVersionId.Value:D}:{x.Revision}:{string.Join(',',x.Rows.OrderBy(y=>y.Sequence).Select(y=>y.Id.ToString("D")))}")),string.Join(';',candidate.PolicyConfiguration.RuleSetVersions.OrderBy(x=>x.Code,StringComparer.Ordinal).Select(x=>$"R:{x.RuleSetVersionId.Value:D}:{x.Revision}:{string.Join(',',x.Rules.OrderBy(y=>y.Priority).ThenBy(y=>y.Id).Select(y=>y.Id.ToString("D")))}"))}));
        var all=Digest($"{company.Value:D}|{period.Value:D}|{revision.ToString(CultureInfo.InvariantCulture)}|{businessDate:yyyy-MM-dd}|{population}|{input}|{cfg}");return new(population,input,cfg,all);
    }
    private static string Value(PayrollInputValue v)=>$"{v.DataType}:{v.DecimalValue?.ToString(CultureInfo.InvariantCulture)}:{v.IntegerValue?.ToString(CultureInfo.InvariantCulture)}:{v.BooleanValue}:{v.DateValue:yyyy-MM-dd}:{v.TextValue}";
    private static string Digest(string value)=>Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}
