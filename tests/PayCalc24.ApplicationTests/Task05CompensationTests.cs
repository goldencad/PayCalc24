using System.Globalization;
using PayCalc24.Compensation.Model;
using PayCalc24.Compensation.Services;
using PayCalc24.Contracts.Compensation;
using PayCalc24.Contracts.Diagnostics;
using PayCalc24.Contracts.Identity;
using PayCalc24.Contracts.Temporal;

namespace PayCalc24.ApplicationTests;

#pragma warning disable CA1861

public sealed class Task05CompensationTests
{
    private static readonly DateOnly Jan1=new(2027,1,1),Jul1=new(2027,7,1);

    [Fact] public void CreatesArbitraryPayComponentDraft(){var f=new Fixture();var result=f.Component("BASE");Assert.Equal("BASE",result.Version.Content.Code);Assert.Equal(PublicationState.DRAFT,result.Version.PublicationState);}
    [Fact] public void DuplicateCodeWithinCompanyIsRejectedCaseInsensitively(){var f=new Fixture();f.Component("BASE");var ex=Assert.Throws<CompensationValidationException>(()=>f.Component("base"));Assert.Equal(DiagnosticCodes.DuplicatePayComponentCode,ex.Diagnostic.Code);}
    [Fact] public void SameCodeIsAllowedByIndependentCompanyScopedCatalogs(){var a=new Fixture();var b=new Fixture();Assert.Equal("BASE",a.Component("BASE").Version.Content.Code);Assert.Equal("BASE",b.Component("BASE").Version.Content.Code);}
    [Fact] public void PublishedComponentIsImmutable(){var f=new Fixture();var c=f.Component("BASE");f.Service.PublishPayComponent(f.CompanyId,c.Id,1);var ex=Assert.Throws<CompensationValidationException>(()=>f.Service.UpdatePayComponentDraft(f.CompanyId,c.Id,1,new(Jan1,null),f.Content("CHANGED")));Assert.Equal(DiagnosticCodes.PublishedConfigurationImmutable,ex.Diagnostic.Code);}
    [Fact] public void HistoricalAndFutureComponentVersionsResolveByBusinessDate(){var f=new Fixture();var c=f.Component("BASE",new(Jan1,null));f.Service.PublishPayComponent(f.CompanyId,c.Id,1);f.Service.ClosePayComponent(f.CompanyId,c.Id,1,Jul1);f.Service.CreatePayComponentDraft(f.CompanyId,c.Id,new(Jul1,null),f.Content("BASE","Base v2"));f.Service.PublishPayComponent(f.CompanyId,c.Id,2);Assert.Equal("Base BASE",f.Service.GetEffectivePayComponent(f.CompanyId,"BASE",Jan1).Version.Content.Name);Assert.Equal("Base v2",f.Service.GetEffectivePayComponent(f.CompanyId,"BASE",Jul1).Version.Content.Name);}
    [Fact] public void SchemeOrdersArbitraryComponentsDeterministicallyAndRejectsDuplicates(){var f=new Fixture();var b=f.Component("BASE");var a=f.Component("ATTENDANCE");var bonus=f.Component("BONUS_X");var scheme=f.Scheme("DYNAMIC",[(bonus.Id,30),(b.Id,10),(a.Id,20)]);Assert.Equal(["BASE","ATTENDANCE","BONUS_X"],scheme.Version.Content.Components.Select(x=>f.Code(x.PayComponentId)));var ex=Assert.Throws<CompensationValidationException>(()=>f.Service.AddSchemeComponent(f.CompanyId,scheme.Id,1,new(CompensationSchemeComponentId.From(Guid.NewGuid()),b.Id,40,true,null,null,CatalogStatus.ACTIVE)));Assert.Equal(DiagnosticCodes.DuplicateSchemeComponent,ex.Diagnostic.Code);}
    [Fact] public void PLabelsAndBusinessLabelsUseExactlyTheSameModel(){var f=new Fixture();var dynamicIds=new[]{f.Component("BASE").Id,f.Component("ATTENDANCE").Id,f.Component("BONUS_X").Id};var pIds=new[]{f.Component("P1").Id,f.Component("P2").Id,f.Component("P3").Id};var dynamic=f.Scheme("DYNAMIC",dynamicIds.Select((x,i)=>(x,(i+1)*10)).ToArray());var threeP=f.Scheme("THREE_P",pIds.Select((x,i)=>(x,(i+1)*10)).ToArray());Assert.Equal(3,dynamic.Version.Content.Components.Count);Assert.Equal(3,threeP.Version.Content.Components.Count);}
    [Fact] public void PublishedSchemeHistoryRemainsResolvableAfterFutureVersion(){var f=new Fixture();var basePay=f.Component("BASE");var bonus=f.Component("BONUS_X");var s=f.Scheme("STANDARD",[(basePay.Id,10)],new(Jan1,null));f.Service.PublishScheme(f.CompanyId,s.Id,1);f.Service.CloseScheme(f.CompanyId,s.Id,1,Jul1);var v2=f.Service.CreateSchemeDraft(f.CompanyId,s.Id,new(Jul1,null),new("STANDARD","Standard v2",null,CatalogStatus.ACTIVE,[f.Item(basePay.Id,10),f.Item(bonus.Id,20)]));f.Service.PublishScheme(f.CompanyId,s.Id,v2.Version.VersionNumber);Assert.Single(f.Service.ResolveEffectiveScheme(f.CompanyId,"STANDARD",Jan1).Version.Content.Components);Assert.Equal(2,f.Service.ResolveEffectiveScheme(f.CompanyId,"STANDARD",Jul1).Version.Content.Components.Count);}
    [Theory][InlineData("en-US")][InlineData("vi-VN")] public void CultureDoesNotChangeCodesMembershipOrderOrResolution(string culture){var old=CultureInfo.CurrentCulture;try{CultureInfo.CurrentCulture=new(culture);var f=new Fixture();var p1=f.Component("P1");var p2=f.Component("P2");var s=f.Scheme("3P",[(p2.Id,20),(p1.Id,10)]);f.Service.PublishScheme(f.CompanyId,s.Id,1);var effective=f.Service.ResolveEffectiveScheme(f.CompanyId,"3P",Jan1);Assert.Equal("3P",effective.Version.Content.Code);Assert.Equal([10,20],effective.Version.Content.Components.Select(x=>x.Sequence));}finally{CultureInfo.CurrentCulture=old;}}
    [Fact] public void CoreDoesNotDefineBusinessSpecificPEnumsOrFormulaExecution(){var assembly=typeof(CompensationCatalogService).Assembly;Assert.DoesNotContain(assembly.GetTypes(),x=>x.IsEnum&&x.Name.Contains("P1",StringComparison.OrdinalIgnoreCase));Assert.DoesNotContain(assembly.GetTypes(),x=>x.Name.Contains("FormulaEngine",StringComparison.OrdinalIgnoreCase)||x.Name.Contains("Evaluator",StringComparison.OrdinalIgnoreCase));}

