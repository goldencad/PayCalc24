using PayCalc24.Contracts.Identity;
using PayCalc24.Contracts.Organization;
using PayCalc24.Contracts.PayrollInput;
using PayCalc24.Contracts.Temporal;

namespace PayCalc24.PayrollInput.Model;

public sealed class PayrollInputDefinition
{
    private readonly List<PayrollInputDefinitionVersion> versions = [];
    public PayrollInputDefinition(PayrollInputDefinitionId id, CompanyId companyId) { Id = id; CompanyId = companyId; }
    private PayrollInputDefinition() { }
    public PayrollInputDefinitionId Id { get; private set; }
    public CompanyId CompanyId { get; private set; }
    public IReadOnlyList<PayrollInputDefinitionVersion> Versions => versions;
    internal void Add(PayrollInputDefinitionVersion version) => versions.Add(version);
}

public sealed class PayrollInputDefinitionVersion
{
    public PayrollInputDefinitionVersion(Guid id, int revision, EffectivePeriod period, PayrollInputDefinitionContent content)
    { Id = id; Revision = revision; EffectivePeriod = period; Content = content; }
    private PayrollInputDefinitionVersion() { Content = default!; }
    public Guid Id { get; private set; }
    public int Revision { get; private set; }
    public EffectivePeriod EffectivePeriod { get; private set; }
    public PublicationState PublicationState { get; private set; } = PublicationState.DRAFT;
    public PayrollInputDefinitionContent Content { get; private set; }
    internal void Change(EffectivePeriod period, PayrollInputDefinitionContent content) { EffectivePeriod = period; Content = content; }
    internal void Publish() => PublicationState = PublicationState.PUBLISHED;
    internal void Close(DateOnly effectiveTo) { EffectivePeriod = new(EffectivePeriod.EffectiveFrom, effectiveTo); PublicationState = PublicationState.SUPERSEDED; }
}

/// <summary>Accepted business fields have no setters; corrections can only append another instance.</summary>
public sealed class PayrollInputLedgerEntry
{
    public PayrollInputLedgerEntry(PayrollInputLedgerEntryDto value)
    {
        Id=value.Id; CompanyId=value.CompanyId; PayrollSubjectId=value.PayrollSubjectId; PayrollPeriodId=value.PayrollPeriodId;
        BusinessDate=value.BusinessDate; InputDefinitionId=value.InputDefinitionId; InputDefinitionRevision=value.InputDefinitionRevision;
        InputCode=value.InputCode; Value=value.Value; DataType=value.DataType; UnitType=value.UnitType; AggregationType=value.AggregationType;
        SourceType=value.SourceType; SourceSystem=value.SourceSystem; SourceReference=value.SourceReference; ObservedAt=value.ObservedAt;
        EffectiveDate=value.EffectiveDate; RecordedAt=value.RecordedAt; RecordedBy=value.RecordedBy; CorrelationId=value.CorrelationId;
        IdempotencyKey=value.IdempotencyKey; SupersedesEntryId=value.SupersedesEntryId;
    }
    public PayrollInputLedgerEntryId Id { get; }
    public CompanyId CompanyId { get; }
    public PayrollSubjectId PayrollSubjectId { get; }
    public PayrollPeriodId PayrollPeriodId { get; }
    public DateOnly BusinessDate { get; }
    public PayrollInputDefinitionId InputDefinitionId { get; }
    public int InputDefinitionRevision { get; }
    public string InputCode { get; }
    public PayrollInputValue Value { get; }
    public PayrollInputDataType DataType { get; }
    public PayrollInputUnitType UnitType { get; }
    public PayrollInputAggregationType AggregationType { get; }
    public PayrollInputSourceType SourceType { get; }
    public string? SourceSystem { get; }
    public string? SourceReference { get; }
    public DateTimeOffset? ObservedAt { get; }
    public DateOnly? EffectiveDate { get; }
    public DateTimeOffset RecordedAt { get; }
    public UserId? RecordedBy { get; }
    public string CorrelationId { get; }
    public string IdempotencyKey { get; }
    public PayrollInputLedgerEntryId? SupersedesEntryId { get; }
    public PayrollInputLedgerEntryDto ToDto() => new(Id,CompanyId,PayrollSubjectId,PayrollPeriodId,BusinessDate,InputDefinitionId,InputDefinitionRevision,InputCode,Value,DataType,UnitType,AggregationType,SourceType,SourceSystem,SourceReference,ObservedAt,EffectiveDate,RecordedAt,RecordedBy,CorrelationId,IdempotencyKey,SupersedesEntryId);
}
