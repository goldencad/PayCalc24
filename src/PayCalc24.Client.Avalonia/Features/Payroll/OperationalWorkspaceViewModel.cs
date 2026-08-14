using System.Collections.ObjectModel;
using PayCalc24.Client.Avalonia.Presentation;
using PayCalc24.Contracts.Identity;

namespace PayCalc24.Client.Avalonia.Features.Payroll;

public sealed record StatusCard(string Label, string Value, string Tone, string TargetArea = "DASHBOARD");
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
public sealed record ScenarioRow(string Code, string Name, string Revision, string BaseSnapshot, string Status, string Overrides);
public sealed record ApprovalRow(string Case, string Revision, string Run, string Fingerprint, string Status, string Actor);
public sealed record ApprovalEventRow(string Timestamp, string Actor, string Action, string Reason, string Status);
public sealed record AccountingRow(string Account, string Description, decimal Debit, decimal Credit, string Dimension, string Reference);
public sealed record ReportRow(string Type, string Company, string Period, string Revision, string Run, string Status);

/// <summary>Deterministic presentation-only demo projection. No payroll decision is made here.</summary>
public static class DemoPayrollProjection
{
    public static OperationalWorkspaceViewModel Create(CompanyPresentationContext company, CultureState culture) => new(company, culture,
        [new("Population", "24", "Info", "SUBJECTS"), new("Input readiness", "READY", "Success", "INPUTS"), new("Calculation", "COMPLETED", "Success", "CALCULATE"),
         new("Validation", "1 WARNING", "Warning", "VALIDATE"), new("Funding", "98.4%", "Warning", "FUNDS"), new("Approval", "IN REVIEW", "Warning", "APPROVAL"), new("Settlement", "CALCULATED", "Success", "SETTLEMENT")],
        [new("DATA", "Data", "COMPLETED"), new("PREPARE", "Prepare", "COMPLETED"), new("CALCULATE", "Calculate", "COMPLETED"), new("VALIDATE", "Validate", "CURRENT"), new("APPROVAL", "Approve", "PENDING"), new("SETTLEMENT", "Settle", "PENDING"), new("REPORTS", "Report", "AVAILABLE")],
        [new("E001", "Nguyen An", "Operations", "Specialist", "G5", "ACTIVE", "STANDARD"), new("E002", "Tran Binh", "Sales", "Manager", "G7", "ACTIVE", "VARIABLE")],
        [new("BASE", 32_000_000m, "FORMULA", "CALCULATED", 10, "FORMULA:BASE_V3", null), new("ATT_ALLOWANCE", 1_200_000m, "FORMULA", "CALCULATED", 20, "ATTENDANCE", null), new("PERFORMANCE_BONUS", 4_800_000m, "FORMULA", "FUNDED", 30, "KPI", 4_700_000m)],
        [new("PERFORMANCE_POOL", 120_000_000m, 121_900_000m, 120_000_000m, 1_900_000m, 0m, .9844m, "WEIGHTED")],
        [new("PIT", "TAX", 3_240_000m, "CALCULATED"), new("SOCIAL_INSURANCE", "EMPLOYEE_CONTRIBUTION", 2_400_000m, "CALCULATED"), new("EXTERNAL_ADJUSTMENT", "OTHER_DEDUCTION", null, "UNAVAILABLE")],
        [new("WARNING", "FUND", "FUND.COVERAGE_SHORTAGE", "Funding demand exceeds available amount."), new("INFO", "PERIOD", "PAYROLL.REVISION_PINNED", "Snapshot revision 1 is selected.")],
        [new("E001", "WORK_DAYS", "21.5", "days", "ATTENDANCE", "2026-07-31", "3"), new("E002", "SALES_VALUE", "840000000", "VND", "ERP", "2026-07-31", "2")],
        [new("E001", "2026-07", "21.5 days", "VALID", "ATTENDANCE_BATCH_07"), new("E002", "2026-07", "22 days", "VALID", "ATTENDANCE_BATCH_07")],
        [new("E001", "QUALITY", "Quality score", 92m, "80–100", "VALID", "KPI_BATCH_07"), new("E002", "SALES", "Sales achievement", 105m, ">= 100", "VALID", "KPI_BATCH_07")],
        [new("E001", "BASE", 32_000_000m, 31_000_000m, 1_000_000m, "Assignment revision")],
        [new("SCN-001", "July hiring plan", "2", "SNAPSHOT-1", "DRAFT", "2 input overrides")],
        [new("APR-2026-07", "1", "DEMO-RUN-001", "7a21…f04c", "IN_REVIEW", "demo.reviewer")],
        [new("2026-08-01 09:12", "demo.operator", "SUBMIT", "Ready for review", "SUBMITTED"), new("2026-08-01 10:04", "demo.reviewer", "START REVIEW", "", "IN_REVIEW")],
        [new("6421", "Payroll expense", 38_000_000m, 0m, "OPS", "DEMO-RUN-001"), new("3341", "Payroll payable", 0m, 38_000_000m, "OPS", "DEMO-RUN-001")],
        [new("Payroll summary", "DEMO", "2026-07", "1", "DEMO-RUN-001", "AVAILABLE"), new("Subject detail", "DEMO", "2026-07", "1", "DEMO-RUN-001", "AVAILABLE"), new("Settlement summary", "DEMO", "2026-07", "1", "DEMO-RUN-001", "AVAILABLE")]);
}

