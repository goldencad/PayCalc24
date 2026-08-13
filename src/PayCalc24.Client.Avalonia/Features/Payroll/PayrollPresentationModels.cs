using PayCalc24.Client.Avalonia.Presentation;
using PayCalc24.Contracts.Integration;
using PayCalc24.Contracts.PayrollReview;

namespace PayCalc24.Client.Avalonia.Features.Payroll;

public sealed record DashboardProjection(string PeriodLabel, string LifecycleState,
    string CalculationStatus, string ApprovalStatus, int SubjectCount, int BlockingValidationCount);

public sealed class DashboardViewModel(DashboardProjection projection) : ViewModelBase
{
    public DashboardProjection Projection { get; } = projection;
}

/// <summary>Displays Task 15/18 immutable output; it has no engine or mutation dependency.</summary>
public sealed class PayrollSubjectDetailViewModel(
    IReadOnlyList<ComponentExplanation> components,
    IReadOnlyList<FundExplanation> funds,
    IReadOnlyList<StatutoryCalculationResult> statutory,
    NetPayResult? netPay,
    EmployerCostResult? employerCost,
    string provenanceHash) : ViewModelBase
{
    public IReadOnlyList<ComponentExplanation> Components { get; } = components;
    public IReadOnlyList<FundExplanation> Funds { get; } = funds;
    public IReadOnlyList<StatutoryCalculationResult> Statutory { get; } = statutory;
    public NetPayResult? NetPay { get; } = netPay;
    public EmployerCostResult? EmployerCost { get; } = employerCost;
    public string ProvenanceHash { get; } = provenanceHash;
}
