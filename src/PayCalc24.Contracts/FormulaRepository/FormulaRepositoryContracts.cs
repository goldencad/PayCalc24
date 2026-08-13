using PayCalc24.Contracts.Identity;
using PayCalc24.Contracts.Temporal;

namespace PayCalc24.Contracts.FormulaRepository;

// Stable language-neutral repository type codes intentionally mirror their API values.
#pragma warning disable CA1720

public readonly record struct FormulaDefinitionId(Guid Value) { public static FormulaDefinitionId From(Guid value)=>value==Guid.Empty?throw new ArgumentException("Identifier cannot be empty.",nameof(value)):new(value); }
public readonly record struct FormulaVersionId(Guid Value) { public static FormulaVersionId From(Guid value)=>value==Guid.Empty?throw new ArgumentException("Identifier cannot be empty.",nameof(value)):new(value); }
public readonly record struct ParameterSetVersionId(Guid Value) { public static ParameterSetVersionId From(Guid value)=>value==Guid.Empty?throw new ArgumentException("Identifier cannot be empty.",nameof(value)):new(value); }
public readonly record struct LookupTableVersionId(Guid Value) { public static LookupTableVersionId From(Guid value)=>value==Guid.Empty?throw new ArgumentException("Identifier cannot be empty.",nameof(value)):new(value); }
public readonly record struct RuleSetVersionId(Guid Value) { public static RuleSetVersionId From(Guid value)=>value==Guid.Empty?throw new ArgumentException("Identifier cannot be empty.",nameof(value)):new(value); }

public enum FormulaLifecycleStatus { DRAFT, VALIDATED, TESTED, APPROVED, PUBLISHED, RETIRED }
public enum FormulaScopeType { COMPANY, ORGANIZATION, EMPLOYEE, FUND, COMPONENT }
public enum FormulaDataType { DECIMAL, INTEGER, BOOLEAN, DATE, TEXT }
public enum FormulaDependencyType { FORMULA, INPUT, PARAMETER, LOOKUP }
public enum LookupMatchType { EXACT, RANGE, THRESHOLD }

public sealed record FormulaDefinitionDto(FormulaDefinitionId Id,CompanyId CompanyId,string Code,string Name,string? Description,FormulaDataType ReturnType,FormulaScopeType ScopeType,bool IsActive);
public sealed record FormulaVersionDto(FormulaVersionId Id,FormulaDefinitionId FormulaDefinitionId,CompanyId CompanyId,int Revision,string ExpressionText,string? ExpressionAstJson,EffectivePeriod EffectivePeriod,FormulaLifecycleStatus LifecycleStatus,string? Checksum,DateTimeOffset CreatedAt,UserId CreatedBy,DateTimeOffset? ValidatedAt,DateTimeOffset? TestedAt,DateTimeOffset? ApprovedAt,DateTimeOffset? PublishedAt,UserId? PublishedBy,DateTimeOffset? RetiredAt);
public sealed record FormulaDependencyDto(Guid Id,CompanyId CompanyId,FormulaVersionId FormulaVersionId,FormulaDependencyType DependencyType,string ReferenceCode,Guid? ReferencedEntityId,bool IsRequired,int? Sequence);
public sealed record FormulaTestCaseDto(Guid Id,CompanyId CompanyId,FormulaVersionId FormulaVersionId,string Name,string InputJson,FormulaTypedValue? ExpectedValue,FormulaDataType? ExpectedDataType,decimal? Tolerance,string? ExpectedDiagnosticCode,bool IsRequired,string? Status);

