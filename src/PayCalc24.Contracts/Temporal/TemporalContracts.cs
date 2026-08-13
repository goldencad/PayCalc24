using PayCalc24.Contracts.Identity;

namespace PayCalc24.Contracts.Temporal;

public readonly record struct DefinitionId(Guid Value)
{
    public static DefinitionId From(Guid value) => value == Guid.Empty
        ? throw new ArgumentException("Definition identifier cannot be empty.", nameof(value))
        : new DefinitionId(value);
}

public readonly record struct DefinitionVersionId(Guid Value)
{
    public static DefinitionVersionId From(Guid value) => value == Guid.Empty
        ? throw new ArgumentException("Version identifier cannot be empty.", nameof(value))
        : new DefinitionVersionId(value);
}

/// <summary>
/// A half-open business-date interval [EffectiveFrom, EffectiveTo). EffectiveTo null means no upper bound.
/// Construction validation is performed by the application lifecycle service so failures use stable diagnostics.
/// </summary>
public readonly record struct EffectivePeriod(DateOnly EffectiveFrom, DateOnly? EffectiveTo)
{
    public bool Contains(DateOnly businessDate) =>
        businessDate >= EffectiveFrom &&
        (EffectiveTo is null || businessDate < EffectiveTo.Value);

    public bool Overlaps(EffectivePeriod other) =>
        (EffectiveTo is null || other.EffectiveFrom < EffectiveTo.Value) &&
        (other.EffectiveTo is null || EffectiveFrom < other.EffectiveTo.Value);
}

public enum PublicationState
{
    DRAFT,
    PUBLISHED,
    SUPERSEDED
}

public static class TemporalAuditActions
{
    public const string DefinitionCreated = "TEMPORAL.DEFINITION_CREATED";
    public const string DraftVersionCreated = "TEMPORAL.DRAFT_VERSION_CREATED";
    public const string VersionChanged = "TEMPORAL.VERSION_CHANGED";
    public const string VersionPublished = "TEMPORAL.VERSION_PUBLISHED";
    public const string VersionSuperseded = "TEMPORAL.VERSION_SUPERSEDED";
}

/// <summary>Persistence-facing snapshot. Infrastructure implementations must scope every operation by CompanyId.</summary>
public sealed record VersionedDefinitionSnapshot<TContent>(
    CompanyId CompanyId,
    DefinitionId DefinitionId,
    DefinitionVersionId VersionId,
    int VersionNumber,
    EffectivePeriod EffectivePeriod,
    PublicationState State,
    TContent Content);
