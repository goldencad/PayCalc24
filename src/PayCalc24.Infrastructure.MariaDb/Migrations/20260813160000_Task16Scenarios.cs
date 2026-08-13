using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PayCalc24.Infrastructure.MariaDb.Organization;

#nullable disable

namespace PayCalc24.Infrastructure.MariaDb.Migrations;

[DbContext(typeof(PayCalc24DbContext))]
[Migration("20260813160000_Task16Scenarios")]
public sealed class Task16Scenarios : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            CREATE TABLE ScenarioDefinitions (
              Id CHAR(36) NOT NULL, CompanyId CHAR(36) NOT NULL, Code VARCHAR(64) NOT NULL,
              Name VARCHAR(256) NOT NULL, Description VARCHAR(1024) NULL, ScenarioType VARCHAR(24) NOT NULL,
              BaselinePayrollPeriodId CHAR(36) NULL, BaselineSnapshotId CHAR(36) NULL,
              BaselineSnapshotRevision INT NULL, Status VARCHAR(24) NOT NULL,
              CreatedBy CHAR(36) NOT NULL, CreatedAt DATETIME(6) NOT NULL, CorrelationId VARCHAR(128) NOT NULL,
              PRIMARY KEY (Id), UNIQUE KEY UX_ScenarioDefinitions_Company_Code (CompanyId, Code)
            );
            CREATE TABLE ScenarioSnapshots (
              Id CHAR(36) NOT NULL, ScenarioDefinitionId CHAR(36) NOT NULL, CompanyId CHAR(36) NOT NULL,
              Revision INT NOT NULL, ScenarioType VARCHAR(24) NOT NULL, ExecutionMode VARCHAR(24) NOT NULL,
              BaselinePayrollPeriodId CHAR(36) NOT NULL, BaselineSnapshotId CHAR(36) NOT NULL,
              BaselineSnapshotRevision INT NOT NULL, BaselineSnapshotHash CHAR(64) NOT NULL,
              BusinessDate DATE NOT NULL, HistoricalFactsJson JSON NOT NULL, PolicyConfigurationJson JSON NOT NULL,
              EngineVersionsJson JSON NOT NULL, CreatedBy CHAR(36) NOT NULL, CreatedAt DATETIME(6) NOT NULL,
              ScenarioHash CHAR(64) NOT NULL, PRIMARY KEY (Id),
              UNIQUE KEY UX_ScenarioSnapshots_Definition_Revision (ScenarioDefinitionId, Revision),
              KEY IX_ScenarioSnapshots_Company_Baseline (CompanyId, BaselineSnapshotId),
              CONSTRAINT FK_ScenarioSnapshots_Definition FOREIGN KEY (ScenarioDefinitionId) REFERENCES ScenarioDefinitions(Id) ON DELETE RESTRICT
            );
            CREATE TABLE ScenarioPolicyOverrides (
              Id CHAR(36) NOT NULL, ScenarioSnapshotId CHAR(36) NOT NULL, PolicyKind VARCHAR(48) NOT NULL,
              BaselineVersionId CHAR(36) NULL, OverrideVersionId CHAR(36) NOT NULL, Reason VARCHAR(512) NULL,
              OverrideOrder INT NOT NULL, PRIMARY KEY (Id),
              UNIQUE KEY UX_ScenarioPolicyOverrides_Canonical (ScenarioSnapshotId, PolicyKind, OverrideOrder),
              CONSTRAINT FK_ScenarioPolicyOverrides_Snapshot FOREIGN KEY (ScenarioSnapshotId) REFERENCES ScenarioSnapshots(Id) ON DELETE RESTRICT
            );
            CREATE TABLE ScenarioInputOverrides (
              Id CHAR(36) NOT NULL, ScenarioSnapshotId CHAR(36) NOT NULL, PayrollSubjectId CHAR(36) NOT NULL,
              PayrollInputDefinitionId CHAR(36) NOT NULL, InputCode VARCHAR(128) NOT NULL,
              DataType VARCHAR(24) NOT NULL, Unit VARCHAR(32) NOT NULL, OriginalValue VARCHAR(2048) NULL,
              OverrideValue VARCHAR(2048) NOT NULL, Reason VARCHAR(512) NULL, OverrideSequence INT NOT NULL,
              PRIMARY KEY (Id), UNIQUE KEY UX_ScenarioInputOverrides_Canonical (ScenarioSnapshotId, PayrollSubjectId, PayrollInputDefinitionId),
              CONSTRAINT FK_ScenarioInputOverrides_Snapshot FOREIGN KEY (ScenarioSnapshotId) REFERENCES ScenarioSnapshots(Id) ON DELETE RESTRICT
            );
            CREATE TABLE ScenarioExecutions (
              Id CHAR(36) NOT NULL, CompanyId CHAR(36) NOT NULL, ScenarioSnapshotId CHAR(36) NOT NULL,
              ScenarioRevision INT NOT NULL, ExecutionMode VARCHAR(24) NOT NULL, Status VARCHAR(24) NOT NULL,
              CalculationRunId CHAR(36) NULL, ScenarioHash CHAR(64) NOT NULL, ResultHash CHAR(64) NULL,
              EngineVersionsJson JSON NOT NULL, FundResultIdsJson JSON NOT NULL,
              StartedAt DATETIME(6) NOT NULL, CompletedAt DATETIME(6) NULL, CorrelationId VARCHAR(128) NOT NULL,
              IdempotencyKey VARCHAR(128) NOT NULL, RequestFingerprint CHAR(64) NOT NULL, DiagnosticCode VARCHAR(128) NULL,
              PRIMARY KEY (Id), UNIQUE KEY UX_ScenarioExecutions_Idempotency (CompanyId, ScenarioSnapshotId, IdempotencyKey),
              KEY IX_ScenarioExecutions_Snapshot_Status (ScenarioSnapshotId, Status),
              CONSTRAINT FK_ScenarioExecutions_Snapshot FOREIGN KEY (ScenarioSnapshotId) REFERENCES ScenarioSnapshots(Id) ON DELETE RESTRICT
            );
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable("ScenarioExecutions");
        migrationBuilder.DropTable("ScenarioInputOverrides");
        migrationBuilder.DropTable("ScenarioPolicyOverrides");
        migrationBuilder.DropTable("ScenarioSnapshots");
        migrationBuilder.DropTable("ScenarioDefinitions");
    }
}
