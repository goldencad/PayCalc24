using System.Security.Cryptography;
using System.Text;
using PayCalc24.Contracts.Diagnostics;
using PayCalc24.Contracts.Identity;
using PayCalc24.Contracts.Operations;
using PayCalc24.Contracts.PayrollApproval;
using PayCalc24.Contracts.PayrollCalculation;
using PayCalc24.Contracts.PayrollReview;
using PayCalc24.Contracts.PayrollInput;

namespace PayCalc24.PayrollApproval.Services;

public sealed class PayrollApprovalService(ICompanyContext company, ICurrentUser user,
    ICorrelationContext correlation, IPayrollApprovalAuthorization authorization,
    IPayrollApprovalRepository repository, IPayrollApprovalArtifactSource artifacts,
    IPayrollReviewService review, IPayrollRevisionOrchestrator revisions,
    IPayrollPeriodCloseBoundary periodClose, Func<DateTimeOffset>? clock = null) : IPayrollApprovalService
{
    private DateTimeOffset Now()=>clock?.Invoke() ?? DateTimeOffset.UtcNow;

    public PayrollApprovalCaseDto Create(CreateApprovalCaseCommand command)
    {
        RequireKey(command.IdempotencyKey); ValidateArtifacts(command.Artifacts);
        var fingerprint=Hash("CREATE", ArtifactFingerprint(command.Artifacts), command.SupersedesApprovalCaseId?.Value);
        var existing=repository.GetByIdempotency(company.CompanyId,"CREATE",command.IdempotencyKey);
        if(existing is not null) return Same(existing.ApprovalFingerprint==ArtifactFingerprint(command.Artifacts) && existing.SupersedesApprovalCaseId==command.SupersedesApprovalCaseId,existing);
        if(command.SupersedesApprovalCaseId is { } prior) Require(prior);
        var at=Now(); var actor=user.UserId; var fp=ArtifactFingerprint(command.Artifacts);
        var value=new PayrollApprovalCaseDto(PayrollApprovalCaseId.From(Guid.NewGuid()),company.CompanyId,
            command.Artifacts.PayrollPeriodId,command.Artifacts.SnapshotId,command.Artifacts.SnapshotRevision,
            command.Artifacts.SnapshotHash,command.Artifacts.CalculationRunId,command.Artifacts.CalculationResultHash,
            command.Artifacts.FundResultHashes.Order(StringComparer.Ordinal).ToArray(),command.Artifacts.ReviewContextFingerprint,fp,
            PayrollApprovalStatus.DRAFT,1,command.SupersedesApprovalCaseId,at,actor,
            SubmittedAt:null,SubmittedBy:null,ReviewStartedAt:null,ReviewedBy:null,ApprovedAt:null,ApprovedBy:null,
            RejectedAt:null,RejectedBy:null,LockedAt:null,LockedBy:null,CurrentDecisionReason:null,CorrelationId:correlation.CorrelationId);
        repository.Add(value,"CREATE",command.IdempotencyKey,fingerprint); return value;
    }

    public PayrollApprovalCaseDto Get(PayrollApprovalCaseId id)=>Require(id);
    public PayrollApprovalCaseDto Submit(ApprovalTransitionCommand c)
    {
        authorization.Demand(company.CompanyId,user.UserId,PayrollApprovalPermission.PAYROLL_SUBMIT);
        var value=Require(c.ApprovalCaseId); var context=Context(value); var current=artifacts.GetAuthoritativeArtifacts(context);
        ValidatePinned(value,current);
        var validation=review.GetPayrollValidationSummary(context);
        if(!current.CalculationSucceeded || !current.RequiredFundsComplete || validation.Blocking)
            throw Error(DiagnosticCodes.PayrollApprovalSubmitBlocked,("blocking",validation.Blocking));
        return Transition(value,c,PayrollApprovalStatus.DRAFT,PayrollApprovalStatus.SUBMITTED,null);
    }
    public PayrollApprovalCaseDto StartReview(ApprovalTransitionCommand c)
    { authorization.Demand(company.CompanyId,user.UserId,PayrollApprovalPermission.PAYROLL_REVIEW); return Transition(Require(c.ApprovalCaseId),c,PayrollApprovalStatus.SUBMITTED,PayrollApprovalStatus.IN_REVIEW,null); }
    public PayrollApprovalCaseDto Approve(ApprovalTransitionCommand c)
    {
        authorization.Demand(company.CompanyId,user.UserId,PayrollApprovalPermission.PAYROLL_APPROVE);
        var value=Require(c.ApprovalCaseId); authorization.ValidateDecisionActors(value,user.UserId);
        var current=artifacts.GetAuthoritativeArtifacts(Context(value)); ValidatePinned(value,current);
        if(!artifacts.IsLatestProductionRevision(current)) throw Error(DiagnosticCodes.PayrollApprovalStaleCase,("snapshotRevision",value.SnapshotRevision));
        if(review.GetPayrollValidationSummary(Context(value)).Blocking) throw Error(DiagnosticCodes.PayrollApprovalApprovalBlocked,("approvalCaseId",value.Id.Value));
        return Transition(value,c,PayrollApprovalStatus.IN_REVIEW,PayrollApprovalStatus.APPROVED,Optional(c.Reason));
    }
    public PayrollApprovalCaseDto Reject(ApprovalTransitionCommand c)
    {
        authorization.Demand(company.CompanyId,user.UserId,PayrollApprovalPermission.PAYROLL_REVIEW);
        var reason=Required(c.Reason,DiagnosticCodes.PayrollApprovalRejectionReasonRequired);
        return Transition(Require(c.ApprovalCaseId),c,PayrollApprovalStatus.IN_REVIEW,PayrollApprovalStatus.REJECTED,reason);
    }
    public PayrollApprovalCaseDto Lock(ApprovalTransitionCommand c)
    {
        authorization.Demand(company.CompanyId,user.UserId,PayrollApprovalPermission.PAYROLL_LOCK);
        var value=Require(c.ApprovalCaseId); if(value.Status==PayrollApprovalStatus.LOCKED)return value;
        var current=artifacts.GetAuthoritativeArtifacts(Context(value)); ValidatePinned(value,current);
        if(!artifacts.IsLatestProductionRevision(current)) throw Error(DiagnosticCodes.PayrollApprovalStaleCase,("snapshotRevision",value.SnapshotRevision));
        var result=Transition(value,c,PayrollApprovalStatus.APPROVED,PayrollApprovalStatus.LOCKED,Optional(c.Reason));
        periodClose.CloseCalculatedPeriod(company.CompanyId,value.PayrollPeriodId); return result;
    }
    public PayrollValidationSummary GetExactReviewContext(PayrollApprovalCaseId id)=>review.GetPayrollValidationSummary(Context(Require(id)));
    public IReadOnlyList<PayrollApprovalEventDto> GetHistory(PayrollApprovalCaseId id){Require(id);return repository.History(company.CompanyId,id);}
    public IReadOnlyList<PayrollApprovalCaseDto> ListRevisions(PayrollPeriodId id)=>repository.List(company.CompanyId,id).OrderBy(x=>x.SnapshotRevision).ToArray();
    public PayrollApprovalCaseDto? GetCurrentAuthoritative(PayrollPeriodId id)=>repository.List(company.CompanyId,id).Where(x=>x.Status==PayrollApprovalStatus.LOCKED).OrderByDescending(x=>x.SnapshotRevision).FirstOrDefault();

    public PayrollAdjustmentRequestDto RequestAdjustment(RequestAdjustmentCommand command)
    {
        authorization.Demand(company.CompanyId,user.UserId,PayrollApprovalPermission.PAYROLL_ADJUST); RequireKey(command.IdempotencyKey);
        var reason=Required(command.Reason,DiagnosticCodes.PayrollApprovalAdjustmentReasonRequired); var source=Require(command.SourceApprovalCaseId);
        if(source.Status is not (PayrollApprovalStatus.APPROVED or PayrollApprovalStatus.REJECTED or PayrollApprovalStatus.LOCKED))
            throw Error(DiagnosticCodes.PayrollApprovalInvalidTransition,("from",source.Status));
        var fp=Hash(source.Id.Value,command.AdjustmentType,reason); var old=repository.GetAdjustmentByIdempotency(company.CompanyId,"REQUEST_ADJUSTMENT",command.IdempotencyKey);
        if(old is not null) return SameAdjustment(Hash(old.SourceApprovalCaseId.Value,old.AdjustmentType,old.Reason)==fp,old);
        var value=new PayrollAdjustmentRequestDto(PayrollAdjustmentRequestId.From(Guid.NewGuid()),company.CompanyId,source.PayrollPeriodId,
            source.Id,source.SnapshotId,source.CalculationRunId,command.AdjustmentType,PayrollAdjustmentStatus.REQUESTED,reason,user.UserId,Now(),null,null,null,null,null,correlation.CorrelationId,1);
        repository.AddAdjustment(value,"REQUEST_ADJUSTMENT",command.IdempotencyKey,fp); return value;
    }
    public PayrollAdjustmentRequestDto AuthorizeAdjustment(PayrollAdjustmentRequestId id,long expectedRevision,string key)
    {
        authorization.Demand(company.CompanyId,user.UserId,PayrollApprovalPermission.PAYROLL_ADJUST); RequireKey(key);
        var value=RequireAdjustment(id); if(value.Status==PayrollAdjustmentStatus.AUTHORIZED) return value;
        if(value.Revision!=expectedRevision) throw Error(DiagnosticCodes.PayrollApprovalConcurrencyConflict,("expectedRevision",expectedRevision),("actualRevision",value.Revision));
        if(value.Status!=PayrollAdjustmentStatus.REQUESTED) throw Error(DiagnosticCodes.PayrollApprovalInvalidTransition,("from",value.Status));
        var next=value with{Status=PayrollAdjustmentStatus.AUTHORIZED,AuthorizedBy=user.UserId,AuthorizedAt=Now(),Revision=value.Revision+1};
        repository.UpdateAdjustment(next,expectedRevision,"AUTHORIZE_ADJUSTMENT",key,Hash(id.Value)); return next;
    }
    public PayrollAdjustmentRequestDto StartNewRevision(StartRevisionCommand command)
    {
        authorization.Demand(company.CompanyId,user.UserId,PayrollApprovalPermission.PAYROLL_ADJUST); RequireKey(command.IdempotencyKey);
        var value=RequireAdjustment(command.AdjustmentRequestId);
        if(value.Status!=PayrollAdjustmentStatus.AUTHORIZED) throw Error(DiagnosticCodes.PayrollApprovalAdjustmentNotAuthorized,("status",value.Status));
        if(value.Revision!=command.ExpectedRevision) throw Error(DiagnosticCodes.PayrollApprovalConcurrencyConflict,("expectedRevision",command.ExpectedRevision),("actualRevision",value.Revision));
        var started=revisions.StartNewRevision(value);
        if(started.SnapshotRevision<=Require(value.SourceApprovalCaseId).SnapshotRevision) throw Error(DiagnosticCodes.PayrollApprovalApprovalBlocked,("reason","revision-must-increase"));
        var nextCase=Create(new(started.Artifacts,value.SourceApprovalCaseId,command.IdempotencyKey+":approval-case"));
        var next=value with{Status=PayrollAdjustmentStatus.REVISION_STARTED,NewSnapshotId=started.SnapshotId,NewSnapshotRevision=started.SnapshotRevision,NewApprovalCaseId=nextCase.Id,Revision=value.Revision+1};
        repository.UpdateAdjustment(next,value.Revision,"START_REVISION",command.IdempotencyKey,Hash(value.Id.Value,started.SnapshotId.Value,started.SnapshotRevision)); return next;
    }

    private PayrollApprovalCaseDto Transition(PayrollApprovalCaseDto value,ApprovalTransitionCommand c,PayrollApprovalStatus from,PayrollApprovalStatus to,string? reason)
    {
        RequireKey(c.IdempotencyKey); if(value.Status==to) return value;
        if(value.Revision!=c.ExpectedRevision) throw Error(DiagnosticCodes.PayrollApprovalConcurrencyConflict,("expectedRevision",c.ExpectedRevision),("actualRevision",value.Revision));
        if(value.Status!=from) throw Error(DiagnosticCodes.PayrollApprovalInvalidTransition,("from",value.Status),("to",to));
        var at=Now(); var actor=user.UserId; var next=value with{Status=to,Revision=value.Revision+1,CurrentDecisionReason=reason};
        next=to switch{PayrollApprovalStatus.SUBMITTED=>next with{SubmittedAt=at,SubmittedBy=actor},PayrollApprovalStatus.IN_REVIEW=>next with{ReviewStartedAt=at,ReviewedBy=actor},PayrollApprovalStatus.APPROVED=>next with{ApprovedAt=at,ApprovedBy=actor},PayrollApprovalStatus.REJECTED=>next with{RejectedAt=at,RejectedBy=actor},PayrollApprovalStatus.LOCKED=>next with{LockedAt=at,LockedBy=actor},_=>next};
        var fp=Hash(value.Id.Value,from,to,reason);
        repository.Update(next,value.Revision,to.ToString(),c.IdempotencyKey,fp);
        repository.Append(new(PayrollApprovalEventId.From(Guid.NewGuid()),value.Id,company.CompanyId,from,to,actor,at,reason,correlation.CorrelationId,c.IdempotencyKey)); return next;
    }
    private PayrollApprovalCaseDto Require(PayrollApprovalCaseId id)=>repository.Get(company.CompanyId,id)??throw Error(DiagnosticCodes.PayrollApprovalCaseNotFound,("approvalCaseId",id.Value));
    private PayrollAdjustmentRequestDto RequireAdjustment(PayrollAdjustmentRequestId id)=>repository.GetAdjustment(company.CompanyId,id)??throw Error(DiagnosticCodes.PayrollApprovalCaseNotFound,("adjustmentRequestId",id.Value));
    private static ReviewResultContext Context(PayrollApprovalCaseDto value)=>new(value.CompanyId,value.PayrollPeriodId,value.SnapshotId,value.CalculationRunId);
    private void ValidateArtifacts(ApprovalArtifactContext value){if(value.CompanyId!=company.CompanyId)throw Error(DiagnosticCodes.PayrollApprovalCrossCompanyReference,("companyId",value.CompanyId.Value));if(value.ExecutionMode!=PayrollExecutionMode.Production)throw Error(DiagnosticCodes.PayrollApprovalNonProductionResult,("executionMode",value.ExecutionMode));}
    private static void ValidatePinned(PayrollApprovalCaseDto value,ApprovalArtifactContext current){if(value.SnapshotHash!=current.SnapshotHash||value.CalculationResultHash!=current.CalculationResultHash||!value.FundResultHashes.Order(StringComparer.Ordinal).SequenceEqual(current.FundResultHashes.Order(StringComparer.Ordinal))||value.ReviewContextFingerprint!=current.ReviewContextFingerprint)throw Error(DiagnosticCodes.PayrollApprovalApprovalBlocked,("reason","artifact-fingerprint-mismatch"));}
    private static string ArtifactFingerprint(ApprovalArtifactContext a)=>Hash(a.CompanyId.Value,a.PayrollPeriodId.Value,a.SnapshotId.Value,a.SnapshotRevision,a.SnapshotHash,a.CalculationRunId.Value,a.CalculationResultHash,string.Join('|',a.FundResultHashes.Order(StringComparer.Ordinal)),a.ReviewContextFingerprint);
    private static string Hash(params object?[] values)=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join("\u001f",values.Select(x=>x?.ToString()??""))))).ToLowerInvariant();
    private static string Required(string? value,string code)=>string.IsNullOrWhiteSpace(value)?throw Error(code):value.Trim(); private static string? Optional(string? value)=>string.IsNullOrWhiteSpace(value)?null:value.Trim();
    private static void RequireKey(string value){if(string.IsNullOrWhiteSpace(value))throw Error(DiagnosticCodes.PayrollApprovalIdempotencyConflict,("reason","key-required"));}
    private static PayrollApprovalCaseDto Same(bool same,PayrollApprovalCaseDto value)=>same?value:throw Error(DiagnosticCodes.PayrollApprovalIdempotencyConflict);
    private static PayrollAdjustmentRequestDto SameAdjustment(bool same,PayrollAdjustmentRequestDto value)=>same?value:throw Error(DiagnosticCodes.PayrollApprovalIdempotencyConflict);
    private static PayrollApprovalException Error(string code,params (string Key,object? Value)[] args)=>new(new(code,DiagnosticSeverity.Error,args.ToDictionary(x=>x.Key,x=>x.Value)));
}
