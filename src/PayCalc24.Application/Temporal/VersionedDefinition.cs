using PayCalc24.Contracts.Identity;
using PayCalc24.Contracts.Temporal;

namespace PayCalc24.Application.Temporal;

/// <summary>
/// Reusable aggregate for one stable logical definition and its version history.
/// TContent should be an immutable value/record; published content can only be replaced by creating a new version.
/// </summary>
public sealed class VersionedDefinition<TContent>
{
    private readonly List<DefinitionVersion<TContent>> _versions = [];

    public VersionedDefinition(CompanyId companyId, DefinitionId definitionId)
    {
        CompanyId = companyId;
        DefinitionId = definitionId;
    }

    public CompanyId CompanyId { get; }
    public DefinitionId DefinitionId { get; }
    public IReadOnlyList<DefinitionVersion<TContent>> Versions => _versions;

    internal void Add(DefinitionVersion<TContent> version) => _versions.Add(version);
}

public sealed class DefinitionVersion<TContent>
{
    internal DefinitionVersion(
        DefinitionVersionId versionId,
        int versionNumber,
        EffectivePeriod effectivePeriod,
        TContent content)
    {
        VersionId = versionId;
        VersionNumber = versionNumber;
        EffectivePeriod = effectivePeriod;
        Content = content;
    }

    public DefinitionVersionId VersionId { get; }
    public int VersionNumber { get; }
    public EffectivePeriod EffectivePeriod { get; private set; }
    public PublicationState State { get; private set; } = PublicationState.DRAFT;
    public TContent Content { get; private set; }

    internal void Change(EffectivePeriod effectivePeriod, TContent content)
    {
        EffectivePeriod = effectivePeriod;
        Content = content;
    }

    internal void Publish() => State = PublicationState.PUBLISHED;
    internal void Supersede(DateOnly effectiveTo)
    {
        EffectivePeriod = new EffectivePeriod(EffectivePeriod.EffectiveFrom, effectiveTo);
        State = PublicationState.SUPERSEDED;
    }
}
