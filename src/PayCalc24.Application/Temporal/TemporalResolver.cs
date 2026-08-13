using PayCalc24.Application.Identity;
using PayCalc24.Contracts.Diagnostics;
using PayCalc24.Contracts.Identity;
using PayCalc24.Contracts.Temporal;

namespace PayCalc24.Application.Temporal;

public sealed class TemporalResolver(ICompanyScopeGuard companyScopeGuard)
{
    public DefinitionVersion<TContent> Resolve<TContent>(
        VersionedDefinition<TContent> definition,
        CompanyId requestedCompanyId,
        DateOnly businessDate)
    {
        companyScopeGuard.EnsureCurrent(requestedCompanyId);
        companyScopeGuard.EnsureCurrent(definition.CompanyId);

        var matches = definition.Versions
            .Where(version => version.State is PublicationState.PUBLISHED or PublicationState.SUPERSEDED)
            .Where(version => version.EffectivePeriod.Contains(businessDate))
            .ToArray();

        EnsureSingleMatch(definition.DefinitionId, businessDate, matches.Length);
        return matches[0];
    }

    public VersionedDefinitionSnapshot<TContent> Resolve<TContent>(
        CompanyId requestedCompanyId,
        DefinitionId definitionId,
        DateOnly businessDate,
        IReadOnlyCollection<VersionedDefinitionSnapshot<TContent>> persistedVersions)
    {
        companyScopeGuard.EnsureCurrent(requestedCompanyId);
        foreach (var version in persistedVersions.Where(version => version.DefinitionId == definitionId))
        {
            companyScopeGuard.EnsureCurrent(version.CompanyId);
        }

        var matches = persistedVersions
            .Where(version => version.CompanyId == requestedCompanyId)
            .Where(version => version.DefinitionId == definitionId)
            .Where(version => version.State is PublicationState.PUBLISHED or PublicationState.SUPERSEDED)
            .Where(version => version.EffectivePeriod.Contains(businessDate))
            .ToArray();

        EnsureSingleMatch(definitionId, businessDate, matches.Length);
        return matches[0];
    }

    private static void EnsureSingleMatch(DefinitionId definitionId, DateOnly businessDate, int matchCount)
    {
        if (matchCount == 1)
        {
            return;
        }

        var arguments = new Dictionary<string, object?>
        {
            ["definitionId"] = definitionId.Value,
            ["businessDate"] = businessDate,
            ["matchCount"] = matchCount
        };

        throw new TemporalValidationException(new Diagnostic(
            matchCount == 0
                ? DiagnosticCodes.EffectiveVersionNotFound
                : DiagnosticCodes.EffectiveVersionAmbiguous,
            DiagnosticSeverity.Error,
            arguments));
    }
}
