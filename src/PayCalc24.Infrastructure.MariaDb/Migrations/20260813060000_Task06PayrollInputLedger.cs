using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
#pragma warning disable CA1861
namespace PayCalc24.Infrastructure.MariaDb.Migrations;

public partial class Task06PayrollInputLedger : Migration
{
    protected override void Up(MigrationBuilder m)
    {
        m.CreateTable("PayrollInputDefinitionVersions",columns:t=>new{
            Id=t.Column<string>("char(36)",maxLength:36),DefinitionId=t.Column<string>("char(36)",maxLength:36),CompanyId=t.Column<string>("char(36)",maxLength:36),Revision=t.Column<int>("int"),Code=t.Column<string>("varchar(64)",maxLength:64),Name=t.Column<string>("varchar(256)",maxLength:256),Description=t.Column<string>("varchar(1024)",maxLength:1024,nullable:true),DataType=t.Column<int>("int"),UnitType=t.Column<int>("int"),SourceType=t.Column<int>("int"),AggregationType=t.Column<int>("int"),IsRequired=t.Column<bool>("tinyint(1)"),AllowManualEntry=t.Column<bool>("tinyint(1)"),AllowExternalEntry=t.Column<bool>("tinyint(1)"),AllowOverride=t.Column<bool>("tinyint(1)"),MinDecimal=t.Column<decimal>("decimal(28,8)",precision:28,scale:8,nullable:true),MaxDecimal=t.Column<decimal>("decimal(28,8)",precision:28,scale:8,nullable:true),MinInteger=t.Column<long>("bigint",nullable:true),MaxInteger=t.Column<long>("bigint",nullable:true),MaxTextLength=t.Column<int>("int",nullable:true),DisplayOrder=t.Column<int>("int",nullable:true),EffectiveFrom=t.Column<DateOnly>("date"),EffectiveTo=t.Column<DateOnly>("date",nullable:true),Status=t.Column<int>("int"),PublicationState=t.Column<int>("int")
        },constraints:c=>c.PrimaryKey("PK_PayrollInputDefinitionVersions",x=>x.Id));
        m.CreateIndex("UX_PayrollInputDefinition_Company_Code_Revision","PayrollInputDefinitionVersions",new[]{"CompanyId","Code","Revision"},unique:true);
        m.CreateIndex("IX_PayrollInputDefinition_Effective","PayrollInputDefinitionVersions",new[]{"CompanyId","DefinitionId","EffectiveFrom","EffectiveTo"});

        m.CreateTable("PayrollInputLedgerEntries",columns:t=>new{
            Id=t.Column<string>("char(36)",maxLength:36),CompanyId=t.Column<string>("char(36)",maxLength:36),PayrollSubjectId=t.Column<string>("char(36)",maxLength:36),PayrollPeriodId=t.Column<string>("char(36)",maxLength:36),BusinessDate=t.Column<DateOnly>("date"),InputDefinitionId=t.Column<string>("char(36)",maxLength:36),InputDefinitionRevision=t.Column<int>("int"),InputCode=t.Column<string>("varchar(64)",maxLength:64),DataType=t.Column<int>("int"),UnitType=t.Column<int>("int"),AggregationType=t.Column<int>("int"),DecimalValue=t.Column<decimal>("decimal(28,8)",precision:28,scale:8,nullable:true),IntegerValue=t.Column<long>("bigint",nullable:true),BooleanValue=t.Column<bool>("tinyint(1)",nullable:true),DateValue=t.Column<DateOnly>("date",nullable:true),TextValue=t.Column<string>("varchar(4000)",maxLength:4000,nullable:true),SourceType=t.Column<int>("int"),SourceSystem=t.Column<string>("varchar(128)",maxLength:128,nullable:true),SourceReference=t.Column<string>("varchar(256)",maxLength:256,nullable:true),ObservedAt=t.Column<DateTimeOffset>("datetime(6)",nullable:true),EffectiveDate=t.Column<DateOnly>("date",nullable:true),RecordedAt=t.Column<DateTimeOffset>("datetime(6)"),RecordedBy=t.Column<string>("char(36)",maxLength:36,nullable:true),CorrelationId=t.Column<string>("varchar(128)",maxLength:128),IdempotencyKey=t.Column<string>("varchar(256)",maxLength:256),SupersedesEntryId=t.Column<string>("char(36)",maxLength:36,nullable:true)
        },constraints:c=>{c.PrimaryKey("PK_PayrollInputLedgerEntries",x=>x.Id);c.ForeignKey("FK_PayrollInputLedger_Supersedes",x=>x.SupersedesEntryId,"PayrollInputLedgerEntries","Id",onDelete:ReferentialAction.Restrict);c.CheckConstraint("CK_PayrollInputLedger_TypedValue","(DataType = 0 AND DecimalValue IS NOT NULL AND IntegerValue IS NULL AND BooleanValue IS NULL AND DateValue IS NULL AND TextValue IS NULL) OR (DataType = 1 AND DecimalValue IS NULL AND IntegerValue IS NOT NULL AND BooleanValue IS NULL AND DateValue IS NULL AND TextValue IS NULL) OR (DataType = 2 AND DecimalValue IS NULL AND IntegerValue IS NULL AND BooleanValue IS NOT NULL AND DateValue IS NULL AND TextValue IS NULL) OR (DataType = 3 AND DecimalValue IS NULL AND IntegerValue IS NULL AND BooleanValue IS NULL AND DateValue IS NOT NULL AND TextValue IS NULL) OR (DataType = 4 AND DecimalValue IS NULL AND IntegerValue IS NULL AND BooleanValue IS NULL AND DateValue IS NULL AND TextValue IS NOT NULL)");});
        m.CreateIndex("UX_PayrollInputLedger_Idempotency","PayrollInputLedgerEntries",new[]{"CompanyId","IdempotencyKey"},unique:true);
        m.CreateIndex("IX_PayrollInputLedger_Effective","PayrollInputLedgerEntries",new[]{"CompanyId","PayrollSubjectId","PayrollPeriodId","InputDefinitionId"});
        m.CreateIndex("IX_PayrollInputLedger_DefinitionRevision","PayrollInputLedgerEntries",new[]{"CompanyId","InputDefinitionId","InputDefinitionRevision"});
        m.CreateIndex("IX_PayrollInputLedger_Supersedes","PayrollInputLedgerEntries",new[]{"CompanyId","SupersedesEntryId"});
        m.CreateIndex("IX_PayrollInputLedger_SourceReference","PayrollInputLedgerEntries",new[]{"CompanyId","SourceReference"});
    }
    protected override void Down(MigrationBuilder m){m.DropTable("PayrollInputLedgerEntries");m.DropTable("PayrollInputDefinitionVersions");}
}
