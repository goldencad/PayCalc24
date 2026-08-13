using PayCalc24.Contracts.Diagnostics;
using PayCalc24.FormulaEngine.Ast;
using PayCalc24.FormulaEngine.Diagnostics;
using PayCalc24.FormulaEngine.Functions;
using PayCalc24.FormulaEngine.Model;
using PayCalc24.FormulaEngine.Parsing;

namespace PayCalc24.FormulaEngine.Execution;

public sealed class SafeFormulaEngine
{
    public const string EngineVersion = "1.0.0";
    private readonly FormulaParser _parser;
    private readonly FunctionCatalog _functions;
    private readonly FormulaEngineLimits _limits;

    public SafeFormulaEngine(FunctionCatalog? functions = null, FormulaEngineLimits? limits = null)
    { _limits=limits??new(); _parser=new(_limits); _functions=functions??new(); }

    public FormulaValidationResult Validate(string expression, IReadOnlyDictionary<string, FormulaValue>? referenceSchema = null)
    {
        try
        {
            var ast=_parser.Parse(expression);
            FormulaValueType? type=referenceSchema is null ? null : Infer(ast,referenceSchema);
            return new(true,ast,CanonicalAstSerializer.Serialize(ast),type,[]);
        }
        catch(FormulaFailure failure) { return new(false,null,null,null,[failure.Diagnostic]); }
        catch(OverflowException) { return new(false,null,null,null,[Error(FormulaDiagnosticCodes.DecimalOverflow)]); }
        catch(FormatException) { return new(false,null,null,null,[Error(FormulaDiagnosticCodes.SyntaxError)]); }
    }

    public FormulaEvaluationResult Evaluate(string expression, FormulaExecutionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        try
        {
            var ast=_parser.Parse(expression);
            _=Infer(ast,MergeSchema(context));
            return Evaluate(ast,context);
        }
        catch(FormulaFailure failure) { return Failure(context,failure.Diagnostic); }
        catch(OverflowException) { return Failure(context,Error(FormulaDiagnosticCodes.DecimalOverflow)); }
        catch(FormatException) { return Failure(context,Error(FormulaDiagnosticCodes.SyntaxError)); }
    }

    public FormulaEvaluationResult Evaluate(AstNode ast, FormulaExecutionContext context)
    {
        ArgumentNullException.ThrowIfNull(ast); ArgumentNullException.ThrowIfNull(context);
        try
        {
            var evaluated=EvaluateNode(ast,context,1);
            return new(true,evaluated.Value,evaluated.Value.Type,null,evaluated.Trace,Provenance(context));
        }
        catch(FormulaFailure failure) { return Failure(context,failure.Diagnostic); }
        catch(OverflowException) { return Failure(context,Error(FormulaDiagnosticCodes.DecimalOverflow)); }
    }

    private (FormulaValue Value,ExecutionTraceNode Trace) EvaluateNode(AstNode node, FormulaExecutionContext context, int depth)
    {
        if(depth>_limits.MaxEvaluationDepth)throw new FormulaFailure(FormulaDiagnosticCodes.ResourceLimitExceeded,new Dictionary<string,object?>{{"limit","evaluation_depth"}});
        switch(node)
        {
            case LiteralNode literal: return (literal.Value,new("literal",ResultValue:literal.Value.CanonicalText(),DataType:literal.Value.Type));
            case ReferenceNode reference:
                if(!context.TryResolve(reference.Code,out var resolved))throw new FormulaFailure(FormulaDiagnosticCodes.UnknownReference,new Dictionary<string,object?>{{"referenceCode",reference.Code}});
                return (resolved,new("reference",ReferenceCode:reference.Code,ResolvedValue:resolved.CanonicalText(),ResultValue:resolved.CanonicalText(),DataType:resolved.Type));
            case UnaryExpressionNode unary:
                var child=EvaluateNode(unary.Operand,context,depth+1); var unaryValue=Unary(unary.Operator,child.Value);
                return (unaryValue,new("unary",Operator:unary.Operator,ResultValue:unaryValue.CanonicalText(),DataType:unaryValue.Type,Children:[child.Trace]));
            case BinaryExpressionNode binary:
                var left=EvaluateNode(binary.Left,context,depth+1);
                if(binary.Operator=="AND"&&!left.Value.AsBoolean())return (FormulaValue.Boolean(false),new("binary",Operator:"AND",ResultValue:"false",DataType:FormulaValueType.Boolean,Children:[left.Trace]));
                if(binary.Operator=="OR"&&left.Value.AsBoolean())return (FormulaValue.Boolean(true),new("binary",Operator:"OR",ResultValue:"true",DataType:FormulaValueType.Boolean,Children:[left.Trace]));
                var right=EvaluateNode(binary.Right,context,depth+1); var binaryValue=Binary(binary.Operator,left.Value,right.Value);
                return (binaryValue,new("binary",Operator:binary.Operator,ResultValue:binaryValue.CanonicalText(),DataType:binaryValue.Type,Children:[left.Trace,right.Trace]));
            case FunctionCallNode call when call.Name=="IF":
                if(call.Arguments.Count!=3)throw new FormulaFailure(FormulaDiagnosticCodes.InvalidArgumentCount);
                var condition=EvaluateNode(call.Arguments[0],context,depth+1); if(condition.Value.Type!=FormulaValueType.Boolean)throw new FormulaFailure(FormulaDiagnosticCodes.TypeMismatch);
                var branch=EvaluateNode(call.Arguments[condition.Value.AsBoolean()?1:2],context,depth+1);
                return (branch.Value,new("function",FunctionName:"IF",ResultValue:branch.Value.CanonicalText(),DataType:branch.Value.Type,Children:[condition.Trace,branch.Trace]));
            case FunctionCallNode call:
                if(!_functions.TryGet(call.Name,out var function))throw new FormulaFailure(FormulaDiagnosticCodes.UnknownFunction,new Dictionary<string,object?>{{"functionName",call.Name}});
                var args=call.Arguments.Select(x=>EvaluateNode(x,context,depth+1)).ToArray(); var result=function.Evaluate(args.Select(x=>x.Value).ToArray());
                return (result,new("function",FunctionName:call.Name,ResultValue:result.CanonicalText(),DataType:result.Type,Children:args.Select(x=>x.Trace).ToArray()));
            default: throw new FormulaFailure(FormulaDiagnosticCodes.InvalidAst);
        }
    }

