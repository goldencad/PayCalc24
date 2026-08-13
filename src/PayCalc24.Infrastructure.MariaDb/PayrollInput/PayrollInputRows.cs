using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PayCalc24.Contracts.PayrollInput;
using PayCalc24.Contracts.Temporal;
using PayCalc24.Infrastructure.MariaDb.Compensation;

namespace PayCalc24.Infrastructure.MariaDb.PayrollInput;

// EF-only rows keep persistence details out of the PayrollInput module and its public contracts.
internal sealed class PayrollInputDefinitionVersionRow
{
    public Guid Id{get;set;} public Guid DefinitionId{get;set;} public Guid CompanyId{get;set;} public int Revision{get;set;}
    public string Code{get;set;}=""; public string Name{get;set;}=""; public string? Description{get;set;}
    public PayrollInputDataType DataType{get;set;} public PayrollInputUnitType UnitType{get;set;} public PayrollInputSourceType SourceType{get;set;}
    public PayrollInputAggregationType AggregationType{get;set;} public bool IsRequired{get;set;} public bool AllowManualEntry{get;set;}
    public bool AllowExternalEntry{get;set;} public bool AllowOverride{get;set;} public decimal? MinDecimal{get;set;} public decimal? MaxDecimal{get;set;}
    public long? MinInteger{get;set;} public long? MaxInteger{get;set;} public int? MaxTextLength{get;set;} public int? DisplayOrder{get;set;}
    public DateOnly EffectiveFrom{get;set;} public DateOnly? EffectiveTo{get;set;} public PayrollInputDefinitionStatus Status{get;set;} public PublicationState PublicationState{get;set;}
}

internal sealed class PayrollInputLedgerEntryRow
{
    public Guid Id{get;set;} public Guid CompanyId{get;set;} public Guid PayrollSubjectId{get;set;} public Guid PayrollPeriodId{get;set;}
    public DateOnly BusinessDate{get;set;} public Guid InputDefinitionId{get;set;} public int InputDefinitionRevision{get;set;} public string InputCode{get;set;}="";
    public PayrollInputDataType DataType{get;set;} public PayrollInputUnitType UnitType{get;set;} public PayrollInputAggregationType AggregationType{get;set;}
    public decimal? DecimalValue{get;set;} public long? IntegerValue{get;set;} public bool? BooleanValue{get;set;} public DateOnly? DateValue{get;set;} public string? TextValue{get;set;}
    public PayrollInputSourceType SourceType{get;set;} public string? SourceSystem{get;set;} public string? SourceReference{get;set;}
    public DateTimeOffset? ObservedAt{get;set;} public DateOnly? EffectiveDate{get;set;} public DateTimeOffset RecordedAt{get;set;} public Guid? RecordedBy{get;set;}
    public string CorrelationId{get;set;}=""; public string IdempotencyKey{get;set;}=""; public Guid? SupersedesEntryId{get;set;}
}

internal sealed class PayrollInputDefinitionVersionRowConfiguration:IEntityTypeConfiguration<PayrollInputDefinitionVersionRow>
{
    public void Configure(EntityTypeBuilder<PayrollInputDefinitionVersionRow>b){b.ToTable("PayrollInputDefinitionVersions");b.HasKey(x=>x.Id);Uuid(b.Property(x=>x.Id));Uuid(b.Property(x=>x.DefinitionId));Uuid(b.Property(x=>x.CompanyId));b.Property(x=>x.Code).HasMaxLength(64).IsRequired();b.Property(x=>x.Name).HasMaxLength(256).IsRequired();b.Property(x=>x.Description).HasMaxLength(1024);b.Property(x=>x.MinDecimal).HasPrecision(28,8);b.Property(x=>x.MaxDecimal).HasPrecision(28,8);b.Property(x=>x.EffectiveFrom).HasColumnType("date");b.Property(x=>x.EffectiveTo).HasColumnType("date");b.HasIndex(x=>new{x.CompanyId,x.Code,x.Revision}).IsUnique();b.HasIndex(x=>new{x.CompanyId,x.DefinitionId,x.EffectiveFrom,x.EffectiveTo});}
    private static void Uuid(PropertyBuilder<Guid> p)=>PayComponentVersionRowConfiguration.Uuid(p);
}

