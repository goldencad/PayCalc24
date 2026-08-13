using PayCalc24.Contracts.Diagnostics;
using PayCalc24.Contracts.Identity;
using PayCalc24.Contracts.PayrollFunds;

namespace PayCalc24.PayrollFunds.Model;

internal sealed class PayrollFundVersion
{
    internal PayrollFundVersion(PayrollFundVersionId id,PayrollFundDefinitionId definitionId,CompanyId companyId,string code,int revision,
        CreatePayrollFundDraft value,DateTimeOffset createdAt,UserId createdBy)
    { Id=id;DefinitionId=definitionId;CompanyId=companyId;Code=NormalizeCode(code);Revision=revision;CreatedAt=createdAt;CreatedBy=createdBy;Apply(value.Name,value.Description,value.FundType,value.Scope,value.Source,value.Policy,value.EffectiveFrom,value.EffectiveTo,value.CurrencyCode,value.ParentFundDefinitionId); }
    public PayrollFundVersionId Id{get;} public PayrollFundDefinitionId DefinitionId{get;} public CompanyId CompanyId{get;} public string Code{get;}
    public int Revision{get;} public string Name{get;private set;}=""; public string? Description{get;private set;} public PayrollFundType FundType{get;private set;}
    public FundScopeReference Scope{get;private set;}=null!; public FundSourceConfiguration Source{get;private set;}=null!; public FundAllocationPolicy Policy{get;private set;}=null!;
    public string? CurrencyCode{get;private set;} public PayrollFundDefinitionId? ParentFundDefinitionId{get;private set;} public DateOnly EffectiveFrom{get;private set;}
    public DateOnly? EffectiveTo{get;private set;} public FundLifecycleStatus LifecycleStatus{get;private set;}=FundLifecycleStatus.DRAFT;
    public DateTimeOffset CreatedAt{get;} public UserId CreatedBy{get;} public DateTimeOffset? PublishedAt{get;private set;} public UserId? PublishedBy{get;private set;}
    internal void Update(UpdatePayrollFundDraft c)=>Apply(c.Name,c.Description,c.FundType,c.Scope,c.Source,c.Policy,c.EffectiveFrom,c.EffectiveTo,c.CurrencyCode,c.ParentFundDefinitionId);
    internal void Publish(DateTimeOffset at,UserId by){if(LifecycleStatus!=FundLifecycleStatus.DRAFT)Throw(DiagnosticCodes.PayrollFundPublishedImmutable);LifecycleStatus=FundLifecycleStatus.PUBLISHED;PublishedAt=at;PublishedBy=by;}
    internal void Retire(DateOnly to){if(LifecycleStatus!=FundLifecycleStatus.PUBLISHED)Throw(DiagnosticCodes.PayrollFundPublishedImmutable);if(to<=EffectiveFrom)Throw(DiagnosticCodes.InvalidEffectiveRange);EffectiveTo=to;LifecycleStatus=FundLifecycleStatus.RETIRED;}
    internal PayrollFundVersionDto ToDto()=>new(Id,DefinitionId,CompanyId,Code,Name,Description,FundType,Scope,Source,Policy,CurrencyCode,ParentFundDefinitionId,EffectiveFrom,EffectiveTo,LifecycleStatus,Revision,CreatedAt,CreatedBy,PublishedAt,PublishedBy);
    private void Apply(string name,string? description,PayrollFundType type,FundScopeReference scope,FundSourceConfiguration source,FundAllocationPolicy policy,DateOnly from,DateOnly? to,string? currency,PayrollFundDefinitionId? parent)
    {
        if(LifecycleStatus!=FundLifecycleStatus.DRAFT)Throw(DiagnosticCodes.PayrollFundPublishedImmutable);
        if(string.IsNullOrWhiteSpace(name)||to is not null&&to<=from)Throw(DiagnosticCodes.InvalidEffectiveRange);
        if(scope.Type==FundScopeType.COMPANY&&scope.EntityId is not null||scope.Type!=FundScopeType.COMPANY&&scope.EntityId is null)Throw(DiagnosticCodes.PayrollFundInvalidScope);
        var validSource=source.Type switch{FundSourceType.FIXED=>source.FixedAmount is>=0m,FundSourceType.INPUT=>!string.IsNullOrWhiteSpace(source.InputCode),FundSourceType.FORMULA=>source.FormulaDefinitionId is not null&&source.FormulaVersionId is not null&&!string.IsNullOrWhiteSpace(source.FormulaChecksum),_=>false};
        if(!validSource)Throw(DiagnosticCodes.PayrollFundInvalidSource);
        if(policy.Method is not(FundAllocationMethod.PROPORTIONAL or FundAllocationMethod.PRIORITY or FundAllocationMethod.WEIGHTED))Throw(DiagnosticCodes.PayrollFundInvalidAllocationMethod);
        if(policy.DecimalScale is<0 or>8||policy.MinCoverage is<0m||policy.MaxCoverage is<0m||policy.MinCoverage is not null&&policy.MaxCoverage is not null&&policy.MinCoverage>policy.MaxCoverage)Throw(DiagnosticCodes.PayrollFundInvalidAllocationMethod);
        Name=name.Trim();Description=description?.Trim();FundType=type;Scope=scope;Source=source;Policy=policy;EffectiveFrom=from;EffectiveTo=to;CurrencyCode=currency?.Trim().ToUpperInvariant();ParentFundDefinitionId=parent;
    }
    internal static string NormalizeCode(string code){if(string.IsNullOrWhiteSpace(code))Throw(DiagnosticCodes.PayrollFundInvalidSource);return code.Trim().ToUpperInvariant();}
    internal static void Throw(string code,IReadOnlyDictionary<string,object?>? args=null)=>throw new PayrollFundException(new(code,DiagnosticSeverity.Error,args??new Dictionary<string,object?>()));
}
