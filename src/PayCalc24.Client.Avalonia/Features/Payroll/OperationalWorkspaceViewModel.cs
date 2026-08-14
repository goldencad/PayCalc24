using System.Collections.ObjectModel;
using PayCalc24.Client.Avalonia.Presentation;
using PayCalc24.Contracts.Identity;

namespace PayCalc24.Client.Avalonia.Features.Payroll;

public sealed record StatusCard(string Label, string Value, string Tone);
public sealed record WorkspaceStep(string Code, string Label, string Status);
public sealed record SubjectRow(string EmployeeCode, string Name, string Organization, string Position, string JobGrade, string Status, string Scheme);
public sealed record ComponentRow(string Code, decimal Value, string Method, string Status, int Sequence, string Source, decimal? FundedAmount);
public sealed record FundRow(string Code, decimal Available, decimal Demand, decimal Funded, decimal Deficit, decimal Reserve, decimal Coverage, string Method);
public sealed record StatutoryRow(string Code, string Category, decimal? Amount, string Status);
public sealed record FindingRow(string Severity, string Scope, string Code, string Message);
public sealed record InputRow(string EmployeeCode, string Code, string Value, string Unit, string Source, string EffectiveDate, string Revision);
public sealed record AttendanceRow(string EmployeeCode, string Period, string Value, string Status, string Source);
public sealed record KpiRow(string EmployeeCode, string Code, string Name, decimal Value, string Target, string Status, string Source);
public sealed record VarianceRow(string EmployeeCode, string Component, decimal Current, decimal Reference, decimal Difference, string Reason);
public sealed record ApprovalRow(string Case, string Revision, string Run, string Fingerprint, string Status, string Actor);
public sealed record AccountingRow(string Account, string Description, decimal Debit, decimal Credit, string Dimension, string Reference);
public sealed record ReportRow(string Type, string Company, string Period, string Revision, string Run, string Status);

/// <summary>
/// Development-only deterministic projection. It proves the complete desktop composition without
/// claiming production database connectivity and contains no payroll calculations.
/// </summary>
public static class DemoPayrollProjection
{
    public static OperationalWorkspaceViewModel Create(CompanyPresentationContext company, CultureState culture) =>
        new(company, culture,
            [new("Population", "24", "Info"), new("Period", "2026-07 · FROZEN", "Success"),
             new("Calculation", "COMPLETED", "Success"), new("Approval", "IN_REVIEW", "Warning"),
             new("Funding", "98.4%", "Warning"), new("Settlement", "CALCULATED", "Success")],
            [new("DATA", "Data", "COMPLETED"), new("PREPARE", "Prepare", "COMPLETED"),
             new("CALCULATE", "Calculate", "COMPLETED"), new("REVIEW", "Review", "CURRENT"),
             new("APPROVE", "Approve", "PENDING"), new("SETTLEMENT", "Settlement", "PENDING"),
             new("REPORTS", "Reports", "AVAILABLE")],
            [new("E001", "Nguyen An", "Operations", "Specialist", "G5", "ACTIVE", "STANDARD"),
             new("E002", "Tran Binh", "Sales", "Manager", "G7", "ACTIVE", "SALES")],
            [new("BASE", 32_000_000m, "FORMULA", "CALCULATED", 10, "FORMULA:BASE_V3", null),
             new("ATT_ALLOWANCE", 1_200_000m, "FORMULA", "CALCULATED", 20, "ATTENDANCE", null),
             new("PERFORMANCE_BONUS", 4_800_000m, "FORMULA", "FUNDED", 30, "KPI", 4_700_000m)],
            [new("PERFORMANCE_POOL", 120_000_000m, 121_900_000m, 120_000_000m, 1_900_000m, 0m, .9844m, "WEIGHTED")],
            [new("PIT", "TAX", 3_240_000m, "CALCULATED"), new("SOCIAL_INSURANCE", "EMPLOYEE_CONTRIBUTION", 2_400_000m, "CALCULATED"),
             new("EXTERNAL_ADJUSTMENT", "OTHER_DEDUCTION", null, "UNAVAILABLE")],
            [new("WARNING", "FUND", "FUND.COVERAGE_SHORTAGE", "Funding demand exceeds available amount."),
             new("INFO", "PERIOD", "PAYROLL.REVISION_PINNED", "Snapshot revision 1 is selected.")],
            [new("E001", "WORK_DAYS", "21.5", "days", "ATTENDANCE", "2026-07-31", "3"),
             new("E002", "SALES_VALUE", "840000000", "VND", "ERP", "2026-07-31", "2")],
            [new("E001", "2026-07", "21.5 days", "VALID", "ATTENDANCE_BATCH_07"), new("E002", "2026-07", "22 days", "VALID", "ATTENDANCE_BATCH_07")],
            [new("E001", "QUALITY", "Quality score", 92m, "80–100", "VALID", "KPI_BATCH_07"), new("E002", "SALES", "Sales achievement", 105m, ">= 100", "VALID", "KPI_BATCH_07")],
            [new("E001", "BASE", 32_000_000m, 31_000_000m, 1_000_000m, "Assignment revision")],
            [new("APR-2026-07", "1", "DEMO-RUN-001", "7a21…f04c", "IN_REVIEW", "demo.reviewer")],
            [new("6421", "Payroll expense", 38_000_000m, 0m, "OPS", "DEMO-RUN-001"), new("3341", "Payroll payable", 0m, 38_000_000m, "OPS", "DEMO-RUN-001")],
            [new("Payroll summary", "DEMO", "2026-07", "1", "DEMO-RUN-001", "AVAILABLE"), new("Subject detail", "DEMO", "2026-07", "1", "DEMO-RUN-001", "AVAILABLE"), new("Settlement summary", "DEMO", "2026-07", "1", "DEMO-RUN-001", "AVAILABLE")]);
}

