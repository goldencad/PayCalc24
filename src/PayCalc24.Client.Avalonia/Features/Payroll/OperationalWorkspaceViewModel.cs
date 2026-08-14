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
             new("INFO", "PERIOD", "PAYROLL.REVISION_PINNED", "Snapshot revision 1 is selected.")]);
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
        IReadOnlyList<StatutoryRow> statutory, IReadOnlyList<FindingRow> findings)
    {
        this.company = company;
        this.culture = culture;
        Cards = cards; Steps = steps; Subjects = subjects; Components = components; Funds = funds;
        Statutory = statutory; Findings = findings;
        Areas = new(["DASHBOARD", "SUBJECTS", "INPUTS", "ATTENDANCE", "KPI", "PREPARE", "CALCULATE",
            "FUNDS", "VALIDATE", "EXPLAIN", "VARIANCE", "SCENARIO", "APPROVAL", "SETTLEMENT", "ACCOUNTING", "REPORTS"]);
        company.CompanyChanged += (_, _) => ResetForCompany();
        culture.CultureChanged += (_, _) => { Changed(nameof(Header)); Changed(nameof(AreaTitle)); Changed(nameof(CompanyBackstage));
            Changed(nameof(LanguageBackstage)); Changed(nameof(AppearanceBackstage)); Changed(nameof(SettingsBackstage));
            Changed(nameof(AboutBackstage)); Changed(nameof(DemoNotice)); Changed(nameof(RevisionIdentity));
            Changed(nameof(DemoLabel)); Changed(nameof(WorkflowLabel)); Changed(nameof(SubjectsLabel));
            Changed(nameof(ComponentsLabel)); Changed(nameof(FundsLabel)); Changed(nameof(ValidationLabel));
            Changed(nameof(StatutoryLabel)); Changed(nameof(MissingStatutoryNotice)); };
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
