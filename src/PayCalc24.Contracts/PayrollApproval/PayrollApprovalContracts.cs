using PayCalc24.Contracts.Diagnostics;
using PayCalc24.Contracts.Identity;
using PayCalc24.Contracts.PayrollCalculation;
using PayCalc24.Contracts.PayrollInput;
using PayCalc24.Contracts.PayrollReview;

namespace PayCalc24.Contracts.PayrollApproval;

public readonly record struct PayrollApprovalCaseId(Guid Value) { public static PayrollApprovalCaseId From(Guid value)=>new(value); }
public readonly record struct PayrollApprovalEventId(Guid Value) { public static PayrollApprovalEventId From(Guid value)=>new(value); }
public readonly record struct PayrollAdjustmentRequestId(Guid Value) { public static PayrollAdjustmentRequestId From(Guid value)=>new(value); }

public enum PayrollApprovalStatus { Draft, Submitted, InReview, Approved, Rejected, Locked }
public enum PayrollAdjustmentType { InputCorrection, PolicyCorrection, AssignmentCorrection, ManualAdjustment, Other }
public enum PayrollAdjustmentStatus { Requested, Authorized, RevisionStarted }
public enum PayrollApprovalAction { Submit, Review, Approve, Lock, Adjust }

public sealed record ApprovalArtifactContext(CompanyId CompanyId, PayrollPeriodId PayrollPeriodId,
    PayrollCalculationSnapshotId SnapshotId, int SnapshotRevision, string SnapshotHash,
    PayrollCalculationRunId CalculationRunId, string CalculationResultHash,
    PayrollExecutionMode ExecutionMode, IReadOnlyList<string> FundResultHashes,
    string ReviewContextFingerprint, bool CalculationSucceeded, bool RequiredFundsComplete,
    bool IsLatestProductionRevision);

public sealed record PayrollApprovalCaseDto(PayrollApprovalCaseId Id, CompanyId CompanyId,
    PayrollPeriodId PayrollPeriodId, PayrollCalculationSnapshotId SnapshotId, int SnapshotRevision,
    string SnapshotHash, PayrollCalculationRunId CalculationRunId, string CalculationResultHash,
    IReadOnlyList<string> FundResultHashes, string ReviewContextFingerprint, string ApprovalFingerprint,
    PayrollApprovalStatus Status, long Revision, PayrollApprovalCaseId? SupersedesApprovalCaseId,
    DateTimeOffset CreatedAt, UserId CreatedBy, DateTimeOffset? SubmittedAt, UserId? SubmittedBy,
    DateTimeOffset? ReviewStartedAt, UserId? ReviewedBy, DateTimeOffset? ApprovedAt, UserId? ApprovedBy,
    DateTimeOffset? RejectedAt, UserId? RejectedBy, DateTimeOffset? LockedAt, UserId? LockedBy,
    string? CurrentDecisionReason, string CorrelationId);

public sealed record PayrollApprovalEventDto(PayrollApprovalEventId Id, PayrollApprovalCaseId ApprovalCaseId,
    CompanyId CompanyId, PayrollApprovalStatus FromStatus, PayrollApprovalStatus ToStatus,
    UserId ActorUserId, DateTimeOffset OccurredAt, string? Reason, string CorrelationId, string IdempotencyKey);

public sealed record PayrollAdjustmentRequestDto(PayrollAdjustmentRequestId Id, CompanyId CompanyId,
    PayrollPeriodId PayrollPeriodId, PayrollApprovalCaseId SourceApprovalCaseId,
    PayrollCalculationSnapshotId SourceSnapshotId, PayrollCalculationRunId SourceCalculationRunId,
    PayrollAdjustmentType AdjustmentType, PayrollAdjustmentStatus Status, string Reason,
    UserId RequestedBy, DateTimeOffset RequestedAt, UserId? AuthorizedBy, DateTimeOffset? AuthorizedAt,
    PayrollCalculationSnapshotId? NewSnapshotId, int? NewSnapshotRevision,
    PayrollApprovalCaseId? NewApprovalCaseId, string CorrelationId, long Revision);

public sealed record CreateApprovalCaseCommand(ApprovalArtifactContext Artifacts,
    PayrollApprovalCaseId? SupersedesApprovalCaseId, string IdempotencyKey);
