using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using PayCalc24.Contracts.Diagnostics;
using PayCalc24.Contracts.Identity;
using PayCalc24.Contracts.Operations;
using PayCalc24.Contracts.Organization;
using PayCalc24.Contracts.PayrollInput;
using PayCalc24.Contracts.Temporal;
using PayCalc24.PayrollInput.Model;

namespace PayCalc24.PayrollInput.Services;

public sealed class PayrollInputValidationException(Diagnostic diagnostic) : Exception(diagnostic.Code)
{
    public Diagnostic Diagnostic { get; } = diagnostic;
}

public static class PayrollInputAuditActions
{
    public const string DefinitionDraftCreated = "PAYROLL_INPUT.DEFINITION_DRAFT_CREATED";
    public const string DefinitionDraftUpdated = "PAYROLL_INPUT.DEFINITION_DRAFT_UPDATED";
    public const string DefinitionPublished = "PAYROLL_INPUT.DEFINITION_PUBLISHED";
    public const string LedgerAccepted = "PAYROLL_INPUT.LEDGER_ACCEPTED";
    public const string LedgerCorrected = "PAYROLL_INPUT.LEDGER_CORRECTED";
}

public sealed class PayrollInputDefinitionService(
    ICompanyContext companyContext, ICurrentUser currentUser, ICorrelationContext correlationContext,
    IAuditWriter auditWriter, TimeProvider timeProvider) : IPayrollInputDefinitionService
{
    private readonly List<PayrollInputDefinition> definitions = [];

    public async ValueTask<PayrollInputDefinitionDto> CreateDraftAsync(CompanyId companyId, PayrollInputDefinitionId id, EffectivePeriod period, PayrollInputDefinitionContent content, CancellationToken cancellationToken = default)
    {
        Scope(companyId); ValidatePeriod(period); ValidateContent(content);
        var definition=definitions.SingleOrDefault(x=>x.Id==id);
        if(definition is null){definition=new(id,companyId);definitions.Add(definition);}else if(definition.CompanyId!=companyId)Throw(DiagnosticCodes.CrossCompanyInputDefinition,[]);
        EnsureCodeAvailable(companyId,content.Code,definition);
        var version=new PayrollInputDefinitionVersion(Guid.NewGuid(),definition.Versions.Count==0?1:definition.Versions.Max(x=>x.Revision)+1,period,Normalize(content));
        definition.Add(version); var dto=Dto(definition,version); await Audit(dto,PayrollInputAuditActions.DefinitionDraftCreated,cancellationToken); return dto;
    }

    public async ValueTask<PayrollInputDefinitionDto> UpdateDraftAsync(CompanyId companyId, PayrollInputDefinitionId id, int revision, EffectivePeriod period, PayrollInputDefinitionContent content, CancellationToken cancellationToken = default)
    {
        Scope(companyId);ValidatePeriod(period);ValidateContent(content);var definition=Definition(companyId,id);EnsureCodeAvailable(companyId,content.Code,definition);var version=Version(definition,revision);
        if(version.PublicationState!=PublicationState.DRAFT)Throw(DiagnosticCodes.PublishedInputDefinitionImmutable,new(){["revision"]=revision});
        version.Change(period,Normalize(content));var dto=Dto(definition,version);await Audit(dto,PayrollInputAuditActions.DefinitionDraftUpdated,cancellationToken);return dto;
    }

    public async ValueTask<PayrollInputDefinitionDto> PublishAsync(CompanyId companyId, PayrollInputDefinitionId id, int revision, CancellationToken cancellationToken = default)
    {
        Scope(companyId);var definition=Definition(companyId,id);var version=Version(definition,revision);if(version.PublicationState!=PublicationState.DRAFT)Throw(DiagnosticCodes.PublishedInputDefinitionImmutable,new(){["revision"]=revision});
        if(definition.Versions.Any(x=>x!=version&&Published(x)&&x.EffectivePeriod.Overlaps(version.EffectivePeriod)))Throw(DiagnosticCodes.PublishedVersionOverlap,new(){["revision"]=revision});
        version.Publish();var dto=Dto(definition,version);await Audit(dto,PayrollInputAuditActions.DefinitionPublished,cancellationToken);return dto;
    }

    public void Close(CompanyId companyId, PayrollInputDefinitionId id, int revision, DateOnly effectiveTo)
    { Scope(companyId);var version=Version(Definition(companyId,id),revision);if(version.PublicationState!=PublicationState.PUBLISHED)Throw(DiagnosticCodes.InvalidPublicationState,[]);ValidatePeriod(new(version.EffectivePeriod.EffectiveFrom,effectiveTo));version.Close(effectiveTo); }

    public IReadOnlyList<PayrollInputDefinitionDto> List(CompanyId companyId, PayrollInputDefinitionSearch search)
    { Scope(companyId);return definitions.Where(x=>x.CompanyId==companyId).SelectMany(x=>x.Versions.Select(v=>Dto(x,v))).Where(x=>(search.Status is null||x.Content.Status==search.Status)&&(string.IsNullOrWhiteSpace(search.SearchText)||x.Content.Code.Contains(search.SearchText,StringComparison.OrdinalIgnoreCase)||x.Content.Name.Contains(search.SearchText,StringComparison.OrdinalIgnoreCase))).OrderBy(x=>x.Content.Code,StringComparer.OrdinalIgnoreCase).ThenBy(x=>x.Revision).ToArray(); }

    public PayrollInputDefinitionDto GetByCode(CompanyId companyId, string code, int revision)
    { Scope(companyId);var matches=definitions.Where(x=>x.CompanyId==companyId).SelectMany(x=>x.Versions.Select(v=>(x,v))).Where(x=>Eq(x.v.Content.Code,code)&&x.v.Revision==revision).ToArray();if(matches.Length!=1)Throw(DiagnosticCodes.InputDefinitionNotFound,new(){["code"]=code,["revision"]=revision});return Dto(matches[0].x,matches[0].v); }

    public PayrollInputDefinitionDto ResolveEffective(CompanyId companyId, string code, DateOnly businessDate)
    { Scope(companyId);var matches=definitions.Where(x=>x.CompanyId==companyId).SelectMany(x=>x.Versions.Select(v=>(x,v))).Where(x=>Eq(x.v.Content.Code,code)&&Published(x.v)&&x.v.EffectivePeriod.Contains(businessDate)).ToArray();return Effective(matches,businessDate,code); }

    public PayrollInputDefinitionDto ResolveEffective(CompanyId companyId, PayrollInputDefinitionId id, DateOnly businessDate)
    { Scope(companyId);var definition=Definition(companyId,id);var matches=definition.Versions.Where(x=>Published(x)&&x.EffectivePeriod.Contains(businessDate)).Select(x=>(definition,x)).ToArray();return Effective(matches,businessDate,id.Value.ToString("D")); }

    private static PayrollInputDefinitionDto Effective((PayrollInputDefinition x,PayrollInputDefinitionVersion v)[] matches,DateOnly date,string reference)
    { if(matches.Length!=1)Throw(matches.Length==0?DiagnosticCodes.EffectiveInputDefinitionNotFound:DiagnosticCodes.EffectiveInputDefinitionAmbiguous,new(){["reference"]=reference,["businessDate"]=date,["matchCount"]=matches.Length});return Dto(matches[0].x,matches[0].v); }
    private void EnsureCodeAvailable(CompanyId companyId,string code,PayrollInputDefinition except){if(definitions.Any(x=>x!=except&&x.CompanyId==companyId&&x.Versions.Any(v=>Eq(v.Content.Code,code))))Throw(DiagnosticCodes.DuplicateInputDefinitionCode,new(){["code"]=code});}
    private PayrollInputDefinition Definition(CompanyId companyId,PayrollInputDefinitionId id){var any=definitions.SingleOrDefault(x=>x.Id==id);if(any is not null&&any.CompanyId!=companyId)Throw(DiagnosticCodes.CrossCompanyInputDefinition,[]);return any??throw Error(DiagnosticCodes.InputDefinitionNotFound,new(){["definitionId"]=id.Value});}
    private static PayrollInputDefinitionVersion Version(PayrollInputDefinition d,int r)=>d.Versions.SingleOrDefault(x=>x.Revision==r)??throw Error(DiagnosticCodes.InputDefinitionNotFound,new(){["revision"]=r});
    private static void ValidateContent(PayrollInputDefinitionContent c){if(string.IsNullOrWhiteSpace(c.Code)||string.IsNullOrWhiteSpace(c.Name))throw new ArgumentException("Code and name are required.");if(c.Validation is { } v&&(v.MinDecimal>v.MaxDecimal||v.MinInteger>v.MaxInteger||v.MaxTextLength<0))Throw(DiagnosticCodes.PayrollInputValueOutOfRange,[]);var numeric=c.DataType is PayrollInputDataType.DECIMAL or PayrollInputDataType.INTEGER;if(c.AggregationType is PayrollInputAggregationType.SUM or PayrollInputAggregationType.MIN or PayrollInputAggregationType.MAX&&!numeric||c.AggregationType==PayrollInputAggregationType.AVERAGE&&c.DataType!=PayrollInputDataType.DECIMAL||c.AggregationType==PayrollInputAggregationType.COUNT&&c.DataType!=PayrollInputDataType.INTEGER)Throw(DiagnosticCodes.InvalidPayrollInputAggregation,new(){["dataType"]=c.DataType,["aggregationType"]=c.AggregationType});}
    private static PayrollInputDefinitionContent Normalize(PayrollInputDefinitionContent c)=>c with{Code=c.Code.Trim(),Name=c.Name.Trim(),Description=string.IsNullOrWhiteSpace(c.Description)?null:c.Description.Trim()};
    private static void ValidatePeriod(EffectivePeriod p){if(p.EffectiveTo is not null&&p.EffectiveFrom>=p.EffectiveTo)Throw(DiagnosticCodes.InvalidEffectiveRange,[]);}
    private void Scope(CompanyId id){if(id!=companyContext.CompanyId)Throw(DiagnosticCodes.CompanyScopeMismatch,new(){["requestedCompanyId"]=id.Value,["currentCompanyId"]=companyContext.CompanyId.Value});}
    private async ValueTask Audit(PayrollInputDefinitionDto dto,string action,CancellationToken token)=>await auditWriter.WriteAsync(new(dto.CompanyId,currentUser.UserId,action,"PayrollInputDefinition",dto.Id.Value.ToString("D"),correlationContext.CorrelationId,timeProvider.GetUtcNow(),new Dictionary<string,object?>{{"revision",dto.Revision},{"code",dto.Content.Code}}),token);
    private static bool Published(PayrollInputDefinitionVersion v)=>v.PublicationState is PublicationState.PUBLISHED or PublicationState.SUPERSEDED;
    private static bool Eq(string a,string b)=>StringComparer.OrdinalIgnoreCase.Equals(a.Trim(),b.Trim());
    private static PayrollInputDefinitionDto Dto(PayrollInputDefinition d,PayrollInputDefinitionVersion v)=>new(d.Id,d.CompanyId,v.Revision,v.EffectivePeriod,v.PublicationState,v.Content);
    private static PayrollInputValidationException Error(string code,Dictionary<string,object?> args)=>new(new(code,DiagnosticSeverity.Error,args));
    private static void Throw(string code,Dictionary<string,object?> args)=>throw Error(code,args);
}

