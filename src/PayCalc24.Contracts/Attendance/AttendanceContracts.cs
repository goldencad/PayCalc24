using PayCalc24.Contracts.Diagnostics;
using PayCalc24.Contracts.Identity;
using PayCalc24.Contracts.Organization;
using PayCalc24.Contracts.PayrollInput;
using PayCalc24.Contracts.Temporal;

namespace PayCalc24.Contracts.Attendance;

public readonly record struct AttendanceSourceId(Guid Value) { public static AttendanceSourceId From(Guid value)=>new(Required(value)); private static Guid Required(Guid value)=>value==Guid.Empty?throw new ArgumentException("Identifier cannot be empty.",nameof(value)):value; }
public readonly record struct AttendanceMappingVersionId(Guid Value) { public static AttendanceMappingVersionId From(Guid value)=>new(Required(value)); private static Guid Required(Guid value)=>value==Guid.Empty?throw new ArgumentException("Identifier cannot be empty.",nameof(value)):value; }
public readonly record struct AttendanceImportBatchId(Guid Value) { public static AttendanceImportBatchId From(Guid value)=>new(Required(value)); private static Guid Required(Guid value)=>value==Guid.Empty?throw new ArgumentException("Identifier cannot be empty.",nameof(value)):value; }
public readonly record struct AttendanceFactId(Guid Value) { public static AttendanceFactId From(Guid value)=>new(Required(value)); private static Guid Required(Guid value)=>value==Guid.Empty?throw new ArgumentException("Identifier cannot be empty.",nameof(value)):value; }
public readonly record struct AttendancePolicyDefinitionId(Guid Value) { public static AttendancePolicyDefinitionId From(Guid value)=>new(Required(value)); private static Guid Required(Guid value)=>value==Guid.Empty?throw new ArgumentException("Identifier cannot be empty.",nameof(value)):value; }
public readonly record struct AttendancePolicyVersionId(Guid Value) { public static AttendancePolicyVersionId From(Guid value)=>new(Required(value)); private static Guid Required(Guid value)=>value==Guid.Empty?throw new ArgumentException("Identifier cannot be empty.",nameof(value)):value; }

public enum AttendanceSourceKind { FILE, DEVICE, APPLICATION, API, MANUAL, OTHER }
public enum AttendanceImportStatus { DRAFT, VALIDATED, COMMITTED, FAILED }
public enum AttendanceQuantityUnit { HOURS, DAYS, MINUTES, COUNT, BOOLEAN }
public enum AttendanceFactKind { ExpectedWork, ActualWork, PaidLeave, UnpaidLeave, Late, EarlyLeave, Overtime, Absence, Status }
public enum AttendanceAggregation { SUM, MIN, MAX, LATEST }

public sealed record AttendanceSourceDto(AttendanceSourceId Id,CompanyId CompanyId,string Code,string Name,AttendanceSourceKind Kind,string? ExternalSystem,string TimeZoneId,bool Active);
public sealed record AttendanceFieldMap(string CanonicalField,string SourceField,bool Required=false);
public sealed record AttendanceMappingVersionDto(AttendanceMappingVersionId Id,AttendanceSourceId SourceId,CompanyId CompanyId,int Revision,PublicationState PublicationState,IReadOnlyList<AttendanceFieldMap> Fields);
public sealed record AttendanceRawRow(string RowReference,IReadOnlyDictionary<string,string?> Values);
public sealed record AttendanceCanonicalFact(AttendanceFactId Id,CompanyId CompanyId,PayrollSubjectId PayrollSubjectId,DateOnly BusinessDate,AttendanceFactKind Kind,decimal? Quantity,AttendanceQuantityUnit Unit,bool? BooleanValue,string? Code,string SourceRecordReference,AttendanceImportBatchId BatchId);
public sealed record AttendanceDiagnostic(string Code,DiagnosticSeverity Severity,string RowReference,IReadOnlyDictionary<string,object?> Arguments);

