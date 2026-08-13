using PayCalc24.Application.Identity;
using PayCalc24.Application.Temporal;
using PayCalc24.Contracts.Diagnostics;
using PayCalc24.Contracts.Identity;
using PayCalc24.Contracts.Localization;
using PayCalc24.Contracts.Operations;
using PayCalc24.Contracts.Temporal;

namespace PayCalc24.ApplicationTests;

public sealed class Task03TemporalFrameworkTests
{
    private static readonly DateOnly Jan1 = new(2026, 1, 1);
    private static readonly DateOnly Jul1 = new(2026, 7, 1);

    [Fact]
    public async Task AcceptsBoundedAndOpenEndedEffectiveRanges()
    {
        var fixture = new Fixture();
        var definition = await fixture.CreateAsync();

        var bounded = await fixture.Service.CreateDraftAsync(definition, fixture.CompanyId, VersionId(), 1,
            new EffectivePeriod(Jan1, Jul1), "v1");
        var open = await fixture.Service.CreateDraftAsync(definition, fixture.CompanyId, VersionId(), 2,
            new EffectivePeriod(Jul1, null), "v2");

        Assert.Equal(Jul1, bounded.EffectivePeriod.EffectiveTo);
        Assert.Null(open.EffectivePeriod.EffectiveTo);
    }

    [Theory]
    [InlineData(2026, 7, 1)]
    [InlineData(2026, 6, 30)]
    public async Task RejectsEmptyOrReversedEffectiveRanges(int year, int month, int day)
    {
        var fixture = new Fixture();
        var definition = await fixture.CreateAsync();
        var to = new DateOnly(year, month, day);

        var exception = await Assert.ThrowsAsync<TemporalValidationException>(async () =>
            await fixture.Service.CreateDraftAsync(definition, fixture.CompanyId, VersionId(), 1,
                new EffectivePeriod(Jul1, to), "invalid"));

        Assert.Equal(DiagnosticCodes.InvalidEffectiveRange, exception.Diagnostic.Code);
    }

    [Fact]
    public async Task ResolvesBeforeAndAfterTransitionWithHalfOpenBoundaries()
    {
        var fixture = new Fixture();
        var definition = await fixture.CreateAsync();
        var first = await fixture.DraftAsync(definition, 1, new(Jan1, Jul1), "v1");
        var second = await fixture.DraftAsync(definition, 2, new(Jul1, null), "v2");
        await fixture.Service.PublishAsync(definition, fixture.CompanyId, first);
        await fixture.Service.PublishAsync(definition, fixture.CompanyId, second);

        Assert.Same(first, fixture.Resolver.Resolve(definition, fixture.CompanyId, Jan1));
        Assert.Same(first, fixture.Resolver.Resolve(definition, fixture.CompanyId, Jul1.AddDays(-1)));
        Assert.Same(second, fixture.Resolver.Resolve(definition, fixture.CompanyId, Jul1));
    }

    [Fact]
    public async Task NoEffectiveVersionFailsExplicitly()
    {
        var fixture = new Fixture();
        var definition = await fixture.CreateAsync();

        var exception = Assert.Throws<TemporalValidationException>(() =>
            fixture.Resolver.Resolve(definition, fixture.CompanyId, Jan1));

        Assert.Equal(DiagnosticCodes.EffectiveVersionNotFound, exception.Diagnostic.Code);
    }

    [Fact]
    public void PersistedOverlapFailsAsAmbiguousRatherThanChoosingAVersion()
    {
        var fixture = new Fixture();
        var definitionId = DefinitionId.From(Guid.NewGuid());
        VersionedDefinitionSnapshot<string>[] corrupt =
        [
            new(fixture.CompanyId, definitionId, VersionId(), 1, new(Jan1, null), PublicationState.PUBLISHED, "v1"),
            new(fixture.CompanyId, definitionId, VersionId(), 2, new(Jul1, null), PublicationState.PUBLISHED, "v2")
        ];

        var exception = Assert.Throws<TemporalValidationException>(() =>
            fixture.Resolver.Resolve(fixture.CompanyId, definitionId, Jul1, corrupt));

        Assert.Equal(DiagnosticCodes.EffectiveVersionAmbiguous, exception.Diagnostic.Code);
        Assert.Equal(2, exception.Diagnostic.Arguments["matchCount"]);
    }

