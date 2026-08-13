using System.Globalization;
using System.Text.Json;
using PayCalc24.Contracts.FormulaRepository;
using PayCalc24.FormulaEngine.Diagnostics;
using PayCalc24.FormulaEngine.Execution;
using PayCalc24.FormulaEngine.Model;

namespace PayCalc24.FormulaEngine.Testing;

public sealed record FormulaTestRunResult(Guid TestCaseId, string Name, bool Passed,
    FormulaEvaluationResult Evaluation, string? FailureDiagnosticCode);

public sealed class FormulaTestRunner(SafeFormulaEngine engine)
{
    public FormulaTestRunResult Run(FormulaTestCaseDto testCase, string expression, FormulaExecutionContext baseContext)
    {
        ArgumentNullException.ThrowIfNull(testCase);
        try
        {
            var values=ParseInputs(testCase.InputJson);
            var context=baseContext with { Values=values, FormulaVersionId=testCase.FormulaVersionId };
            var evaluation=engine.Evaluate(expression,context);
            var passed=Matches(testCase,evaluation);
            return new(testCase.Id,testCase.Name,passed,evaluation,passed?null:FormulaDiagnosticCodes.TestExpectationFailed);
        }
        catch(JsonException)
        {
            var evaluation=engine.Evaluate("(",baseContext);
            return new(testCase.Id,testCase.Name,false,evaluation,FormulaDiagnosticCodes.TestExpectationFailed);
        }
    }
    private static bool Matches(FormulaTestCaseDto testCase,FormulaEvaluationResult result)
    {
        if(testCase.ExpectedDiagnosticCode is not null)return !result.Success&&result.Diagnostic?.Code==testCase.ExpectedDiagnosticCode;
        if(!result.Success||result.Value is null||testCase.ExpectedValue is null)return false;
        var expected=FromContract(testCase.ExpectedValue);if(testCase.ExpectedDataType is not null&&Map(result.Value.Value.Type)!=testCase.ExpectedDataType)return false;
        if(expected.IsNumeric&&result.Value.Value.IsNumeric)return Math.Abs(expected.AsDecimal()-result.Value.Value.AsDecimal())<=(testCase.Tolerance??0m);
        return expected==result.Value.Value;
    }
    private static Dictionary<string,FormulaValue> ParseInputs(string json)
    {
        using var document=JsonDocument.Parse(json);if(document.RootElement.ValueKind!=JsonValueKind.Object)throw new JsonException();
        var result=new Dictionary<string,FormulaValue>(StringComparer.OrdinalIgnoreCase);
        foreach(var property in document.RootElement.EnumerateObject())result[property.Name.ToUpperInvariant()]=property.Value.ValueKind switch
        {
            JsonValueKind.Number when property.Value.TryGetInt64(out var integer)=>FormulaValue.Integer(integer),
            JsonValueKind.Number=>FormulaValue.Decimal(property.Value.GetDecimal()),
            JsonValueKind.True=>FormulaValue.Boolean(true),JsonValueKind.False=>FormulaValue.Boolean(false),
            JsonValueKind.String=>FormulaValue.Text(property.Value.GetString()!),JsonValueKind.Null=>FormulaValue.Null,_=>throw new JsonException()
        };
        return result;
    }
    private static FormulaValue FromContract(FormulaTypedValue value)=>value.DataType switch
    {
        FormulaDataType.DECIMAL=>FormulaValue.Decimal(value.DecimalValue!.Value),FormulaDataType.INTEGER=>FormulaValue.Integer(value.IntegerValue!.Value),
        FormulaDataType.BOOLEAN=>FormulaValue.Boolean(value.BooleanValue!.Value),FormulaDataType.DATE=>FormulaValue.Date(value.DateValue!.Value),
        FormulaDataType.TEXT=>FormulaValue.Text(value.TextValue!),_=>throw new ArgumentOutOfRangeException(nameof(value))
    };
    private static FormulaDataType Map(FormulaValueType type)=>type switch{FormulaValueType.Decimal=>FormulaDataType.DECIMAL,FormulaValueType.Integer=>FormulaDataType.INTEGER,FormulaValueType.Boolean=>FormulaDataType.BOOLEAN,FormulaValueType.Date=>FormulaDataType.DATE,FormulaValueType.Text=>FormulaDataType.TEXT,_=>throw new InvalidOperationException()};
}
