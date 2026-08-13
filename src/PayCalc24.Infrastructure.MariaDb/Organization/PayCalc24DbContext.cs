using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PayCalc24.Contracts.Identity;
using PayCalc24.Contracts.Organization;
using PayCalc24.Organization.Model;

namespace PayCalc24.Infrastructure.MariaDb.Organization;

public sealed class PayCalc24DbContext(DbContextOptions<PayCalc24DbContext> options) : DbContext(options)
{
    public DbSet<PayrollSubject> PayrollSubjects => Set<PayrollSubject>();
    public DbSet<EmployeeDependent> EmployeeDependents => Set<EmployeeDependent>();
    public DbSet<OrganizationUnit> OrganizationUnits => Set<OrganizationUnit>();
    public DbSet<Position> Positions => Set<Position>();
    public DbSet<JobGrade> JobGrades => Set<JobGrade>();
    public DbSet<PayrollAssignment> PayrollAssignments => Set<PayrollAssignment>();
    protected override void OnModelCreating(ModelBuilder modelBuilder) => modelBuilder.ApplyConfigurationsFromAssembly(typeof(PayCalc24DbContext).Assembly);
}

internal static class Mapping
{
    public static PropertyBuilder<TId> Uuid<TId>(this PropertyBuilder<TId> property, Func<TId, Guid> unwrap, Func<Guid, TId> wrap)
        where TId : struct => property.HasConversion(value => unwrap(value).ToString("D"), value => wrap(Guid.Parse(value))).HasColumnType("char(36)").HasMaxLength(36);
    public static void Company<TEntity>(EntityTypeBuilder<TEntity> builder) where TEntity : class => builder.Property<CompanyId>("CompanyId").HasConversion(value => value.Value.ToString("D"), value => CompanyId.From(Guid.Parse(value))).HasColumnType("char(36)").HasMaxLength(36);
    public static void Period<TEntity>(EntityTypeBuilder<TEntity> builder, string propertyName, string prefix) where TEntity : class
    {
        builder.ComplexProperty<PayCalc24.Contracts.Temporal.EffectivePeriod>(propertyName, period =>
        {
            period.Property("EffectiveFrom").HasColumnName(prefix + "From").HasColumnType("date");
            period.Property("EffectiveTo").HasColumnName(prefix + "To").HasColumnType("date");
        });
    }
}

