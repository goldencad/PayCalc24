using PayCalc24.Attendance.Services;
using PayCalc24.Contracts.Attendance;
using PayCalc24.Contracts.Diagnostics;
using PayCalc24.Contracts.Identity;
using PayCalc24.Contracts.Operations;
using PayCalc24.Contracts.Organization;
using PayCalc24.Contracts.PayrollInput;
using PayCalc24.Contracts.Temporal;

#pragma warning disable CA1725

namespace PayCalc24.ApplicationTests;

public sealed class Task12AttendanceTests
{
 [Fact]
 public async Task DifferentLayoutsNormalizeAndExplicitAlternativePolicyBackTestsWithoutPublishing()
 {
  var f=new Fixture();var source=await f.Service.CreateSourceAsync(f.Company,"GENERIC","Generic",AttendanceSourceKind.FILE,"UTC");
  var m1=await f.Mapping(source.Id,new("employeeCode","employeeCode",true),new("businessDate","date",true),new("kind","kind",true),new("quantity","hours"),new("unit","unit",true));
  var m2=await f.Mapping(source.Id,new("employeeCode","staffNo",true),new("businessDate","day",true),new("kind","fact",true),new("quantity","points"),new("unit","measure",true));
  var p1=await f.Policy("BASE",1m);var p2=await f.Policy("ALT",2m);
  var a=await f.Service.PreviewAsync(f.Command(source.Id,m1.Id,p1.Id,"one",new Dictionary<string,string?>{{"employeeCode","E01"},{"date","2026-08-01"},{"kind","ActualWork"},{"hours","8.5"},{"unit","HOURS"}}));
  var b=await f.Service.PreviewAsync(f.Command(source.Id,m2.Id,p1.Id,"two",new Dictionary<string,string?>{{"staffNo","E01"},{"day","2026-08-01"},{"fact","ActualWork"},{"points","8.5"},{"measure","HOURS"}}));
  Assert.Equal(a.DerivedInputs.Single().Value,b.DerivedInputs.Single().Value);var replay=f.Service.Evaluate(f.Company,a.BatchId,p1.Id);Assert.Equal(a.DerivedInputs.Single().ResultHash,replay.DerivedInputs.Single().ResultHash);
  var backtest=f.Service.Evaluate(f.Company,a.BatchId,p2.Id);Assert.Equal(17m,backtest.DerivedInputs.Single().Value.DecimalValue);Assert.Empty(f.Ledger.Submissions);
 }

 [Fact]
 public async Task BlockingRowsPreventCommitAndIdempotencyConflictIsStable()
 {
  var f=new Fixture();var source=await f.Service.CreateSourceAsync(f.Company,"S","S",AttendanceSourceKind.API,"UTC");var map=await f.Mapping(source.Id,new("employeeCode","employeeCode",true),new("businessDate","date",true),new("kind","kind",true),new("quantity","q"),new("unit","unit",true));var policy=await f.Policy("P",1m);
  var command=f.Command(source.Id,map.Id,policy.Id,"same",new Dictionary<string,string?>{{"employeeCode","UNKNOWN"},{"date","2026-08-01"},{"kind","ActualWork"},{"q","8"},{"unit","HOURS"}});var preview=await f.Service.PreviewAsync(command);Assert.Contains(preview.Diagnostics,x=>x.Code==DiagnosticCodes.AttendanceUnknownPayrollSubject);
  var ex=await Assert.ThrowsAsync<AttendanceException>(async()=>await f.Service.CommitAsync(new(f.Company,preview.BatchId,"same")));Assert.Equal(DiagnosticCodes.AttendanceBlockingValidationFailed,ex.Diagnostic.Code);
  var changed=command with{Rows=[new("row",new Dictionary<string,string?>{{"employeeCode","E01"},{"date","2026-08-01"},{"kind","ActualWork"},{"q","9"},{"unit","HOURS"}})]};var conflict=await Assert.ThrowsAsync<AttendanceException>(async()=>await f.Service.PreviewAsync(changed));Assert.Equal(DiagnosticCodes.AttendanceImportIdempotencyConflict,conflict.Diagnostic.Code);
 }

