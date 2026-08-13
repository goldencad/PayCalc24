using PayCalc24.Compensation.Model;
using PayCalc24.Contracts.Compensation;
using PayCalc24.Contracts.Diagnostics;
using PayCalc24.Contracts.Identity;
using PayCalc24.Contracts.Temporal;

namespace PayCalc24.Compensation.Services;

public sealed class CompensationCatalogService(ICompanyContext companyContext) : ICompensationCatalogService
{
    private readonly List<PayComponent> components = [];
    private readonly List<CompensationScheme> schemes = [];

    public PayComponentDto CreatePayComponentDraft(CompanyId companyId, PayComponentId id, EffectivePeriod period, PayComponentContent content)
    {
        Scope(companyId); Validate(content); Validate(period);
        var definition = components.SingleOrDefault(x => x.Id == id);
        if (definition is null) { definition = new(id, companyId); components.Add(definition); }
        else if (definition.CompanyId != companyId) CrossCompany();
        EnsureComponentCodeAvailable(companyId, content.Code, definition);
        var version = NewVersion(definition, period, content); return Dto(definition, version);
    }

    public PayComponentDto UpdatePayComponentDraft(CompanyId companyId, PayComponentId id, int versionNumber, EffectivePeriod period, PayComponentContent content)
    {
        Scope(companyId); Validate(content); Validate(period); var definition = Component(companyId, id); EnsureComponentCodeAvailable(companyId, content.Code, definition);
        var version = Draft(definition, versionNumber); version.Change(period, content); return Dto(definition, version);
    }

    public PayComponentDto PublishPayComponent(CompanyId companyId, PayComponentId id, int versionNumber)
    { Scope(companyId); var definition = Component(companyId, id); var version = Draft(definition, versionNumber); EnsureNoPublishedOverlap(definition.Versions, version); version.Publish(); return Dto(definition, version); }

    public void ClosePayComponent(CompanyId companyId, PayComponentId id, int versionNumber, DateOnly effectiveTo)
    { Scope(companyId); var version = Version(Component(companyId, id), versionNumber); Close(version, effectiveTo); }

    public IReadOnlyList<PayComponentDto> ListPayComponents(CompanyId companyId, CatalogSearch search)
    { Scope(companyId); return components.Where(x=>x.CompanyId==companyId).SelectMany(x=>x.Versions.Select(v=>Dto(x,v))).Where(x=>Match(x.Version.Content.Code,x.Version.Content.Name,x.Version.Content.Status,search)).OrderBy(x=>x.Version.Content.Code,StringComparer.OrdinalIgnoreCase).ThenBy(x=>x.Version.VersionNumber).ToArray(); }

    public PayComponentDto GetEffectivePayComponent(CompanyId companyId, string code, DateOnly businessDate)
    {
        Scope(companyId); var matches=components.Where(x=>x.CompanyId==companyId).SelectMany(x=>x.Versions.Select(v=>(x,v))).Where(x=>Eq(x.v.Content.Code,code)&&Published(x.v)&&x.v.EffectivePeriod.Contains(businessDate)).ToArray();
        if(matches.Length!=1) Throw(matches.Length==0?DiagnosticCodes.PayComponentNotFound:DiagnosticCodes.EffectiveVersionAmbiguous,new(){["code"]=code,["businessDate"]=businessDate,["matchCount"]=matches.Length}); return Dto(matches[0].x,matches[0].v);
    }

    public CompensationSchemeDto CreateSchemeDraft(CompanyId companyId, CompensationSchemeId id, EffectivePeriod period, CompensationSchemeContent content)
    {
        Scope(companyId); Validate(period); ValidateScheme(companyId, content); var definition=schemes.SingleOrDefault(x=>x.Id==id);
        if(definition is null){definition=new(id,companyId);schemes.Add(definition);} else if(definition.CompanyId!=companyId) CrossCompany();
        EnsureSchemeCodeAvailable(companyId,content.Code,definition); var version=NewVersion(definition,period,Normalize(content)); return Dto(definition,version);
    }

    public CompensationSchemeDto UpdateSchemeDraft(CompanyId companyId, CompensationSchemeId id, int versionNumber, EffectivePeriod period, CompensationSchemeContent content)
    { Scope(companyId);Validate(period);ValidateScheme(companyId,content);var d=Scheme(companyId,id);EnsureSchemeCodeAvailable(companyId,content.Code,d);var v=Draft(d,versionNumber);v.Change(period,Normalize(content));return Dto(d,v); }

    public CompensationSchemeDto AddSchemeComponent(CompanyId companyId, CompensationSchemeId id, int versionNumber, SchemeComponentContent component)
    { Scope(companyId);var d=Scheme(companyId,id);var v=Draft(d,versionNumber);var items=v.Content.Components.Append(component).ToArray();var content=v.Content with{Components=items};ValidateScheme(companyId,content);v.Change(v.EffectivePeriod,Normalize(content));return Dto(d,v); }

