using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using PayCalc24.Contracts.Reporting;

#pragma warning disable CA1305 // Every display value below is explicitly formatted with the requested report culture.

namespace PayCalc24.Reporting.Services;

public sealed class PayrollReportService(IPayrollReportSource source, IPayrollReportRenderer renderer)
    : IPayrollReportService
{
    public PayrollReportResult Generate(PayrollReportRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.SnapshotRevision <= 0) throw new ArgumentOutOfRangeException(nameof(request));
        _ = CultureInfo.GetCultureInfo(request.Culture);
        var projection = source.GetSource(request);
        if (projection.Identity != request)
            throw new InvalidOperationException("REPORT.SOURCE_IDENTITY_MISMATCH");
        return renderer.Render(projection);
    }
}
/// <summary>Small deterministic CI renderer. Commercial renderers can implement the same port.</summary>
public sealed class DeterministicPortableTextRenderer : IPayrollReportRenderer
{
    public string Identity => "PayCalc24.PortableText";
    public string Version => "1.0.0";

    public PayrollReportResult Render(PayrollReportSource source)
    {
        var request = source.Identity;
        var culture = CultureInfo.GetCultureInfo(request.Culture);
        var text = new StringBuilder()
            .AppendLine("PAYCALC24 REPORT")
            .AppendLine($"Type: {request.ReportType}")
            .AppendLine($"Company: {request.CompanyId.Value:D}")
            .AppendLine($"Period: {request.PayrollPeriodId.Value:D}")
            .AppendLine($"Snapshot: {request.SnapshotId.Value:D} / Revision {request.SnapshotRevision}")
            .AppendLine($"Run: {request.CalculationRunId.Value:D}")
            .AppendLine($"Business date: {source.BusinessDate.ToString("d", culture)}")
            .AppendLine($"Source hash: {source.SourceHash}");
        AppendBody(text, source, culture);
        var content = Encoding.UTF8.GetBytes(text.ToString().Replace("\r\n", "\n", StringComparison.Ordinal));
        var hash = Convert.ToHexStringLower(SHA256.HashData(content));
        var provenance = new PayrollReportProvenance(request.CompanyId, request.PayrollPeriodId,
            request.SnapshotId, request.SnapshotRevision, request.CalculationRunId, source.SourceHash,
            Identity, Version, request.Culture, request.ReportType);
        return new(content, "text/plain; charset=utf-8",
            $"payroll-{request.ReportType.ToString().ToLowerInvariant()}-r{request.SnapshotRevision}.txt",
            hash, provenance, []);
    }

    private static void AppendBody(StringBuilder text, PayrollReportSource source, CultureInfo culture)
    {
        switch (source.Identity.ReportType)
        {
            case PayrollReportType.PayrollSummary when source.Summary is { } value:
                text.AppendLine($"Subjects: {value.SubjectCount}")
                    .AppendLine($"Calculated: {value.CalculatedSubjectCount}")
                    .AppendLine($"Total calculated: {value.TotalCalculated.ToString("N2", culture)}")
                    .AppendLine($"Total funded: {value.TotalFunded.ToString("N2", culture)}")
                    .AppendLine($"Blocking validations: {value.BlockingValidationCount}");
                break;
            case PayrollReportType.PayrollSubjectDetail when source.SubjectDetail is { } value:
                text.AppendLine($"Employee code: {value.EmployeeCode}")
                    .AppendLine($"Components: {value.Components.Count}")
                    .AppendLine($"Funds: {value.Funds.Count}")
                    .AppendLine($"Statutory results: {value.StatutoryResults.Count}")
                    .AppendLine($"Net pay: {value.NetPay?.NetPay.ToString("N2", culture) ?? "N/A"}")
                    .AppendLine($"Employer cost: {value.EmployerCost?.TotalEmployerCost.ToString("N2", culture) ?? "N/A"}")
                    .AppendLine($"Explain hash: {value.ExplainHash}");
                break;
            case PayrollReportType.PayrollSettlementSummary when source.SettlementSummary is { } value:
                text.AppendLine($"Subjects: {value.SubjectCount}")
                    .AppendLine($"Total net pay: {value.TotalNetPay.ToString("N2", culture)}")
                    .AppendLine($"Total employer cost: {value.TotalEmployerCost.ToString("N2", culture)}")
                    .AppendLine($"Result hashes: {string.Join(',', value.SourceResultHashes.Order(StringComparer.Ordinal))}");
                break;
            default: throw new InvalidOperationException("REPORT.PROJECTION_MISSING");
        }
    }
}
#pragma warning restore CA1305
