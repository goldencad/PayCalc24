using PayCalc24.Contracts.Identity;
using PayCalc24.Contracts.Organization;
using PayCalc24.Contracts.Temporal;

namespace PayCalc24.Contracts.PayrollInput;

// These identifiers intentionally mirror the stable language-neutral API data-type codes.
#pragma warning disable CA1720

public readonly record struct PayrollInputDefinitionId(Guid Value)
{
    public static PayrollInputDefinitionId From(Guid value) => value == Guid.Empty
        ? throw new ArgumentException("Identifier cannot be empty.", nameof(value)) : new(value);
}

public readonly record struct PayrollInputLedgerEntryId(Guid Value)
{
    public static PayrollInputLedgerEntryId From(Guid value) => value == Guid.Empty
        ? throw new ArgumentException("Identifier cannot be empty.", nameof(value)) : new(value);
}

/// <summary>Minimum typed period identity used until the Payroll Period module owns its lifecycle.</summary>
public readonly record struct PayrollPeriodId(Guid Value)
{
    public static PayrollPeriodId From(Guid value) => value == Guid.Empty
        ? throw new ArgumentException("Identifier cannot be empty.", nameof(value)) : new(value);
}

public enum PayrollInputDataType { DECIMAL, INTEGER, BOOLEAN, DATE, TEXT }
public enum PayrollInputUnitType { NONE, AMOUNT, DAYS, HOURS, PERCENT, COUNT, SCORE }
public enum PayrollInputSourceType { MANUAL, ATTENDANCE, PERFORMANCE, IMPORT, API, EXTERNAL, SYSTEM, OTHER }
public enum PayrollInputAggregationType { NONE, SUM, AVERAGE, MIN, MAX, LATEST, COUNT }
public enum PayrollInputDefinitionStatus { ACTIVE, INACTIVE }

public sealed record PayrollInputValidation(decimal? MinDecimal = null, decimal? MaxDecimal = null, long? MinInteger = null, long? MaxInteger = null, int? MaxTextLength = null);

public sealed record PayrollInputDefinitionContent(
    string Code, string Name, string? Description, PayrollInputDataType DataType,
    PayrollInputUnitType UnitType, PayrollInputSourceType SourceType,
    PayrollInputAggregationType AggregationType, bool IsRequired, bool AllowManualEntry,
    bool AllowExternalEntry, bool AllowOverride, PayrollInputValidation? Validation,
    int? DisplayOrder, PayrollInputDefinitionStatus Status);

public sealed record PayrollInputDefinitionDto(
    PayrollInputDefinitionId Id, CompanyId CompanyId, int Revision,
    EffectivePeriod EffectivePeriod, PublicationState PublicationState,
    PayrollInputDefinitionContent Content);

public sealed record PayrollInputDefinitionSearch(string? SearchText = null, PayrollInputDefinitionStatus? Status = null);

/// <summary>Canonical discriminated value. Exactly one typed member is populated.</summary>
public sealed record PayrollInputValue(
    PayrollInputDataType DataType, decimal? DecimalValue = null, long? IntegerValue = null,
    bool? BooleanValue = null, DateOnly? DateValue = null, string? TextValue = null)
{
    public static PayrollInputValue Decimal(decimal value) => new(PayrollInputDataType.DECIMAL, DecimalValue: value);
    public static PayrollInputValue Integer(long value) => new(PayrollInputDataType.INTEGER, IntegerValue: value);
    public static PayrollInputValue Boolean(bool value) => new(PayrollInputDataType.BOOLEAN, BooleanValue: value);
    public static PayrollInputValue Date(DateOnly value) => new(PayrollInputDataType.DATE, DateValue: value);
    public static PayrollInputValue Text(string value) => new(PayrollInputDataType.TEXT, TextValue: value ?? throw new ArgumentNullException(nameof(value)));
}

public sealed record SubmitPayrollInput(
    CompanyId CompanyId, PayrollSubjectId PayrollSubjectId, PayrollPeriodId PayrollPeriodId,
    DateOnly BusinessDate, PayrollInputValue Value, PayrollInputSourceType SourceType,
    string IdempotencyKey, PayrollInputDefinitionId? InputDefinitionId = null, string? InputCode = null,
    string? SourceSystem = null, string? SourceReference = null, DateTimeOffset? ObservedAt = null,
    DateOnly? EffectiveDate = null, string? CorrelationId = null);

