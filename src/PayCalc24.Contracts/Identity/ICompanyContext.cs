namespace PayCalc24.Contracts.Identity;

/// <summary>Validated company scope supplied by the TS24 Core boundary.</summary>
public interface ICompanyContext
{
    CompanyId CompanyId { get; }
}
