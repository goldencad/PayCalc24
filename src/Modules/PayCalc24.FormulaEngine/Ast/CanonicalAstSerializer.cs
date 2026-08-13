using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using PayCalc24.FormulaEngine.Model;

namespace PayCalc24.FormulaEngine.Ast;

public static class CanonicalAstSerializer
{
    public static string Serialize(AstNode node)
    {
        ArgumentNullException.ThrowIfNull(node);
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Encoder = JavaScriptEncoder.Default })) Write(writer, node);
        return Encoding.UTF8.GetString(stream.ToArray());
    }
    private static void Write(Utf8JsonWriter writer, AstNode node)
    {
        writer.WriteStartObject();
        switch (node)
        {
            case LiteralNode literal:
                writer.WriteString("nodeType", "literal"); writer.WriteString("dataType", literal.Value.Type.ToString().ToUpperInvariant());
                writer.WritePropertyName("value"); WriteValue(writer,literal.Value); break;
            case ReferenceNode reference:
                writer.WriteString("nodeType", "reference"); writer.WriteString("code", reference.Code); break;
            case UnaryExpressionNode unary:
                writer.WriteString("nodeType", "unary"); writer.WriteString("operator", unary.Operator); writer.WritePropertyName("operand"); Write(writer,unary.Operand); break;
            case BinaryExpressionNode binary:
                writer.WriteString("nodeType", "binary"); writer.WriteString("operator", binary.Operator); writer.WritePropertyName("left"); Write(writer,binary.Left); writer.WritePropertyName("right"); Write(writer,binary.Right); break;
            case FunctionCallNode call:
                writer.WriteString("nodeType", "function"); writer.WriteString("name", call.Name); writer.WriteStartArray("arguments"); foreach(var argument in call.Arguments) Write(writer,argument); writer.WriteEndArray(); break;
            default: throw new ArgumentException("Unsupported AST node.",nameof(node));
        }
        writer.WriteEndObject();
    }
    private static void WriteValue(Utf8JsonWriter writer, FormulaValue value)
    {
        switch(value.Type)
        {
            case FormulaValueType.Decimal: writer.WriteRawValue(value.CanonicalText()); break;
            case FormulaValueType.Integer: writer.WriteNumberValue(value.AsInteger()); break;
            case FormulaValueType.Boolean: writer.WriteBooleanValue(value.AsBoolean()); break;
            case FormulaValueType.Date or FormulaValueType.Text: writer.WriteStringValue(value.CanonicalText()); break;
            default: writer.WriteNullValue(); break;
        }
    }
}
