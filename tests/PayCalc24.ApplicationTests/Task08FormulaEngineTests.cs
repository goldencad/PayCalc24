using System.Globalization;
using PayCalc24.Contracts.FormulaRepository;
using PayCalc24.Contracts.Identity;
using PayCalc24.FormulaEngine.Diagnostics;
using PayCalc24.FormulaEngine.Execution;
using PayCalc24.FormulaEngine.Model;
using PayCalc24.FormulaEngine.Parsing;
using PayCalc24.FormulaEngine.Testing;

namespace PayCalc24.ApplicationTests;

public sealed class Task08FormulaEngineTests
{
    private readonly SafeFormulaEngine _engine=new();
    [Theory]
    [InlineData("2 + 3 * 4","14")]
    [InlineData("(2 + 3) * 4","20")]
    [InlineData("IF(TRUE, 10, 1 / 0)","10")]
    [InlineData("MIN(4, 2, 8) + MAX(4, 2, 8)","10")]
    [InlineData("ABS(-5)","5")]
    [InlineData("ROUND(2.345, 2)","2.35")]
    [InlineData("COALESCE(NULL, 7)","7")]
    public void EvaluatesCoreLanguage(string expression,string expected)
    { var result=_engine.Evaluate(expression,Context());Assert.True(result.Success);Assert.Equal(expected,result.Value!.Value.CanonicalText());Assert.NotNull(result.Trace); }

    [Fact] public void BooleanReferencesAndComparisonWork(){var result=_engine.Evaluate("A > 10 AND B <= 20",Context(new(){["A"]=FormulaValue.Integer(11),["B"]=FormulaValue.Integer(20)}));Assert.True(result.Value!.Value.AsBoolean());}
    [Fact] public void P3InterpolationIsGeneric(){var values=new Dictionary<string,FormulaValue>{{"FINAL_ACHIEVEMENT",FormulaValue.Decimal(.92m)},{"ACHIEVEMENT_FLOOR",FormulaValue.Decimal(.70m)},{"P3_FLOOR",FormulaValue.Decimal(13000000m)},{"ACHIEVEMENT_TARGET",FormulaValue.Decimal(1m)},{"P3_TARGET",FormulaValue.Decimal(17000000m)}};var result=_engine.Evaluate("INTERPOLATE(FINAL_ACHIEVEMENT, ACHIEVEMENT_FLOOR, P3_FLOOR, ACHIEVEMENT_TARGET, P3_TARGET)",Context(values));Assert.True(result.Success);Assert.Equal(15933333.333333333333333333333m,result.Value!.Value.AsDecimal());}
    [Theory]
    [InlineData("MISSING + 1",FormulaDiagnosticCodes.UnknownReference)]
    [InlineData("NOPE(1)",FormulaDiagnosticCodes.UnknownFunction)]
    [InlineData("TRUE + 1",FormulaDiagnosticCodes.TypeMismatch)]
    [InlineData("1 / 0",FormulaDiagnosticCodes.DivisionByZero)]
    [InlineData("1 +",FormulaDiagnosticCodes.SyntaxError)]
    [InlineData("INTERPOLATE(1, 2, 3, 2, 4)",FormulaDiagnosticCodes.InvalidInterpolation)]
    public void FailsClosedWithStableDiagnostics(string expression,string code){var result=_engine.Evaluate(expression,Context());Assert.False(result.Success);Assert.Equal(code,result.Diagnostic!.Code);}
    [Fact] public void SerializationIsCanonical(){var first=_engine.Validate("2+3*4");var second=_engine.Validate(" 2 + 3 * 4 ");Assert.True(first.Success);Assert.Equal(first.CanonicalAstJson,second.CanonicalAstJson);}
    [Fact] public void CultureDoesNotChangeFormula(){var original=CultureInfo.CurrentCulture;try{foreach(var culture in new[]{"vi-VN","en-US","fr-FR"}){CultureInfo.CurrentCulture=CultureInfo.GetCultureInfo(culture);Assert.Equal("3.75",_engine.Evaluate("1.25 + 2.50",Context()).Value!.Value.CanonicalText());}Assert.False(_engine.Evaluate("1,25",Context()).Success);}finally{CultureInfo.CurrentCulture=original;}}
    [Fact] public void ResourceLimitsAreEnforced(){var engine=new SafeFormulaEngine(limits:new FormulaEngineLimits(MaxExpressionLength:8));Assert.Equal(FormulaDiagnosticCodes.ResourceLimitExceeded,engine.Evaluate("1 + 2 + 3 + 4",Context()).Diagnostic!.Code);}
    [Fact] public void AllModesUseSameAstAndRuntime(){var validation=_engine.Validate("RATE * 100");Assert.True(validation.Success);foreach(var mode in Enum.GetValues<FormulaExecutionMode>()){var values=new Dictionary<string,FormulaValue>{{"RATE",FormulaValue.Decimal(mode==FormulaExecutionMode.BackTest?2m:1m)}};var result=_engine.Evaluate(validation.Ast!,Context(values,mode));Assert.True(result.Success);Assert.Equal(mode,result.Provenance.ExecutionMode);Assert.Equal(SafeFormulaEngine.EngineVersion,result.Provenance.EngineVersion);}}
    [Fact] public void CompanyFormulasHaveNoBusinessBranching(){var companyA=new Dictionary<string,FormulaValue>{{"P3_ELIGIBLE",FormulaValue.Integer(10)}};var companyB=new Dictionary<string,FormulaValue>{{"SALES_INCENTIVE",FormulaValue.Integer(7)}};Assert.Equal("20",_engine.Evaluate("P3_ELIGIBLE * 2",Context(companyA)).Value!.Value.CanonicalText());Assert.Equal("21",_engine.Evaluate("SALES_INCENTIVE * 3",Context(companyB)).Value!.Value.CanonicalText());}
    [Fact] public void StoredFormulaTestCasesRunWithToleranceAndDiagnostics(){var id=FormulaVersionId.From(Guid.NewGuid());var success=new FormulaTestCaseDto(Guid.NewGuid(),CompanyId.From(Guid.NewGuid()),id,"value","{\"A\":2.001}",FormulaTypedValue.Decimal(2m),FormulaDataType.DECIMAL,.01m,null,true,null);var runner=new FormulaTestRunner(_engine);Assert.True(runner.Run(success,"A",Context()).Passed);var diagnostic=success with{Id=Guid.NewGuid(),Name="error",InputJson="{}",ExpectedValue=null,ExpectedDiagnosticCode=FormulaDiagnosticCodes.UnknownReference};Assert.True(runner.Run(diagnostic,"A",Context()).Passed);Assert.False(runner.Run(success with{Tolerance=0m},"A",Context()).Passed);}
    private static FormulaExecutionContext Context(Dictionary<string,FormulaValue>? values=null,FormulaExecutionMode mode=FormulaExecutionMode.Production)=>new(CompanyId.From(Guid.Parse("11111111-1111-1111-1111-111111111111")),new DateOnly(2026,8,13),mode,values??new(StringComparer.OrdinalIgnoreCase),null,"task-08",FormulaVersionId:FormulaVersionId.From(Guid.Parse("22222222-2222-2222-2222-222222222222")));
}
