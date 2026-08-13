using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
namespace PayCalc24.Infrastructure.MariaDb.Migrations;

public partial class Task10PayrollCalculationResults : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
ALTER TABLE PayrollSnapshotPayComponentVersions ADD ComponentCode varchar(64) NULL, ADD Required tinyint(1) NOT NULL DEFAULT 1, ADD SourceReference varchar(128) NULL, ADD ExpectedDataType varchar(16) NULL;
ALTER TABLE PayrollSnapshotFormulaVersions ADD Expression text NULL;
CREATE TABLE PayrollSnapshotComponentDependencies (SnapshotId char(36) NOT NULL,CompensationSchemeId char(36) NOT NULL,PayComponentId char(36) NOT NULL,DependsOnPayComponentId char(36) NOT NULL,PRIMARY KEY(SnapshotId,CompensationSchemeId,PayComponentId,DependsOnPayComponentId));
CREATE TABLE PayrollCalculationRuns (Id char(36) NOT NULL,CompanyId char(36) NOT NULL,PayrollPeriodId char(36) NOT NULL,SnapshotId char(36) NOT NULL,SnapshotRevision int NOT NULL,ExecutionMode int NOT NULL,EngineVersion varchar(32) NOT NULL,Status int NOT NULL,StartedAt datetime(6) NOT NULL,StartedBy char(36) NOT NULL,CompletedAt datetime(6) NULL,CompletedBy char(36) NULL,CorrelationId varchar(128) NOT NULL,IdempotencyKey varchar(128) NOT NULL,RequestFingerprint char(64) NOT NULL,SnapshotHash char(64) NOT NULL,ResultHash char(64) NULL,FailureDiagnosticCode varchar(160) NULL,IsAuthoritative tinyint(1) NOT NULL,PRIMARY KEY(Id),UNIQUE KEY UX_CalculationRuns_Idempotency(CompanyId,SnapshotId,IdempotencyKey),KEY IX_CalculationRuns_Period(CompanyId,PayrollPeriodId),KEY IX_CalculationRuns_Snapshot_Mode(SnapshotId,ExecutionMode));
CREATE TABLE PayrollSubjectCalculationResults (Id char(36) NOT NULL,CalculationRunId char(36) NOT NULL,CompanyId char(36) NOT NULL,PayrollSubjectId char(36) NOT NULL,EmployeeCode varchar(64) NOT NULL,ComponentResultCount int NOT NULL,CalculationStatus int NOT NULL,ResultHash char(64) NOT NULL,DiagnosticCode varchar(160) NULL,CreatedAt datetime(6) NOT NULL,PRIMARY KEY(Id),UNIQUE KEY UX_SubjectResults_Run_Subject(CalculationRunId,PayrollSubjectId),CONSTRAINT FK_SubjectResults_Run FOREIGN KEY(CalculationRunId) REFERENCES PayrollCalculationRuns(Id));
CREATE TABLE PayComponentCalculationResults (Id char(36) NOT NULL,SubjectResultId char(36) NOT NULL,CalculationRunId char(36) NOT NULL,CompanyId char(36) NOT NULL,PayrollPeriodId char(36) NOT NULL,SnapshotId char(36) NOT NULL,PayrollSubjectId char(36) NOT NULL,CompensationSchemeVersionId char(36) NOT NULL,PayComponentId char(36) NOT NULL,PayComponentVersion int NOT NULL,ComponentCode varchar(64) NOT NULL,Sequence int NOT NULL,CalculationMethod int NOT NULL,Status int NOT NULL,ResultDataType varchar(16) NULL,DecimalValue decimal(28,8) NULL,IntegerValue bigint NULL,BooleanValue tinyint(1) NULL,DateValue date NULL,TextValue varchar(4000) NULL,FormulaDefinitionId char(36) NULL,FormulaVersionId char(36) NULL,FormulaChecksum char(64) NULL,ExplainTrace json NULL,DiagnosticCode varchar(160) NULL,EngineVersion varchar(32) NOT NULL,ExecutionMode int NOT NULL,CorrelationId varchar(128) NOT NULL,ResultHash char(64) NOT NULL,CreatedAt datetime(6) NOT NULL,PRIMARY KEY(Id),UNIQUE KEY UX_ComponentResults_Subject_Component(SubjectResultId,PayComponentId),KEY IX_ComponentResults_Run_Subject(CalculationRunId,PayrollSubjectId),CONSTRAINT FK_ComponentResults_Subject FOREIGN KEY(SubjectResultId) REFERENCES PayrollSubjectCalculationResults(Id));
CREATE TABLE CalculationInputProvenance (ComponentResultId char(36) NOT NULL,PayrollInputLedgerEntryId char(36) NOT NULL,PRIMARY KEY(ComponentResultId,PayrollInputLedgerEntryId),CONSTRAINT FK_CalculationInputProvenance_Result FOREIGN KEY(ComponentResultId) REFERENCES PayComponentCalculationResults(Id));
CREATE TABLE CalculationParameterVersionProvenance (ComponentResultId char(36) NOT NULL,ParameterSetVersionId char(36) NOT NULL,PRIMARY KEY(ComponentResultId,ParameterSetVersionId));
CREATE TABLE CalculationLookupVersionProvenance (ComponentResultId char(36) NOT NULL,LookupTableVersionId char(36) NOT NULL,PRIMARY KEY(ComponentResultId,LookupTableVersionId));
CREATE TABLE CalculationRuleSetVersionProvenance (ComponentResultId char(36) NOT NULL,RuleSetVersionId char(36) NOT NULL,PRIMARY KEY(ComponentResultId,RuleSetVersionId));
""");
    }
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        foreach(var table in new[]{"CalculationRuleSetVersionProvenance","CalculationLookupVersionProvenance","CalculationParameterVersionProvenance","CalculationInputProvenance","PayComponentCalculationResults","PayrollSubjectCalculationResults","PayrollCalculationRuns","PayrollSnapshotComponentDependencies"})migrationBuilder.DropTable(table);
        migrationBuilder.Sql("ALTER TABLE PayrollSnapshotFormulaVersions DROP COLUMN Expression; ALTER TABLE PayrollSnapshotPayComponentVersions DROP COLUMN ExpectedDataType,DROP COLUMN SourceReference,DROP COLUMN Required,DROP COLUMN ComponentCode;");
    }
}
