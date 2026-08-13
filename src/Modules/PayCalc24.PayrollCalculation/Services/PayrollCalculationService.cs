using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using PayCalc24.Contracts.Compensation;
using PayCalc24.Contracts.Diagnostics;
using PayCalc24.Contracts.Identity;
using PayCalc24.Contracts.Operations;
using PayCalc24.Contracts.Organization;
using PayCalc24.Contracts.PayrollCalculation;
using PayCalc24.Contracts.PayrollInput;
using PayCalc24.Contracts.FormulaRepository;
using PayCalc24.FormulaEngine.Execution;
using PayCalc24.FormulaEngine.Model;
using PayCalc24.PayrollCalculation.Execution;
using PayCalc24.PayrollCalculation.Model;

namespace PayCalc24.PayrollCalculation.Services;

/// <summary>Run-scoped orchestration over an immutable snapshot. This type performs no repository or provider access.</summary>
public sealed class PayrollCalculationService(ICompanyContext companyContext, ICurrentUser currentUser,
    ICorrelationContext correlationContext, TimeProvider timeProvider, IPayrollPeriodService periodService,
    IPayrollSnapshotQueryService snapshotQuery, SafeFormulaEngine? formulaEngine = null) : IPayrollCalculationService
{
    private readonly SafeFormulaEngine engine=formulaEngine??new();
    private readonly List<PayrollCalculationRunDto> runs=[];
    private readonly List<PayrollSubjectCalculationResultDto> subjects=[];
    private readonly List<PayComponentCalculationResultDto> components=[];
    private readonly Dictionary<(CompanyId,PayrollCalculationSnapshotId,string),(string,PayrollCalculationRunId)> keys=[];
    private readonly Lock gate=new();

    public async ValueTask<PayrollCalculationRunDto> StartAsync(StartPayrollCalculation command,CancellationToken token=default)
    {
        Scope(command.CompanyId);
        if(string.IsNullOrWhiteSpace(command.IdempotencyKey))throw new ArgumentException("Idempotency key is required.",nameof(command));
        var snapshot=snapshotQuery.GetSnapshotById(command.CompanyId,command.SnapshotId);
        if(snapshot.CompanyId!=command.CompanyId)Fail(DiagnosticCodes.PayrollCalculationCrossCompanyReference);
        var period=periodService.GetById(command.CompanyId,snapshot.PayrollPeriodId);
        if(command.ExecutionMode==PayrollExecutionMode.Production&&period.LifecycleStatus!=PayrollPeriodStatus.FROZEN)Fail(DiagnosticCodes.PayrollCalculationSnapshotNotFrozen);
        if(command.ExecutionMode==PayrollExecutionMode.Production&&command.AlternativePolicy is not null)Fail(DiagnosticCodes.PayrollCalculationComponentInvalid);
        if(command.ExpectedSnapshotHash is not null&&!StringComparer.Ordinal.Equals(command.ExpectedSnapshotHash,snapshot.SnapshotHash))Fail(DiagnosticCodes.PayrollCalculationSnapshotHashInvalid);
        var policy=command.AlternativePolicy??snapshot.PolicyConfiguration;
        var fingerprint=Hash($"{command.SnapshotId.Value:D}|{snapshot.SnapshotRevision}|{command.ExecutionMode}|{snapshot.SnapshotHash}|{PolicyFingerprint(policy)}");
        PayrollCalculationRunDto run;
        lock(gate)
        {
            if(keys.TryGetValue((command.CompanyId,command.SnapshotId,command.IdempotencyKey),out var existing))
            {
                if(existing.Item1!=fingerprint)Fail(DiagnosticCodes.PayrollCalculationIdempotencyConflict);
                return FindRun(command.CompanyId,existing.Item2);
            }
            if(command.ExecutionMode==PayrollExecutionMode.Production&&runs.Any(x=>x.SnapshotId==command.SnapshotId&&x.ExecutionMode==PayrollExecutionMode.Production&&x.Status is PayrollCalculationRunStatus.RUNNING or PayrollCalculationRunStatus.SUCCEEDED))
                Fail(DiagnosticCodes.PayrollCalculationConcurrentRun);
            var now=timeProvider.GetUtcNow();var id=PayrollCalculationRunId.From(Guid.NewGuid());
            run=new(id,command.CompanyId,snapshot.PayrollPeriodId,snapshot.Id,snapshot.SnapshotRevision,command.ExecutionMode,
                SafeFormulaEngine.EngineVersion,PayrollCalculationRunStatus.RUNNING,now,currentUser.UserId,null,null,
                correlationContext.CorrelationId,command.IdempotencyKey,snapshot.SnapshotHash,null,null,false);
            runs.Add(run);keys[(command.CompanyId,command.SnapshotId,command.IdempotencyKey)]=(fingerprint,id);
        }
        try
        {
            var producedSubjects=new List<PayrollSubjectCalculationResultDto>();var producedComponents=new List<PayComponentCalculationResultDto>();
            foreach(var subject in snapshot.HistoricalFacts.Subjects.OrderBy(x=>x.EmployeeCode,StringComparer.Ordinal).ThenBy(x=>x.PayrollSubjectId.Value))
                CalculateSubject(run,snapshot,policy,subject,producedSubjects,producedComponents);
            var resultHash=Hash(string.Join("\n",producedSubjects.OrderBy(x=>x.PayrollSubjectId.Value).Select(x=>x.ResultHash)));
            if(command.ExecutionMode==PayrollExecutionMode.Production)
                await periodService.MarkCalculatedAsync(command.CompanyId,period.Id,period.Revision,token);
            lock(gate)
            {
                components.AddRange(producedComponents);subjects.AddRange(producedSubjects);
                run=run with{Status=PayrollCalculationRunStatus.SUCCEEDED,CompletedAt=timeProvider.GetUtcNow(),CompletedBy=currentUser.UserId,ResultHash=resultHash,IsAuthoritative=command.ExecutionMode==PayrollExecutionMode.Production};
                Replace(run);
            }
            return run;
        }
        catch(PayrollCalculationException failure)
        {
            lock(gate){run=run with{Status=PayrollCalculationRunStatus.FAILED,CompletedAt=timeProvider.GetUtcNow(),CompletedBy=currentUser.UserId,FailureDiagnosticCode=failure.Diagnostic.Code};Replace(run);}
            return run;
        }
    }

    private void CalculateSubject(PayrollCalculationRunDto run,PayrollCalculationSnapshotDto snapshot,
        SnapshotPolicyConfiguration policy,SnapshotSubjectFact subject,List<PayrollSubjectCalculationResultDto> subjectResults,
        List<PayComponentCalculationResultDto> componentResults)
    {
        var schemeId=subject.CompensationSchemeId??throw PayrollPeriod.Error(DiagnosticCodes.PayrollCalculationSchemeMissing,[]);
        var scheme=policy.CompensationVersions.SingleOrDefault(x=>x.CompensationSchemeId==schemeId)
            ??throw PayrollPeriod.Error(DiagnosticCodes.PayrollCalculationSchemeMissing,[]);
        var ordered=TopologicalOrder(scheme.Components);
        var calculated=new Dictionary<string,FormulaValue>(StringComparer.OrdinalIgnoreCase);
        var current=new List<PayComponentCalculationResultDto>();
        foreach(var component in ordered)
        {
            var code=string.IsNullOrWhiteSpace(component.ComponentCode)?component.PayComponentId.Value.ToString("D"):component.ComponentCode.Trim().ToUpperInvariant();
            var result=CalculateComponent(run,snapshot,policy,subject,scheme,component,code,calculated);
            if(result.Status==PayrollCalculationResultStatus.FAILED)throw PayrollPeriod.Error(result.DiagnosticCode??DiagnosticCodes.PayrollCalculationSubjectFailed,[]);
            current.Add(result);if(result.ResultValue is not null)calculated[code+"_RESULT"]=Map(result.ResultValue);
        }
        current=current.OrderBy(x=>x.Sequence).ThenBy(x=>x.ComponentCode,StringComparer.Ordinal).ToList();componentResults.AddRange(current);
        var hash=Hash(string.Join("\n",current.Select(x=>x.ResultHash)));
        subjectResults.Add(new(PayrollSubjectCalculationResultId.From(Guid.NewGuid()),run.Id,run.CompanyId,subject.PayrollSubjectId,
            subject.EmployeeCode,current.Count,PayrollCalculationResultStatus.SUCCEEDED,hash,null,timeProvider.GetUtcNow()));
    }

    private PayComponentCalculationResultDto CalculateComponent(PayrollCalculationRunDto run,PayrollCalculationSnapshotDto snapshot,
        SnapshotPolicyConfiguration policy,SnapshotSubjectFact subject,SnapshotCompensationVersion scheme,
        SnapshotPayComponentVersion component,string code,IReadOnlyDictionary<string,FormulaValue> calculated)
    {
        PayrollInputValue? value=null;string? dataType=null;string? diagnostic=null;ExecutionTraceNode? trace=null;
        FormulaDefinitionId? formulaDefinition=null;FormulaVersionId? formulaVersion=null;string? checksum=null;
        IReadOnlyList<PayrollInputLedgerEntryId> inputIds=[];
        switch(component.CalculationMethod)
        {
            case CalculationMethod.INPUT:
                var inputCode=component.SourceReference??code;
                var input=snapshot.HistoricalFacts.Inputs.SingleOrDefault(x=>x.PayrollSubjectId==subject.PayrollSubjectId&&StringComparer.OrdinalIgnoreCase.Equals(x.Code,inputCode));
                if(input is null){diagnostic=component.Required?DiagnosticCodes.PayrollCalculationRequiredInputMissing:null;break;}
                value=input.ResolvedValue;dataType=input.DataType.ToString();inputIds=input.ContributingLedgerEntryIds;break;
            case CalculationMethod.FIXED:
                var parameterCode=component.SourceReference;
                var parameter=policy.ParameterVersions.SelectMany(x=>x.Values).SingleOrDefault(x=>parameterCode is not null&&StringComparer.OrdinalIgnoreCase.Equals(x.Code,parameterCode));
                if(parameter is null){diagnostic=DiagnosticCodes.PayrollCalculationComponentInvalid;break;}
                value=Map(parameter.Value);dataType=parameter.Value.DataType.ToString();break;
            case CalculationMethod.FORMULA:
                if(component.FormulaDefinitionId is null){diagnostic=DiagnosticCodes.PayrollCalculationFormulaVersionMissing;break;}
                var pinned=policy.FormulaVersions.SingleOrDefault(x=>x.FormulaDefinitionId==component.FormulaDefinitionId.Value);
                if(pinned is null||string.IsNullOrWhiteSpace(pinned.Expression)){diagnostic=DiagnosticCodes.PayrollCalculationFormulaVersionMissing;break;}
                var context=SnapshotExecutionContextMapper.Map(snapshot,subject.PayrollSubjectId,pinned.FormulaDefinitionId,run.CorrelationId,run.ExecutionMode,calculated,policy);
                var evaluated=engine.Evaluate(pinned.Expression,context);formulaDefinition=pinned.FormulaDefinitionId;formulaVersion=pinned.FormulaVersionId;checksum=pinned.Checksum;trace=evaluated.Trace;
                if(!evaluated.Success){diagnostic=evaluated.Diagnostic!.Code;break;}
                value=Map(evaluated.Value??throw PayrollPeriod.Error(DiagnosticCodes.PayrollCalculationResultTypeMismatch,[]));dataType=evaluated.DataType!.Value.ToString().ToUpperInvariant();inputIds=evaluated.Provenance.ReferencedInputEntryIds;break;
            default: diagnostic=DiagnosticCodes.PayrollCalculationUnsupportedMethod;break;
        }
        if(diagnostic is null&&component.ExpectedDataType is not null&&!StringComparer.OrdinalIgnoreCase.Equals(component.ExpectedDataType,dataType))diagnostic=DiagnosticCodes.PayrollCalculationResultTypeMismatch;
        var status=diagnostic is null?PayrollCalculationResultStatus.SUCCEEDED:PayrollCalculationResultStatus.FAILED;
        var traceJson=trace is null?null:JsonSerializer.Serialize(trace);
        var canonical=$"{snapshot.SnapshotHash}|{subject.PayrollSubjectId.Value:D}|{component.PayComponentId.Value:D}|{component.Version}|{code}|{component.Sequence}|{component.CalculationMethod}|{dataType}|{Canonical(value)}|{formulaVersion?.Value:D}|{checksum}|{string.Join(',',inputIds.OrderBy(x=>x.Value).Select(x=>x.Value.ToString("D")))}|{string.Join(',',policy.ParameterVersions.Select(x=>x.ParameterSetVersionId.Value).Order())}|{SafeFormulaEngine.EngineVersion}";
        return new(PayComponentCalculationResultId.From(Guid.NewGuid()),run.Id,run.CompanyId,run.PayrollPeriodId,snapshot.Id,
            subject.PayrollSubjectId,scheme.CompensationSchemeId,component.PayComponentId,component.Version,code,component.Sequence,
            component.CalculationMethod,status,value,dataType,formulaDefinition,formulaVersion,checksum,traceJson,diagnostic,inputIds.ToArray(),
            policy.ParameterVersions.Select(x=>x.ParameterSetVersionId).OrderBy(x=>x.Value).ToArray(),policy.LookupVersions.Select(x=>x.LookupTableVersionId).OrderBy(x=>x.Value).ToArray(),
            policy.RuleSetVersions.Select(x=>x.RuleSetVersionId).OrderBy(x=>x.Value).ToArray(),SafeFormulaEngine.EngineVersion,run.ExecutionMode,run.CorrelationId,Hash(canonical),timeProvider.GetUtcNow());
    }

    internal static IReadOnlyList<SnapshotPayComponentVersion> TopologicalOrder(IReadOnlyList<SnapshotPayComponentVersion> source)
    {
        if(source.GroupBy(x=>x.PayComponentId).Any(x=>x.Count()!=1))Fail(DiagnosticCodes.PayrollCalculationComponentInvalid);
        var byId=source.ToDictionary(x=>x.PayComponentId);var indegree=source.ToDictionary(x=>x.PayComponentId,_=>0);var edges=source.ToDictionary(x=>x.PayComponentId,_=>new List<PayComponentId>());
        foreach(var item in source)foreach(var dependency in item.DependsOn??[]){if(!byId.ContainsKey(dependency))Fail(DiagnosticCodes.PayrollCalculationDependencyMissing);edges[dependency].Add(item.PayComponentId);indegree[item.PayComponentId]++;}
        var ready=new SortedSet<SnapshotPayComponentVersion>(Comparer<SnapshotPayComponentVersion>.Create((a,b)=>{var c=a.Sequence.CompareTo(b.Sequence);if(c!=0)return c;return a.PayComponentId.Value.CompareTo(b.PayComponentId.Value);}));
        foreach(var item in source.Where(x=>indegree[x.PayComponentId]==0))ready.Add(item);var result=new List<SnapshotPayComponentVersion>();
        while(ready.Count>0){var next=ready.Min!;ready.Remove(next);result.Add(next);foreach(var dependent in edges[next.PayComponentId])if(--indegree[dependent]==0)ready.Add(byId[dependent]);}
        if(result.Count!=source.Count)Fail(DiagnosticCodes.PayrollCalculationDependencyCycle);return result;
    }

    public PayrollCalculationRunDto GetRun(CompanyId companyId,PayrollCalculationRunId runId){Scope(companyId);return FindRun(companyId,runId);}
    public PayrollCalculationRunDto? ResolveByIdempotencyKey(CompanyId companyId,PayrollCalculationSnapshotId snapshotId,string idempotencyKey){Scope(companyId);return keys.TryGetValue((companyId,snapshotId,idempotencyKey),out var item)?FindRun(companyId,item.Item2):null;}
    public PayrollCalculationRunDto? GetAuthoritativeResult(CompanyId companyId,PayrollCalculationSnapshotId snapshotId){Scope(companyId);return runs.SingleOrDefault(x=>x.CompanyId==companyId&&x.SnapshotId==snapshotId&&x.IsAuthoritative&&x.Status==PayrollCalculationRunStatus.SUCCEEDED);}
    public IReadOnlyList<PayrollSubjectCalculationResultDto> ListSubjectResults(CompanyId companyId,PayrollCalculationRunId runId){_=GetRun(companyId,runId);return subjects.Where(x=>x.CalculationRunId==runId).OrderBy(x=>x.EmployeeCode,StringComparer.Ordinal).ToArray();}
    public PayrollSubjectCalculationResultDto GetSubjectResult(CompanyId companyId,PayrollCalculationRunId runId,PayrollSubjectId subjectId)=>ListSubjectResults(companyId,runId).Single(x=>x.PayrollSubjectId==subjectId);
    public IReadOnlyList<PayComponentCalculationResultDto> ListComponentResults(CompanyId companyId,PayrollCalculationRunId runId,PayrollSubjectId? subjectId=null){_=GetRun(companyId,runId);return components.Where(x=>x.CalculationRunId==runId&&(subjectId is null||x.PayrollSubjectId==subjectId)).OrderBy(x=>x.Sequence).ThenBy(x=>x.ComponentCode,StringComparer.Ordinal).ToArray();}
    public PayComponentCalculationResultDto GetComponentResult(CompanyId companyId,PayComponentCalculationResultId resultId){Scope(companyId);return components.Single(x=>x.CompanyId==companyId&&x.Id==resultId);}
    private PayrollCalculationRunDto FindRun(CompanyId companyId,PayrollCalculationRunId id)=>runs.Single(x=>x.CompanyId==companyId&&x.Id==id);
    private void Replace(PayrollCalculationRunDto run){var index=runs.FindIndex(x=>x.Id==run.Id);runs[index]=run;}
    private void Scope(CompanyId companyId){if(companyContext.CompanyId!=companyId)Fail(DiagnosticCodes.CompanyScopeMismatch);}
    private static void Fail(string code)=>throw PayrollPeriod.Error(code,[]);
    private static string Hash(string value)=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
    private static string PolicyFingerprint(SnapshotPolicyConfiguration p)=>string.Join('|',p.CompensationVersions.OrderBy(x=>x.CompensationSchemeId.Value).Select(x=>$"{x.CompensationSchemeId.Value:D}:{x.SchemeVersion}"))+'|'+string.Join('|',p.FormulaVersions.OrderBy(x=>x.FormulaVersionId.Value).Select(x=>$"{x.FormulaVersionId.Value:D}:{x.Checksum}"));
    private static string Canonical(PayrollInputValue? v)=>v is null?"":v.DataType switch{PayrollInputDataType.DECIMAL=>v.DecimalValue!.Value.ToString(System.Globalization.CultureInfo.InvariantCulture),PayrollInputDataType.INTEGER=>v.IntegerValue!.Value.ToString(System.Globalization.CultureInfo.InvariantCulture),PayrollInputDataType.BOOLEAN=>v.BooleanValue!.Value?"true":"false",PayrollInputDataType.DATE=>v.DateValue!.Value.ToString("yyyy-MM-dd",System.Globalization.CultureInfo.InvariantCulture),PayrollInputDataType.TEXT=>v.TextValue!,_=>""};
    private static FormulaValue Map(PayrollInputValue v)=>v.DataType switch{PayrollInputDataType.DECIMAL=>FormulaValue.Decimal(v.DecimalValue!.Value),PayrollInputDataType.INTEGER=>FormulaValue.Integer(v.IntegerValue!.Value),PayrollInputDataType.BOOLEAN=>FormulaValue.Boolean(v.BooleanValue!.Value),PayrollInputDataType.DATE=>FormulaValue.Date(v.DateValue!.Value),PayrollInputDataType.TEXT=>FormulaValue.Text(v.TextValue!),_=>throw new ArgumentOutOfRangeException(nameof(v))};
    private static PayrollInputValue Map(FormulaValue v)=>v.Type switch{FormulaValueType.Decimal=>PayrollInputValue.Decimal(v.AsDecimal()),FormulaValueType.Integer=>PayrollInputValue.Integer(v.AsInteger()),FormulaValueType.Boolean=>PayrollInputValue.Boolean(v.AsBoolean()),FormulaValueType.Date=>PayrollInputValue.Date((DateOnly)v.RawValue!),FormulaValueType.Text=>PayrollInputValue.Text((string)v.RawValue!),_=>throw PayrollPeriod.Error(DiagnosticCodes.PayrollCalculationResultTypeMismatch,[])};
    private static PayrollInputValue Map(FormulaTypedValue v)=>v.DataType switch{FormulaDataType.DECIMAL=>PayrollInputValue.Decimal(v.DecimalValue!.Value),FormulaDataType.INTEGER=>PayrollInputValue.Integer(v.IntegerValue!.Value),FormulaDataType.BOOLEAN=>PayrollInputValue.Boolean(v.BooleanValue!.Value),FormulaDataType.DATE=>PayrollInputValue.Date(v.DateValue!.Value),FormulaDataType.TEXT=>PayrollInputValue.Text(v.TextValue!),_=>throw new ArgumentOutOfRangeException(nameof(v))};
}
