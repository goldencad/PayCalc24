using PayCalc24.Application.Identity;
using PayCalc24.Contracts.Diagnostics;
using PayCalc24.Contracts.Identity;
using PayCalc24.Contracts.Operations;
using PayCalc24.Contracts.Temporal;

namespace PayCalc24.Application.Temporal;

public sealed class VersionPublicationService(
    ICompanyScopeGuard companyScopeGuard,
    ICurrentUser currentUser,
    ICorrelationContext correlationContext,
    IAuditWriter auditWriter,
    TimeProvider timeProvider)
{
    public async ValueTask<VersionedDefinition<TContent>> CreateDefinitionAsync<TContent>(
        CompanyId requestedCompanyId,
        DefinitionId definitionId,
        CancellationToken cancellationToken = default)
    {
        companyScopeGuard.EnsureCurrent(requestedCompanyId);
        var definition = new VersionedDefinition<TContent>(requestedCompanyId, definitionId);
        await AuditAsync(definition, TemporalAuditActions.DefinitionCreated, null, cancellationToken);
        return definition;
    }

    public async ValueTask<DefinitionVersion<TContent>> CreateDraftAsync<TContent>(
        VersionedDefinition<TContent> definition,
        CompanyId requestedCompanyId,
        DefinitionVersionId versionId,
        int versionNumber,
        EffectivePeriod effectivePeriod,
        TContent content,
        CancellationToken cancellationToken = default)
    {
        EnsureCompany(definition, requestedCompanyId);
        ValidatePeriod(effectivePeriod);
        if (versionNumber <= 0 || definition.Versions.Any(v => v.VersionNumber == versionNumber || v.VersionId == versionId))
        {
            Throw(DiagnosticCodes.InvalidVersionNumber, new()
            {
                ["definitionId"] = definition.DefinitionId.Value,
                ["versionId"] = versionId.Value,
                ["versionNumber"] = versionNumber
            });
        }

        var version = new DefinitionVersion<TContent>(versionId, versionNumber, effectivePeriod, content);
        definition.Add(version);
        await AuditAsync(definition, TemporalAuditActions.DraftVersionCreated, version, cancellationToken);
        return version;
    }

    public async ValueTask ChangeDraftAsync<TContent>(
        VersionedDefinition<TContent> definition,
        CompanyId requestedCompanyId,
        DefinitionVersion<TContent> version,
        EffectivePeriod effectivePeriod,
        TContent content,
        CancellationToken cancellationToken = default)
    {
        EnsureCompany(definition, requestedCompanyId);
        EnsureMember(definition, version);
        if (version.State != PublicationState.DRAFT)
        {
            Throw(DiagnosticCodes.PublishedVersionImmutable, VersionArguments(definition, version));
        }

        ValidatePeriod(effectivePeriod);
        version.Change(effectivePeriod, content);
        await AuditAsync(definition, TemporalAuditActions.VersionChanged, version, cancellationToken);
    }

    public async ValueTask PublishAsync<TContent>(
        VersionedDefinition<TContent> definition,
        CompanyId requestedCompanyId,
        DefinitionVersion<TContent> version,
        CancellationToken cancellationToken = default)
    {
        EnsureCompany(definition, requestedCompanyId);
        EnsureMember(definition, version);
        ValidatePeriod(version.EffectivePeriod);
        if (version.State != PublicationState.DRAFT)
        {
            Throw(DiagnosticCodes.InvalidPublicationState, VersionArguments(definition, version));
        }

        var overlap = definition.Versions.FirstOrDefault(candidate =>
            candidate != version &&
            candidate.State is PublicationState.PUBLISHED or PublicationState.SUPERSEDED &&
            candidate.EffectivePeriod.Overlaps(version.EffectivePeriod));
        if (overlap is not null)
        {
            var arguments = VersionArguments(definition, version);
            arguments["overlappingVersionId"] = overlap.VersionId.Value;
            Throw(DiagnosticCodes.PublishedVersionOverlap, arguments);
        }

        version.Publish();
        await AuditAsync(definition, TemporalAuditActions.VersionPublished, version, cancellationToken);
    }

    public async ValueTask SupersedeAsync<TContent>(
        VersionedDefinition<TContent> definition,
        CompanyId requestedCompanyId,
        DefinitionVersion<TContent> version,
        DateOnly effectiveTo,
        CancellationToken cancellationToken = default)
    {
        EnsureCompany(definition, requestedCompanyId);
        EnsureMember(definition, version);
        if (version.State != PublicationState.PUBLISHED)
        {
            Throw(DiagnosticCodes.InvalidPublicationState, VersionArguments(definition, version));
        }

        ValidatePeriod(new EffectivePeriod(version.EffectivePeriod.EffectiveFrom, effectiveTo));
        if (version.EffectivePeriod.EffectiveTo is not null && effectiveTo > version.EffectivePeriod.EffectiveTo.Value)
        {
            Throw(DiagnosticCodes.InvalidEffectiveRange, new()
            {
                ["effectiveFrom"] = version.EffectivePeriod.EffectiveFrom,
                ["effectiveTo"] = effectiveTo,
                ["publishedEffectiveTo"] = version.EffectivePeriod.EffectiveTo
            });
        }

        version.Supersede(effectiveTo);
        await AuditAsync(definition, TemporalAuditActions.VersionSuperseded, version, cancellationToken);
    }

    private void EnsureCompany<TContent>(VersionedDefinition<TContent> definition, CompanyId requestedCompanyId)
    {
        companyScopeGuard.EnsureCurrent(requestedCompanyId);
        companyScopeGuard.EnsureCurrent(definition.CompanyId);
    }

    private static void EnsureMember<TContent>(
        VersionedDefinition<TContent> definition,
        DefinitionVersion<TContent> version)
    {
        if (!definition.Versions.Contains(version))
        {
            Throw(DiagnosticCodes.InvalidVersionNumber, VersionArguments(definition, version));
        }
    }

    private static void ValidatePeriod(EffectivePeriod period)
    {
        if (period.EffectiveTo is not null && period.EffectiveFrom >= period.EffectiveTo.Value)
        {
            Throw(DiagnosticCodes.InvalidEffectiveRange, new()
            {
                ["effectiveFrom"] = period.EffectiveFrom,
                ["effectiveTo"] = period.EffectiveTo
            });
        }
    }

    private async ValueTask AuditAsync<TContent>(
        VersionedDefinition<TContent> definition,
        string action,
        DefinitionVersion<TContent>? version,
        CancellationToken cancellationToken)
    {
        var details = new Dictionary<string, object?> { ["definitionId"] = definition.DefinitionId.Value };
        if (version is not null)
        {
            details["versionId"] = version.VersionId.Value;
            details["versionNumber"] = version.VersionNumber;
            details["publicationState"] = version.State.ToString();
        }

        await auditWriter.WriteAsync(new AuditEntry(
            definition.CompanyId,
            currentUser.UserId,
            action,
            "VersionedDefinition",
            definition.DefinitionId.Value.ToString("D"),
            correlationContext.CorrelationId,
            timeProvider.GetUtcNow(),
            details), cancellationToken);
    }

    private static Dictionary<string, object?> VersionArguments<TContent>(
        VersionedDefinition<TContent> definition,
        DefinitionVersion<TContent> version) => new()
    {
        ["definitionId"] = definition.DefinitionId.Value,
        ["versionId"] = version.VersionId.Value,
        ["versionNumber"] = version.VersionNumber,
        ["publicationState"] = version.State.ToString()
    };

    private static void Throw(string code, Dictionary<string, object?> arguments) =>
        throw new TemporalValidationException(new Diagnostic(code, DiagnosticSeverity.Error, arguments));
}
