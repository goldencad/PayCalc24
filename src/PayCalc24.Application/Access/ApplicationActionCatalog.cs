using PayCalc24.Contracts.Access;

namespace PayCalc24.Application.Access;

public static class ApplicationActionCatalog
{
    public static readonly IReadOnlyDictionary<string, ApplicationAction> All =
        new ApplicationAction[]
        {
            Read("PAYROLL.PERIOD.READ", "PAYROLL.READ"),
            Read("PAYROLL.SUBJECTS.READ", "PAYROLL.READ"),
            Read("PAYROLL.VALIDATION.READ", "PAYROLL.READ"),
            Read("PAYROLL.EXPLAIN.READ", "PAYROLL.READ"),
            Read("PAYROLL.FUND.READ", "PAYROLL.READ"),
            Read("SCENARIO.RESULT.READ", "SCENARIO.READ"),
            Read("APPROVAL.STATUS.READ", "PAYROLL.READ"),
            Read("SETTLEMENT.READ", "SETTLEMENT.READ"),
            Read("REPORT.RESULT.READ", "REPORT.READ"),
            Execute("PAYROLL.INPUT.SUBMIT", "PAYROLL.INPUT.WRITE"),
            Execute("ATTENDANCE.PREVIEW", "ATTENDANCE.IMPORT"),
            Execute("ATTENDANCE.COMMIT", "ATTENDANCE.IMPORT"),
            Execute("PERFORMANCE.KPI.VALIDATE_BATCH", "KPI.WRITE"),
            Execute("PERFORMANCE.KPI.COMMIT_BATCH", "KPI.WRITE"),
            Execute("PAYROLL.PREPARE", "PAYROLL.PREPARE"),
            Execute("PAYROLL.FREEZE", "PAYROLL.FREEZE"),
            Execute("PAYROLL.CALCULATE", "PAYROLL.CALCULATE"),
            Execute("SCENARIO.RUN", "SCENARIO.RUN"),
            Sensitive("APPROVAL.SUBMIT", "PAYROLL.SUBMIT", false),
            Sensitive("APPROVAL.APPROVE", "PAYROLL.APPROVE", true),
            Sensitive("APPROVAL.REJECT", "PAYROLL.APPROVE", true),
            Sensitive("APPROVAL.LOCK", "PAYROLL.LOCK", true, true),
            Sensitive("APPROVAL.REQUEST_ADJUSTMENT", "PAYROLL.ADJUST", true),
            Execute("REPORT.GENERATE", "REPORT.GENERATE"),
            Sensitive("ACCOUNTING.GENERATE", "ACCOUNTING.GENERATE", true),
            Sensitive("ACCOUNTING.PUBLISH", "ACCOUNTING.PUBLISH", true, true)
        }.ToDictionary(x => x.Code, StringComparer.Ordinal);

    private static ApplicationAction Read(string code, string permission) =>
        new(code, permission, false, ActionSensitivity.READ);
    private static ApplicationAction Execute(string code, string permission) =>
        new(code, permission, true, ActionSensitivity.EXECUTE);
    private static ApplicationAction Sensitive(string code, string permission, bool confirmation, bool irreversible = false) =>
        new(code, permission, true, irreversible ? ActionSensitivity.IRREVERSIBLE : ActionSensitivity.SENSITIVE, confirmation);
}
