using System.Globalization;
using PayCalc24.FormulaEngine.Ast;
using PayCalc24.FormulaEngine.Diagnostics;
using PayCalc24.FormulaEngine.Model;

namespace PayCalc24.FormulaEngine.Parsing;

public sealed record FormulaEngineLimits(int MaxExpressionLength = 4096, int MaxAstNodes = 512, int MaxNestingDepth = 32, int MaxFunctionArguments = 32, int MaxEvaluationDepth = 64);

internal enum TokenKind { End, Number, Text, Identifier, True, False, Null, Plus, Minus, Star, Slash, Equal, NotEqual, Greater, GreaterEqual, Less, LessEqual, And, Or, Not, LeftParen, RightParen, Comma }
internal readonly record struct Token(TokenKind Kind, string Text, int Position);

public sealed class FormulaParser(FormulaEngineLimits? limits = null)
{
    private readonly FormulaEngineLimits _limits = limits ?? new();
    private List<Token> _tokens = [];
    private int _index;
    private int _nodes;

    public AstNode Parse(string expression)
    {
        ArgumentNullException.ThrowIfNull(expression);
        if (expression.Length > _limits.MaxExpressionLength) FailLimit("expression_length");
        _tokens = Tokenize(expression);
        _index = 0;
        _nodes = 0;
        var result = ParseOr(1);
        if (Current.Kind != TokenKind.End) FailSyntax(Current.Position);
        return result;
    }

    private AstNode ParseOr(int depth) { var left = ParseAnd(depth); while (Match(TokenKind.Or)) left = Node(new BinaryExpressionNode("OR", left, ParseAnd(depth)), depth); return left; }
    private AstNode ParseAnd(int depth) { var left = ParseComparison(depth); while (Match(TokenKind.And)) left = Node(new BinaryExpressionNode("AND", left, ParseComparison(depth)), depth); return left; }
    private AstNode ParseComparison(int depth)
    {
        var left = ParseAdditive(depth);
        while (Current.Kind is TokenKind.Equal or TokenKind.NotEqual or TokenKind.Greater or TokenKind.GreaterEqual or TokenKind.Less or TokenKind.LessEqual)
        {
            var op = Advance().Text; left = Node(new BinaryExpressionNode(op, left, ParseAdditive(depth)), depth);
        }
        return left;
    }
    private AstNode ParseAdditive(int depth) { var left = ParseMultiplicative(depth); while (Current.Kind is TokenKind.Plus or TokenKind.Minus) { var op=Advance().Text; left=Node(new BinaryExpressionNode(op,left,ParseMultiplicative(depth)),depth); } return left; }
    private AstNode ParseMultiplicative(int depth) { var left = ParseUnary(depth); while (Current.Kind is TokenKind.Star or TokenKind.Slash) { var op=Advance().Text; left=Node(new BinaryExpressionNode(op,left,ParseUnary(depth)),depth); } return left; }
    private AstNode ParseUnary(int depth)
    {
        if (Current.Kind is TokenKind.Minus or TokenKind.Plus or TokenKind.Not) { var op=Advance().Text.ToUpperInvariant(); return Node(new UnaryExpressionNode(op,ParseUnary(depth+1)),depth); }
        return ParsePrimary(depth);
    }
    private AstNode ParsePrimary(int depth)
    {
        if (depth > _limits.MaxNestingDepth) FailLimit("nesting_depth");
        var token = Advance();
        if (token.Kind == TokenKind.Number)
        {
            if (token.Text.Contains('.', StringComparison.Ordinal)) return Node(new LiteralNode(FormulaValue.Decimal(decimal.Parse(token.Text, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture))), depth);
            return Node(new LiteralNode(FormulaValue.Integer(long.Parse(token.Text, NumberStyles.None, CultureInfo.InvariantCulture))), depth);
        }
        if (token.Kind == TokenKind.Text) return Node(new LiteralNode(FormulaValue.Text(token.Text)), depth);
        if (token.Kind == TokenKind.True) return Node(new LiteralNode(FormulaValue.Boolean(true)), depth);
        if (token.Kind == TokenKind.False) return Node(new LiteralNode(FormulaValue.Boolean(false)), depth);
        if (token.Kind == TokenKind.Null) return Node(new LiteralNode(FormulaValue.Null), depth);
        if (token.Kind == TokenKind.Identifier)
        {
            var name = token.Text.ToUpperInvariant();
            if (!Match(TokenKind.LeftParen)) return Node(new ReferenceNode(name), depth);
            var args = new List<AstNode>();
            if (!Match(TokenKind.RightParen))
            {
                do { if (args.Count == _limits.MaxFunctionArguments) FailLimit("function_arguments"); args.Add(ParseOr(depth + 1)); } while (Match(TokenKind.Comma));
                Expect(TokenKind.RightParen);
            }
            return Node(new FunctionCallNode(name,args),depth);
        }
        if (token.Kind == TokenKind.LeftParen) { var value=ParseOr(depth+1); Expect(TokenKind.RightParen); return value; }
        FailSyntax(token.Position); return null!;
    }
    private AstNode Node(AstNode node, int depth) { if (++_nodes > _limits.MaxAstNodes) FailLimit("ast_nodes"); if (depth > _limits.MaxNestingDepth) FailLimit("nesting_depth"); return node; }
    private Token Current => _tokens[_index];
    private Token Advance() => _tokens[_index++];
    private bool Match(TokenKind kind) { if (Current.Kind != kind) return false; _index++; return true; }
    private void Expect(TokenKind kind) { if (!Match(kind)) FailSyntax(Current.Position); }
    private static void FailSyntax(int position) => throw new FormulaFailure(FormulaDiagnosticCodes.SyntaxError, new Dictionary<string, object?> { ["position"] = position });
    private static void FailLimit(string limit) => throw new FormulaFailure(FormulaDiagnosticCodes.ResourceLimitExceeded, new Dictionary<string, object?> { ["limit"] = limit });