public sealed record FormulaTypedValue(FormulaDataType DataType,decimal? DecimalValue=null,long? IntegerValue=null,bool? BooleanValue=null,DateOnly? DateValue=null,string? TextValue=null)
{
    public static FormulaTypedValue Decimal(decimal value)=>new(FormulaDataType.DECIMAL,DecimalValue:value);
    public static FormulaTypedValue Integer(long value)=>new(FormulaDataType.INTEGER,IntegerValue:value);
    public static FormulaTypedValue Boolean(bool value)=>new(FormulaDataType.BOOLEAN,BooleanValue:value);
    public static FormulaTypedValue Date(DateOnly value)=>new(FormulaDataType.DATE,DateValue:value);
    public static FormulaTypedValue Text(string value)=>new(FormulaDataType.TEXT,TextValue:value);
}
public sealed record ParameterValueDto(Guid Id,CompanyId CompanyId,ParameterSetVersionId ParameterSetVersionId,string Code,string? Name,FormulaTypedValue Value,string? UnitType,string? Description,int? DisplayOrder);
public sealed record ParameterSetVersionDto(ParameterSetVersionId Id,CompanyId CompanyId,string Code,string Name,string? Description,int Revision,EffectivePeriod EffectivePeriod,FormulaLifecycleStatus LifecycleStatus,IReadOnlyList<ParameterValueDto> Values);
public sealed record LookupRowDto(Guid Id,CompanyId CompanyId,LookupTableVersionId LookupTableVersionId,int Sequence,FormulaTypedValue? KeyValue,FormulaTypedValue? LowerBound,FormulaTypedValue? UpperBound,FormulaTypedValue ResultValue,string? Label,string? MetadataJson);
public sealed record LookupTableVersionDto(LookupTableVersionId Id,CompanyId CompanyId,string Code,string Name,string? Description,FormulaDataType KeyDataType,FormulaDataType ValueDataType,LookupMatchType MatchType,bool AllowRangeOverlap,int Revision,EffectivePeriod EffectivePeriod,FormulaLifecycleStatus LifecycleStatus,IReadOnlyList<LookupRowDto> Rows);
public sealed record RuleDto(Guid Id,CompanyId CompanyId,RuleSetVersionId RuleSetVersionId,int Priority,string? ConditionExpressionText,string ResultExpressionText,bool IsElse,string? Status,string? Description);
public sealed record RuleSetVersionDto(RuleSetVersionId Id,CompanyId CompanyId,string Code,string Name,string? Description,FormulaDataType ReturnType,FormulaScopeType ScopeType,int Revision,EffectivePeriod EffectivePeriod,FormulaLifecycleStatus LifecycleStatus,bool StopOnMatch,IReadOnlyList<RuleDto> Rules);

public interface IFormulaRepositoryService
{
    ValueTask<FormulaDefinitionDto> CreateDefinitionAsync(CompanyId companyId,string code,string name,string? description,FormulaDataType returnType,FormulaScopeType scopeType,CancellationToken token=default);
    ValueTask<FormulaVersionDto> CreateDraftVersionAsync(CompanyId companyId,FormulaDefinitionId definitionId,string expressionText,EffectivePeriod period,CancellationToken token=default);
    ValueTask<FormulaVersionDto> UpdateDraftVersionAsync(CompanyId companyId,FormulaVersionId versionId,string expressionText,string? expressionAstJson,EffectivePeriod period,CancellationToken token=default);
    ValueTask<FormulaDependencyDto> AddDependencyAsync(CompanyId companyId,FormulaVersionId versionId,FormulaDependencyType type,string referenceCode,Guid? referencedEntityId=null,bool isRequired=true,int? sequence=null,CancellationToken token=default);
    ValueTask RemoveDependencyAsync(CompanyId companyId,FormulaVersionId versionId,Guid dependencyId,CancellationToken token=default);
    ValueTask<FormulaTestCaseDto> AddTestCaseAsync(CompanyId companyId,FormulaVersionId versionId,string name,string inputJson,FormulaTypedValue? expectedValue,string? expectedDiagnosticCode=null,bool isRequired=true,decimal? tolerance=null,CancellationToken token=default);
    ValueTask UpdateTestCaseAsync(CompanyId companyId,FormulaVersionId versionId,FormulaTestCaseDto testCase,CancellationToken token=default);
    ValueTask RemoveTestCaseAsync(CompanyId companyId,FormulaVersionId versionId,Guid testCaseId,CancellationToken token=default);
    ValueTask<FormulaVersionDto> TransitionAsync(CompanyId companyId,FormulaVersionId versionId,FormulaLifecycleStatus target,CancellationToken token=default);
    IReadOnlyList<FormulaDefinitionDto> ListDefinitions(CompanyId companyId,string? search=null);
    IReadOnlyList<FormulaVersionDto> GetVersionHistory(CompanyId companyId,FormulaDefinitionId definitionId);
    FormulaVersionDto ResolveEffective(CompanyId companyId,string code,DateOnly businessDate);
    IReadOnlyList<FormulaDependencyDto> ListDependencies(CompanyId companyId,FormulaVersionId versionId);
    IReadOnlyList<FormulaTestCaseDto> ListTestCases(CompanyId companyId,FormulaVersionId versionId);
}