public sealed record AttendanceDerivedInputRule(AttendanceFactKind FactKind,AttendanceQuantityUnit FactUnit,string InputCode,PayrollInputDataType DataType,PayrollInputUnitType InputUnit,AttendanceAggregation Aggregation=AttendanceAggregation.SUM,decimal Factor=1m,Guid? FormulaVersionId=null,Guid? ParameterSetVersionId=null,Guid? LookupTableVersionId=null,Guid? RuleSetVersionId=null);
public sealed record AttendancePolicyVersionDto(AttendancePolicyVersionId Id,AttendancePolicyDefinitionId DefinitionId,CompanyId CompanyId,string Code,int Revision,EffectivePeriod EffectivePeriod,PublicationState PublicationState,IReadOnlyList<AttendanceDerivedInputRule> Rules);
public sealed record AttendanceDerivedInputCandidate(PayrollSubjectId PayrollSubjectId,DateOnly BusinessDate,string InputCode,PayrollInputValue Value,PayrollInputDataType DataType,PayrollInputUnitType Unit,AttendancePolicyVersionId PolicyVersionId,IReadOnlyList<AttendanceFactId> FactIds,string ResultHash,Guid? FormulaVersionId=null,Guid? ParameterSetVersionId=null,Guid? LookupTableVersionId=null,Guid? RuleSetVersionId=null);
public sealed record AttendanceImportPreview(AttendanceImportBatchId BatchId,AttendanceImportStatus Status,string Fingerprint,IReadOnlyList<AttendanceCanonicalFact> Facts,IReadOnlyList<AttendanceDerivedInputCandidate> DerivedInputs,IReadOnlyList<AttendanceDiagnostic> Diagnostics,int AcceptedRows,int RejectedRows,int WarningCount);
public sealed record AttendanceImportBatchDto(AttendanceImportBatchId Id,CompanyId CompanyId,AttendanceSourceId SourceId,AttendanceMappingVersionId MappingVersionId,AttendancePolicyVersionId PolicyVersionId,PayrollPeriodId PayrollPeriodId,DateOnly DateFrom,DateOnly DateTo,AttendanceImportStatus Status,string SourceReference,string SourceFingerprint,string IdempotencyKey,DateTimeOffset ImportedAt,UserId ImportedBy,string CorrelationId,int RowCount,int AcceptedCount,int RejectedCount,int WarningCount);
public sealed record PreviewAttendanceImport(CompanyId CompanyId,AttendanceSourceId SourceId,AttendanceMappingVersionId MappingVersionId,AttendancePolicyVersionId? PolicyVersionId,PayrollPeriodId PayrollPeriodId,DateOnly DateFrom,DateOnly DateTo,string SourceReference,string IdempotencyKey,IReadOnlyList<AttendanceRawRow> Rows);
public sealed record CommitAttendanceImport(CompanyId CompanyId,AttendanceImportBatchId BatchId,string IdempotencyKey,IReadOnlyDictionary<string,PayrollInputLedgerEntryId>? SupersededEntries=null);
public sealed record AttendanceCommitResult(AttendanceImportBatchDto Batch,IReadOnlyList<PayrollInputLedgerEntryDto> LedgerEntries,bool IsIdempotentRetry);

public interface IAttendancePayrollSubjectResolver { PayrollSubjectId? ResolveByEmployeeCode(CompanyId companyId,string employeeCode); CompanyId? FindCompany(PayrollSubjectId subjectId); }
public interface IAttendanceService
{
 ValueTask<AttendanceSourceDto> CreateSourceAsync(CompanyId companyId,string code,string name,AttendanceSourceKind kind,string timeZoneId,string? externalSystem=null,CancellationToken cancellationToken=default);
 ValueTask<AttendanceMappingVersionDto> CreateMappingDraftAsync(CompanyId companyId,AttendanceSourceId sourceId,IReadOnlyList<AttendanceFieldMap> fields,CancellationToken cancellationToken=default);
 ValueTask<AttendanceMappingVersionDto> PublishMappingAsync(CompanyId companyId,AttendanceMappingVersionId id,CancellationToken cancellationToken=default);
 ValueTask<AttendancePolicyVersionDto> CreatePolicyDraftAsync(CompanyId companyId,string code,EffectivePeriod period,IReadOnlyList<AttendanceDerivedInputRule> rules,AttendancePolicyDefinitionId? definitionId=null,CancellationToken cancellationToken=default);
 ValueTask<AttendancePolicyVersionDto> PublishPolicyAsync(CompanyId companyId,AttendancePolicyVersionId id,CancellationToken cancellationToken=default);
 AttendancePolicyVersionDto ResolvePolicy(CompanyId companyId,string code,DateOnly businessDate);
 ValueTask<AttendanceImportPreview> PreviewAsync(PreviewAttendanceImport command,CancellationToken cancellationToken=default);
 ValueTask<AttendanceImportPreview> ValidateAsync(CompanyId companyId,AttendanceImportBatchId batchId,CancellationToken cancellationToken=default);
 ValueTask<AttendanceCommitResult> CommitAsync(CommitAttendanceImport command,CancellationToken cancellationToken=default);
 AttendanceImportPreview Evaluate(CompanyId companyId,AttendanceImportBatchId batchId,AttendancePolicyVersionId explicitPolicyVersionId);
 AttendanceImportBatchDto GetBatch(CompanyId companyId,AttendanceImportBatchId batchId);
}
