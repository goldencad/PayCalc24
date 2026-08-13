using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PayCalc24.Contracts.Compensation;
using PayCalc24.Contracts.Temporal;

namespace PayCalc24.Infrastructure.MariaDb.Compensation;

// Persistence rows are deliberately internal; APIs expose Contracts DTOs only.
internal sealed class PayComponentVersionRow
{
    public Guid Id { get; set; } public Guid DefinitionId { get; set; } public Guid CompanyId { get; set; }
    public int VersionNumber { get; set; } public string Code { get; set; }=""; public string Name { get; set; }=""; public string? Description { get; set; }
    public PayComponentType ComponentType { get; set; } public CalculationMethod CalculationMethod { get; set; }
    public string? FormulaReference { get; set; } public string? FundSourceReference { get; set; }
    public bool IsProratable { get; set; } public bool IsAttendanceBased { get; set; } public bool IsPerformanceBased { get; set; }
    public bool IsTaxRelevant { get; set; } public bool IsInsuranceRelevant { get; set; } public bool IsGrossEligible { get; set; }
    public int? DisplayOrder { get; set; } public DateOnly EffectiveFrom { get; set; } public DateOnly? EffectiveTo { get; set; }
    public CatalogStatus Status { get; set; } public PublicationState PublicationState { get; set; }
}
internal sealed class CompensationSchemeVersionRow
{
    public Guid Id { get; set; } public Guid DefinitionId { get; set; } public Guid CompanyId { get; set; }
    public int VersionNumber { get; set; } public string Code { get; set; }=""; public string Name { get; set; }=""; public string? Description { get; set; }
    public DateOnly EffectiveFrom { get; set; } public DateOnly? EffectiveTo { get; set; } public CatalogStatus Status { get; set; } public PublicationState PublicationState { get; set; }
}
internal sealed class CompensationSchemeComponentRow
{
    public Guid Id { get; set; } public Guid CompanyId { get; set; } public Guid SchemeVersionId { get; set; } public Guid PayComponentDefinitionId { get; set; }
    public int Sequence { get; set; } public bool Required { get; set; } public CalculationMethod? OverrideCalculationMethod { get; set; }
    public string? OverrideFormulaReference { get; set; } public CatalogStatus Status { get; set; }
}
internal sealed class PayComponentVersionRowConfiguration:IEntityTypeConfiguration<PayComponentVersionRow>
{ public void Configure(EntityTypeBuilder<PayComponentVersionRow>b){b.ToTable("PayComponentVersions");b.HasKey(x=>x.Id);Uuid(b.Property(x=>x.Id));Uuid(b.Property(x=>x.DefinitionId));Uuid(b.Property(x=>x.CompanyId));b.Property(x=>x.Code).HasMaxLength(64).IsRequired();b.Property(x=>x.Name).HasMaxLength(256).IsRequired();b.Property(x=>x.Description).HasMaxLength(1024);b.Property(x=>x.FormulaReference).HasMaxLength(128);b.Property(x=>x.FundSourceReference).HasMaxLength(128);b.Property(x=>x.EffectiveFrom).HasColumnType("date");b.Property(x=>x.EffectiveTo).HasColumnType("date");b.HasIndex(x=>new{x.CompanyId,x.Code,x.VersionNumber}).IsUnique();b.HasIndex(x=>new{x.CompanyId,x.Code,x.EffectiveFrom,x.EffectiveTo});}
  internal static void Uuid(PropertyBuilder<Guid>p)=>p.HasConversion(x=>x.ToString("D"),x=>Guid.Parse(x)).HasColumnType("char(36)").HasMaxLength(36); }
internal sealed class CompensationSchemeVersionRowConfiguration:IEntityTypeConfiguration<CompensationSchemeVersionRow>
{ public void Configure(EntityTypeBuilder<CompensationSchemeVersionRow>b){b.ToTable("CompensationSchemeVersions");b.HasKey(x=>x.Id);PayComponentVersionRowConfiguration.Uuid(b.Property(x=>x.Id));PayComponentVersionRowConfiguration.Uuid(b.Property(x=>x.DefinitionId));PayComponentVersionRowConfiguration.Uuid(b.Property(x=>x.CompanyId));b.Property(x=>x.Code).HasMaxLength(64).IsRequired();b.Property(x=>x.Name).HasMaxLength(256).IsRequired();b.Property(x=>x.Description).HasMaxLength(1024);b.Property(x=>x.EffectiveFrom).HasColumnType("date");b.Property(x=>x.EffectiveTo).HasColumnType("date");b.HasIndex(x=>new{x.CompanyId,x.Code,x.VersionNumber}).IsUnique();b.HasIndex(x=>new{x.CompanyId,x.Code,x.EffectiveFrom,x.EffectiveTo});} }
internal sealed class CompensationSchemeComponentRowConfiguration:IEntityTypeConfiguration<CompensationSchemeComponentRow>
{ public void Configure(EntityTypeBuilder<CompensationSchemeComponentRow>b){b.ToTable("CompensationSchemeComponents");b.HasKey(x=>x.Id);PayComponentVersionRowConfiguration.Uuid(b.Property(x=>x.Id));PayComponentVersionRowConfiguration.Uuid(b.Property(x=>x.CompanyId));PayComponentVersionRowConfiguration.Uuid(b.Property(x=>x.SchemeVersionId));PayComponentVersionRowConfiguration.Uuid(b.Property(x=>x.PayComponentDefinitionId));b.Property(x=>x.OverrideFormulaReference).HasMaxLength(128);b.HasIndex(x=>new{x.CompanyId,x.SchemeVersionId,x.Sequence}).IsUnique();b.HasIndex(x=>new{x.CompanyId,x.SchemeVersionId,x.PayComponentDefinitionId}).IsUnique();b.HasOne<CompensationSchemeVersionRow>().WithMany().HasForeignKey(x=>x.SchemeVersionId).OnDelete(DeleteBehavior.Restrict);} }