    [Fact]
    public async Task DraftCanChangeButPublishedVersionCannotBeSilentlyMutated()
    {
        var fixture = new Fixture();
        var definition = await fixture.CreateAsync();
        var version = await fixture.DraftAsync(definition, 1, new(Jan1, null), "draft");
        await fixture.Service.ChangeDraftAsync(definition, fixture.CompanyId, version, new(Jan1, null), "changed");
        Assert.Equal("changed", version.Content);
        await fixture.Service.PublishAsync(definition, fixture.CompanyId, version);

        var exception = await Assert.ThrowsAsync<TemporalValidationException>(async () =>
            await fixture.Service.ChangeDraftAsync(definition, fixture.CompanyId, version, new(Jan1, null), "mutated"));

        Assert.Equal(DiagnosticCodes.PublishedVersionImmutable, exception.Diagnostic.Code);
        Assert.Equal("changed", version.Content);
    }

    [Fact]
    public async Task PublishingValidAdjacentVersionsSucceedsAndOverlapIsRejected()
    {
        var fixture = new Fixture();
        var definition = await fixture.CreateAsync();
        var first = await fixture.DraftAsync(definition, 1, new(Jan1, Jul1), "v1");
        var second = await fixture.DraftAsync(definition, 2, new(Jul1, null), "v2");
        var overlap = await fixture.DraftAsync(definition, 3, new(Jul1.AddDays(-1), null), "bad");
        await fixture.Service.PublishAsync(definition, fixture.CompanyId, first);
        await fixture.Service.PublishAsync(definition, fixture.CompanyId, second);

        var exception = await Assert.ThrowsAsync<TemporalValidationException>(async () =>
            await fixture.Service.PublishAsync(definition, fixture.CompanyId, overlap));

        Assert.Equal(PublicationState.PUBLISHED, first.State);
        Assert.Equal(PublicationState.PUBLISHED, second.State);
        Assert.Equal(DiagnosticCodes.PublishedVersionOverlap, exception.Diagnostic.Code);
    }

    [Fact]
    public async Task SupersededVersionRemainsHistoricallyIdentifiableAndResolvable()
    {
        var fixture = new Fixture();
        var definition = await fixture.CreateAsync();
        var version = await fixture.DraftAsync(definition, 1, new(Jan1, null), "v1");
        await fixture.Service.PublishAsync(definition, fixture.CompanyId, version);
        await fixture.Service.SupersedeAsync(definition, fixture.CompanyId, version, Jul1);
        var successor = await fixture.DraftAsync(definition, 2, new(Jul1, null), "v2");
        await fixture.Service.PublishAsync(definition, fixture.CompanyId, successor);

        Assert.Contains(version, definition.Versions);
        Assert.Equal(PublicationState.SUPERSEDED, version.State);
        Assert.Same(version, fixture.Resolver.Resolve(definition, fixture.CompanyId, Jan1));
        Assert.Same(successor, fixture.Resolver.Resolve(definition, fixture.CompanyId, Jul1));
    }

    [Fact]
    public async Task CrossCompanyCannotResolveOrMutateDefinition()
    {
        var fixture = new Fixture();
        var other = CompanyId.From(Guid.NewGuid());
        var definition = await fixture.CreateAsync();
        var version = await fixture.DraftAsync(definition, 1, new(Jan1, null), "v1");

        Assert.Throws<CompanyScopeViolationException>(() => fixture.Resolver.Resolve(definition, other, Jan1));
        await Assert.ThrowsAsync<CompanyScopeViolationException>(async () =>
            await fixture.Service.ChangeDraftAsync(definition, other, version, new(Jan1, null), "bad"));
    }

