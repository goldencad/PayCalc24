using PayCalc24.Contracts.Diagnostics;

namespace PayCalc24.Application.Temporal;

public sealed class TemporalValidationException(Diagnostic diagnostic) : Exception(diagnostic.Code)
{
    public Diagnostic Diagnostic { get; } = diagnostic;
}
