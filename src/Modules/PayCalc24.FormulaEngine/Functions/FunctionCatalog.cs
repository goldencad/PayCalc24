using PayCalc24.FormulaEngine.Diagnostics;
using PayCalc24.FormulaEngine.Model;

namespace PayCalc24.FormulaEngine.Functions;

public interface ICalculationFunction
{
    string Name { get; }
    FormulaValue Evaluate(IReadOnlyList<FormulaValue> arguments);
}

public sealed class FunctionCatalog
{
    private readonly Dictionary<string, ICalculationFunction> _functions;
    public FunctionCatalog(IEnumerable<ICalculationFunction>? functions = null)
    {
        var items = functions?.ToArray() ?? BuiltIns.All;
        _functions = items.ToDictionary(x=>x.Name,StringComparer.OrdinalIgnoreCase);
    }
    public bool TryGet(string name, out ICalculationFunction function) => _functions.TryGetValue(name,out function!);
}

internal static class FunctionGuard
{
    public static void Count(IReadOnlyList<FormulaValue> args, int min, int max)
    { if(args.Count<min||args.Count>max) throw new FormulaFailure(FormulaDiagnosticCodes.InvalidArgumentCount,new Dictionary<string,object?>{{"actual",args.Count},{"minimum",min},{"maximum",max}}); }
    public static void Numeric(IEnumerable<FormulaValue> args) { if(args.Any(x=>!x.IsNumeric)) throw new FormulaFailure(FormulaDiagnosticCodes.TypeMismatch); }
}

internal static class BuiltIns
{
    public static ICalculationFunction[] All { get; } = [new MinFunction(),new MaxFunction(),new AbsFunction(),new RoundFunction(),new CoalesceFunction(),new InterpolateFunction(),new DateFunction()];
    private sealed class MinFunction : ICalculationFunction { public string Name=>"MIN"; public FormulaValue Evaluate(IReadOnlyList<FormulaValue> a){FunctionGuard.Count(a,1,int.MaxValue);FunctionGuard.Numeric(a);return FormulaValue.Decimal(a.Min(x=>x.AsDecimal()));} }
    private sealed class MaxFunction : ICalculationFunction { public string Name=>"MAX"; public FormulaValue Evaluate(IReadOnlyList<FormulaValue> a){FunctionGuard.Count(a,1,int.MaxValue);FunctionGuard.Numeric(a);return FormulaValue.Decimal(a.Max(x=>x.AsDecimal()));} }
    private sealed class AbsFunction : ICalculationFunction { public string Name=>"ABS"; public FormulaValue Evaluate(IReadOnlyList<FormulaValue> a){FunctionGuard.Count(a,1,1);FunctionGuard.Numeric(a);try{return a[0].Type==FormulaValueType.Integer?FormulaValue.Integer(Math.Abs(a[0].AsInteger())):FormulaValue.Decimal(Math.Abs(a[0].AsDecimal()));}catch(OverflowException){throw new FormulaFailure(FormulaDiagnosticCodes.DecimalOverflow);} } }
    private sealed class RoundFunction : ICalculationFunction { public string Name=>"ROUND"; public FormulaValue Evaluate(IReadOnlyList<FormulaValue> a){FunctionGuard.Count(a,1,2);FunctionGuard.Numeric(a);var digits=a.Count==2&&a[1].Type==FormulaValueType.Integer?(int)a[1].AsInteger():a.Count==1?0:throw new FormulaFailure(FormulaDiagnosticCodes.TypeMismatch);if(digits is <0 or >28)throw new FormulaFailure(FormulaDiagnosticCodes.TypeMismatch);return FormulaValue.Decimal(decimal.Round(a[0].AsDecimal(),digits,MidpointRounding.AwayFromZero));} }
    private sealed class CoalesceFunction : ICalculationFunction { public string Name=>"COALESCE"; public FormulaValue Evaluate(IReadOnlyList<FormulaValue> a){FunctionGuard.Count(a,1,int.MaxValue);return a.FirstOrDefault(x=>x.Type!=FormulaValueType.Null,FormulaValue.Null);} }
    private sealed class InterpolateFunction : ICalculationFunction { public string Name=>"INTERPOLATE"; public FormulaValue Evaluate(IReadOnlyList<FormulaValue> a){FunctionGuard.Count(a,5,5);FunctionGuard.Numeric(a);var x=a[0].AsDecimal();var x1=a[1].AsDecimal();var y1=a[2].AsDecimal();var x2=a[3].AsDecimal();var y2=a[4].AsDecimal();if(x1==x2)throw new FormulaFailure(FormulaDiagnosticCodes.InvalidInterpolation);try{return FormulaValue.Decimal(y1+((x-x1)*(y2-y1)/(x2-x1)));}catch(OverflowException){throw new FormulaFailure(FormulaDiagnosticCodes.DecimalOverflow);}} }
    private sealed class DateFunction : ICalculationFunction { public string Name=>"DATE"; public FormulaValue Evaluate(IReadOnlyList<FormulaValue> a){FunctionGuard.Count(a,1,1);if(a[0].Type!=FormulaValueType.Text||!DateOnly.TryParseExact(a[0].CanonicalText(),"yyyy-MM-dd",System.Globalization.CultureInfo.InvariantCulture,System.Globalization.DateTimeStyles.None,out var date))throw new FormulaFailure(FormulaDiagnosticCodes.TypeMismatch);return FormulaValue.Date(date);} }
}
