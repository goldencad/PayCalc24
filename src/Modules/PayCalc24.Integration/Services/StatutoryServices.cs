using PayCalc24.Contracts.Diagnostics;
using PayCalc24.Contracts.Identity;
using PayCalc24.Contracts.Integration;

namespace PayCalc24.Integration.Services;

public sealed class StatutoryProviderRegistry(IEnumerable<(CompanyId CompanyId,IStatutoryProvider Provider)> providers)
    : IStatutoryProviderRegistry
{
    private readonly IReadOnlyList<(CompanyId CompanyId,IStatutoryProvider Provider)> providers=providers.ToArray();
    public IStatutoryProvider Resolve(CompanyId companyId,StatutoryProviderIdentity pinnedIdentity)
    {
        var provider=providers.SingleOrDefault(x=>x.CompanyId==companyId&&x.Provider.Identity==pinnedIdentity).Provider;
        return provider??throw Error(DiagnosticCodes.StatutoryProviderVersionUnavailable,
            ("provider",pinnedIdentity.ProviderCode),("version",pinnedIdentity.ProviderVersion),("policy",pinnedIdentity.PolicyVersion));
    }
    private static StatutoryIntegrationException Error(string code,params (string Key,object? Value)[] args)=>
        new(new(code,DiagnosticSeverity.Error,args.ToDictionary(x=>x.Key,x=>x.Value)));
}

public sealed class StatutoryCalculationService(IStatutoryProviderRegistry registry,IStatutoryResultRepository repository)
{
    public StatutoryCalculationResult Calculate(StatutoryCalculationRequest request)
    {
        ValidateHash(request);
        var existing=repository.Find(request.CompanyId,request.Provider,request.RequestHash);
        if(existing is not null)return existing;
        var provider=registry.Resolve(request.CompanyId,request.Provider);
        StatutoryCalculationResult result;
        try { result=provider.Calculate(request); }
        catch(Exception exception) when(exception is not StatutoryIntegrationException)
        {
            result=new(new(Guid.NewGuid()),request.CompanyId,request.PayrollPeriodId,request.SnapshotId,
                request.SnapshotRevision,request.CalculationRunId,request.PayrollSubjectId,StatutoryResultKind.Other,
                StatutoryResultStatus.Failed,request.Provider,request.BusinessDate,null,null,[],
                [new(DiagnosticCodes.StatutoryCalculationFailed,DiagnosticSeverity.Error,
                    new Dictionary<string,object?>{{"exceptionType",exception.GetType().Name}})],request.RequestHash,
                CanonicalHash.Create(request.RequestHash,StatutoryResultStatus.Failed),request.CorrelationId);
        }
        if(result.CompanyId!=request.CompanyId||result.PayrollSubjectId!=request.PayrollSubjectId||
           result.CalculationRunId!=request.CalculationRunId)
            throw Error(DiagnosticCodes.StatutoryCrossCompanyReference);
        repository.Add(result);
        return result;
    }
    public static string ComputeRequestHash(StatutoryCalculationRequest request)=>CanonicalHash.Create(
        request.CompanyId.Value,request.PayrollPeriodId.Value,request.SnapshotId.Value,request.SnapshotRevision,
        request.CalculationRunId.Value,request.PayrollSubjectId.Value,request.BusinessDate,request.ExecutionMode,
        request.EligibleDependentCount,request.Provider.JurisdictionCode,request.Provider.ProviderCode,
        request.Provider.ProviderVersion,request.Provider.PolicyVersion,
        string.Join(";",request.PayrollFacts.OrderBy(x=>x.Code,StringComparer.Ordinal).Select(x=>$"{x.Code}:{x.Amount:0.00000000}:{x.SourceResultId}:{x.FundedAmount:0.00000000}")),
        string.Join(";",request.Classifications.OrderBy(x=>x.Code,StringComparer.Ordinal).Select(x=>$"{x.Code}:{x.Value}:{x.SourceReference}")));
    private static void ValidateHash(StatutoryCalculationRequest request)
    { if(request.RequestHash!=ComputeRequestHash(request))throw Error(DiagnosticCodes.StatutoryRequestInvalid); }
    private static StatutoryIntegrationException Error(string code)=>new(new(code,DiagnosticSeverity.Error,new Dictionary<string,object?>()));
}

public sealed class InMemoryStatutoryResultRepository : IStatutoryResultRepository
{
    private readonly object gate=new();
    private readonly Dictionary<(CompanyId,string,string,string,string),StatutoryCalculationResult> values=[];
    public StatutoryCalculationResult? Find(CompanyId companyId,StatutoryProviderIdentity provider,string requestHash)
    { lock(gate)return values.GetValueOrDefault(Key(companyId,provider,requestHash)); }
    public void Add(StatutoryCalculationResult result)
    { lock(gate){var key=Key(result.CompanyId,result.Provider,result.RequestHash);if(values.TryGetValue(key,out var old)&&old.ResultHash!=result.ResultHash)throw Error();values.TryAdd(key,result);} }
    private static (CompanyId,string,string,string,string) Key(CompanyId companyId,StatutoryProviderIdentity provider,string hash)=>
        (companyId,provider.ProviderCode,provider.ProviderVersion,provider.PolicyVersion,hash);
    private static StatutoryIntegrationException Error()=>new(new(DiagnosticCodes.StatutoryIdempotencyConflict,DiagnosticSeverity.Error,new Dictionary<string,object?>()));
}
