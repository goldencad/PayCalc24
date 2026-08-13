using PayCalc24.Contracts.Diagnostics;
using PayCalc24.Contracts.Localization;

namespace PayCalc24.Application.Localization;

public sealed class LocalizationService(ILocalizationResourceProvider resources) : ILocalizationService
{
    public LocalizedResource Resolve(string resourceKey, string? preferredCulture)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(resourceKey);

        var cultureSupported = SupportedCultures.All.Contains(preferredCulture, StringComparer.OrdinalIgnoreCase);
        var requested = cultureSupported
            ? SupportedCultures.All.Single(c => string.Equals(c, preferredCulture, StringComparison.OrdinalIgnoreCase))
            : SupportedCultures.Default;

        var cultureDiagnostic = cultureSupported || string.IsNullOrWhiteSpace(preferredCulture)
            ? null
            : new Diagnostic(
                DiagnosticCodes.UnsupportedCulture,
                DiagnosticSeverity.Warning,
                new Dictionary<string, object?>
                {
                    ["requestedCulture"] = preferredCulture,
                    ["fallbackCulture"] = SupportedCultures.Default
                });

        if (resources.TryGet(requested, resourceKey, out var localized))
        {
            return new LocalizedResource(resourceKey, localized, requested, cultureDiagnostic);
        }

        if (!string.Equals(requested, SupportedCultures.Default, StringComparison.Ordinal) &&
            resources.TryGet(SupportedCultures.Default, resourceKey, out var fallback))
        {
            return new LocalizedResource(resourceKey, fallback, SupportedCultures.Default, null);
        }

        var diagnostic = new Diagnostic(
            DiagnosticCodes.LocalizationResourceMissing,
            DiagnosticSeverity.Warning,
            new Dictionary<string, object?>
            {
                ["resourceKey"] = resourceKey,
                ["requestedCulture"] = requested,
                ["fallbackCulture"] = SupportedCultures.Default
            });

        return new LocalizedResource(resourceKey, resourceKey, SupportedCultures.Default, diagnostic);
    }
}
