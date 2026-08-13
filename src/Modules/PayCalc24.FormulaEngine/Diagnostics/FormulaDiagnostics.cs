using PayCalc24.Contracts.Diagnostics;

namespace PayCalc24.FormulaEngine.Diagnostics;

public static class FormulaDiagnosticCodes
{
    public const string SyntaxError = "FORMULA_ENGINE.SYNTAX_ERROR";
    public const string UnknownReference = "FORMULA_ENGINE.UNKNOWN_REFERENCE";
    public const string UnknownFunction = "FORMULA_ENGINE.UNKNOWN_FUNCTION";
    public const string InvalidArgumentCount = "FORMULA_ENGINE.INVALID_ARGUMENT_COUNT";
    public const string TypeMismatch = "FORMULA_ENGINE.TYPE_MISMATCH";
    public const string DivisionByZero = "FORMULA_ENGINE.DIVISION_BY_ZERO";
    public const string DecimalOverflow = "FORMULA_ENGINE.DECIMAL_OVERFLOW";
    public const string InvalidInterpolation = "FORMULA_ENGINE.INVALID_INTERPOLATION";
    public const string ResourceLimitExceeded = "FORMULA_ENGINE.RESOURCE_LIMIT_EXCEEDED";
    public const string InvalidAst = "FORMULA_ENGINE.INVALID_AST";
    public const string TestExpectationFailed = "FORMULA_ENGINE.TEST_EXPECTATION_FAILED";
}

internal sealed class FormulaFailure(string code, IReadOnlyDictionary<string, object?>? arguments = null) : Exception(code)
{
    public Diagnostic Diagnostic { get; } = new(code, DiagnosticSeverity.Error, arguments ?? new Dictionary<string, object?>());
}