    [Theory]
    [InlineData(SupportedCultures.English)]
    [InlineData(SupportedCultures.Vietnamese)]
    public async Task TemporalBehaviorIsIndependentOfPreferredCulture(string preferredCulture)
    {
        var fixture = new Fixture();
        var definition = await fixture.CreateAsync();
        var versionId = VersionId();
        var version = await fixture.Service.CreateDraftAsync(definition, fixture.CompanyId, versionId, 1,
            new EffectivePeriod(Jan1, null), preferredCulture);
        await fixture.Service.PublishAsync(definition, fixture.CompanyId, version);

        var resolved = fixture.Resolver.Resolve(definition, fixture.CompanyId, Jan1);

        Assert.Equal(versionId, resolved.VersionId);
        Assert.Equal(PublicationState.PUBLISHED, resolved.State);
        Assert.Equal(Jan1, resolved.EffectivePeriod.EffectiveFrom);
    }

    [Fact]
    public async Task LifecycleActionsUseTask02AuditBoundary()
    {
        var fixture = new Fixture();
        var definition = await fixture.CreateAsync();
        var version = await fixture.DraftAsync(definition, 1, new(Jan1, null), "v1");
        await fixture.Service.PublishAsync(definition, fixture.CompanyId, version);
        await fixture.Service.SupersedeAsync(definition, fixture.CompanyId, version, Jul1);

        Assert.Equal(
            [TemporalAuditActions.DefinitionCreated, TemporalAuditActions.DraftVersionCreated,
             TemporalAuditActions.VersionPublished, TemporalAuditActions.VersionSuperseded],
            fixture.Audit.Entries.Select(entry => entry.ActionCode));
        Assert.All(fixture.Audit.Entries, entry => Assert.Equal(fixture.CompanyId, entry.CompanyId));
    }

    private static DefinitionVersionId VersionId() => DefinitionVersionId.From(Guid.NewGuid());

    private sealed class Fixture
    {
        public CompanyId CompanyId { get; } = CompanyId.From(Guid.NewGuid());
        public RecordingAuditWriter Audit { get; } = new();
        public VersionPublicationService Service { get; }
        public TemporalResolver Resolver { get; }

        public Fixture()
        {
            var guard = new CompanyScopeGuard(new CompanyContext(CompanyId));
            Service = new VersionPublicationService(guard, new CurrentUser(UserId.From(Guid.NewGuid())),
                new CorrelationContext(), Audit, TimeProvider.System);
            Resolver = new TemporalResolver(guard);
        }

        public ValueTask<VersionedDefinition<string>> CreateAsync() =>
            Service.CreateDefinitionAsync<string>(CompanyId, DefinitionId.From(Guid.NewGuid()));

        public ValueTask<DefinitionVersion<string>> DraftAsync(
            VersionedDefinition<string> definition, int number, EffectivePeriod period, string content) =>
            Service.CreateDraftAsync(definition, CompanyId, VersionId(), number, period, content);
    }

    private sealed record CompanyContext(CompanyId CompanyId) : ICompanyContext;
    private sealed record CurrentUser(UserId UserId) : ICurrentUser
    {
        public bool HasPermission(string permissionCode) => false;
    }
    private sealed class CorrelationContext : ICorrelationContext
    {
        public string CorrelationId => "task-03-tests";
        public string? IdempotencyKey => null;
    }
    private sealed class RecordingAuditWriter : IAuditWriter
    {
        public List<AuditEntry> Entries { get; } = [];
        public ValueTask WriteAsync(AuditEntry entry, CancellationToken cancellationToken = default)
        {
            Entries.Add(entry);
            return ValueTask.CompletedTask;
        }
    }
}
