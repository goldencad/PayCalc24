using PayCalc24.Contracts.Diagnostics;
using PayCalc24.Contracts.Identity;

namespace PayCalc24.Application.Identity;

public interface ICompanyScopeGuard
{
    void EnsureCurrent(CompanyId requestedCompanyId);
}

public sealed class CompanyScopeGuard(ICompanyContext companyContext) : ICompanyScopeGuard
{
    public void EnsureCurrent(CompanyId requestedCompanyId)
    {
        if (requestedCompanyId == companyContext.CompanyId)
        {
            return;
        }

        throw new CompanyScopeViolationException(new Diagnostic(
            DiagnosticCodes.CompanyScopeMismatch,
            DiagnosticSeverity.Error,
            new Dictionary<string, object?>
            {
                ["requestedCompanyId"] = requestedCompanyId.Value,
                ["currentCompanyId"] = companyContext.CompanyId.Value
            }));
    }
}

public sealed class CompanyScopeViolationException(Diagnostic diagnostic) : Exception(diagnostic.Code)
{
    public Diagnostic Diagnostic { get; } = diagnostic;
}