internal sealed class PayrollInputLedgerEntryRowConfiguration:IEntityTypeConfiguration<PayrollInputLedgerEntryRow>
{
    public void Configure(EntityTypeBuilder<PayrollInputLedgerEntryRow>b){b.ToTable("PayrollInputLedgerEntries",t=>t.HasCheckConstraint("CK_PayrollInputLedger_TypedValue","(DataType = 0 AND DecimalValue IS NOT NULL AND IntegerValue IS NULL AND BooleanValue IS NULL AND DateValue IS NULL AND TextValue IS NULL) OR (DataType = 1 AND DecimalValue IS NULL AND IntegerValue IS NOT NULL AND BooleanValue IS NULL AND DateValue IS NULL AND TextValue IS NULL) OR (DataType = 2 AND DecimalValue IS NULL AND IntegerValue IS NULL AND BooleanValue IS NOT NULL AND DateValue IS NULL AND TextValue IS NULL) OR (DataType = 3 AND DecimalValue IS NULL AND IntegerValue IS NULL AND BooleanValue IS NULL AND DateValue IS NOT NULL AND TextValue IS NULL) OR (DataType = 4 AND DecimalValue IS NULL AND IntegerValue IS NULL AND BooleanValue IS NULL AND DateValue IS NULL AND TextValue IS NOT NULL)"));b.HasKey(x=>x.Id);Uuid(b.Property(x=>x.Id));Uuid(b.Property(x=>x.CompanyId));Uuid(b.Property(x=>x.PayrollSubjectId));Uuid(b.Property(x=>x.PayrollPeriodId));Uuid(b.Property(x=>x.InputDefinitionId));b.Property(x=>x.RecordedBy).HasConversion(x=>x.HasValue?x.Value.ToString("D"):null,x=>x==null?null:Guid.Parse(x)).HasColumnType("char(36)").HasMaxLength(36);b.Property(x=>x.SupersedesEntryId).HasConversion(x=>x.HasValue?x.Value.ToString("D"):null,x=>x==null?null:Guid.Parse(x)).HasColumnType("char(36)").HasMaxLength(36);b.Property(x=>x.BusinessDate).HasColumnType("date");b.Property(x=>x.EffectiveDate).HasColumnType("date");b.Property(x=>x.DateValue).HasColumnType("date");b.Property(x=>x.DecimalValue).HasPrecision(28,8);b.Property(x=>x.InputCode).HasMaxLength(64).IsRequired();b.Property(x=>x.TextValue).HasMaxLength(4000);b.Property(x=>x.SourceSystem).HasMaxLength(128);b.Property(x=>x.SourceReference).HasMaxLength(256);b.Property(x=>x.CorrelationId).HasMaxLength(128).IsRequired();b.Property(x=>x.IdempotencyKey).HasMaxLength(256).IsRequired();b.HasIndex(x=>new{x.CompanyId,x.IdempotencyKey}).IsUnique();b.HasIndex(x=>new{x.CompanyId,x.PayrollSubjectId,x.PayrollPeriodId,x.InputDefinitionId});b.HasIndex(x=>new{x.CompanyId,x.InputDefinitionId,x.InputDefinitionRevision});b.HasIndex(x=>new{x.CompanyId,x.SupersedesEntryId});b.HasIndex(x=>new{x.CompanyId,x.SourceReference});b.HasOne<PayrollInputLedgerEntryRow>().WithMany().HasForeignKey(x=>x.SupersedesEntryId).OnDelete(DeleteBehavior.Restrict);}
    private static void Uuid(PropertyBuilder<Guid> p)=>PayComponentVersionRowConfiguration.Uuid(p);
}