 [Fact]
 public async Task CommitPublishesViaLedgerAndCorrectionUsesSupersession()
 {
  var f=new Fixture();var source=await f.Service.CreateSourceAsync(f.Company,"S","S",AttendanceSourceKind.FILE,"UTC");var map=await f.Mapping(source.Id,new("employeeCode","employeeCode",true),new("businessDate","date",true),new("kind","kind",true),new("quantity","q"),new("unit","unit",true));var policy=await f.Policy("P",1m);var preview=await f.Service.PreviewAsync(f.Command(source.Id,map.Id,policy.Id,"first",new Dictionary<string,string?>{{"employeeCode","E01"},{"date","2026-08-01"},{"kind","ActualWork"},{"q","8"},{"unit","HOURS"}}));var committed=await f.Service.CommitAsync(new(f.Company,preview.BatchId,"first"));Assert.Single(committed.LedgerEntries);Assert.Single(f.Ledger.Submissions);
  var correction=await f.Service.PreviewAsync(f.Command(source.Id,map.Id,policy.Id,"correction",new Dictionary<string,string?>{{"employeeCode","E01"},{"date","2026-08-01"},{"kind","ActualWork"},{"q","9"},{"unit","HOURS"}}));var key=$"{f.Subject.Value:D}|2026-08-01|ANY_HOURS";await f.Service.CommitAsync(new(f.Company,correction.BatchId,"correction",new Dictionary<string,PayrollInputLedgerEntryId>{{key,committed.LedgerEntries[0].Id}}));Assert.Single(f.Ledger.Corrections);Assert.Equal(committed.LedgerEntries[0].Id,f.Ledger.Corrections[0].SupersedesEntryId);
 }

