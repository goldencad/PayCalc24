using PayCalc24.Contracts.Identity;
using PayCalc24.Contracts.Temporal;

namespace PayCalc24.Contracts.Compensation;

public readonly record struct PayComponentId(Guid Value) { public static PayComponentId From(Guid value) => value == Guid.Empty ? throw new ArgumentException("Identifier cannot be empty.", nameof(value)) : new(value); }
public readonly record struct CompensationSchemeId(Guid Value) { public static CompensationSchemeId From(Guid value) => value == Guid.Empty ? throw new ArgumentException("Identifier cannot be empty.", nameof(value)) : new(value); }
public readonly record struct CompensationSchemeComponentId(Guid Value) { public static CompensationSchemeComponentId From(Guid value) => value == Guid.Empty ? throw new ArgumentException("Identifier cannot be empty.", nameof(value)) : new(value); }

// These categories describe stable engine behaviour. Company business labels remain catalog data.
public enum PayComponentType { FIXED, VARIABLE, ALLOWANCE, OVERTIME, COMMISSION, BONUS, DEDUCTION, ADJUSTMENT, OTHER }
public enum CalculationMethod { FIXED, INPUT, FORMULA, LOOKUP, RULE, EXTERNAL, MANUAL }
public enum CatalogStatus { ACTIVE, INACTIVE }

public sealed record PayComponentContent(
    string Code, string Name, string? Description, PayComponentType ComponentType,
    CalculationMethod CalculationMethod, string? FormulaReference, string? FundSourceReference,
    bool IsProratable, bool IsAttendanceBased, bool IsPerformanceBased, bool IsTaxRelevant,
    bool IsInsuranceRelevant, bool IsGrossEligible, int? DisplayOrder, CatalogStatus Status);

public sealed record SchemeComponentContent(
    CompensationSchemeComponentId Id, PayComponentId PayComponentId, int Sequence, bool Required,
    CalculationMethod? OverrideCalculationMethod, string? OverrideFormulaReference, CatalogStatus Status);

public sealed record CompensationSchemeContent(
    string Code, string Name, string? Description, CatalogStatus Status,
    IReadOnlyList<SchemeComponentContent> Components);

public sealed record CatalogVersionDto<TContent>(
    int VersionNumber, EffectivePeriod EffectivePeriod, PublicationState PublicationState, TContent Content);

public sealed record PayComponentDto(
    PayComponentId Id, CompanyId CompanyId, CatalogVersionDto<PayComponentContent> Version);
public sealed record CompensationSchemeDto(
    CompensationSchemeId Id, CompanyId CompanyId, CatalogVersionDto<CompensationSchemeContent> Version);

public sealed record CatalogSearch(string? SearchText = null, CatalogStatus? Status = null);

public interface ICompensationCatalogService
{
    PayComponentDto CreatePayComponentDraft(CompanyId companyId, PayComponentId id, EffectivePeriod period, PayComponentContent content);
    PayComponentDto UpdatePayComponentDraft(CompanyId companyId, PayComponentId id, int versionNumber, EffectivePeriod period, PayComponentContent content);
    PayComponentDto PublishPayComponent(CompanyId companyId, PayComponentId id, int versionNumber);
    void ClosePayComponent(CompanyId companyId, PayComponentId id, int versionNumber, DateOnly effectiveTo);
    IReadOnlyList<PayComponentDto> ListPayComponents(CompanyId companyId, CatalogSearch search);
    PayComponentDto GetEffectivePayComponent(CompanyId companyId, string code, DateOnly businessDate);

    CompensationSchemeDto CreateSchemeDraft(CompanyId companyId, CompensationSchemeId id, EffectivePeriod period, CompensationSchemeContent content);
    CompensationSchemeDto UpdateSchemeDraft(CompanyId companyId, CompensationSchemeId id, int versionNumber, EffectivePeriod period, CompensationSchemeContent content);
    CompensationSchemeDto AddSchemeComponent(CompanyId companyId, CompensationSchemeId id, int versionNumber, SchemeComponentContent component);
    CompensationSchemeDto RemoveSchemeComponent(CompanyId companyId, CompensationSchemeId id, int versionNumber, PayComponentId componentId);
    CompensationSchemeDto ReorderSchemeComponent(CompanyId companyId, CompensationSchemeId id, int versionNumber, PayComponentId componentId, int sequence);
    CompensationSchemeDto PublishScheme(CompanyId companyId, CompensationSchemeId id, int versionNumber);
    void CloseScheme(CompanyId companyId, CompensationSchemeId id, int versionNumber, DateOnly effectiveTo);
    IReadOnlyList<CompensationSchemeDto> ListSchemes(CompanyId companyId, CatalogSearch search);
    CompensationSchemeDto ResolveEffectiveScheme(CompanyId companyId, string code, DateOnly businessDate);
    CompensationSchemeDto ResolveEffectiveScheme(CompanyId companyId, CompensationSchemeId id, DateOnly businessDate);
}