    private FormulaValueType Infer(AstNode node, IReadOnlyDictionary<string,FormulaValue>? schema)
    {
        switch(node)
        {
            case LiteralNode literal:return literal.Value.Type;
            case ReferenceNode reference:
                if(schema is null)return FormulaValueType.Null;
                if(!schema.TryGetValue(reference.Code,out var value))throw new FormulaFailure(FormulaDiagnosticCodes.UnknownReference,new Dictionary<string,object?>{{"referenceCode",reference.Code}});
                return value.Type;
            case UnaryExpressionNode unary:
                var operand=Infer(unary.Operand,schema); if(unary.Operator=="NOT") { Require(operand==FormulaValueType.Boolean);return FormulaValueType.Boolean; } Require(operand is FormulaValueType.Integer or FormulaValueType.Decimal);return operand;
            case BinaryExpressionNode binary:
                var left=Infer(binary.Left,schema);var right=Infer(binary.Right,schema);
                if(binary.Operator is "AND" or "OR"){Require(left==FormulaValueType.Boolean&&right==FormulaValueType.Boolean);return FormulaValueType.Boolean;}
                if(binary.Operator is "+" or "-" or "*" or "/"){Require(IsNumeric(left)&&IsNumeric(right));return binary.Operator=="/"||left==FormulaValueType.Decimal||right==FormulaValueType.Decimal?FormulaValueType.Decimal:FormulaValueType.Integer;}
                Require(Comparable(left,right));return FormulaValueType.Boolean;
            case FunctionCallNode call when call.Name=="IF":
                Require(call.Arguments.Count==3,FormulaDiagnosticCodes.InvalidArgumentCount);Require(Infer(call.Arguments[0],schema)==FormulaValueType.Boolean);var yes=Infer(call.Arguments[1],schema);var no=Infer(call.Arguments[2],schema);Require(Compatible(yes,no));return Promote(yes,no);
            case FunctionCallNode call:
                if(!_functions.TryGet(call.Name,out _))throw new FormulaFailure(FormulaDiagnosticCodes.UnknownFunction,new Dictionary<string,object?>{{"functionName",call.Name}});
                foreach(var argument in call.Arguments) _=Infer(argument,schema);
                return call.Name switch{"ABS"=>Infer(call.Arguments[0],schema),"DATE"=>FormulaValueType.Date,"COALESCE"=>call.Arguments.Select(x=>Infer(x,schema)).FirstOrDefault(x=>x!=FormulaValueType.Null,FormulaValueType.Null),_=>FormulaValueType.Decimal};
            default:throw new FormulaFailure(FormulaDiagnosticCodes.InvalidAst);
        }
    }
    private static FormulaValue Unary(string op,FormulaValue value)=>op switch{"NOT" when value.Type==FormulaValueType.Boolean=>FormulaValue.Boolean(!value.AsBoolean()),"+" when value.IsNumeric=>value,"-" when value.Type==FormulaValueType.Integer=>FormulaValue.Integer(checked(-value.AsInteger())),"-" when value.Type==FormulaValueType.Decimal=>FormulaValue.Decimal(-value.AsDecimal()),_=>throw new FormulaFailure(FormulaDiagnosticCodes.TypeMismatch)};
    private static FormulaValue Binary(string op,FormulaValue left,FormulaValue right)
    {
        if(op is "+" or "-" or "*" or "/")
        {
            if(!left.IsNumeric||!right.IsNumeric)throw new FormulaFailure(FormulaDiagnosticCodes.TypeMismatch);
            if(op=="/"&&right.AsDecimal()==0)throw new FormulaFailure(FormulaDiagnosticCodes.DivisionByZero);
            if(op!="/"&&left.Type==FormulaValueType.Integer&&right.Type==FormulaValueType.Integer)return FormulaValue.Integer(op switch{"+"=>checked(left.AsInteger()+right.AsInteger()),"-"=>checked(left.AsInteger()-right.AsInteger()),_=>checked(left.AsInteger()*right.AsInteger())});
            return FormulaValue.Decimal(op switch{"+"=>left.AsDecimal()+right.AsDecimal(),"-"=>left.AsDecimal()-right.AsDecimal(),"*"=>left.AsDecimal()*right.AsDecimal(),_=>left.AsDecimal()/right.AsDecimal()});
        }
        if(op is "AND" or "OR") { if(left.Type!=FormulaValueType.Boolean||right.Type!=FormulaValueType.Boolean)throw new FormulaFailure(FormulaDiagnosticCodes.TypeMismatch);return FormulaValue.Boolean(op=="AND"?left.AsBoolean()&&right.AsBoolean():left.AsBoolean()||right.AsBoolean()); }
        if(!Comparable(left.Type,right.Type))throw new FormulaFailure(FormulaDiagnosticCodes.TypeMismatch);
        var comparison=Compare(left,right);return FormulaValue.Boolean(op switch{"="=>comparison==0,"!="=>comparison!=0,">"=>comparison>0,">="=>comparison>=0,"<"=>comparison<0,"<="=>comparison<=0,_=>throw new FormulaFailure(FormulaDiagnosticCodes.InvalidAst)});
    }
    private static int Compare(FormulaValue a,FormulaValue b){if(a.Type==FormulaValueType.Null||b.Type==FormulaValueType.Null)return a.Type==b.Type?0:a.Type==FormulaValueType.Null?-1:1;if(a.IsNumeric&&b.IsNumeric)return a.AsDecimal().CompareTo(b.AsDecimal());return a.Type switch{FormulaValueType.Boolean=>a.AsBoolean().CompareTo(b.AsBoolean()),FormulaValueType.Date=>((DateOnly)a.RawValue!).CompareTo((DateOnly)b.RawValue!),FormulaValueType.Text=>StringComparer.Ordinal.Compare((string)a.RawValue!,(string)b.RawValue!),_=>throw new FormulaFailure(FormulaDiagnosticCodes.TypeMismatch)};}
    private static bool IsNumeric(FormulaValueType x)=>x is FormulaValueType.Integer or FormulaValueType.Decimal;
    private static bool Comparable(FormulaValueType a,FormulaValueType b)=>a==b||(IsNumeric(a)&&IsNumeric(b));
    private static bool Compatible(FormulaValueType a,FormulaValueType b)=>a==FormulaValueType.Null||b==FormulaValueType.Null||Comparable(a,b);
    private static FormulaValueType Promote(FormulaValueType a,FormulaValueType b)=>a==FormulaValueType.Null?b:b==FormulaValueType.Null?a:IsNumeric(a)&&IsNumeric(b)&&(a==FormulaValueType.Decimal||b==FormulaValueType.Decimal)?FormulaValueType.Decimal:a;
    private static void Require(bool condition,string code=FormulaDiagnosticCodes.TypeMismatch){if(!condition)throw new FormulaFailure(code);}
    private static Dictionary<string,FormulaValue> MergeSchema(FormulaExecutionContext context){var result=new Dictionary<string,FormulaValue>(context.Values,StringComparer.OrdinalIgnoreCase);if(context.Parameters is not null)foreach(var item in context.Parameters)result[item.Key]=item.Value;return result;}
    private static Diagnostic Error(string code)=>new(code,DiagnosticSeverity.Error,new Dictionary<string,object?>());
    private static FormulaEvaluationResult Failure(FormulaExecutionContext context,Diagnostic diagnostic)=>new(false,null,null,diagnostic,new("error",DiagnosticCode:diagnostic.Code),Provenance(context));
    private static FormulaProvenance Provenance(FormulaExecutionContext c)=>new(c.FormulaDefinitionId,c.FormulaVersionId,c.FormulaChecksum,c.ParameterSetVersionIds??[],c.LookupTableVersionIds??[],c.RuleSetVersionIds??[],c.InputEntryIds?.Values.SelectMany(x=>x).Distinct().ToArray()??[],c.ExecutionMode,c.ScenarioId,c.CorrelationId,EngineVersion);
}