    private sealed class Fixture
    {
        public CompanyId CompanyId{get;}=CompanyId.From(Guid.NewGuid()); public CompensationCatalogService Service{get;} private readonly Dictionary<PayComponentId,string> codes=[];
        public Fixture(){Service=new(new Context(CompanyId));}
        public PayComponentContent Content(string code,string? name=null)=>new(code,name??$"Base {code}",null,PayComponentType.FIXED,CalculationMethod.INPUT,null,null,true,false,false,true,true,true,10,CatalogStatus.ACTIVE);
        public PayComponentDto Component(string code,EffectivePeriod? period=null){var dto=Service.CreatePayComponentDraft(CompanyId,PayComponentId.From(Guid.NewGuid()),period??new(Jan1,null),Content(code));codes[dto.Id]=code;return dto;}
        public string Code(PayComponentId id)=>codes[id];
        public SchemeComponentContent Item(PayComponentId id,int sequence)=>new(CompensationSchemeComponentId.From(Guid.NewGuid()),id,sequence,true,null,null,CatalogStatus.ACTIVE);
        public CompensationSchemeDto Scheme(string code,(PayComponentId Id,int Sequence)[] items,EffectivePeriod? period=null)=>Service.CreateSchemeDraft(CompanyId,CompensationSchemeId.From(Guid.NewGuid()),period??new(Jan1,null),new(code,code,null,CatalogStatus.ACTIVE,items.Select(x=>Item(x.Id,x.Sequence)).ToArray()));
    }
    private sealed record Context(CompanyId CompanyId):ICompanyContext;
}
