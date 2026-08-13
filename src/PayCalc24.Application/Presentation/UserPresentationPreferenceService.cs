using PayCalc24.Contracts.Identity;
using PayCalc24.Contracts.Diagnostics;
using PayCalc24.Contracts.Localization;
using PayCalc24.Contracts.Presentation;

namespace PayCalc24.Application.Presentation;

public sealed class UserPresentationPreferenceService(
    ICurrentUser currentUser,
    IUserPresentationPreferenceStore store) : IUserPresentationPreferenceService
{
    private static readonly UserPresentationPreferences Defaults =
        new(SupportedCultures.Default, ThemeMode.SYSTEM);

    public async ValueTask<UserPresentationPreferences> GetAsync(CancellationToken cancellationToken = default) =>
        await store.GetAsync(currentUser.UserId, cancellationToken) ?? Defaults;

    public async ValueTask<UserPresentationPreferences> UpdateAsync(
        UserPresentationPreferences preferences,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(preferences);
        if (!SupportedCultures.All.Contains(preferences.PreferredCulture, StringComparer.Ordinal))
        {
            throw new PresentationPreferenceValidationException(new Diagnostic(
                DiagnosticCodes.UnsupportedCulture,
                DiagnosticSeverity.Error,
                new Dictionary<string, object?> { ["preferredCulture"] = preferences.PreferredCulture }));
        }

        if (!Enum.IsDefined(preferences.ThemeMode))
        {
            throw new PresentationPreferenceValidationException(new Diagnostic(
                DiagnosticCodes.InvalidThemeMode,
                DiagnosticSeverity.Error,
                new Dictionary<string, object?> { ["themeMode"] = (int)preferences.ThemeMode }));
        }

        await store.SaveAsync(currentUser.UserId, preferences, cancellationToken);
        return preferences;
    }
}

public sealed class PresentationPreferenceValidationException(Diagnostic diagnostic) : Exception(diagnostic.Code)
{
    public Diagnostic Diagnostic { get; } = diagnostic;
}