public sealed class PayrollInputLedgerService(
    ICompanyContext companyContext, IPayrollSubjectScopeReader subjectScope,
    IPayrollInputDefinitionService definitions, ICurrentUser currentUser,
    ICorrelationContext correlationContext, IAuditWriter auditWriter, TimeProvider timeProvider) : IPayrollInputLedgerService
{
    private readonly List<PayrollInputLedgerEntry> entries=[];
    private readonly Dictionary<(CompanyId,string),(string,PayrollInputLedgerEntryId)> idempotency=[];

    public async ValueTask<PayrollInputLedgerEntryDto> SubmitAsync(SubmitPayrollInput command,CancellationToken cancellationToken=default)
    {
        Scope(command.CompanyId);Subject(command.CompanyId,command.PayrollSubjectId);RequiredKey(command.IdempotencyKey);
        var definition=command.InputDefinitionId is { } id?definitions.ResolveEffective(command.CompanyId,id,command.BusinessDate):definitions.ResolveEffective(command.CompanyId,command.InputCode??string.Empty,command.BusinessDate);
        var fingerprint=Fingerprint(command.PayrollSubjectId.Value,command.PayrollPeriodId.Value,definition.Id.Value,command.BusinessDate,command.Value,command.SourceType,command.SourceSystem,command.SourceReference,null);
        if(TryIdempotent(command.CompanyId,command.IdempotencyKey,fingerprint,out var existing))return existing;
        ValidateEntry(definition,command.Value,command.SourceType);
        var dto=Create(command.CompanyId,command.PayrollSubjectId,command.PayrollPeriodId,command.BusinessDate,definition,command.Value,command.SourceType,command.SourceSystem,command.SourceReference,command.ObservedAt,command.EffectiveDate,command.CorrelationId,command.IdempotencyKey,null);
        var entry=new PayrollInputLedgerEntry(dto);entries.Add(entry);idempotency[(command.CompanyId,command.IdempotencyKey)]=(fingerprint,dto.Id);
        await Audit(dto,PayrollInputAuditActions.LedgerAccepted,cancellationToken);return dto;
    }

    public async ValueTask<PayrollInputLedgerEntryDto> CorrectAsync(SubmitPayrollInputCorrection command,CancellationToken cancellationToken=default)
    {
        Scope(command.CompanyId);RequiredKey(command.IdempotencyKey);var target=entries.SingleOrDefault(x=>x.Id==command.SupersedesEntryId)
            ?? throw Error(DiagnosticCodes.SupersededPayrollInputNotFound,new(){["entryId"]=command.SupersedesEntryId.Value});
        if(target.CompanyId!=command.CompanyId)Throw(DiagnosticCodes.CrossCompanyPayrollInputSupersession,[]);Subject(command.CompanyId,target.PayrollSubjectId);
        if(entries.Any(x=>x.SupersedesEntryId==target.Id))Throw(DiagnosticCodes.InvalidPayrollInputSupersessionScope,new(){["reason"]="target_not_active"});
        var definition=definitions.GetByCode(command.CompanyId,target.InputCode,target.InputDefinitionRevision);
        if(!definition.Content.AllowOverride)Throw(DiagnosticCodes.InvalidPayrollInputSupersessionScope,new(){["reason"]="override_not_allowed"});
        ValidateEntry(definition,command.Value,command.SourceType);
        var fingerprint=Fingerprint(target.PayrollSubjectId.Value,target.PayrollPeriodId.Value,target.InputDefinitionId.Value,target.BusinessDate,command.Value,command.SourceType,command.SourceSystem,command.SourceReference,target.Id.Value);
        if(TryIdempotent(command.CompanyId,command.IdempotencyKey,fingerprint,out var existing))return existing;
        var dto=Create(command.CompanyId,target.PayrollSubjectId,target.PayrollPeriodId,target.BusinessDate,definition,command.Value,command.SourceType,command.SourceSystem,command.SourceReference,command.ObservedAt,target.EffectiveDate,command.CorrelationId,command.IdempotencyKey,target.Id);
        var entry=new PayrollInputLedgerEntry(dto);entries.Add(entry);idempotency[(command.CompanyId,command.IdempotencyKey)]=(fingerprint,dto.Id);
        await Audit(dto,PayrollInputAuditActions.LedgerCorrected,cancellationToken);return dto;
    }

    public EffectivePayrollInputDto GetEffectiveInput(CompanyId companyId,PayrollSubjectId subjectId,PayrollPeriodId periodId,PayrollInputDefinitionId definitionId)
    { Scope(companyId);Subject(companyId,subjectId);var active=Active(companyId,subjectId,periodId).Where(x=>x.InputDefinitionId==definitionId).ToArray();if(active.Length==0)Throw(DiagnosticCodes.InputDefinitionNotFound,new(){["definitionId"]=definitionId.Value});return Aggregate(active); }
    public IReadOnlyList<EffectivePayrollInputDto> GetEffectiveInputSet(CompanyId companyId,PayrollSubjectId subjectId,PayrollPeriodId periodId)
    { Scope(companyId);Subject(companyId,subjectId);return Active(companyId,subjectId,periodId).GroupBy(x=>x.InputDefinitionId).Select(x=>Aggregate(x.ToArray())).OrderBy(x=>x.InputCode,StringComparer.OrdinalIgnoreCase).ToArray(); }
    public IReadOnlyList<PayrollInputLedgerEntryDto> GetHistory(CompanyId companyId,PayrollSubjectId subjectId,PayrollPeriodId periodId,PayrollInputDefinitionId? definitionId=null)
    { Scope(companyId);Subject(companyId,subjectId);return entries.Where(x=>x.CompanyId==companyId&&x.PayrollSubjectId==subjectId&&x.PayrollPeriodId==periodId&&(definitionId is null||x.InputDefinitionId==definitionId)).OrderBy(x=>x.RecordedAt).ThenBy(x=>x.Id.Value).Select(x=>x.ToDto()).ToArray(); }
    public PayrollInputSourceTrace GetSourceTrace(CompanyId companyId,PayrollInputLedgerEntryId entryId)
    { Scope(companyId);var e=entries.SingleOrDefault(x=>x.Id==entryId)??throw Error(DiagnosticCodes.SupersededPayrollInputNotFound,[]);if(e.CompanyId!=companyId)Throw(DiagnosticCodes.CrossCompanyPayrollInputSupersession,[]);return new(e.Id,e.SourceType,e.SourceSystem,e.SourceReference,e.ObservedAt,e.RecordedAt,e.CorrelationId,e.IdempotencyKey); }
    public PayrollInputLedgerEntryDto? ResolveByIdempotencyKey(CompanyId companyId,string idempotencyKey){Scope(companyId);return idempotency.TryGetValue((companyId,idempotencyKey),out var item)?entries.Single(x=>x.Id==item.Item2).ToDto():null;}

    private PayrollInputLedgerEntryDto Create(CompanyId companyId,PayrollSubjectId subjectId,PayrollPeriodId periodId,DateOnly businessDate,PayrollInputDefinitionDto d,PayrollInputValue value,PayrollInputSourceType source,string? system,string? reference,DateTimeOffset? observed,DateOnly? effective,string? correlation,string key,PayrollInputLedgerEntryId? supersedes)=>new(PayrollInputLedgerEntryId.From(Guid.NewGuid()),companyId,subjectId,periodId,businessDate,d.Id,d.Revision,d.Content.Code,value,d.Content.DataType,d.Content.UnitType,d.Content.AggregationType,source,Optional(system),Optional(reference),observed,effective,timeProvider.GetUtcNow(),currentUser.UserId,Optional(correlation)??correlationContext.CorrelationId,key,supersedes);
    private PayrollInputLedgerEntry[] Active(CompanyId c,PayrollSubjectId s,PayrollPeriodId p){var scope=entries.Where(x=>x.CompanyId==c&&x.PayrollSubjectId==s&&x.PayrollPeriodId==p).ToArray();var superseded=scope.Where(x=>x.SupersedesEntryId is not null).Select(x=>x.SupersedesEntryId!.Value).ToHashSet();return scope.Where(x=>!superseded.Contains(x.Id)).ToArray();}
    private static EffectivePayrollInputDto Aggregate(PayrollInputLedgerEntry[] values)
    {
        var first=values[0];if(values.Any(x=>x.InputDefinitionRevision!=first.InputDefinitionRevision||x.DataType!=first.DataType||x.UnitType!=first.UnitType||x.AggregationType!=first.AggregationType))Throw(DiagnosticCodes.InvalidPayrollInputAggregation,[]);
        PayrollInputValue value;
        switch(first.AggregationType)
        {
            case PayrollInputAggregationType.NONE when values.Length>1: Throw(DiagnosticCodes.AmbiguousActivePayrollInput,new(){["definitionId"]=first.InputDefinitionId.Value,["count"]=values.Length});return null!;
            case PayrollInputAggregationType.NONE: value=first.Value;break;
            case PayrollInputAggregationType.COUNT: value=PayrollInputValue.Integer(values.LongLength);break;
            case PayrollInputAggregationType.LATEST: value=values.OrderBy(x=>x.ObservedAt??x.RecordedAt).ThenBy(x=>x.RecordedAt).ThenBy(x=>x.Id.Value).Last().Value;break;
            default:value=NumericAggregate(first.AggregationType,values.Select(AsDecimal).ToArray(),first.DataType);break;
        }
        return new(first.InputDefinitionId,first.InputDefinitionRevision,first.InputCode,value,value.DataType,first.UnitType,first.AggregationType,values.OrderBy(x=>x.RecordedAt).ThenBy(x=>x.Id.Value).Select(x=>x.Id).ToArray());
    }
    private static PayrollInputValue NumericAggregate(PayrollInputAggregationType type,decimal[] values,PayrollInputDataType dataType)
    {if(dataType is not (PayrollInputDataType.DECIMAL or PayrollInputDataType.INTEGER))Throw(DiagnosticCodes.InvalidPayrollInputAggregation,new(){["dataType"]=dataType});var result=type switch{PayrollInputAggregationType.SUM=>values.Sum(),PayrollInputAggregationType.AVERAGE=>values.Average(),PayrollInputAggregationType.MIN=>values.Min(),PayrollInputAggregationType.MAX=>values.Max(),_=>throw Error(DiagnosticCodes.InvalidPayrollInputAggregation,[])};return dataType==PayrollInputDataType.INTEGER&&result==decimal.Truncate(result)?PayrollInputValue.Integer(decimal.ToInt64(result)):PayrollInputValue.Decimal(result);}
    private static decimal AsDecimal(PayrollInputLedgerEntry x)=>x.Value.DataType switch{PayrollInputDataType.DECIMAL=>x.Value.DecimalValue!.Value,PayrollInputDataType.INTEGER=>x.Value.IntegerValue!.Value,_=>throw Error(DiagnosticCodes.InvalidPayrollInputAggregation,[])};
    private static void ValidateEntry(PayrollInputDefinitionDto d,PayrollInputValue value,PayrollInputSourceType source){ValidateShape(value);if(value.DataType!=d.Content.DataType)Throw(DiagnosticCodes.InvalidPayrollInputValueType,new(){["expected"]=d.Content.DataType,["actual"]=value.DataType});if(source==PayrollInputSourceType.MANUAL&&!d.Content.AllowManualEntry)Throw(DiagnosticCodes.ManualPayrollInputNotAllowed,[]);if(source!=PayrollInputSourceType.MANUAL&&!d.Content.AllowExternalEntry)Throw(DiagnosticCodes.ExternalPayrollInputNotAllowed,[]);var v=d.Content.Validation;if(v is null)return;if(value.DecimalValue is { } dec&&(dec<v.MinDecimal||dec>v.MaxDecimal)||value.IntegerValue is { } integer&&(integer<v.MinInteger||integer>v.MaxInteger)||value.TextValue is { } text&&v.MaxTextLength is { } max&&text.Length>max)Throw(DiagnosticCodes.PayrollInputValueOutOfRange,[]);}
    private static void ValidateShape(PayrollInputValue v){var populated=new object?[]{v.DecimalValue,v.IntegerValue,v.BooleanValue,v.DateValue,v.TextValue}.Count(x=>x is not null);var matches=v.DataType switch{PayrollInputDataType.DECIMAL=>v.DecimalValue is not null,PayrollInputDataType.INTEGER=>v.IntegerValue is not null,PayrollInputDataType.BOOLEAN=>v.BooleanValue is not null,PayrollInputDataType.DATE=>v.DateValue is not null,PayrollInputDataType.TEXT=>v.TextValue is not null,_=>false};if(populated!=1||!matches)Throw(DiagnosticCodes.InvalidPayrollInputValueType,[]);}
    private bool TryIdempotent(CompanyId companyId,string key,string fingerprint,out PayrollInputLedgerEntryDto dto){if(idempotency.TryGetValue((companyId,key),out var found)){if(found.Item1!=fingerprint)Throw(DiagnosticCodes.DuplicatePayrollInputIdempotencyKey,new(){["idempotencyKey"]=key});dto=entries.Single(x=>x.Id==found.Item2).ToDto();return true;}dto=null!;return false;}
    private void Subject(CompanyId companyId,PayrollSubjectId subjectId){var owner=subjectScope.FindCompany(subjectId);if(owner is null)Throw(DiagnosticCodes.CrossCompanyPayrollSubject,new(){["reason"]="not_found"});if(owner!=companyId)Throw(DiagnosticCodes.CrossCompanyPayrollSubject,[]);}
    private void Scope(CompanyId id){if(id!=companyContext.CompanyId)Throw(DiagnosticCodes.CompanyScopeMismatch,[]);}
    private async ValueTask Audit(PayrollInputLedgerEntryDto dto,string action,CancellationToken token)=>await auditWriter.WriteAsync(new(dto.CompanyId,currentUser.UserId,action,"PayrollInputLedgerEntry",dto.Id.Value.ToString("D"),dto.CorrelationId,dto.RecordedAt,new Dictionary<string,object?>{{"payrollSubjectId",dto.PayrollSubjectId.Value},{"payrollPeriodId",dto.PayrollPeriodId.Value},{"inputDefinitionId",dto.InputDefinitionId.Value},{"revision",dto.InputDefinitionRevision},{"supersedesEntryId",dto.SupersedesEntryId?.Value}}),token);
    private static string Fingerprint(params object?[] values){var canonical=string.Join('|',values.Select(x=>x switch{PayrollInputValue v=>$"{v.DataType}:{v.DecimalValue?.ToString(CultureInfo.InvariantCulture)}:{v.IntegerValue}:{v.BooleanValue}:{v.DateValue:yyyy-MM-dd}:{v.TextValue}",DateOnly d=>d.ToString("yyyy-MM-dd",CultureInfo.InvariantCulture),_=>x?.ToString()??string.Empty}));return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));}
    private static void RequiredKey(string key){if(string.IsNullOrWhiteSpace(key))throw new ArgumentException("Idempotency key is required.",nameof(key));}
    private static string? Optional(string? value)=>string.IsNullOrWhiteSpace(value)?null:value.Trim();
    private static PayrollInputValidationException Error(string code,Dictionary<string,object?> args)=>new(new(code,DiagnosticSeverity.Error,args));
    private static void Throw(string code,Dictionary<string,object?> args)=>throw Error(code,args);
}
