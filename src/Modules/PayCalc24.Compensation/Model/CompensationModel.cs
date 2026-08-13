using PayCalc24.Contracts.Compensation;
using PayCalc24.Contracts.Diagnostics;
using PayCalc24.Contracts.Identity;
using PayCalc24.Contracts.Temporal;

namespace PayCalc24.Compensation.Model;

public sealed class CompensationValidationException(Diagnostic diagnostic) : Exception(diagnostic.Code)
{
    public Diagnostic Diagnostic { get; } = diagnostic;
}

public abstract class CatalogDefinition<TId, TContent> where TId : struct
{
    private readonly List<CatalogVersion<TContent>> versions = [];
    protected CatalogDefinition(TId id, CompanyId companyId) { Id = id; CompanyId = companyId; }
    protected CatalogDefinition() { }
    public TId Id { get; private set; }
    public CompanyId CompanyId { get; private set; }
    public IReadOnlyList<CatalogVersion<TContent>> Versions => versions;
    internal void Add(CatalogVersion<TContent> version) => versions.Add(version);
}

public sealed class PayComponent : CatalogDefinition<PayComponentId, PayComponentContent>
{
    public PayComponent(PayComponentId id, CompanyId companyId) : base(id, companyId) { }
    private PayComponent() { }
}

public sealed class CompensationScheme : CatalogDefinition<CompensationSchemeId, CompensationSchemeContent>
{
    public CompensationScheme(CompensationSchemeId id, CompanyId companyId) : base(id, companyId) { }
    private CompensationScheme() { }
}

public sealed class CatalogVersion<TContent>
{
    public CatalogVersion(Guid id, int versionNumber, EffectivePeriod effectivePeriod, TContent content)
    {
        if (id == Guid.Empty) throw new ArgumentException("Identifier cannot be empty.", nameof(id));
        Id = id; VersionNumber = versionNumber; EffectivePeriod = effectivePeriod; Content = content;
    }
    private CatalogVersion() { Content = default!; }
    public Guid Id { get; private set; }
    public int VersionNumber { get; private set; }
    public EffectivePeriod EffectivePeriod { get; private set; }
    public PublicationState PublicationState { get; private set; } = PublicationState.DRAFT;
    public TContent Content { get; private set; }
    internal void Change(EffectivePeriod period, TContent content) { EffectivePeriod = period; Content = content; }
    internal void Publish() => PublicationState = PublicationState.PUBLISHED;
    internal void Close(DateOnly effectiveTo) { EffectivePeriod = new(EffectivePeriod.EffectiveFrom, effectiveTo); PublicationState = PublicationState.SUPERSEDED; }
}
