using PayCalc24.FormulaEngine.Model;

namespace PayCalc24.FormulaEngine.Ast;

public abstract record AstNode;
public sealed record LiteralNode(FormulaValue Value) : AstNode;
public sealed record ReferenceNode(string Code) : AstNode;
public sealed record UnaryExpressionNode(string Operator, AstNode Operand) : AstNode;
public sealed record BinaryExpressionNode(string Operator, AstNode Left, AstNode Right) : AstNode;
public sealed record FunctionCallNode(string Name, IReadOnlyList<AstNode> Arguments) : AstNode;