    public CompensationSchemeDto RemoveSchemeComponent(CompanyId companyId, CompensationSchemeId id, int versionNumber, PayComponentId componentId)
    { Scope(companyId);var d=Scheme(companyId,id);var v=Draft(d,versionNumber);v.Change(v.EffectivePeriod,v.Content with{Components=v.Content.Components.Where(x=>x.PayComponentId!=componentId).ToArray()});return Dto(d,v); }

    public CompensationSchemeDto ReorderSchemeComponent(CompanyId companyId, CompensationSchemeId id, int versionNumber, PayComponentId componentId, int sequence)
    { Scope(companyId);var d=Scheme(companyId,id);var v=Draft(d,versionNumber);var items=v.Content.Components.Select(x=>x.PayComponentId==componentId?x with{Sequence=sequence}:x).ToArray();var content=v.Content with{Components=items};ValidateScheme(companyId,content);v.Change(v.EffectivePeriod,Normalize(content));return Dto(d,v); }

    public CompensationSchemeDto PublishScheme(CompanyId companyId, CompensationSchemeId id, int versionNumber)
    { Scope(companyId);var d=Scheme(companyId,id);var v=Draft(d,versionNumber);ValidateScheme(companyId,v.Content);EnsureNoPublishedOverlap(d.Versions,v);v.Publish();return Dto(d,v); }

    public void CloseScheme(CompanyId companyId, CompensationSchemeId id, int versionNumber, DateOnly effectiveTo)
    { Scope(companyId); Close(Version(Scheme(companyId,id),versionNumber),effectiveTo); }

    public IReadOnlyList<CompensationSchemeDto> ListSchemes(CompanyId companyId, CatalogSearch search)
    { Scope(companyId);return schemes.Where(x=>x.CompanyId==companyId).SelectMany(x=>x.Versions.Select(v=>Dto(x,v))).Where(x=>Match(x.Version.Content.Code,x.Version.Content.Name,x.Version.Content.Status,search)).OrderBy(x=>x.Version.Content.Code,StringComparer.OrdinalIgnoreCase).ThenBy(x=>x.Version.VersionNumber).ToArray(); }

    public CompensationSchemeDto ResolveEffectiveScheme(CompanyId companyId, string code, DateOnly businessDate)
    { Scope(companyId);var matches=schemes.Where(x=>x.CompanyId==companyId).SelectMany(x=>x.Versions.Select(v=>(x,v))).Where(x=>Eq(x.v.Content.Code,code)&&Published(x.v)&&x.v.EffectivePeriod.Contains(businessDate)).ToArray();if(matches.Length!=1)Throw(matches.Length==0?DiagnosticCodes.EffectiveSchemeNotFound:DiagnosticCodes.EffectiveSchemeAmbiguous,new(){["code"]=code,["businessDate"]=businessDate,["matchCount"]=matches.Length});return Dto(matches[0].x,matches[0].v); }

    public CompensationSchemeDto ResolveEffectiveScheme(CompanyId companyId, CompensationSchemeId id, DateOnly businessDate)
    { Scope(companyId);var d=Scheme(companyId,id);var matches=d.Versions.Where(x=>Published(x)&&x.EffectivePeriod.Contains(businessDate)).ToArray();if(matches.Length!=1)Throw(matches.Length==0?DiagnosticCodes.EffectiveSchemeNotFound:DiagnosticCodes.EffectiveSchemeAmbiguous,new(){["schemeId"]=id.Value,["businessDate"]=businessDate,["matchCount"]=matches.Length});return Dto(d,matches[0]); }

