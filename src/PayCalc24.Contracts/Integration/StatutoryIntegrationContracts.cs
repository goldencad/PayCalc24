using PayCalc24.Contracts.Diagnostics;
using PayCalc24.Contracts.Identity;
using PayCalc24.Contracts.Organization;
using PayCalc24.Contracts.PayrollApproval;
using PayCalc24.Contracts.PayrollCalculation;
using PayCalc24.Contracts.PayrollInput;

namespace PayCalc24.Contracts.Integration;

public readonly record struct StatutoryResultId(Guid Value);
public readonly record struct NetPayResultId(Guid Value);
public readonly record struct EmployerCostResultId(Guid Value);
public readonly record struct PayrollAccountingDocumentId(Guid Value);
public enum StatutoryResultStatus { Calculated, NotCalculated, Unavailable, Failed, NotApplicable }
public enum StatutoryResultKind { Insurance, IncomeTax, Other }
public enum ContributionParty { Employee, Employer }
public enum PayrollAmountClassification { GrossEarning, NetSettlementEarning, PayrollCost, OtherDeduction, EmployerPaidCost }
public enum AccountingSide { Debit, Credit }
public enum IntegrationDeliveryStatus { Pending, Succeeded, Failed }

public sealed record StatutoryProviderIdentity(string JurisdictionCode, string ProviderCode,
    string ProviderVersion, string PolicyVersion);
public sealed record StatutoryPayrollFact(string Code, decimal Amount, string SourceResultId,
    decimal? CalculatedAmount = null, decimal? FundedAmount = null);
public sealed record StatutorySubjectClassification(string Code, string Value, string SourceReference);
public sealed record StatutoryCalculationRequest(CompanyId CompanyId, PayrollPeriodId PayrollPeriodId,
    PayrollCalculationSnapshotId SnapshotId, int SnapshotRevision, PayrollCalculationRunId CalculationRunId,
    PayrollApprovalCaseId? ApprovalCaseId, PayrollSubjectId PayrollSubjectId, DateOnly BusinessDate,
    PayrollExecutionMode ExecutionMode, IReadOnlyList<StatutoryPayrollFact> PayrollFacts,
    int EligibleDependentCount, IReadOnlyList<EmployeeDependentId> EligibleDependentIds,
    IReadOnlyList<StatutorySubjectClassification> Classifications, StatutoryProviderIdentity Provider,
    string CorrelationId, string RequestHash);

public sealed record StatutoryContributionItem(string Code, string Category, ContributionParty Party,
    decimal CalculationBase, decimal? Rate, decimal Amount, string? PolicyReference = null,
    string? CapOrFloorReference = null);
public sealed record StatutoryDeductionItem(string Code, decimal Amount, string? PolicyReference = null,
    string? SourceReference = null);
public sealed record IncomeTaxBandItem(string Code, decimal? LowerBound, decimal? UpperBound,
    decimal? Rate, decimal Amount, string? PolicyReference = null);
public sealed record InsuranceCalculationResult(IReadOnlyList<StatutoryContributionItem> ContributionItems,
    decimal TotalEmployeeContribution, decimal TotalEmployerContribution);
public sealed record IncomeTaxCalculationResult(decimal TaxableIncome, decimal? AssessableIncome,
    IReadOnlyList<StatutoryDeductionItem> DeductionItems, IReadOnlyList<IncomeTaxBandItem> TaxBandItems,
    decimal TaxAmount);
public sealed record StatutoryCalculationResult(StatutoryResultId Id, CompanyId CompanyId,
    PayrollPeriodId PayrollPeriodId, PayrollCalculationSnapshotId SnapshotId, int SnapshotRevision,
    PayrollCalculationRunId CalculationRunId, PayrollSubjectId PayrollSubjectId,
    StatutoryResultKind Kind, StatutoryResultStatus Status, StatutoryProviderIdentity Provider,
    DateOnly BusinessDate, InsuranceCalculationResult? Insurance, IncomeTaxCalculationResult? IncomeTax,
    IReadOnlyList<StatutoryDeductionItem> OtherDeductions, IReadOnlyList<Diagnostic> Diagnostics,
    string RequestHash, string ResultHash, string CorrelationId);

public interface IStatutoryProvider
{
    StatutoryProviderIdentity Identity { get; }
    StatutoryCalculationResult Calculate(StatutoryCalculationRequest request);
}
public interface IStatutoryProviderRegistry
{
    IStatutoryProvider Resolve(CompanyId companyId, StatutoryProviderIdentity pinnedIdentity);
}
public interface IStatutoryResultRepository
{
    StatutoryCalculationResult? Find(CompanyId companyId, StatutoryProviderIdentity provider, string requestHash);
    void Add(StatutoryCalculationResult result);
}