public sealed class OperationalWorkspaceViewModel : ViewModelBase
{
    private readonly CompanyPresentationContext company; private readonly CultureState culture;
    private string selectedArea = "DASHBOARD", searchText = string.Empty, severityFilter = "ALL";
    private bool busy; private string? errorCode; private object? selectedItem;

    public OperationalWorkspaceViewModel(CompanyPresentationContext company, CultureState culture, IReadOnlyList<StatusCard> cards,
        IReadOnlyList<WorkspaceStep> steps, IReadOnlyList<SubjectRow> subjects, IReadOnlyList<ComponentRow> components,
        IReadOnlyList<FundRow> funds, IReadOnlyList<StatutoryRow> statutory, IReadOnlyList<FindingRow> findings,
        IReadOnlyList<InputRow> inputs, IReadOnlyList<AttendanceRow> attendance, IReadOnlyList<KpiRow> kpis,
        IReadOnlyList<VarianceRow> variances, IReadOnlyList<ScenarioRow> scenarios, IReadOnlyList<ApprovalRow> approvals,
        IReadOnlyList<ApprovalEventRow> approvalEvents, IReadOnlyList<AccountingRow> accounting, IReadOnlyList<ReportRow> reports)
    {
        this.company = company; this.culture = culture; Cards = cards; Steps = steps; Subjects = subjects; Components = components;
        Funds = funds; Statutory = statutory; Findings = findings; Inputs = inputs; Attendance = attendance; Kpis = kpis;
        Variances = variances; Scenarios = scenarios; Approvals = approvals; ApprovalEvents = approvalEvents; Accounting = accounting; Reports = reports;
        Areas = new(["DASHBOARD", "SUBJECTS", "INPUTS", "ATTENDANCE", "KPI", "PREPARE", "CALCULATE", "FUNDS", "VALIDATE", "EXPLAIN", "VARIANCE", "SCENARIO", "APPROVAL", "SETTLEMENT", "ACCOUNTING", "REPORTS"]);
        company.CompanyChanged += (_, _) => ResetForCompany(); culture.CultureChanged += (_, _) => Changed(string.Empty);
        SelectAreaCommand = new DelegateCommand<string>(SelectArea); DrillDownCommand = new DelegateCommand<string>(SelectArea);
        SelectItemCommand = new DelegateCommand<object>(x => SelectedItem = x); ClearSearchCommand = new DelegateCommand(() => SearchText = string.Empty);
        SetSeverityCommand = new DelegateCommand<string>(x => SeverityFilter = x ?? "ALL");
        RunCommand = new AsyncDelegateCommand(async () => { Busy = true; ErrorCode = null; try { await Task.Delay(120); } catch { ErrorCode = "DESKTOP.OPERATION_FAILED"; } finally { Busy = false; } });
    }

