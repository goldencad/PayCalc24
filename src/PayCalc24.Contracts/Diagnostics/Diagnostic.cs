namespace PayCalc24.Contracts.Diagnostics;

public enum DiagnosticSeverity
{
    Info,
    Warning,
    Error
}

/// <summary>Language-neutral diagnostic. Arguments may contain canonical identifiers and are never translated.</summary>
public sealed record Diagnostic(
    string Code,
    DiagnosticSeverity Severity,
    IReadOnlyDictionary<string, object?> Arguments);
