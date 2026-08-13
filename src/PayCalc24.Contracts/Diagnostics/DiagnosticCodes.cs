namespace PayCalc24.Contracts.Diagnostics;

public static class DiagnosticCodes
{
    public const string CompanyScopeMismatch = "CORE.COMPANY_SCOPE_MISMATCH";
    public const string LocalizationResourceMissing = "PRESENTATION.LOCALIZATION_RESOURCE_MISSING";
    public const string UnsupportedCulture = "PRESENTATION.UNSUPPORTED_CULTURE";
    public const string InvalidThemeMode = "PRESENTATION.INVALID_THEME_MODE";
    public const string IdempotencyConflict = "CORE.IDEMPOTENCY_CONFLICT";
}
