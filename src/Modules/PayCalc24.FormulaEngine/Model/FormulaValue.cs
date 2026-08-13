using System.Globalization;

namespace PayCalc24.FormulaEngine.Model;

#pragma warning disable CA1720
public enum FormulaValueType { Decimal, Integer, Boolean, Date, Text, Null }

public readonly record struct FormulaValue(FormulaValueType Type, object? RawValue)
{
    public static FormulaValue Decimal(decimal value) => new(FormulaValueType.Decimal, value);
    public static FormulaValue Integer(long value) => new(FormulaValueType.Integer, value);
    public static FormulaValue Boolean(bool value) => new(FormulaValueType.Boolean, value);
    public static FormulaValue Date(DateOnly value) => new(FormulaValueType.Date, value);
    public static FormulaValue Text(string value) => new(FormulaValueType.Text, value ?? throw new ArgumentNullException(nameof(value)));
    public static FormulaValue Null => new(FormulaValueType.Null, null);
    public bool IsNumeric => Type is FormulaValueType.Decimal or FormulaValueType.Integer;
    public decimal AsDecimal() => Type switch { FormulaValueType.Decimal => (decimal)RawValue!, FormulaValueType.Integer => (long)RawValue!, _ => throw new InvalidOperationException() };
    public long AsInteger() => Type == FormulaValueType.Integer ? (long)RawValue! : throw new InvalidOperationException();
    public bool AsBoolean() => Type == FormulaValueType.Boolean ? (bool)RawValue! : throw new InvalidOperationException();
    public string CanonicalText() => Type switch
    {
        FormulaValueType.Decimal => ((decimal)RawValue!).ToString(CultureInfo.InvariantCulture),
        FormulaValueType.Integer => ((long)RawValue!).ToString(CultureInfo.InvariantCulture),
        FormulaValueType.Boolean => (bool)RawValue! ? "true" : "false",
        FormulaValueType.Date => ((DateOnly)RawValue!).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        FormulaValueType.Text => (string)RawValue!,
        _ => "null"
    };
}
#pragma warning restore CA1720