public sealed class OperationalWorkspaceViewModel : ViewModelBase
{
    private readonly CompanyPresentationContext company;
    private readonly CultureState culture;
    private string selectedArea = "DASHBOARD";
    private bool busy;

    public OperationalWorkspaceViewModel(CompanyPresentationContext company, CultureState culture,
        IReadOnlyList<StatusCard> cards, IReadOnlyList<WorkspaceStep> steps, IReadOnlyList<SubjectRow> subjects,
        IReadOnlyList<ComponentRow> components, IReadOnlyList<FundRow> funds,
        IReadOnlyList<StatutoryRow> statutory, IReadOnlyList<FindingRow> findings,
        IReadOnlyList<InputRow> inputs, IReadOnlyList<AttendanceRow> attendance, IReadOnlyList<KpiRow> kpis,
        IReadOnlyList<VarianceRow> variances, IReadOnlyList<ApprovalRow> approvals,
        IReadOnlyList<AccountingRow> accounting, IReadOnlyList<ReportRow> reports)
    {
        this.company = company;
        this.culture = culture;
        Cards = cards; Steps = steps; Subjects = subjects; Components = components; Funds = funds;
        Statutory = statutory; Findings = findings;
        Inputs = inputs; Attendance = attendance; Kpis = kpis; Variances = variances;
        Approvals = approvals; Accounting = accounting; Reports = reports;
        Areas = new(["DASHBOARD", "SUBJECTS", "INPUTS", "ATTENDANCE", "KPI", "PREPARE", "CALCULATE",
            "FUNDS", "VALIDATE", "EXPLAIN", "VARIANCE", "SCENARIO", "APPROVAL", "SETTLEMENT", "ACCOUNTING", "REPORTS"]);
        company.CompanyChanged += (_, _) => ResetForCompany();
        culture.CultureChanged += (_, _) => Changed(string.Empty);
        SelectAreaCommand = new DelegateCommand<string>(SelectArea);
        RunCommand = new AsyncDelegateCommand(async () => { Busy = true; await Task.Yield(); Busy = false; });
    }