internal sealed class PayrollSubjectConfiguration : IEntityTypeConfiguration<PayrollSubject>
{
    public void Configure(EntityTypeBuilder<PayrollSubject> b) { b.ToTable("PayrollSubjects"); b.HasKey(x=>x.Id); b.Property(x=>x.Id).Uuid(x=>x.Value,PayrollSubjectId.From); Mapping.Company(b); b.Property(x=>x.EmployeeCode).HasMaxLength(64).IsRequired(); b.Property(x=>x.FullName).HasMaxLength(256).IsRequired(); b.Property(x=>x.NationalId).HasMaxLength(64); b.Property(x=>x.AttendanceCode).HasMaxLength(64); b.Property(x=>x.SourceSystem).HasMaxLength(64); b.Property(x=>x.ExternalEmployeeId).HasMaxLength(128); Mapping.Period(b,nameof(PayrollSubject.EffectivePeriod),"Effective"); b.HasIndex(x=>new{x.CompanyId,x.EmployeeCode}).IsUnique(); b.HasIndex(x=>new{x.CompanyId,x.AttendanceCode}); b.HasMany(x=>x.Dependents).WithOne().HasForeignKey(x=>new{x.CompanyId,x.PayrollSubjectId}).HasPrincipalKey(x=>new{x.CompanyId,x.Id}).OnDelete(DeleteBehavior.Restrict); }
}
internal sealed class EmployeeDependentConfiguration : IEntityTypeConfiguration<EmployeeDependent>
{
    public void Configure(EntityTypeBuilder<EmployeeDependent> b) { b.ToTable("EmployeeDependents"); b.HasKey(x=>x.Id); b.Property(x=>x.Id).Uuid(x=>x.Value,EmployeeDependentId.From); Mapping.Company(b); b.Property(x=>x.PayrollSubjectId).Uuid(x=>x.Value,PayrollSubjectId.From); b.Property(x=>x.FullName).HasMaxLength(256).IsRequired(); b.Property(x=>x.Relationship).HasMaxLength(64).IsRequired(); b.Property(x=>x.NationalId).HasMaxLength(64); Mapping.Period(b,nameof(EmployeeDependent.EffectivePeriod),"Effective"); b.Property(x=>x.EligibilityFrom).HasColumnType("date"); b.Property(x=>x.EligibilityTo).HasColumnType("date"); b.Property(x=>x.DeductionFrom).HasColumnType("date"); b.Property(x=>x.DeductionTo).HasColumnType("date"); b.Ignore(x=>x.EligibilityPeriod); b.Ignore(x=>x.DeductionPeriod); b.HasIndex(x=>new{x.CompanyId,x.PayrollSubjectId}); }
}
internal sealed class OrganizationUnitConfiguration : IEntityTypeConfiguration<OrganizationUnit>
{
    public void Configure(EntityTypeBuilder<OrganizationUnit> b) { b.ToTable("OrganizationUnits"); b.HasKey(x=>x.Id); b.Property(x=>x.Id).Uuid(x=>x.Value,OrganizationUnitId.From); Mapping.Company(b); b.Property(x=>x.ParentId).HasConversion(x=>x.HasValue?x.Value.Value.ToString("D"):null,x=>x==null?null:OrganizationUnitId.From(Guid.Parse(x))).HasColumnType("char(36)"); b.Property(x=>x.Code).HasMaxLength(64); b.Property(x=>x.Name).HasMaxLength(256); Mapping.Period(b,nameof(OrganizationUnit.EffectivePeriod),"Effective"); b.HasIndex(x=>new{x.CompanyId,x.Code}).IsUnique(); b.HasOne<OrganizationUnit>().WithMany().HasForeignKey(x=>new{x.CompanyId,x.ParentId}).HasPrincipalKey(x=>new{x.CompanyId,x.Id}).OnDelete(DeleteBehavior.Restrict); }
}
internal sealed class PositionConfiguration : IEntityTypeConfiguration<Position>
{ public void Configure(EntityTypeBuilder<Position>b){b.ToTable("Positions");b.HasKey(x=>x.Id);b.Property(x=>x.Id).Uuid(x=>x.Value,PositionId.From);Mapping.Company(b);b.Property(x=>x.Code).HasMaxLength(64);b.Property(x=>x.Name).HasMaxLength(256);Mapping.Period(b,nameof(Position.EffectivePeriod),"Effective");b.HasIndex(x=>new{x.CompanyId,x.Code}).IsUnique();} }
internal sealed class JobGradeConfiguration : IEntityTypeConfiguration<JobGrade>
{ public void Configure(EntityTypeBuilder<JobGrade>b){b.ToTable("JobGrades");b.HasKey(x=>x.Id);b.Property(x=>x.Id).Uuid(x=>x.Value,JobGradeId.From);Mapping.Company(b);b.Property(x=>x.Code).HasMaxLength(64);b.Property(x=>x.Name).HasMaxLength(256);Mapping.Period(b,nameof(JobGrade.EffectivePeriod),"Effective");b.HasIndex(x=>new{x.CompanyId,x.Code}).IsUnique();} }
internal sealed class PayrollAssignmentConfiguration : IEntityTypeConfiguration<PayrollAssignment>
{ public void Configure(EntityTypeBuilder<PayrollAssignment>b){b.ToTable("PayrollAssignments");b.HasKey(x=>x.Id);b.Property(x=>x.Id).Uuid(x=>x.Value,PayrollAssignmentId.From);Mapping.Company(b);b.Property(x=>x.PayrollSubjectId).Uuid(x=>x.Value,PayrollSubjectId.From);b.Property(x=>x.OrganizationUnitId).Uuid(x=>x.Value,OrganizationUnitId.From);b.Property(x=>x.PositionId).Uuid(x=>x.Value,PositionId.From);b.Property(x=>x.JobGradeId).HasConversion(x=>x.HasValue?x.Value.Value.ToString("D"):null,x=>x==null?null:JobGradeId.From(Guid.Parse(x))).HasColumnType("char(36)");Mapping.Period(b,nameof(PayrollAssignment.EffectivePeriod),"Effective");b.HasIndex(x=>new{x.CompanyId,x.PayrollSubjectId,x.IsPrimary});} }
