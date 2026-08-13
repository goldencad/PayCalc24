using PayCalc24.Contracts.Diagnostics;
using PayCalc24.Contracts.Identity;
using PayCalc24.Contracts.Integration;
using PayCalc24.Contracts.Organization;
using PayCalc24.Contracts.PayrollCalculation;
using PayCalc24.Contracts.PayrollInput;
using PayCalc24.Contracts.PayrollReview;

namespace PayCalc24.Contracts.Reporting;

public enum PayrollReportType { PayrollSummary, PayrollSubjectDetail, PayrollSettlementSummary }
public enum PayrollReportOutputFormat { PortableText }

/// <summary>An exact immutable source identity. No member may be resolved as latest/current.</summary>
public sealed record PayrollReportRequest(CompanyId CompanyId, PayrollPeriodId PayrollPeriodId,
    PayrollCalculationSnapshotId SnapshotId, int SnapshotRevision, PayrollCalculationRunId CalculationRunId,
    PayrollReportType ReportType, string Culture, PayrollReportOutputFormat OutputFormat,
    PayrollSubjectId? PayrollSubjectId = null, IReadOnlyDictionary<string, string>? Parameters = null);

public sealed record PayrollReportProvenance(CompanyId CompanyId, PayrollPeriodId PayrollPeriodId,
    PayrollCalculationSnapshotId SnapshotId, int SnapshotRevision, PayrollCalculationRunId CalculationRunId,
    string SourceHash, string RendererIdentity, string RendererVersion, string Culture,
    PayrollReportType ReportType);

public sealed record PayrollReportResult(byte[] Content, string ContentType, string FileName,
    string ResultHash, PayrollReportProvenance Provenance, IReadOnlyList<Diagnostic> Diagnostics);

public sealed record PayrollSummaryProjection(int SubjectCount, int CalculatedSubjectCount,
    decimal TotalCalculated, decimal TotalFunded, int BlockingValidationCount);
public sealed record PayrollSubjectDetailProjection(string EmployeeCode,
    IReadOnlyList<ComponentExplanation> Components, IReadOnlyList<FundExplanation> Funds,
    IReadOnlyList<StatutoryCalculationResult> StatutoryResults, NetPayResult? NetPay,
    EmployerCostResult? EmployerCost, string ExplainHash);
public sealed record PayrollSettlementSummaryProjection(int SubjectCount, decimal TotalNetPay,
    decimal TotalEmployerCost, IReadOnlyList<string> SourceResultHashes);

public sealed record PayrollReportSource(PayrollReportRequest Identity, string SourceHash,
    DateOnly BusinessDate, PayrollSummaryProjection? Summary = null,
    PayrollSubjectDetailProjection? SubjectDetail = null,
    PayrollSettlementSummaryProjection? SettlementSummary = null);

public interface IPayrollReportSource { PayrollReportSource GetSource(PayrollReportRequest request); }
public interface IPayrollReportRenderer
{
    string Identity { get; }
    string Version { get; }
    PayrollReportResult Render(PayrollReportSource source);
}
public interface IPayrollReportService { PayrollReportResult Generate(PayrollReportRequest request); }