    public ObservableCollection<string> Areas { get; }
    public IReadOnlyList<StatusCard> Cards { get; }
    public IReadOnlyList<WorkspaceStep> Steps { get; }
    public IReadOnlyList<SubjectRow> Subjects { get; }
    public IReadOnlyList<ComponentRow> Components { get; }
    public IReadOnlyList<FundRow> Funds { get; }
    public IReadOnlyList<StatutoryRow> Statutory { get; }
    public IReadOnlyList<FindingRow> Findings { get; }
    public IReadOnlyList<InputRow> Inputs { get; }
    public IReadOnlyList<AttendanceRow> Attendance { get; }
    public IReadOnlyList<KpiRow> Kpis { get; }
    public IReadOnlyList<VarianceRow> Variances { get; }
    public IReadOnlyList<ApprovalRow> Approvals { get; }
    public IReadOnlyList<AccountingRow> Accounting { get; }
    public IReadOnlyList<ReportRow> Reports { get; }
    public string CompanyLabel => company.CompanyId.Value.ToString("N")[..8].ToUpperInvariant();
    public string Header => culture.Culture == "vi-VN" ? "Kỳ lương 07/2026 · Bản chụp 1" : "Payroll 07/2026 · Snapshot 1";
    public string CompanyBackstage => Local("Company context and selection", "Ngữ cảnh và lựa chọn công ty");
    public string LanguageBackstage => Local("English (United States) / Vietnamese", "Tiếng Anh (Hoa Kỳ) / Tiếng Việt");
    public string AppearanceBackstage => Local("System / Light / Dark", "Hệ thống / Sáng / Tối");
    public string SettingsBackstage => Local("Desktop presentation settings", "Cài đặt giao diện máy tính");
    public string AboutBackstage => Local("PayCalc24 0.1.0-mvp · Actipro evaluation", "PayCalc24 0.1.0-mvp · Bản đánh giá Actipro");
    public string DemoNotice => Local("ACTIPRO TRIAL · NON-PRODUCTION DEMO", "ACTIPRO TRIAL · DỮ LIỆU DEMO PHI SẢN XUẤT");
    public string RevisionIdentity => Local("Exact revision · snapshot 1 · run DEMO-RUN-001 · hash 7a21…f04c", "Đúng phiên bản · bản chụp 1 · lượt DEMO-RUN-001 · hash 7a21…f04c");
    public string DemoLabel => Local("DEMO", "DEMO");
    public string WorkflowLabel => Local("Workflow", "Quy trình");
    public string SubjectsLabel => Local("Payroll subjects", "Nhân sự tính lương");
    public string ComponentsLabel => Local("Dynamic component results", "Kết quả thành phần động");
    public string FundsLabel => Local("Fund allocation", "Phân bổ nguồn quỹ");
    public string ValidationLabel => Local("Validation / review", "Kiểm tra / rà soát");
    public string StatutoryLabel => Local("Statutory / Net Pay / Employer Cost", "Nghĩa vụ / Thực lĩnh / Chi phí doanh nghiệp");
    public string MissingStatutoryNotice => Local("Missing statutory values remain UNAVAILABLE and are never rendered as zero.", "Dữ liệu nghĩa vụ còn thiếu hiển thị UNAVAILABLE và không bao giờ thành số không.");
    public string RibbonHome => Local("HOME", "TRANG CHỦ");
    public string RibbonInput => Local("INPUT", "ĐẦU VÀO");
    public string RibbonPayroll => Local("PAYROLL", "TÍNH LƯƠNG");
    public string RibbonReview => Local("REVIEW", "RÀ SOÁT");
    public string RibbonApproval => Local("APPROVAL", "PHÊ DUYỆT");
    public string RibbonFinance => Local("FINANCE", "TÀI CHÍNH");
    public string RibbonReports => Local("REPORTS", "BÁO CÁO");
    public string RibbonNavigate => Local("Navigate", "Điều hướng");
    public string RibbonDashboard => Local("Dashboard", "Tổng quan");
    public string RibbonSubjects => Local("Subjects", "Nhân sự");
    public string RibbonInputs => Local("Payroll Inputs", "Dữ liệu lương");
    public string RibbonAttendance => Local("Attendance", "Chấm công");
    public string RibbonKpi => Local("KPI", "KPI");
    public string RibbonPrepare => Local("Prepare", "Chuẩn bị");
    public string RibbonCalculate => Local("Calculate", "Tính lương");
    public string RibbonFunds => Local("Funds", "Nguồn quỹ");
    public string RibbonValidate => Local("Validate", "Kiểm tra");
    public string RibbonExplain => Local("Explain", "Giải trình");
    public string RibbonVariance => Local("Variance", "Biến động");
    public string RibbonScenario => Local("Scenario", "Kịch bản");
    public string RibbonApprovalAction => Local("Approval", "Phê duyệt");
    public string RibbonSettlement => Local("Settlement", "Quyết toán");
    public string RibbonAccounting => Local("Accounting", "Hạch toán");
    public string RibbonReportAction => Local("Reports", "Báo cáo");
    public string BackstageSettings => Local("Settings", "Cài đặt");
    public string BackstageAbout => Local("Application / About", "Ứng dụng / Giới thiệu");
    public string BackstageExit => Local("Exit", "Thoát");
    public string SelectedArea { get => selectedArea; private set { selectedArea = value; Changed(); Changed(nameof(AreaTitle)); } }
    public string AreaTitle => culture.Culture == "vi-VN" ? Translate(SelectedArea) : SelectedArea.Replace('_', ' ');
    public bool Busy { get => busy; private set { busy = value; Changed(); Changed(nameof(ActivityText)); } }
    public string ActivityText => Busy ? "Working…" : "Ready · demo adapter · production database not connected";
    public DelegateCommand<string> SelectAreaCommand { get; }
    public AsyncDelegateCommand RunCommand { get; }
    public void SelectArea(string? area) { if (area is not null && Areas.Contains(area)) SelectedArea = area; }
    private void ResetForCompany() { SelectedArea = "DASHBOARD"; Changed(nameof(CompanyLabel)); }
    private string Local(string english, string vietnamese) => culture.Culture == "vi-VN" ? vietnamese : english;
    private static string Translate(string code) => code switch
    {
        "DASHBOARD" => "TỔNG QUAN", "SUBJECTS" => "NHÂN SỰ TÍNH LƯƠNG", "INPUTS" => "DỮ LIỆU ĐẦU VÀO",
        "ATTENDANCE" => "CHẤM CÔNG", "KPI" => "HIỆU SUẤT / KPI", "PREPARE" => "CHUẨN BỊ / ĐÓNG BĂNG",
        "CALCULATE" => "TÍNH LƯƠNG", "FUNDS" => "NGUỒN QUỸ", "VALIDATE" => "KIỂM TRA",
        "EXPLAIN" => "GIẢI TRÌNH", "VARIANCE" => "BIẾN ĐỘNG", "SCENARIO" => "KỊCH BẢN",
        "APPROVAL" => "PHÊ DUYỆT", "SETTLEMENT" => "QUYẾT TOÁN", "ACCOUNTING" => "HẠCH TOÁN",
        "REPORTS" => "BÁO CÁO", _ => code
    };
}
