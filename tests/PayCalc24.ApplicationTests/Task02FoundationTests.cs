using PayCalc24.Application.Identity;
using PayCalc24.Application.Localization;
using PayCalc24.Application.Presentation;
using PayCalc24.Contracts.Diagnostics;
using PayCalc24.Contracts.Identity;
using PayCalc24.Contracts.Localization;
using PayCalc24.Contracts.Presentation;

namespace PayCalc24.ApplicationTests;

public sealed class Task02FoundationTests
{
    [Fact]
    public void CompanyGuardRejectsCrossCompanyAccessWithStableDiagnostic()
    {
        var current = CompanyId.From(Guid.NewGuid());
        var other = CompanyId.From(Guid.NewGuid());
        var guard = new CompanyScopeGuard(new CompanyContext(current));

        var exception = Assert.Throws<CompanyScopeViolationException>(() => guard.EnsureCurrent(other));

        Assert.Equal(DiagnosticCodes.CompanyScopeMismatch, exception.Diagnostic.Code);
        Assert.Equal(other.Value, exception.Diagnostic.Arguments["requestedCompanyId"]);
        Assert.Equal(current.Value, exception.Diagnostic.Arguments["currentCompanyId"]);
    }

    [Fact]
    public void CompanyGuardAllowsCurrentCompany()
    {
        var current = CompanyId.From(Guid.NewGuid());
        new CompanyScopeGuard(new CompanyContext(current)).EnsureCurrent(current);
    }

    [Fact]
    public void LocalizationFallbackIsDeterministic()
    {
        var resources = new DictionaryResources(new Dictionary<(string, string), string>
        {
            [(SupportedCultures.English, "Common.Save")] = "Save"
        });
        var service = new LocalizationService(resources);

        var result = service.Resolve("Common.Save", SupportedCultures.Vietnamese);

        Assert.Equal("Save", result.Value);
        Assert.Equal(SupportedCultures.English, result.ResolvedCulture);
        Assert.Null(result.Diagnostic);
    }

    [Fact]
    public void MissingLocalizationReturnsCanonicalKeyAndDiagnostic()
    {
        var result = new LocalizationService(new DictionaryResources(
                new Dictionary<(string Culture, string Key), string>()))
            .Resolve("PayrollPeriod.Calculate", SupportedCultures.Vietnamese);

        Assert.Equal("PayrollPeriod.Calculate", result.Value);
        Assert.Equal(DiagnosticCodes.LocalizationResourceMissing, result.Diagnostic?.Code);
    }

    [Fact]
    public void UnsupportedCultureUsesDefaultAndEmitsStableDiagnostic()
    {
        var service = new LocalizationService(new DictionaryResources(new Dictionary<(string, string), string>
        {
            [(SupportedCultures.English, "Common.Save")] = "Save"
        }));

        var result = service.Resolve("Common.Save", "fr-FR");

        Assert.Equal("Save", result.Value);
        Assert.Equal(SupportedCultures.English, result.ResolvedCulture);
        Assert.Equal(DiagnosticCodes.UnsupportedCulture, result.Diagnostic?.Code);
    }

    [Fact]
    public void LocalizationNeverChangesCanonicalCodesOrDiagnosticArguments()
    {
        const string canonicalCode = "INPUT.GROSS_PAY";
        var service = new LocalizationService(new DictionaryResources(new Dictionary<(string, string), string>
        {
            [(SupportedCultures.Vietnamese, "Input.DisplayName")] = "Tổng thu nhập"
        }));

        var result = service.Resolve(canonicalCode, SupportedCultures.Vietnamese);

        Assert.Equal(canonicalCode, result.Value);
        Assert.Equal(canonicalCode, result.Diagnostic?.Arguments["resourceKey"]);
    }

    [Fact]
    public async Task PreferenceChangesOnlyWriteUserPresentationState()
    {
        var userId = UserId.From(Guid.NewGuid());
        var store = new RecordingPreferenceStore();
        var service = new UserPresentationPreferenceService(new CurrentUser(userId), store);
        var updated = new UserPresentationPreferences(SupportedCultures.Vietnamese, ThemeMode.DARK);

        await service.UpdateAsync(updated);

        Assert.Equal(userId, store.SavedUserId);
        Assert.Equal(updated, store.SavedPreferences);
        Assert.Equal(1, store.WriteCount);
        Assert.DoesNotContain(typeof(UserPresentationPreferences).GetProperties(), p => p.Name.Contains("Company", StringComparison.Ordinal));
    }

    [Fact]
    public async Task PreferenceDefaultsAreStable()
    {
        var service = new UserPresentationPreferenceService(
            new CurrentUser(UserId.From(Guid.NewGuid())),
            new RecordingPreferenceStore());

        var result = await service.GetAsync();

        Assert.Equal(SupportedCultures.English, result.PreferredCulture);
        Assert.Equal(ThemeMode.SYSTEM, result.ThemeMode);
    }

    [Fact]
    public async Task InvalidPreferenceReturnsLanguageNeutralDiagnostic()
    {
        var service = new UserPresentationPreferenceService(
            new CurrentUser(UserId.From(Guid.NewGuid())),
            new RecordingPreferenceStore());

        var exception = await Assert.ThrowsAsync<PresentationPreferenceValidationException>(async () =>
            await service.UpdateAsync(new UserPresentationPreferences("fr-FR", ThemeMode.SYSTEM)));

        Assert.Equal(DiagnosticCodes.UnsupportedCulture, exception.Diagnostic.Code);
    }

    private sealed record CompanyContext(CompanyId CompanyId) : ICompanyContext;

    private sealed record CurrentUser(UserId UserId) : ICurrentUser
    {
        public bool HasPermission(string permissionCode) => false;
    }

    private sealed class DictionaryResources(IReadOnlyDictionary<(string Culture, string Key), string> resources)
        : ILocalizationResourceProvider
    {
        public bool TryGet(string culture, string resourceKey, out string value) =>
            resources.TryGetValue((culture, resourceKey), out value!);
    }

    private sealed class RecordingPreferenceStore : IUserPresentationPreferenceStore
    {
        public int WriteCount { get; private set; }
        public UserId SavedUserId { get; private set; }
        public UserPresentationPreferences? SavedPreferences { get; private set; }

        public ValueTask<UserPresentationPreferences?> GetAsync(UserId userId, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<UserPresentationPreferences?>(null);

        public ValueTask SaveAsync(
            UserId userId,
            UserPresentationPreferences preferences,
            CancellationToken cancellationToken = default)
        {
            SavedUserId = userId;
            SavedPreferences = preferences;
            WriteCount++;
            return ValueTask.CompletedTask;
        }
    }
}
