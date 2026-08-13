using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
namespace PayCalc24.Infrastructure.MariaDb.Migrations;

public partial class Task18StatutoryAccounting : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)=>migrationBuilder.Sql("""
CREATE TABLE StatutoryCalculationResults (
 Id char(36) NOT NULL, CompanyId char(36) NOT NULL, PayrollPeriodId char(36) NOT NULL, SnapshotId char(36) NOT NULL,
 SnapshotRevision int NOT NULL, CalculationRunId char(36) NOT NULL, PayrollSubjectId char(36) NOT NULL,
 ResultKind varchar(24) NOT NULL, Status varchar(24) NOT NULL, JurisdictionCode varchar(16) NOT NULL,
 ProviderCode varchar(128) NOT NULL, ProviderVersion varchar(64) NOT NULL, PolicyVersion varchar(64) NOT NULL,
 BusinessDate date NOT NULL, RequestHash varchar(64) NOT NULL, ResultHash varchar(64) NOT NULL, CorrelationId varchar(128) NOT NULL,
 TotalEmployeeContribution decimal(28,8) NULL, TotalEmployerContribution decimal(28,8) NULL,
 TaxableIncome decimal(28,8) NULL, AssessableIncome decimal(28,8) NULL, TaxAmount decimal(28,8) NULL,
 PRIMARY KEY(Id), UNIQUE KEY UX_Statutory_Request(CompanyId,ProviderCode,ProviderVersion,PolicyVersion,RequestHash),
 KEY IX_Statutory_Run(CompanyId,SnapshotId,CalculationRunId), KEY IX_Statutory_Subject(CompanyId,PayrollSubjectId,PayrollPeriodId)
);
CREATE TABLE StatutoryContributionItems (
 Id char(36) NOT NULL, StatutoryResultId char(36) NOT NULL, Code varchar(128) NOT NULL, Category varchar(64) NOT NULL,
 Party varchar(16) NOT NULL, CalculationBase decimal(28,8) NOT NULL, Rate decimal(28,8) NULL, Amount decimal(28,8) NOT NULL,
 PolicyReference varchar(256) NULL, CapOrFloorReference varchar(256) NULL, PRIMARY KEY(Id),
 KEY IX_Contribution_Result(StatutoryResultId,Party,Code)
);
CREATE TABLE StatutoryDeductionItems (
 Id char(36) NOT NULL, StatutoryResultId char(36) NOT NULL, Code varchar(128) NOT NULL,
 Amount decimal(28,8) NOT NULL, PolicyReference varchar(256) NULL, SourceReference varchar(256) NULL,
 PRIMARY KEY(Id), KEY IX_Deduction_Result(StatutoryResultId,Code)
);
CREATE TABLE IncomeTaxBandItems (
 Id char(36) NOT NULL, StatutoryResultId char(36) NOT NULL, Code varchar(128) NOT NULL,
 LowerBound decimal(28,8) NULL, UpperBound decimal(28,8) NULL, Rate decimal(28,8) NULL,
 Amount decimal(28,8) NOT NULL, PolicyReference varchar(256) NULL, PRIMARY KEY(Id), KEY IX_TaxBand_Result(StatutoryResultId)
);
CREATE TABLE NetPayResults (
 Id char(36) NOT NULL, CompanyId char(36) NOT NULL, PayrollPeriodId char(36) NOT NULL, SnapshotId char(36) NOT NULL,
 SnapshotRevision int NOT NULL, CalculationRunId char(36) NOT NULL, ApprovalCaseId char(36) NULL, PayrollSubjectId char(36) NOT NULL,
 TotalEarnings decimal(28,8) NOT NULL, TotalEmployeeStatutoryDeductions decimal(28,8) NOT NULL,
 TotalOtherDeductions decimal(28,8) NOT NULL, NetPay decimal(28,8) NOT NULL, ResultHash varchar(64) NOT NULL,
 ExecutionMode varchar(16) NOT NULL, PRIMARY KEY(Id), KEY IX_NetPay_Run(CompanyId,SnapshotId,CalculationRunId)
);
CREATE TABLE EmployerCostResults (
 Id char(36) NOT NULL, CompanyId char(36) NOT NULL, PayrollPeriodId char(36) NOT NULL, SnapshotId char(36) NOT NULL,
 SnapshotRevision int NOT NULL, CalculationRunId char(36) NOT NULL, PayrollSubjectId char(36) NOT NULL,
 PayrollCost decimal(28,8) NOT NULL, EmployerStatutoryCost decimal(28,8) NOT NULL,
 TotalEmployerCost decimal(28,8) NOT NULL, ResultHash varchar(64) NOT NULL, PRIMARY KEY(Id),
 KEY IX_EmployerCost_Run(CompanyId,SnapshotId,CalculationRunId)
);
CREATE TABLE PayrollAccountingDocuments (
 Id char(36) NOT NULL, CompanyId char(36) NOT NULL, PayrollPeriodId char(36) NOT NULL, SnapshotId char(36) NOT NULL,
 SnapshotRevision int NOT NULL, CalculationRunId char(36) NOT NULL, ApprovalCaseId char(36) NOT NULL,
 CurrencyCode varchar(8) NOT NULL, PostingDate date NOT NULL, DocumentReference varchar(128) NOT NULL,
 DocumentVersion int NOT NULL, TotalDebit decimal(28,8) NOT NULL, TotalCredit decimal(28,8) NOT NULL,
 ResultHash varchar(64) NOT NULL, ExecutionMode varchar(16) NOT NULL, PRIMARY KEY(Id),
 UNIQUE KEY UX_Accounting_Identity(CompanyId,PayrollPeriodId,SnapshotRevision,CalculationRunId,DocumentVersion)
);
CREATE TABLE PayrollAccountingLines (
 Id char(36) NOT NULL, AccountingDocumentId char(36) NOT NULL, PostingKey varchar(128) NOT NULL,
 Side varchar(8) NOT NULL, Amount decimal(28,8) NOT NULL, SourceReference varchar(256) NOT NULL,
 OrganizationDimension varchar(128) NULL, PayrollSubjectId char(36) NULL, PRIMARY KEY(Id),
 KEY IX_AccountingLine_Document(AccountingDocumentId,PostingKey)
);
CREATE TABLE PayrollIntegrationDeliveries (
 Id char(36) NOT NULL, CompanyId char(36) NOT NULL, AccountingDocumentId char(36) NOT NULL,
 TargetSystem varchar(64) NOT NULL, IdempotencyKey varchar(128) NOT NULL, RequestFingerprint varchar(64) NOT NULL,
 Status varchar(16) NOT NULL, ExternalDocumentReference varchar(256) NULL, ResultHash varchar(64) NOT NULL,
 CorrelationId varchar(128) NOT NULL, PRIMARY KEY(Id), UNIQUE KEY UX_Delivery_Retry(CompanyId,TargetSystem,IdempotencyKey),
 KEY IX_Delivery_Document(CompanyId,AccountingDocumentId,TargetSystem)
);
""");
    protected override void Down(MigrationBuilder migrationBuilder)=>migrationBuilder.Sql("DROP TABLE PayrollIntegrationDeliveries; DROP TABLE PayrollAccountingLines; DROP TABLE PayrollAccountingDocuments; DROP TABLE EmployerCostResults; DROP TABLE NetPayResults; DROP TABLE IncomeTaxBandItems; DROP TABLE StatutoryDeductionItems; DROP TABLE StatutoryContributionItems; DROP TABLE StatutoryCalculationResults;");
}