public sealed record SettlementAmountItem(string Code, decimal Amount, PayrollAmountClassification Classification,
    string SourceResultId, decimal? CalculatedAmount = null, decimal? FundedAmount = null);
public sealed record NetPayResult(NetPayResultId Id, CompanyId CompanyId, PayrollPeriodId PayrollPeriodId,
    PayrollCalculationSnapshotId SnapshotId, int SnapshotRevision, PayrollCalculationRunId CalculationRunId,
    PayrollApprovalCaseId? ApprovalCaseId, PayrollSubjectId PayrollSubjectId,
    IReadOnlyList<SettlementAmountItem> EarningItems, IReadOnlyList<SettlementAmountItem> OtherDeductionItems,
    IReadOnlyList<StatutoryResultId> StatutoryResultIds, decimal TotalEarnings,
    decimal TotalEmployeeStatutoryDeductions, decimal TotalOtherDeductions, decimal NetPay,
    string ResultHash, PayrollExecutionMode ExecutionMode);
public sealed record EmployerCostResult(EmployerCostResultId Id, CompanyId CompanyId,
    PayrollPeriodId PayrollPeriodId, PayrollCalculationSnapshotId SnapshotId, int SnapshotRevision,
    PayrollCalculationRunId CalculationRunId, PayrollSubjectId PayrollSubjectId,
    IReadOnlyList<SettlementAmountItem> PayrollCostItems,
    IReadOnlyList<StatutoryContributionItem> EmployerContributionItems,
    decimal PayrollCost, decimal EmployerStatutoryCost, decimal TotalEmployerCost, string ResultHash);
public sealed record ComposeSettlementRequest(CompanyId CompanyId, PayrollPeriodId PayrollPeriodId,
    PayrollCalculationSnapshotId SnapshotId, int SnapshotRevision, PayrollCalculationRunId CalculationRunId,
    PayrollApprovalCaseId? ApprovalCaseId, PayrollSubjectId PayrollSubjectId, PayrollExecutionMode ExecutionMode,
    IReadOnlyList<SettlementAmountItem> PayrollItems, IReadOnlyList<StatutoryCalculationResult> RequiredStatutoryResults);
public sealed record PayrollSettlementResult(NetPayResult NetPay, EmployerCostResult EmployerCost);
public interface IPayrollSettlementComposer { PayrollSettlementResult Compose(ComposeSettlementRequest request); }

public sealed record PayrollAccountingLine(string PostingKey, AccountingSide Side, decimal Amount,
    string SourceReference, string? OrganizationDimension = null, PayrollSubjectId? PayrollSubjectId = null);
public sealed record PayrollAccountingDocument(PayrollAccountingDocumentId Id, CompanyId CompanyId,
    PayrollPeriodId PayrollPeriodId, PayrollCalculationSnapshotId SnapshotId, int SnapshotRevision,
    PayrollCalculationRunId CalculationRunId, PayrollApprovalCaseId ApprovalCaseId,
    string CurrencyCode, DateOnly PostingDate, string DocumentReference, int DocumentVersion,
    IReadOnlyList<PayrollAccountingLine> Lines, decimal TotalDebit, decimal TotalCredit,
    string ResultHash, PayrollExecutionMode ExecutionMode);
public sealed record AccountingPublishRequest(PayrollAccountingDocument Document, string TargetSystem,
    string IdempotencyKey, string CorrelationId);
public sealed record AccountingPublishResult(string DeliveryId, IntegrationDeliveryStatus Status,
    string? ExternalDocumentReference, string ResultHash, Diagnostic? Diagnostic = null);
public interface IPayrollAccountingPublisher
{
    AccountingPublishResult Publish(AccountingPublishRequest request);
}
public interface IAccountingDeliveryRepository
{
    AccountingPublishResult? Find(CompanyId companyId, string targetSystem, string idempotencyKey,
        out string? requestFingerprint);
    void Add(CompanyId companyId, string targetSystem, string idempotencyKey,
        string requestFingerprint, AccountingPublishResult result);
}
public interface IExternalAccountingAdapter
{
    string TargetSystem { get; }
    AccountingPublishResult Publish(PayrollAccountingDocument document, string correlationId);
}

public sealed class StatutoryIntegrationException(Diagnostic diagnostic) : Exception(diagnostic.Code)
{ public Diagnostic Diagnostic { get; } = diagnostic; }