    public ObservableCollection<string> Areas { get; }
    public IReadOnlyList<StatusCard> Cards { get; } public IReadOnlyList<WorkspaceStep> Steps { get; }
    public IReadOnlyList<SubjectRow> Subjects { get; } public IReadOnlyList<ComponentRow> Components { get; }
    public IReadOnlyList<FundRow> Funds { get; } public IReadOnlyList<StatutoryRow> Statutory { get; }
    public IReadOnlyList<FindingRow> Findings { get; } public IReadOnlyList<InputRow> Inputs { get; }
    public IReadOnlyList<AttendanceRow> Attendance { get; } public IReadOnlyList<KpiRow> Kpis { get; }
    public IReadOnlyList<VarianceRow> Variances { get; } public IReadOnlyList<ScenarioRow> Scenarios { get; }
    public IReadOnlyList<ApprovalRow> Approvals { get; } public IReadOnlyList<ApprovalEventRow> ApprovalEvents { get; }
    public IReadOnlyList<AccountingRow> Accounting { get; } public IReadOnlyList<ReportRow> Reports { get; }
    public IEnumerable<SubjectRow> FilteredSubjects => Filter(Subjects, x => $"{x.EmployeeCode} {x.Name} {x.Organization} {x.Status}");
    public IEnumerable<InputRow> FilteredInputs => Filter(Inputs, x => $"{x.EmployeeCode} {x.Code} {x.Source}");
    public IEnumerable<AttendanceRow> FilteredAttendance => Filter(Attendance, x => $"{x.EmployeeCode} {x.Period} {x.Source} {x.Status}");
    public IEnumerable<KpiRow> FilteredKpis => Filter(Kpis, x => $"{x.EmployeeCode} {x.Code} {x.Name} {x.Status}");
    public IEnumerable<FindingRow> FilteredFindings => Filter(Findings.Where(x => SeverityFilter == "ALL" || x.Severity == SeverityFilter), x => $"{x.Code} {x.Message} {x.Scope}");
    public IEnumerable<VarianceRow> FilteredVariances => Filter(Variances, x => $"{x.EmployeeCode} {x.Component} {x.Reason}");

    public string CompanyLabel => company.CompanyId.Value.ToString("N")[..8].ToUpperInvariant();
    public string Header => Local("Payroll 07/2026 · Snapshot 1 · IN REVIEW", "Kỳ lương 07/2026 · Bản chụp 1 · ĐANG RÀ SOÁT");
    public string DemoNotice => Local("ACTIPRO TRIAL · NON-PRODUCTION DEMO", "ACTIPRO TRIAL · DỮ LIỆU DEMO PHI SẢN XUẤT");
    public string RevisionIdentity => Local("Exact revision · snapshot 1 · run DEMO-RUN-001 · hash 7a21…f04c", "Đúng phiên bản · bản chụp 1 · lượt DEMO-RUN-001 · hash 7a21…f04c");
    public string Purpose => Local(Purposes[SelectedArea].English, Purposes[SelectedArea].Vietnamese);
    public string SearchPlaceholder => Local("Search visible rows", "Tìm trong các dòng hiển thị");
    public string EmptyMessage => Local("No records match the current filter.", "Không có dữ liệu phù hợp bộ lọc.");
    public string ReadOnlyReason => Local("Read-only · modification requires an application capability.", "Chỉ đọc · thao tác sửa cần capability từ lớp ứng dụng.");
    public string MissingStatutoryNotice => Local("UNAVAILABLE is not zero. The provider returned no statutory value.", "UNAVAILABLE không phải số không. Nhà cung cấp chưa trả dữ liệu nghĩa vụ.");
    public string ScenarioNotice => Local("SCENARIO · NON-PRODUCTION · no authoritative approval or posting", "KỊCH BẢN · PHI SẢN XUẤT · không phê duyệt hoặc hạch toán chính thức");
    public string AccountingSummary => Local("Total debit 38,000,000 · Total credit 38,000,000 · BALANCED", "Tổng Nợ 38.000.000 · Tổng Có 38.000.000 · CÂN BẰNG");
    public string PrepareResult => Local("Prepared · Snapshot revision 1 · hash 7a21…f04c", "Đã chuẩn bị · Bản chụp 1 · hash 7a21…f04c");
    public string CalculationResult => Local("Completed · Run DEMO-RUN-001 · Revision 1 · result hash 7a21…f04c", "Hoàn tất · Lượt DEMO-RUN-001 · Phiên bản 1 · hash 7a21…f04c");
    public string CompanyBackstage => Local("Company context and selection", "Ngữ cảnh và lựa chọn công ty");
    public string AboutBackstage => Local("PayCalc24 0.1.0-mvp · Actipro evaluation", "PayCalc24 0.1.0-mvp · Bản đánh giá Actipro");
    public string DemoLabel => Local("DEMO", "DEMO"); public string AreaTitle => culture.Culture == "vi-VN" ? Translate(SelectedArea) : SelectedArea.Replace('_', ' ');
    public string ActivityText => Busy ? Local("Working…", "Đang xử lý…") : Local("Ready · demo adapter · production database not connected", "Sẵn sàng · bộ demo · chưa kết nối CSDL sản xuất");
    public string? ErrorCode { get => errorCode; private set { errorCode = value; Changed(); Changed(nameof(HasError)); } }
    public bool HasError => ErrorCode is not null; public string ErrorMessage => Local("The operation could not be completed. Retry when ready.", "Không thể hoàn tất thao tác. Hãy thử lại.");
    public string SearchText { get => searchText; set { searchText = value ?? string.Empty; Changed(); RaiseFiltered(); } }
    public string SeverityFilter { get => severityFilter; private set { severityFilter = value; Changed(); Changed(nameof(FilteredFindings)); } }
    public object? SelectedItem { get => selectedItem; set { selectedItem = value; Changed(); Changed(nameof(HasSelection)); } }
    public bool HasSelection => SelectedItem is not null; public bool Busy { get => busy; private set { busy = value; Changed(); Changed(nameof(ActivityText)); } }
    public bool IsEmpty => SelectedArea switch { "SUBJECTS" => !FilteredSubjects.Any(), "INPUTS" => !FilteredInputs.Any(), "ATTENDANCE" => !FilteredAttendance.Any(), "KPI" => !FilteredKpis.Any(), "VALIDATE" => !FilteredFindings.Any(), "VARIANCE" => !FilteredVariances.Any(), "SCENARIO" => Scenarios.Count == 0, "APPROVAL" => Approvals.Count == 0, "ACCOUNTING" => Accounting.Count == 0, "REPORTS" => Reports.Count == 0, _ => false };
    public bool IsReadOnly => SelectedArea is "SUBJECTS" or "INPUTS" or "ATTENDANCE" or "SCENARIO";
    public bool CanApprove => IsApproval && Approvals.Any(x => x.Status == "IN_REVIEW"); public bool CanLock => IsApproval && Approvals.Count < 0;
    public bool CanCommitKpi => IsKpi && Kpis.All(x => x.Status == "VALID");

