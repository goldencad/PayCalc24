using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
#pragma warning disable CA1861

namespace PayCalc24.Infrastructure.MariaDb.Migrations;

public partial class Task04OrganizationFoundation : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        CreateCatalog(migrationBuilder, "OrganizationUnits", true);
        CreateCatalog(migrationBuilder, "Positions", false);
        CreateCatalog(migrationBuilder, "JobGrades", false);
        migrationBuilder.CreateTable("PayrollSubjects", table => new
        {
            Id=table.Column<string>("char(36)",maxLength:36), CompanyId=table.Column<string>("char(36)",maxLength:36), EmployeeCode=table.Column<string>("varchar(64)",maxLength:64), FullName=table.Column<string>("varchar(256)",maxLength:256),
            NationalIdType=table.Column<int>("int",nullable:true), NationalId=table.Column<string>("varchar(64)",maxLength:64,nullable:true), AttendanceCode=table.Column<string>("varchar(64)",maxLength:64,nullable:true), EmploymentStatus=table.Column<int>("int"),
            SourceSystem=table.Column<string>("varchar(64)",maxLength:64,nullable:true), ExternalEmployeeId=table.Column<string>("varchar(128)",maxLength:128,nullable:true), EffectiveFrom=table.Column<DateOnly>("date"), EffectiveTo=table.Column<DateOnly>("date",nullable:true), Status=table.Column<int>("int")
        }, constraints: table => table.PrimaryKey("PK_PayrollSubjects", x=>x.Id));
        migrationBuilder.CreateIndex("AK_PayrollSubjects_Company_Id","PayrollSubjects",new[]{"CompanyId","Id"},unique:true);
        migrationBuilder.CreateIndex("UX_PayrollSubjects_Company_EmployeeCode","PayrollSubjects",new[]{"CompanyId","EmployeeCode"},unique:true);
        migrationBuilder.CreateTable("EmployeeDependents", table => new
        {
            Id=table.Column<string>("char(36)",maxLength:36), CompanyId=table.Column<string>("char(36)",maxLength:36), PayrollSubjectId=table.Column<string>("char(36)",maxLength:36), DependentCode=table.Column<string>("varchar(64)",nullable:true), FullName=table.Column<string>("varchar(256)",maxLength:256), Relationship=table.Column<string>("varchar(64)",maxLength:64), DateOfBirth=table.Column<DateOnly>("date",nullable:true), NationalId=table.Column<string>("varchar(64)",maxLength:64,nullable:true), EffectiveFrom=table.Column<DateOnly>("date"), EffectiveTo=table.Column<DateOnly>("date",nullable:true), EligibilityFrom=table.Column<DateOnly>("date",nullable:true), EligibilityTo=table.Column<DateOnly>("date",nullable:true), DeductionFrom=table.Column<DateOnly>("date",nullable:true), DeductionTo=table.Column<DateOnly>("date",nullable:true), EligibilityStatus=table.Column<int>("int"), EligibilityReason=table.Column<string>("varchar(256)",nullable:true), SourceSystem=table.Column<string>("varchar(64)",nullable:true), ExternalId=table.Column<string>("varchar(128)",nullable:true), Status=table.Column<int>("int")
        }, constraints: table => { table.PrimaryKey("PK_EmployeeDependents",x=>x.Id); table.ForeignKey("FK_Dependents_Subjects",x=>new{x.CompanyId,x.PayrollSubjectId},"PayrollSubjects",new[]{"CompanyId","Id"},onDelete:ReferentialAction.Restrict); });
        migrationBuilder.CreateIndex("IX_EmployeeDependents_Company_Subject","EmployeeDependents",new[]{"CompanyId","PayrollSubjectId"});
        migrationBuilder.CreateTable("PayrollAssignments", table => new
        {
            Id=table.Column<string>("char(36)",maxLength:36), CompanyId=table.Column<string>("char(36)",maxLength:36), PayrollSubjectId=table.Column<string>("char(36)",maxLength:36), OrganizationUnitId=table.Column<string>("char(36)",maxLength:36), PositionId=table.Column<string>("char(36)",maxLength:36), JobGradeId=table.Column<string>("char(36)",maxLength:36,nullable:true), EffectiveFrom=table.Column<DateOnly>("date"), EffectiveTo=table.Column<DateOnly>("date",nullable:true), IsPrimary=table.Column<bool>("tinyint(1)"), Status=table.Column<int>("int"), SourceSystem=table.Column<string>("varchar(64)",nullable:true), ExternalAssignmentId=table.Column<string>("varchar(128)",nullable:true)
        }, constraints: table => table.PrimaryKey("PK_PayrollAssignments",x=>x.Id));
        migrationBuilder.CreateIndex("IX_Assignments_Company_Subject_Primary","PayrollAssignments",new[]{"CompanyId","PayrollSubjectId","IsPrimary"});
    }

    private static void CreateCatalog(MigrationBuilder migrationBuilder,string name,bool hierarchy)
    {
        migrationBuilder.CreateTable(name, table => new
        {
            Id=table.Column<string>("char(36)",maxLength:36), CompanyId=table.Column<string>("char(36)",maxLength:36), Code=table.Column<string>("varchar(64)",maxLength:64), Name=table.Column<string>("varchar(256)",maxLength:256),
            ParentId=table.Column<string>("char(36)",maxLength:36,nullable:true), UnitType=table.Column<int>("int",nullable:true), Description=table.Column<string>("varchar(512)",nullable:true), ManagementLevel=table.Column<string>("varchar(64)",nullable:true), Level=table.Column<string>("varchar(64)",nullable:true), EffectiveFrom=table.Column<DateOnly>("date"), EffectiveTo=table.Column<DateOnly>("date",nullable:true), Status=table.Column<int>("int"), SortOrder=table.Column<int>("int",nullable:true)
        }, constraints: table => table.PrimaryKey($"PK_{name}",x=>x.Id));
        migrationBuilder.CreateIndex($"UX_{name}_Company_Code",name,new[]{"CompanyId","Code"},unique:true);
        migrationBuilder.CreateIndex($"AK_{name}_Company_Id",name,new[]{"CompanyId","Id"},unique:true);
        if(hierarchy) migrationBuilder.AddForeignKey(name:"FK_OrganizationUnits_Parent",table:name,columns:new[]{"CompanyId","ParentId"},principalTable:name,principalColumns:new[]{"CompanyId","Id"},onDelete:ReferentialAction.Restrict);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable("EmployeeDependents"); migrationBuilder.DropTable("PayrollAssignments"); migrationBuilder.DropTable("JobGrades"); migrationBuilder.DropTable("Positions"); migrationBuilder.DropTable("OrganizationUnits"); migrationBuilder.DropTable("PayrollSubjects");
    }
}
