using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
namespace PayCalc24.Infrastructure.MariaDb.Migrations;

public partial class Task17PayrollApproval : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
CREATE TABLE PayrollApprovalCases (
  Id char(36) NOT NULL, CompanyId char(36) NOT NULL, PayrollPeriodId char(36) NOT NULL,
  SnapshotId char(36) NOT NULL, SnapshotRevision int NOT NULL, SnapshotHash varchar(64) NOT NULL,
  CalculationRunId char(36) NOT NULL, CalculationResultHash varchar(64) NOT NULL,
  FundResultHashesJson longtext NOT NULL, ReviewContextFingerprint varchar(64) NOT NULL,
  ApprovalFingerprint varchar(64) NOT NULL, Status varchar(24) NOT NULL, Revision bigint NOT NULL,
  SupersedesApprovalCaseId char(36) NULL, CreatedAt datetime(6) NOT NULL, CreatedBy char(36) NOT NULL,
  SubmittedAt datetime(6) NULL, SubmittedBy char(36) NULL, ReviewStartedAt datetime(6) NULL, ReviewedBy char(36) NULL,
  ApprovedAt datetime(6) NULL, ApprovedBy char(36) NULL, RejectedAt datetime(6) NULL, RejectedBy char(36) NULL,
  LockedAt datetime(6) NULL, LockedBy char(36) NULL, CurrentDecisionReason varchar(1024) NULL, CorrelationId varchar(128) NOT NULL,
  PRIMARY KEY (Id), UNIQUE KEY UX_Approval_Artifact (CompanyId,SnapshotId,CalculationRunId),
  KEY IX_Approval_Period (CompanyId,PayrollPeriodId,SnapshotRevision), KEY IX_Approval_Status (CompanyId,Status)
);
CREATE TABLE PayrollApprovalEvents (
  Id char(36) NOT NULL, CompanyId char(36) NOT NULL, ApprovalCaseId char(36) NOT NULL,
  FromStatus varchar(24) NOT NULL, ToStatus varchar(24) NOT NULL, ActorUserId char(36) NOT NULL,
  OccurredAt datetime(6) NOT NULL, Reason varchar(1024) NULL, CorrelationId varchar(128) NOT NULL,
  IdempotencyKey varchar(128) NOT NULL, PRIMARY KEY (Id),
  UNIQUE KEY UX_ApprovalEvent_Retry (CompanyId,ApprovalCaseId,ToStatus,IdempotencyKey),
  KEY IX_ApprovalEvent_History (CompanyId,ApprovalCaseId,OccurredAt)
);
CREATE TABLE PayrollAdjustmentRequests (
  Id char(36) NOT NULL, CompanyId char(36) NOT NULL, PayrollPeriodId char(36) NOT NULL,
  SourceApprovalCaseId char(36) NOT NULL, SourceSnapshotId char(36) NOT NULL, SourceCalculationRunId char(36) NOT NULL,
  AdjustmentType varchar(32) NOT NULL, Status varchar(24) NOT NULL, Reason varchar(1024) NOT NULL,
  RequestedBy char(36) NOT NULL, RequestedAt datetime(6) NOT NULL, AuthorizedBy char(36) NULL, AuthorizedAt datetime(6) NULL,
  NewSnapshotId char(36) NULL, NewSnapshotRevision int NULL, NewApprovalCaseId char(36) NULL,
  CorrelationId varchar(128) NOT NULL, Revision bigint NOT NULL, PRIMARY KEY (Id),
  KEY IX_Adjustment_Period (CompanyId,PayrollPeriodId), KEY IX_Adjustment_Source (CompanyId,SourceApprovalCaseId),
  KEY IX_Adjustment_Status (CompanyId,Status)
);
""");
    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("DROP TABLE PayrollAdjustmentRequests; DROP TABLE PayrollApprovalEvents; DROP TABLE PayrollApprovalCases;");
}