    private static List<Token> Tokenize(string text)
    {
        var result = new List<Token>();
        for (var i=0;i<text.Length;)
        {
            if (char.IsWhiteSpace(text[i])) { i++; continue; }
            var start=i; var c=text[i++];
            if (char.IsDigit(c)) { while(i<text.Length&&char.IsDigit(text[i]))i++; if(i<text.Length&&text[i]=='.'){i++; if(i>=text.Length||!char.IsDigit(text[i]))FailSyntax(i); while(i<text.Length&&char.IsDigit(text[i]))i++;} result.Add(new(TokenKind.Number,text[start..i],start)); continue; }
            if (char.IsLetter(c)||c=='_') { while(i<text.Length&&(char.IsLetterOrDigit(text[i])||text[i]=='_'))i++; var word=text[start..i]; var upper=word.ToUpperInvariant(); var wordKind=upper switch{"TRUE"=>TokenKind.True,"FALSE"=>TokenKind.False,"NULL"=>TokenKind.Null,"AND"=>TokenKind.And,"OR"=>TokenKind.Or,"NOT"=>TokenKind.Not,_=>TokenKind.Identifier}; result.Add(new(wordKind,word,start)); continue; }
            if (c=='"') { var value=new System.Text.StringBuilder(); var closed=false; while(i<text.Length){c=text[i++];if(c=='"'){closed=true;break;}if(c=='\\'&&i<text.Length){c=text[i++];value.Append(c switch{'"'=>'"','\\'=>'\\','n'=>'\n','r'=>'\r','t'=>'\t',_=>throw new FormulaFailure(FormulaDiagnosticCodes.SyntaxError)});}else value.Append(c);}if(!closed)FailSyntax(start);result.Add(new(TokenKind.Text,value.ToString(),start));continue; }
            var kind=c switch{'+'=>TokenKind.Plus,'-'=>TokenKind.Minus,'*'=>TokenKind.Star,'/'=>TokenKind.Slash,'('=>TokenKind.LeftParen,')'=>TokenKind.RightParen,','=>TokenKind.Comma,'='=>TokenKind.Equal,'!'=>i<text.Length&&text[i]=='='?(i++,TokenKind.NotEqual).Item2:throw new FormulaFailure(FormulaDiagnosticCodes.SyntaxError),'>' => i<text.Length&&text[i]=='='?(i++,TokenKind.GreaterEqual).Item2:TokenKind.Greater,'<' => i<text.Length&&text[i]=='='?(i++,TokenKind.LessEqual).Item2:TokenKind.Less,_=>throw new FormulaFailure(FormulaDiagnosticCodes.SyntaxError,new Dictionary<string,object?>{{"position",start}})};
            result.Add(new(kind,text[start..i],start));
        }
        result.Add(new(TokenKind.End,string.Empty,text.Length)); return result;
    }
}