public sealed record SubmitPayrollInputCorrection(
    CompanyId CompanyId, PayrollInputLedgerEntryId SupersedesEntryId, PayrollInputValue Value,
    string IdempotencyKey, PayrollInputSourceType SourceType = PayrollInputSourceType.MANUAL,
    string? SourceSystem = null, string? SourceReference = null, DateTimeOffset? ObservedAt = null,
    string? CorrelationId = null);

public sealed record PayrollInputLedgerEntryDto(
    PayrollInputLedgerEntryId Id, CompanyId CompanyId, PayrollSubjectId PayrollSubjectId,
    PayrollPeriodId PayrollPeriodId, DateOnly BusinessDate,
    PayrollInputDefinitionId InputDefinitionId, int InputDefinitionRevision,
    string InputCode, PayrollInputValue Value, PayrollInputDataType DataType,
    PayrollInputUnitType UnitType, PayrollInputAggregationType AggregationType,
    PayrollInputSourceType SourceType, string? SourceSystem, string? SourceReference,
    DateTimeOffset? ObservedAt, DateOnly? EffectiveDate, DateTimeOffset RecordedAt,
    UserId? RecordedBy, string CorrelationId, string IdempotencyKey,
    PayrollInputLedgerEntryId? SupersedesEntryId);

public sealed record EffectivePayrollInputDto(
    PayrollInputDefinitionId InputDefinitionId, int InputDefinitionRevision, string InputCode,
    PayrollInputValue Value, PayrollInputDataType DataType, PayrollInputUnitType UnitType,
    PayrollInputAggregationType AggregationType, IReadOnlyList<PayrollInputLedgerEntryId> ContributingEntryIds);

public sealed record PayrollInputSourceTrace(
    PayrollInputLedgerEntryId EntryId, PayrollInputSourceType SourceType, string? SourceSystem,
    string? SourceReference, DateTimeOffset? ObservedAt, DateTimeOffset RecordedAt,
    string CorrelationId, string IdempotencyKey);

public interface IPayrollSubjectScopeReader
{
    CompanyId? FindCompany(PayrollSubjectId payrollSubjectId);
}

public interface IPayrollInputDefinitionService
{
    ValueTask<PayrollInputDefinitionDto> CreateDraftAsync(CompanyId companyId, PayrollInputDefinitionId id, EffectivePeriod period, PayrollInputDefinitionContent content, CancellationToken cancellationToken = default);
    ValueTask<PayrollInputDefinitionDto> UpdateDraftAsync(CompanyId companyId, PayrollInputDefinitionId id, int revision, EffectivePeriod period, PayrollInputDefinitionContent content, CancellationToken cancellationToken = default);
    ValueTask<PayrollInputDefinitionDto> PublishAsync(CompanyId companyId, PayrollInputDefinitionId id, int revision, CancellationToken cancellationToken = default);
    void Close(CompanyId companyId, PayrollInputDefinitionId id, int revision, DateOnly effectiveTo);
    IReadOnlyList<PayrollInputDefinitionDto> List(CompanyId companyId, PayrollInputDefinitionSearch search);
    PayrollInputDefinitionDto GetByCode(CompanyId companyId, string code, int revision);
    PayrollInputDefinitionDto ResolveEffective(CompanyId companyId, string code, DateOnly businessDate);
    PayrollInputDefinitionDto ResolveEffective(CompanyId companyId, PayrollInputDefinitionId id, DateOnly businessDate);
}

public interface IPayrollInputLedgerService
{
    ValueTask<PayrollInputLedgerEntryDto> SubmitAsync(SubmitPayrollInput command, CancellationToken cancellationToken = default);
    ValueTask<PayrollInputLedgerEntryDto> CorrectAsync(SubmitPayrollInputCorrection command, CancellationToken cancellationToken = default);
    EffectivePayrollInputDto GetEffectiveInput(CompanyId companyId, PayrollSubjectId subjectId, PayrollPeriodId periodId, PayrollInputDefinitionId definitionId);
    IReadOnlyList<EffectivePayrollInputDto> GetEffectiveInputSet(CompanyId companyId, PayrollSubjectId subjectId, PayrollPeriodId periodId);
    IReadOnlyList<PayrollInputLedgerEntryDto> GetHistory(CompanyId companyId, PayrollSubjectId subjectId, PayrollPeriodId periodId, PayrollInputDefinitionId? definitionId = null);
    PayrollInputSourceTrace GetSourceTrace(CompanyId companyId, PayrollInputLedgerEntryId entryId);
    PayrollInputLedgerEntryDto? ResolveByIdempotencyKey(CompanyId companyId, string idempotencyKey);
}
#pragma warning restore CA1720