public interface IPolicyRepositoryService
{
    ParameterSetVersionDto CreateParameterDraft(CompanyId companyId,string code,string name,string? description,EffectivePeriod period);
    ParameterSetVersionDto SetParameterValue(CompanyId companyId,ParameterSetVersionId versionId,string code,FormulaTypedValue value,string? unitType=null,string? name=null);
    ParameterSetVersionDto TransitionParameter(CompanyId companyId,ParameterSetVersionId versionId,FormulaLifecycleStatus target);
    ParameterSetVersionDto ResolveParameterSet(CompanyId companyId,string code,DateOnly businessDate);
    LookupTableVersionDto CreateLookupDraft(CompanyId companyId,string code,string name,FormulaDataType keyType,FormulaDataType valueType,LookupMatchType matchType,EffectivePeriod period,bool allowRangeOverlap=false);
    LookupTableVersionDto AddLookupRow(CompanyId companyId,LookupTableVersionId versionId,int sequence,FormulaTypedValue? key,FormulaTypedValue? lower,FormulaTypedValue? upper,FormulaTypedValue result);
    LookupTableVersionDto TransitionLookup(CompanyId companyId,LookupTableVersionId versionId,FormulaLifecycleStatus target);
    LookupTableVersionDto ResolveLookup(CompanyId companyId,string code,DateOnly businessDate);
    RuleSetVersionDto CreateRuleSetDraft(CompanyId companyId,string code,string name,FormulaDataType returnType,FormulaScopeType scope,EffectivePeriod period,bool stopOnMatch);
    RuleSetVersionDto AddRule(CompanyId companyId,RuleSetVersionId versionId,int priority,string? condition,string result,bool isElse=false);
    RuleSetVersionDto TransitionRuleSet(CompanyId companyId,RuleSetVersionId versionId,FormulaLifecycleStatus target);
    RuleSetVersionDto ResolveRuleSet(CompanyId companyId,string code,DateOnly businessDate);
}

#pragma warning restore CA1720

public static class FormulaRepositoryAuditActions
{
    public const string DefinitionCreated="FORMULA.DEFINITION_CREATED"; public const string DraftCreated="FORMULA.DRAFT_CREATED"; public const string DraftUpdated="FORMULA.DRAFT_UPDATED"; public const string DependencyAdded="FORMULA.DEPENDENCY_ADDED"; public const string DependencyRemoved="FORMULA.DEPENDENCY_REMOVED"; public const string TestCaseChanged="FORMULA.TEST_CASE_CHANGED"; public const string Validated="FORMULA.VALIDATED"; public const string Tested="FORMULA.TESTED"; public const string Approved="FORMULA.APPROVED"; public const string Published="FORMULA.PUBLISHED"; public const string Retired="FORMULA.RETIRED"; public const string ParameterPublished="PARAMETER.PUBLISHED"; public const string LookupPublished="LOOKUP.PUBLISHED"; public const string RuleSetPublished="RULESET.PUBLISHED";
}
