using PayCalc24.Contracts.Diagnostics;

namespace PayCalc24.Contracts.Localization;

public static class SupportedCultures
{
    public const string Vietnamese = "vi-VN";
    public const string English = "en-US";
    public const string Default = English;

    public static IReadOnlyList<string> All { get; } = [Vietnamese, English];
}

public interface ILocalizationResourceProvider
{
    bool TryGet(string culture, string resourceKey, out string value);
}

public sealed record LocalizedResource(
    string ResourceKey,
    string Value,
    string ResolvedCulture,
    Diagnostic? Diagnostic);

public interface ILocalizationService
{
    LocalizedResource Resolve(string resourceKey, string? preferredCulture);
}