    private void ValidateScheme(CompanyId companyId, CompensationSchemeContent content)
    { Required(content.Code,"code");Required(content.Name,"name");if(content.Components.Any(x=>x.Sequence<=0)||content.Components.GroupBy(x=>x.Sequence).Any(x=>x.Count()>1))Throw(DiagnosticCodes.InvalidComponentSequence,[]);if(content.Components.GroupBy(x=>x.PayComponentId).Any(x=>x.Count()>1))Throw(DiagnosticCodes.DuplicateSchemeComponent,[]);foreach(var item in content.Components){var component=components.SingleOrDefault(x=>x.Id==item.PayComponentId);if(component is null)Throw(DiagnosticCodes.PayComponentNotFound,new(){["payComponentId"]=item.PayComponentId.Value});if(component!.CompanyId!=companyId)CrossCompany();} }
    private static CompensationSchemeContent Normalize(CompensationSchemeContent c)=>c with{Code=c.Code.Trim(),Name=c.Name.Trim(),Description=Optional(c.Description),Components=c.Components.OrderBy(x=>x.Sequence).ToArray()};
    private static void Validate(PayComponentContent c){Required(c.Code,"code");Required(c.Name,"name");}
    private void Scope(CompanyId id){if(id!=companyContext.CompanyId)Throw(DiagnosticCodes.CompanyScopeMismatch,new(){["requestedCompanyId"]=id.Value,["currentCompanyId"]=companyContext.CompanyId.Value});}
    private void EnsureComponentCodeAvailable(CompanyId c,string code,PayComponent except){if(components.Any(x=>x!=except&&x.CompanyId==c&&x.Versions.Any(v=>Eq(v.Content.Code,code))))Throw(DiagnosticCodes.DuplicatePayComponentCode,new(){["code"]=code});}
    private void EnsureSchemeCodeAvailable(CompanyId c,string code,CompensationScheme except){if(schemes.Any(x=>x!=except&&x.CompanyId==c&&x.Versions.Any(v=>Eq(v.Content.Code,code))))Throw(DiagnosticCodes.DuplicateCompensationSchemeCode,new(){["code"]=code});}
    private PayComponent Component(CompanyId c,PayComponentId id)=>components.SingleOrDefault(x=>x.Id==id&&x.CompanyId==c)??throw Error(DiagnosticCodes.PayComponentNotFound,new(){["payComponentId"]=id.Value});
    private CompensationScheme Scheme(CompanyId c,CompensationSchemeId id)=>schemes.SingleOrDefault(x=>x.Id==id&&x.CompanyId==c)??throw Error(DiagnosticCodes.EffectiveSchemeNotFound,new(){["schemeId"]=id.Value});
    private static CatalogVersion<T> NewVersion<TId,T>(CatalogDefinition<TId,T> d,EffectivePeriod p,T c)where TId:struct{var v=new CatalogVersion<T>(Guid.NewGuid(),d.Versions.Count==0?1:d.Versions.Max(x=>x.VersionNumber)+1,p,c);d.Add(v);return v;}
    private static CatalogVersion<T> Version<TId,T>(CatalogDefinition<TId,T>d,int n)where TId:struct=>d.Versions.SingleOrDefault(x=>x.VersionNumber==n)??throw Error(DiagnosticCodes.InvalidVersionNumber,new(){["versionNumber"]=n});
    private static CatalogVersion<T> Draft<TId,T>(CatalogDefinition<TId,T>d,int n)where TId:struct{var v=Version(d,n);if(v.PublicationState!=PublicationState.DRAFT)Throw(DiagnosticCodes.PublishedConfigurationImmutable,new(){["versionNumber"]=n});return v;}
    private static void EnsureNoPublishedOverlap<T>(IReadOnlyList<CatalogVersion<T>> versions,CatalogVersion<T> v){if(versions.Any(x=>x!=v&&Published(x)&&x.EffectivePeriod.Overlaps(v.EffectivePeriod)))Throw(DiagnosticCodes.PublishedVersionOverlap,new(){["versionNumber"]=v.VersionNumber});}
    private static void Close<T>(CatalogVersion<T>v,DateOnly to){if(v.PublicationState!=PublicationState.PUBLISHED)Throw(DiagnosticCodes.InvalidPublicationState,[]);Validate(new EffectivePeriod(v.EffectivePeriod.EffectiveFrom,to));v.Close(to);}
    private static bool Published<T>(CatalogVersion<T>v)=>v.PublicationState is PublicationState.PUBLISHED or PublicationState.SUPERSEDED;
    private static void Validate(EffectivePeriod p){if(p.EffectiveTo is not null&&p.EffectiveFrom>=p.EffectiveTo)Throw(DiagnosticCodes.InvalidEffectiveRange,[]);}
    private static bool Match(string code,string name,CatalogStatus status,CatalogSearch s)=>(s.Status is null||s.Status==status)&&(string.IsNullOrWhiteSpace(s.SearchText)||code.Contains(s.SearchText,StringComparison.OrdinalIgnoreCase)||name.Contains(s.SearchText,StringComparison.OrdinalIgnoreCase));
    private static bool Eq(string a,string b)=>StringComparer.OrdinalIgnoreCase.Equals(a.Trim(),b.Trim());
    private static void Required(string value,string field){if(string.IsNullOrWhiteSpace(value))throw new ArgumentException("Value is required.",field);}
    private static string? Optional(string? value)=>string.IsNullOrWhiteSpace(value)?null:value.Trim();
    private static PayComponentDto Dto(PayComponent d,CatalogVersion<PayComponentContent>v)=>new(d.Id,d.CompanyId,new(v.VersionNumber,v.EffectivePeriod,v.PublicationState,v.Content));
    private static CompensationSchemeDto Dto(CompensationScheme d,CatalogVersion<CompensationSchemeContent>v)=>new(d.Id,d.CompanyId,new(v.VersionNumber,v.EffectivePeriod,v.PublicationState,v.Content));
    private static void CrossCompany()=>Throw(DiagnosticCodes.CrossCompanySchemeComponent,[]);
    private static CompensationValidationException Error(string code,Dictionary<string,object?> args)=>new(new(code,DiagnosticSeverity.Error,args));
    private static void Throw(string code,Dictionary<string,object?> args)=>throw Error(code,args);
}
