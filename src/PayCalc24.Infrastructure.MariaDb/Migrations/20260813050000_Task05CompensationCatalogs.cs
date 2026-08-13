using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
#pragma warning disable CA1861
namespace PayCalc24.Infrastructure.MariaDb.Migrations;

public partial class Task05CompensationCatalogs : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable("PayComponentVersions",columns:t=>new{
            Id=t.Column<string>("char(36)",maxLength:36),DefinitionId=t.Column<string>("char(36)",maxLength:36),CompanyId=t.Column<string>("char(36)",maxLength:36),VersionNumber=t.Column<int>("int"),Code=t.Column<string>("varchar(64)",maxLength:64),Name=t.Column<string>("varchar(256)",maxLength:256),Description=t.Column<string>("varchar(1024)",maxLength:1024,nullable:true),ComponentType=t.Column<int>("int"),CalculationMethod=t.Column<int>("int"),FormulaReference=t.Column<string>("varchar(128)",maxLength:128,nullable:true),FundSourceReference=t.Column<string>("varchar(128)",maxLength:128,nullable:true),IsProratable=t.Column<bool>("tinyint(1)"),IsAttendanceBased=t.Column<bool>("tinyint(1)"),IsPerformanceBased=t.Column<bool>("tinyint(1)"),IsTaxRelevant=t.Column<bool>("tinyint(1)"),IsInsuranceRelevant=t.Column<bool>("tinyint(1)"),IsGrossEligible=t.Column<bool>("tinyint(1)"),DisplayOrder=t.Column<int>("int",nullable:true),EffectiveFrom=t.Column<DateOnly>("date"),EffectiveTo=t.Column<DateOnly>("date",nullable:true),Status=t.Column<int>("int"),PublicationState=t.Column<int>("int")
        },constraints:c=>c.PrimaryKey("PK_PayComponentVersions",x=>x.Id));
        migrationBuilder.CreateIndex("UX_PayComponent_Company_Code_Version","PayComponentVersions",new[]{"CompanyId","Code","VersionNumber"},unique:true);
        migrationBuilder.CreateIndex("IX_PayComponent_Effective","PayComponentVersions",new[]{"CompanyId","Code","EffectiveFrom","EffectiveTo"});
        migrationBuilder.CreateIndex("AK_PayComponent_Company_Definition","PayComponentVersions",new[]{"CompanyId","DefinitionId"});

        migrationBuilder.CreateTable("CompensationSchemeVersions",columns:t=>new{
            Id=t.Column<string>("char(36)",maxLength:36),DefinitionId=t.Column<string>("char(36)",maxLength:36),CompanyId=t.Column<string>("char(36)",maxLength:36),VersionNumber=t.Column<int>("int"),Code=t.Column<string>("varchar(64)",maxLength:64),Name=t.Column<string>("varchar(256)",maxLength:256),Description=t.Column<string>("varchar(1024)",maxLength:1024,nullable:true),EffectiveFrom=t.Column<DateOnly>("date"),EffectiveTo=t.Column<DateOnly>("date",nullable:true),Status=t.Column<int>("int"),PublicationState=t.Column<int>("int")
        },constraints:c=>c.PrimaryKey("PK_CompensationSchemeVersions",x=>x.Id));
        migrationBuilder.CreateIndex("UX_Scheme_Company_Code_Version","CompensationSchemeVersions",new[]{"CompanyId","Code","VersionNumber"},unique:true);
        migrationBuilder.CreateIndex("IX_Scheme_Effective","CompensationSchemeVersions",new[]{"CompanyId","Code","EffectiveFrom","EffectiveTo"});

        migrationBuilder.CreateTable("CompensationSchemeComponents",columns:t=>new{
            Id=t.Column<string>("char(36)",maxLength:36),CompanyId=t.Column<string>("char(36)",maxLength:36),SchemeVersionId=t.Column<string>("char(36)",maxLength:36),PayComponentDefinitionId=t.Column<string>("char(36)",maxLength:36),Sequence=t.Column<int>("int"),Required=t.Column<bool>("tinyint(1)"),OverrideCalculationMethod=t.Column<int>("int",nullable:true),OverrideFormulaReference=t.Column<string>("varchar(128)",maxLength:128,nullable:true),Status=t.Column<int>("int")
        },constraints:c=>{c.PrimaryKey("PK_CompensationSchemeComponents",x=>x.Id);c.ForeignKey("FK_SchemeComponents_SchemeVersion",x=>x.SchemeVersionId,"CompensationSchemeVersions","Id",onDelete:ReferentialAction.Restrict);});
        migrationBuilder.CreateIndex("UX_SchemeComponent_Sequence","CompensationSchemeComponents",new[]{"CompanyId","SchemeVersionId","Sequence"},unique:true);
        migrationBuilder.CreateIndex("UX_SchemeComponent_Component","CompensationSchemeComponents",new[]{"CompanyId","SchemeVersionId","PayComponentDefinitionId"},unique:true);
        migrationBuilder.AddColumn<string>("CompensationSchemeId","PayrollAssignments","char(36)",maxLength:36,nullable:true);
        migrationBuilder.CreateIndex("IX_Assignment_Company_Scheme","PayrollAssignments",new[]{"CompanyId","CompensationSchemeId"});
    }
    protected override void Down(MigrationBuilder migrationBuilder)
    { migrationBuilder.DropIndex("IX_Assignment_Company_Scheme","PayrollAssignments");migrationBuilder.DropColumn("CompensationSchemeId","PayrollAssignments");migrationBuilder.DropTable("CompensationSchemeComponents");migrationBuilder.DropTable("CompensationSchemeVersions");migrationBuilder.DropTable("PayComponentVersions"); }
}