    public bool IsDashboard => Is("DASHBOARD"); public bool IsSubjects => Is("SUBJECTS"); public bool IsInputs => Is("INPUTS"); public bool IsAttendance => Is("ATTENDANCE");
    public bool IsKpi => Is("KPI"); public bool IsPrepare => Is("PREPARE"); public bool IsCalculate => Is("CALCULATE"); public bool IsFunds => Is("FUNDS");
    public bool IsValidate => Is("VALIDATE"); public bool IsExplain => Is("EXPLAIN"); public bool IsVariance => Is("VARIANCE"); public bool IsScenario => Is("SCENARIO");
    public bool IsApproval => Is("APPROVAL"); public bool IsSettlement => Is("SETTLEMENT"); public bool IsAccounting => Is("ACCOUNTING"); public bool IsReports => Is("REPORTS");
    public DelegateCommand<string> SelectAreaCommand { get; } public DelegateCommand<object> SelectItemCommand { get; }
    public DelegateCommand ClearSearchCommand { get; } public DelegateCommand<string> SetSeverityCommand { get; }
    public DelegateCommand<string> DrillDownCommand { get; } public AsyncDelegateCommand RunCommand { get; }

    public string SelectedArea { get => selectedArea; private set { if (selectedArea == value) return; selectedArea = value; searchText = string.Empty; SelectedItem = DefaultSelection(value); Changed(); Changed(nameof(AreaTitle)); Changed(nameof(Purpose)); Changed(nameof(IsReadOnly)); Changed(nameof(CanApprove)); Changed(nameof(CanLock)); Changed(nameof(CanCommitKpi)); foreach (var property in AreaProperties) Changed(property); RaiseFiltered(); } }
    public void SelectArea(string? area) { if (area is not null && Areas.Contains(area)) SelectedArea = area; }
    private bool Is(string area) => SelectedArea == area;
    private object? DefaultSelection(string area) => area switch { "SUBJECTS" => Subjects.Count > 0 ? Subjects[0] : null, "INPUTS" => Inputs.Count > 0 ? Inputs[0] : null, "ATTENDANCE" => Attendance.Count > 0 ? Attendance[0] : null, "KPI" => Kpis.Count > 0 ? Kpis[0] : null, "CALCULATE" or "EXPLAIN" => Components.Count > 0 ? Components[0] : null, "FUNDS" => Funds.Count > 0 ? Funds[0] : null, "VALIDATE" => Findings.Count > 0 ? Findings[0] : null, "VARIANCE" => Variances.Count > 0 ? Variances[0] : null, "SCENARIO" => Scenarios.Count > 0 ? Scenarios[0] : null, "APPROVAL" => Approvals.Count > 0 ? Approvals[0] : null, "SETTLEMENT" => Statutory.Count > 0 ? Statutory[0] : null, "ACCOUNTING" => Accounting.Count > 0 ? Accounting[0] : null, "REPORTS" => Reports.Count > 0 ? Reports[0] : null, _ => null };
    private IEnumerable<T> Filter<T>(IEnumerable<T> rows, Func<T, string> text) => string.IsNullOrWhiteSpace(SearchText) ? rows : rows.Where(x => text(x).Contains(SearchText, StringComparison.CurrentCultureIgnoreCase));
    private void RaiseFiltered() { Changed(nameof(FilteredSubjects)); Changed(nameof(FilteredInputs)); Changed(nameof(FilteredAttendance)); Changed(nameof(FilteredKpis)); Changed(nameof(FilteredFindings)); Changed(nameof(FilteredVariances)); Changed(nameof(IsEmpty)); }
    private void ResetForCompany() { selectedArea = "DASHBOARD"; Changed(string.Empty); }
    private string Local(string en, string vi) => culture.Culture == "vi-VN" ? vi : en;
    private static string Translate(string code) => code switch { "DASHBOARD" => "TỔNG QUAN", "SUBJECTS" => "NHÂN SỰ TÍNH LƯƠNG", "INPUTS" => "DỮ LIỆU ĐẦU VÀO", "ATTENDANCE" => "CHẤM CÔNG", "KPI" => "HIỆU SUẤT / KPI", "PREPARE" => "CHUẨN BỊ", "CALCULATE" => "TÍNH LƯƠNG", "FUNDS" => "NGUỒN QUỸ", "VALIDATE" => "KIỂM TRA", "EXPLAIN" => "GIẢI TRÌNH", "VARIANCE" => "BIẾN ĐỘNG", "SCENARIO" => "KỊCH BẢN", "APPROVAL" => "PHÊ DUYỆT", "SETTLEMENT" => "QUYẾT TOÁN", "ACCOUNTING" => "HẠCH TOÁN", "REPORTS" => "BÁO CÁO", _ => code };
    private static readonly string[] AreaProperties = [nameof(IsDashboard), nameof(IsSubjects), nameof(IsInputs), nameof(IsAttendance), nameof(IsKpi), nameof(IsPrepare), nameof(IsCalculate), nameof(IsFunds), nameof(IsValidate), nameof(IsExplain), nameof(IsVariance), nameof(IsScenario), nameof(IsApproval), nameof(IsSettlement), nameof(IsAccounting), nameof(IsReports)];
    private static readonly Dictionary<string, (string English, string Vietnamese)> Purposes = new() { ["DASHBOARD"]=("Operational overview and attention items.","Tổng quan vận hành và các mục cần chú ý."), ["SUBJECTS"]=("Payroll population for the selected period.","Nhân sự tính lương của kỳ đã chọn."), ["INPUTS"]=("Dynamic input facts and provenance.","Dữ liệu đầu vào động và nguồn gốc."), ["ATTENDANCE"]=("Payroll-relevant attendance facts.","Dữ liệu chấm công liên quan đến lương."), ["KPI"]=("Structured performance results.","Kết quả hiệu suất có cấu trúc."), ["PREPARE"]=("Confirm calculation readiness.","Xác nhận mức sẵn sàng tính lương."), ["CALCULATE"]=("Run pinned calculation and inspect components.","Chạy tính lương đã ghim và xem thành phần."), ["FUNDS"]=("Inspect funding coverage and deficits.","Kiểm tra mức cấp quỹ và thiếu hụt."), ["VALIDATE"]=("Resolve central payroll diagnostics.","Xử lý chẩn đoán tập trung."), ["EXPLAIN"]=("Inspect immutable component provenance.","Xem nguồn gốc thành phần bất biến."), ["VARIANCE"]=("Compare pinned payroll contexts.","So sánh các ngữ cảnh đã ghim."), ["SCENARIO"]=("Explore isolated non-production revisions.","Khảo sát phiên bản phi sản xuất tách biệt."), ["APPROVAL"]=("Review the immutable approval lifecycle.","Rà soát vòng đời phê duyệt bất biến."), ["SETTLEMENT"]=("Inspect statutory items and net pay.","Xem nghĩa vụ và thực lĩnh."), ["ACCOUNTING"]=("Inspect the canonical accounting document.","Xem chứng từ hạch toán chuẩn."), ["REPORTS"]=("Generate explicitly pinned reports.","Tạo báo cáo từ ngữ cảnh đã ghim.") };
}