public sealed record ApprovalTransitionCommand(PayrollApprovalCaseId ApprovalCaseId,
    long ExpectedRevision, string IdempotencyKey, string? Reason = null);
public sealed record RequestAdjustmentCommand(PayrollApprovalCaseId SourceApprovalCaseId,
    PayrollAdjustmentType AdjustmentType, string Reason, string IdempotencyKey);
public sealed record StartRevisionCommand(PayrollAdjustmentRequestId AdjustmentRequestId,
    long ExpectedRevision, string IdempotencyKey);
public sealed record RevisionStartResult(PayrollCalculationSnapshotId SnapshotId, int SnapshotRevision,
    PayrollCalculationRunId CalculationRunId, ApprovalArtifactContext Artifacts);

public interface IPayrollApprovalAuthorization
{
    void Demand(CompanyId companyId, UserId userId, PayrollApprovalAction action);
    void ValidateDecisionActors(PayrollApprovalCaseDto approvalCase, UserId actor);
}
public interface IPayrollApprovalArtifactSource
{
    ApprovalArtifactContext GetAuthoritativeArtifacts(ReviewResultContext context);
    bool IsLatestProductionRevision(ApprovalArtifactContext artifacts);
}
public interface IPayrollApprovalRepository
{
    PayrollApprovalCaseDto? GetCase(CompanyId companyId, PayrollApprovalCaseId id);
    PayrollApprovalCaseDto? GetByIdempotency(CompanyId companyId, string operation, string key);
    void Add(PayrollApprovalCaseDto value, string operation, string key, string fingerprint);
    void Update(PayrollApprovalCaseDto value, long expectedRevision, string operation, string key, string fingerprint);
    IReadOnlyList<PayrollApprovalCaseDto> List(CompanyId companyId, PayrollPeriodId periodId);
    void Append(PayrollApprovalEventDto value);
    IReadOnlyList<PayrollApprovalEventDto> History(CompanyId companyId, PayrollApprovalCaseId id);
    PayrollAdjustmentRequestDto? GetAdjustment(CompanyId companyId, PayrollAdjustmentRequestId id);
    PayrollAdjustmentRequestDto? GetAdjustmentByIdempotency(CompanyId companyId, string operation, string key);
    void AddAdjustment(PayrollAdjustmentRequestDto value, string operation, string key, string fingerprint);
    void UpdateAdjustment(PayrollAdjustmentRequestDto value, long expectedRevision, string operation, string key, string fingerprint);
}
public interface IPayrollRevisionOrchestrator
{
    RevisionStartResult StartNewRevision(PayrollAdjustmentRequestDto request);
}
public interface IPayrollPeriodCloseBoundary { void CloseCalculatedPeriod(CompanyId companyId, PayrollPeriodId periodId); }

public interface IPayrollApprovalService
{
    PayrollApprovalCaseDto Create(CreateApprovalCaseCommand command);
    PayrollApprovalCaseDto GetCase(PayrollApprovalCaseId id);
    PayrollApprovalCaseDto Submit(ApprovalTransitionCommand command);
    PayrollApprovalCaseDto StartReview(ApprovalTransitionCommand command);
    PayrollApprovalCaseDto Approve(ApprovalTransitionCommand command);
    PayrollApprovalCaseDto Reject(ApprovalTransitionCommand command);
    PayrollApprovalCaseDto Lock(ApprovalTransitionCommand command);
    PayrollValidationSummary GetExactReviewContext(PayrollApprovalCaseId id);
    IReadOnlyList<PayrollApprovalEventDto> GetHistory(PayrollApprovalCaseId id);
    IReadOnlyList<PayrollApprovalCaseDto> ListRevisions(PayrollPeriodId periodId);
    PayrollApprovalCaseDto? GetCurrentAuthoritative(PayrollPeriodId periodId);
    PayrollAdjustmentRequestDto RequestAdjustment(RequestAdjustmentCommand command);
    PayrollAdjustmentRequestDto AuthorizeAdjustment(PayrollAdjustmentRequestId id, long expectedRevision, string idempotencyKey);
    PayrollAdjustmentRequestDto StartNewRevision(StartRevisionCommand command);
}

public sealed class PayrollApprovalException(Diagnostic diagnostic) : Exception(diagnostic.Code)
{ public Diagnostic Diagnostic { get; } = diagnostic; }
