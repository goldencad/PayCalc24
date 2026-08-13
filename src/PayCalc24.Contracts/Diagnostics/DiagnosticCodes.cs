namespace PayCalc24.Contracts.Diagnostics;

public static class DiagnosticCodes
{
    public const string CompanyScopeMismatch = "CORE.COMPANY_SCOPE_MISMATCH";
    public const string LocalizationResourceMissing = "PRESENTATION.LOCALIZATION_RESOURCE_MISSING";
    public const string UnsupportedCulture = "PRESENTATION.UNSUPPORTED_CULTURE";
    public const string InvalidThemeMode = "PRESENTATION.INVALID_THEME_MODE";
    public const string IdempotencyConflict = "CORE.IDEMPOTENCY_CONFLICT";
    public const string InvalidEffectiveRange = "TEMPORAL.INVALID_EFFECTIVE_RANGE";
    public const string PublishedVersionOverlap = "TEMPORAL.PUBLISHED_VERSION_OVERLAP";
    public const string EffectiveVersionNotFound = "TEMPORAL.EFFECTIVE_VERSION_NOT_FOUND";
    public const string EffectiveVersionAmbiguous = "TEMPORAL.EFFECTIVE_VERSION_AMBIGUOUS";
    public const string PublishedVersionImmutable = "TEMPORAL.PUBLISHED_VERSION_IMMUTABLE";
    public const string InvalidPublicationState = "TEMPORAL.INVALID_PUBLICATION_STATE";
    public const string InvalidVersionNumber = "TEMPORAL.INVALID_VERSION_NUMBER";
    public const string DuplicateEmployeeCode = "ORGANIZATION.DUPLICATE_EMPLOYEE_CODE";
    public const string InvalidOrganizationReference = "ORGANIZATION.INVALID_REFERENCE";
    public const string OrganizationCycle = "ORGANIZATION.HIERARCHY_CYCLE";
    public const string PrimaryAssignmentOverlap = "ORGANIZATION.PRIMARY_ASSIGNMENT_OVERLAP";
    public const string AssignmentNotFound = "ORGANIZATION.ASSIGNMENT_NOT_FOUND";
    public const string DuplicatePayComponentCode = "COMPENSATION.DUPLICATE_PAY_COMPONENT_CODE";
    public const string DuplicateCompensationSchemeCode = "COMPENSATION.DUPLICATE_SCHEME_CODE";
    public const string CrossCompanySchemeComponent = "COMPENSATION.CROSS_COMPANY_SCHEME_COMPONENT";
    public const string DuplicateSchemeComponent = "COMPENSATION.DUPLICATE_SCHEME_COMPONENT";
    public const string InvalidComponentSequence = "COMPENSATION.INVALID_COMPONENT_SEQUENCE";
    public const string PayComponentNotFound = "COMPENSATION.PAY_COMPONENT_NOT_FOUND";
    public const string PublishedConfigurationImmutable = "COMPENSATION.PUBLISHED_CONFIGURATION_IMMUTABLE";
    public const string EffectiveSchemeNotFound = "COMPENSATION.EFFECTIVE_SCHEME_NOT_FOUND";
    public const string EffectiveSchemeAmbiguous = "COMPENSATION.EFFECTIVE_SCHEME_AMBIGUOUS";
    public const string InvalidAssignmentSchemeScope = "COMPENSATION.INVALID_ASSIGNMENT_SCHEME_SCOPE";
}
