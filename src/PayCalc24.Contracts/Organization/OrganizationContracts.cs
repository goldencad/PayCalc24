using PayCalc24.Contracts.Identity;
using PayCalc24.Contracts.Temporal;

namespace PayCalc24.Contracts.Organization;

public readonly record struct PayrollSubjectId(Guid Value) { public static PayrollSubjectId From(Guid value) => new(Required(value)); private static Guid Required(Guid value) => value == Guid.Empty ? throw new ArgumentException("Identifier cannot be empty.", nameof(value)) : value; }
public readonly record struct EmployeeDependentId(Guid Value) { public static EmployeeDependentId From(Guid value) => new(Required(value)); private static Guid Required(Guid value) => value == Guid.Empty ? throw new ArgumentException("Identifier cannot be empty.", nameof(value)) : value; }
public readonly record struct OrganizationUnitId(Guid Value) { public static OrganizationUnitId From(Guid value) => new(Required(value)); private static Guid Required(Guid value) => value == Guid.Empty ? throw new ArgumentException("Identifier cannot be empty.", nameof(value)) : value; }
public readonly record struct PositionId(Guid Value) { public static PositionId From(Guid value) => new(Required(value)); private static Guid Required(Guid value) => value == Guid.Empty ? throw new ArgumentException("Identifier cannot be empty.", nameof(value)) : value; }
public readonly record struct JobGradeId(Guid Value) { public static JobGradeId From(Guid value) => new(Required(value)); private static Guid Required(Guid value) => value == Guid.Empty ? throw new ArgumentException("Identifier cannot be empty.", nameof(value)) : value; }
public readonly record struct PayrollAssignmentId(Guid Value) { public static PayrollAssignmentId From(Guid value) => new(Required(value)); private static Guid Required(Guid value) => value == Guid.Empty ? throw new ArgumentException("Identifier cannot be empty.", nameof(value)) : value; }

public enum NationalIdType { CCCD, PASSPORT, OTHER }
public enum EmploymentStatus { ACTIVE, INACTIVE, SUSPENDED, TERMINATED }
public enum RecordStatus { ACTIVE, INACTIVE }
public enum EligibilityStatus { ELIGIBLE, INELIGIBLE, PENDING }
public enum OrganizationUnitType { DIVISION, CENTER, DEPARTMENT, TEAM, PROJECT, OTHER }

public sealed record PayrollSubjectDto(
    PayrollSubjectId Id, CompanyId CompanyId, string EmployeeCode, string FullName,
    NationalIdType? NationalIdType, string? MaskedNationalId, string? AttendanceCode,
    EmploymentStatus EmploymentStatus, string? SourceSystem, string? ExternalEmployeeId,
    EffectivePeriod EffectivePeriod, RecordStatus Status);

public sealed record EmployeeProviderRecord(
    string SourceSystem, string ExternalEmployeeId, string EmployeeCode, string FullName,
    NationalIdType? NationalIdType, string? NationalId, string? AttendanceCode,
    EmploymentStatus EmploymentStatus, string? OrganizationCode = null, string? PositionCode = null);

public sealed record EmployeeProviderQuery(CompanyId CompanyId, string? SearchText = null);
public sealed record EmployeeSyncResult(int Received, int Created, int Updated, IReadOnlyList<string> DiagnosticCodes);

public interface IEmployeeProvider
{
    IAsyncEnumerable<EmployeeProviderRecord> GetSubjectsAsync(EmployeeProviderQuery query, CancellationToken cancellationToken = default);
    ValueTask<EmployeeProviderRecord?> GetSubjectAsync(CompanyId companyId, string externalEmployeeId, CancellationToken cancellationToken = default);
    ValueTask<EmployeeSyncResult> SyncSubjectsAsync(CompanyId companyId, IReadOnlyCollection<EmployeeProviderRecord> records, CancellationToken cancellationToken = default);
}

public static class SensitiveIdentifier
{
    public static string? Mask(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var trimmed = value.Trim();
        return trimmed.Length <= 4 ? new string('*', trimmed.Length) : new string('*', trimmed.Length - 4) + trimmed[^4..];
    }
}