 private sealed class Fixture
 {
  public CompanyId Company{get;}=CompanyId.From(Guid.NewGuid());public PayrollSubjectId Subject{get;}=PayrollSubjectId.From(Guid.NewGuid());public FakeLedger Ledger{get;}=new();public AttendanceService Service{get;}
  public Fixture(){var context=new Context(Company);Service=new(context,new User(),new Correlation(),new Audit(),TimeProvider.System,new Subjects(Company,Subject),new Definitions(Company),Ledger);}
  public async Task<AttendanceMappingVersionDto> Mapping(AttendanceSourceId id,params AttendanceFieldMap[] fields){var d=await Service.CreateMappingDraftAsync(Company,id,fields);return await Service.PublishMappingAsync(Company,d.Id);}
  public async Task<AttendancePolicyVersionDto> Policy(string code,decimal factor){var d=await Service.CreatePolicyDraftAsync(Company,code,new(new(2026,1,1),new(2027,1,1)),[new(AttendanceFactKind.ActualWork,AttendanceQuantityUnit.HOURS,"ANY_HOURS",PayrollInputDataType.DECIMAL,PayrollInputUnitType.HOURS,Factor:factor)]);return await Service.PublishPolicyAsync(Company,d.Id);}
  public PreviewAttendanceImport Command(AttendanceSourceId s,AttendanceMappingVersionId m,AttendancePolicyVersionId p,string key,Dictionary<string,string?> values)=>new(Company,s,m,p,PayrollPeriodId.From(Guid.NewGuid()),new(2026,8,1),new(2026,8,31),"source",key,[new("row",values)]);
 }
 private sealed record Context(CompanyId CompanyId):ICompanyContext;private sealed class User:ICurrentUser{public UserId UserId{get;}=UserId.From(Guid.NewGuid());public bool HasPermission(string permissionCode)=>true;}private sealed class Correlation:ICorrelationContext{public string CorrelationId=>"corr";public string? IdempotencyKey=>null;}private sealed class Audit:IAuditWriter{public ValueTask WriteAsync(AuditEntry entry,CancellationToken cancellationToken=default)=>ValueTask.CompletedTask;}
 private sealed record Subjects(CompanyId Company,PayrollSubjectId Subject):IAttendancePayrollSubjectResolver{public PayrollSubjectId? ResolveByEmployeeCode(CompanyId c,string code)=>c==Company&&code=="E01"?Subject:null;public CompanyId? FindCompany(PayrollSubjectId id)=>id==Subject?Company:null;}
 private sealed record Definitions(CompanyId Company):IPayrollInputDefinitionService
 {private PayrollInputDefinitionDto Dto=>new(PayrollInputDefinitionId.From(Guid.Parse("10000000-0000-0000-0000-000000000001")),Company,1,new(new(2026,1,1),null),PublicationState.PUBLISHED,new("ANY_HOURS","Any",null,PayrollInputDataType.DECIMAL,PayrollInputUnitType.HOURS,PayrollInputSourceType.ATTENDANCE,PayrollInputAggregationType.SUM,false,false,true,true,null,null,PayrollInputDefinitionStatus.ACTIVE));public PayrollInputDefinitionDto ResolveEffective(CompanyId c,string code,DateOnly d)=>c==Company&&code=="ANY_HOURS"?Dto:throw new InvalidOperationException();public PayrollInputDefinitionDto ResolveEffective(CompanyId c,PayrollInputDefinitionId id,DateOnly d)=>Dto;public PayrollInputDefinitionDto GetByCode(CompanyId c,string code,int r)=>Dto;public ValueTask<PayrollInputDefinitionDto>CreateDraftAsync(CompanyId c,PayrollInputDefinitionId id,EffectivePeriod p,PayrollInputDefinitionContent content,CancellationToken t=default)=>throw new NotSupportedException();public ValueTask<PayrollInputDefinitionDto>UpdateDraftAsync(CompanyId c,PayrollInputDefinitionId id,int r,EffectivePeriod p,PayrollInputDefinitionContent content,CancellationToken t=default)=>throw new NotSupportedException();public ValueTask<PayrollInputDefinitionDto>PublishAsync(CompanyId c,PayrollInputDefinitionId id,int r,CancellationToken t=default)=>throw new NotSupportedException();public void Close(CompanyId c,PayrollInputDefinitionId id,int r,DateOnly d)=>throw new NotSupportedException();public IReadOnlyList<PayrollInputDefinitionDto>List(CompanyId c,PayrollInputDefinitionSearch s)=>[Dto];}
 public sealed class FakeLedger:IPayrollInputLedgerService
 {public List<SubmitPayrollInput> Submissions{get;}=[];public List<SubmitPayrollInputCorrection> Corrections{get;}=[];public ValueTask<PayrollInputLedgerEntryDto>SubmitAsync(SubmitPayrollInput c,CancellationToken t=default){Submissions.Add(c);return ValueTask.FromResult(Make(c,null));}public ValueTask<PayrollInputLedgerEntryDto>CorrectAsync(SubmitPayrollInputCorrection c,CancellationToken t=default){Corrections.Add(c);var original=Submissions[0];return ValueTask.FromResult(Make(original,c.SupersedesEntryId,c.Value));}private static PayrollInputLedgerEntryDto Make(SubmitPayrollInput c,PayrollInputLedgerEntryId? sup,PayrollInputValue? value=null)=>new(PayrollInputLedgerEntryId.From(Guid.NewGuid()),c.CompanyId,c.PayrollSubjectId,c.PayrollPeriodId,c.BusinessDate,PayrollInputDefinitionId.From(Guid.Parse("10000000-0000-0000-0000-000000000001")),1,"ANY_HOURS",value??c.Value,PayrollInputDataType.DECIMAL,PayrollInputUnitType.HOURS,PayrollInputAggregationType.SUM,PayrollInputSourceType.ATTENDANCE,c.SourceSystem,c.SourceReference,null,c.EffectiveDate,DateTimeOffset.UtcNow,null,c.CorrelationId??"corr",c.IdempotencyKey,sup);public EffectivePayrollInputDto GetEffectiveInput(CompanyId c,PayrollSubjectId s,PayrollPeriodId p,PayrollInputDefinitionId d)=>throw new NotSupportedException();public IReadOnlyList<EffectivePayrollInputDto>GetEffectiveInputSet(CompanyId c,PayrollSubjectId s,PayrollPeriodId p)=>[];public IReadOnlyList<PayrollInputLedgerEntryDto>GetHistory(CompanyId c,PayrollSubjectId s,PayrollPeriodId p,PayrollInputDefinitionId? d=null)=>[];public PayrollInputSourceTrace GetSourceTrace(CompanyId c,PayrollInputLedgerEntryId e)=>throw new NotSupportedException();public PayrollInputLedgerEntryDto? ResolveByIdempotencyKey(CompanyId c,string key)=>null;}
}
