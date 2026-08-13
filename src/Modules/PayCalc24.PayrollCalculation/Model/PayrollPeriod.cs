using PayCalc24.Contracts.Diagnostics;
using PayCalc24.Contracts.Identity;
using PayCalc24.Contracts.PayrollCalculation;
using PayCalc24.Contracts.PayrollInput;

namespace PayCalc24.PayrollCalculation.Model;

public sealed class PayrollCalculationException(Diagnostic diagnostic) : Exception(diagnostic.Code)
{
    public Diagnostic Diagnostic { get; } = diagnostic;
}

public sealed class PayrollPeriod
{
    public PayrollPeriod(PayrollPeriodId id, CompanyId companyId, string code, string? name,
        DateOnly periodStart, DateOnly periodEnd, DateOnly businessDate, DateOnly? paymentDate,
        DateTimeOffset createdAt, UserId createdBy)
    {
        ValidateDates(periodStart, periodEnd);
        Id=id; CompanyId=companyId; Code=NormalizeCode(code); Name=Optional(name); PeriodStart=periodStart;
        PeriodEnd=periodEnd; BusinessDate=businessDate; PaymentDate=paymentDate; CreatedAt=createdAt; CreatedBy=createdBy;
    }
    private PayrollPeriod() { }
    public PayrollPeriodId Id { get; private set; }
    public CompanyId CompanyId { get; private set; }
    public string Code { get; private set; } = "";
    public string? Name { get; private set; }
    public DateOnly PeriodStart { get; private set; }
    public DateOnly PeriodEnd { get; private set; }
    public DateOnly BusinessDate { get; private set; }
    public DateOnly? PaymentDate { get; private set; }
    public PayrollPeriodStatus LifecycleStatus { get; private set; } = PayrollPeriodStatus.DRAFT;
    public long Revision { get; private set; } = 1;
    public DateTimeOffset CreatedAt { get; private set; }
    public UserId CreatedBy { get; private set; }
    public DateTimeOffset? UpdatedAt { get; private set; } public UserId? UpdatedBy { get; private set; }
    public DateTimeOffset? PreparedAt { get; private set; } public UserId? PreparedBy { get; private set; }
    public DateTimeOffset? FrozenAt { get; private set; } public UserId? FrozenBy { get; private set; }
    public DateTimeOffset? CalculatedAt { get; private set; } public UserId? CalculatedBy { get; private set; }
    public DateTimeOffset? ClosedAt { get; private set; } public UserId? ClosedBy { get; private set; }
    public DateTimeOffset? ReopenedAt { get; private set; } public UserId? ReopenedBy { get; private set; }

    public void UpdateDraft(long expectedRevision, string? name, DateOnly start, DateOnly end,
        DateOnly businessDate, DateOnly? paymentDate, DateTimeOffset at, UserId by)
    {
        Expect(expectedRevision); Require(PayrollPeriodStatus.DRAFT); ValidateDates(start,end);
        Name=Optional(name); PeriodStart=start; PeriodEnd=end; BusinessDate=businessDate; PaymentDate=paymentDate;
        UpdatedAt=at; UpdatedBy=by; Revision++;
    }
    public void Transition(long expectedRevision, PayrollPeriodStatus target, DateTimeOffset at, UserId by)
    {
        Expect(expectedRevision);
        var valid = (LifecycleStatus,target) switch
        {
            (PayrollPeriodStatus.DRAFT,PayrollPeriodStatus.PREPARED) => true,
            (PayrollPeriodStatus.REOPENED,PayrollPeriodStatus.PREPARED) => true,
            (PayrollPeriodStatus.PREPARED,PayrollPeriodStatus.DRAFT) => true,
            (PayrollPeriodStatus.PREPARED,PayrollPeriodStatus.FROZEN) => true,
            (PayrollPeriodStatus.FROZEN,PayrollPeriodStatus.CALCULATED) => true,
            (PayrollPeriodStatus.CALCULATED,PayrollPeriodStatus.CLOSED) => true,
            (PayrollPeriodStatus.CLOSED,PayrollPeriodStatus.REOPENED) => true,
            _ => false
        };
        if(!valid) Throw(DiagnosticCodes.InvalidPayrollPeriodTransition,new(){["from"]=LifecycleStatus,["to"]=target});
        LifecycleStatus=target; Revision++;
        switch(target)
        {
            case PayrollPeriodStatus.PREPARED: PreparedAt=at; PreparedBy=by; break;
            case PayrollPeriodStatus.FROZEN: FrozenAt=at; FrozenBy=by; break;
            case PayrollPeriodStatus.CALCULATED: CalculatedAt=at; CalculatedBy=by; break;
            case PayrollPeriodStatus.CLOSED: ClosedAt=at; ClosedBy=by; break;
            case PayrollPeriodStatus.REOPENED: ReopenedAt=at; ReopenedBy=by; break;
        }
    }
    public PayrollPeriodDto ToDto()=>new(Id,CompanyId,Code,Name,PeriodStart,PeriodEnd,BusinessDate,PaymentDate,
        LifecycleStatus,Revision,CreatedAt,CreatedBy,UpdatedAt,UpdatedBy,PreparedAt,PreparedBy,FrozenAt,FrozenBy,
        CalculatedAt,CalculatedBy,ClosedAt,ClosedBy,ReopenedAt,ReopenedBy);
    internal void Expect(long revision){if(revision!=Revision)Throw(DiagnosticCodes.PayrollPeriodConcurrencyConflict,new(){["expectedRevision"]=revision,["actualRevision"]=Revision});}
    private void Require(PayrollPeriodStatus status){if(LifecycleStatus!=status)Throw(DiagnosticCodes.InvalidPayrollPeriodTransition,new(){["from"]=LifecycleStatus,["required"]=status});}
    public static string NormalizeCode(string code)=>string.IsNullOrWhiteSpace(code)?throw new ArgumentException("Code is required.",nameof(code)):code.Trim().ToUpperInvariant();
    private static string? Optional(string? value)=>string.IsNullOrWhiteSpace(value)?null:value.Trim();
    private static void ValidateDates(DateOnly start,DateOnly end){if(start>end)Throw(DiagnosticCodes.InvalidPayrollPeriodDateRange,new(){["periodStart"]=start,["periodEnd"]=end});}
    internal static PayrollCalculationException Error(string code,Dictionary<string,object?> args)=>new(new(code,DiagnosticSeverity.Error,args));
    internal static void Throw(string code,Dictionary<string,object?> args)=>throw Error(code,args);
}
