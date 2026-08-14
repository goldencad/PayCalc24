using System.Xml.Linq;

namespace PayCalc24.ArchitectureTests;

public sealed class ProjectDependencyTests
{
    private static readonly string RepositoryRoot = FindRepositoryRoot();

    [Fact]
    public void StatutoryAndAccountingBoundaryIsGenericDynamicAndDecimalOnly()
    {
        var project=LoadProject("src/Modules/PayCalc24.Integration/PayCalc24.Integration.csproj");
        Assert.Equal(["PayCalc24.Contracts","PayCalc24.Domain"],ProjectReferences(project));
        AssertSourceExcludes("src/Modules/PayCalc24.Integration","Microsoft.EntityFrameworkCore","System.Net.Http","Odoo","ezBooks","DateTime.Now","DateTime.Today","GetCurrent(","GetLatest(","P1","P2","P3","float ","double ");
        var contracts=File.ReadAllText(Path.Combine(RepositoryRoot,"src","PayCalc24.Contracts","Integration","StatutoryIntegrationContracts.cs"));
        Assert.Contains("IStatutoryProvider",contracts,StringComparison.Ordinal);Assert.Contains("StatutoryContributionItem",contracts,StringComparison.Ordinal);
        Assert.DoesNotContain("BhxhAmount",contracts,StringComparison.OrdinalIgnoreCase);Assert.DoesNotContain("BhytAmount",contracts,StringComparison.OrdinalIgnoreCase);Assert.DoesNotContain("BhtnAmount",contracts,StringComparison.OrdinalIgnoreCase);
        var migration=File.ReadAllText(Path.Combine(RepositoryRoot,"src","PayCalc24.Infrastructure.MariaDb","Migrations","20260813180000_Task18StatutoryAccounting.cs"));
        foreach(var table in new[]{"StatutoryCalculationResults","StatutoryContributionItems","StatutoryDeductionItems","NetPayResults","EmployerCostResults","PayrollAccountingDocuments","PayrollAccountingLines","PayrollIntegrationDeliveries"})Assert.Contains($"CREATE TABLE {table}",migration,StringComparison.Ordinal);
        Assert.Contains("decimal(28,8)",migration,StringComparison.Ordinal);Assert.DoesNotContain(" float",migration,StringComparison.OrdinalIgnoreCase);Assert.DoesNotContain(" double",migration,StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void PayrollApprovalIsWorkflowOnlyAndHistoricalResultsHaveNoMutationApi()
    {
        var project=LoadProject("src/Modules/PayCalc24.PayrollApproval/PayCalc24.PayrollApproval.csproj");
        Assert.Equal(["PayCalc24.Contracts","PayCalc24.Domain"],ProjectReferences(project));
        AssertSourceExcludes("src/Modules/PayCalc24.PayrollApproval","Avalonia","Microsoft.EntityFrameworkCore","P1","P2","P3","GrossPay","NetPay","PIT","BHXH","UpdateCalculationResult","EditFundResult","EditFrozenSnapshot","ChangeApprovedPayroll","FormulaEngine");
        var migration=File.ReadAllText(Path.Combine(RepositoryRoot,"src","PayCalc24.Infrastructure.MariaDb","Migrations","20260813170000_Task17PayrollApproval.cs"));
        foreach(var table in new[]{"PayrollApprovalCases","PayrollApprovalEvents","PayrollAdjustmentRequests"})Assert.Contains($"CREATE TABLE {table}",migration,StringComparison.Ordinal);
        Assert.Contains("char(36)",migration,StringComparison.Ordinal);Assert.DoesNotContain("decimal(",migration,StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DomainHasNoProjectOrExternalPackageDependencies()
    {
        var project = LoadProject("src/PayCalc24.Domain/PayCalc24.Domain.csproj");

        Assert.Empty(ProjectReferences(project));
        Assert.Empty(PackageReferences(project));
        AssertSourceExcludes("src/PayCalc24.Domain", "Microsoft.EntityFrameworkCore", "Avalonia", "Microsoft.AspNetCore");
    }

    [Fact]
    public void FormulaEngineOnlyDependsOnDomain()
    {
        var project = LoadProject("src/Modules/PayCalc24.FormulaEngine/PayCalc24.FormulaEngine.csproj");

        Assert.Equal(["PayCalc24.Contracts", "PayCalc24.Domain"], ProjectReferences(project));
        Assert.Empty(PackageReferences(project));
        AssertSourceExcludes(
            "src/Modules/PayCalc24.FormulaEngine",
            "Microsoft.EntityFrameworkCore",
            "Avalonia",
            "System.Net.Http",
            "PayCalc24.Infrastructure",
            "Microsoft.CodeAnalysis",
            "System.Reflection",
            "DateTime.Now",
            "DateTime.Today",
            "Process.Start",
            "DynamicMethod",
            "eval(");
    }

    [Fact]
    public void AvaloniaClientOnlyReferencesPublicContracts()
    {
        var project = LoadProject("src/PayCalc24.Client.Avalonia/PayCalc24.Client.Avalonia.csproj");

        Assert.Equal(["PayCalc24.Contracts"], ProjectReferences(project));
        Assert.DoesNotContain(PackageReferences(project), package =>
            package.Contains("EntityFrameworkCore", StringComparison.OrdinalIgnoreCase) ||
            package.Contains("MySql", StringComparison.OrdinalIgnoreCase));
        AssertSourceExcludes(
            "src/PayCalc24.Client.Avalonia",
            "PayCalc24.Infrastructure.MariaDb",
            "Microsoft.EntityFrameworkCore");
    }

    [Fact]
    public void OperationalDesktopContainsNoPayrollEngineOrFixedBusinessModel()
    {
        AssertSourceExcludes("src/PayCalc24.Client.Avalonia",
            "new SafeFormulaEngine", "new PayrollCalculationService", "new PayrollFundService",
            "new AttendanceService", "new PerformanceService", "new StatutoryOrchestrationService",
            "Kpi1", "Kpi2", "Kpi3", "BhxhAmount", "BhytAmount", "BhtnAmount",
            "component.Code == \"P1\"", "component.Code == \"P2\"", "component.Code == \"P3\"");
        var project = LoadProject("src/PayCalc24.Client.Avalonia/PayCalc24.Client.Avalonia.csproj");
        Assert.DoesNotContain(ProjectReferences(project), reference => reference.StartsWith("PayCalc24.", StringComparison.Ordinal) && reference != "PayCalc24.Contracts");
    }

    [Fact]
    public void DefaultMainWindowUsesActiproRibbonBackstageAndRibbonNavigation()
    {
        var path = Path.Combine(RepositoryRoot, "src", "PayCalc24.Client.Avalonia", "MainWindow.axaml");
        var document = XDocument.Load(path);
        Assert.Equal("RibbonWindow", document.Root!.Name.LocalName);
        var ribbon = Assert.Single(document.Descendants(), x => x.Name.LocalName == "Ribbon");
        Assert.Equal("RibbonHost", ribbon.Parent!.Attributes().Single(x => x.Name.LocalName == "Name").Value);
        Assert.Single(document.Descendants(), x => x.Name.LocalName == "RibbonBackstage");
        var expectedAreas = new[] { "DASHBOARD", "INPUTS", "KPI", "CALCULATE", "EXPLAIN", "APPROVAL", "ACCOUNTING", "REPORTS" };
        var parameters = document.Descendants().Where(x => x.Name.LocalName == "BarButton")
            .SelectMany(x => x.Attributes().Where(a => a.Name.LocalName == "CommandParameter"))
            .Select(x => x.Value).ToHashSet(StringComparer.Ordinal);
        Assert.All(expectedAreas, area => Assert.Contains(area, parameters));
        Assert.DoesNotContain(document.Descendants().Where(x => x.Name.LocalName == "ListBox")
            .SelectMany(x => x.Attributes()), x => x.Name.LocalName == "ItemsSource" && x.Value.Contains("Workspace.Areas", StringComparison.Ordinal));
    }

    [Fact]
    public void DesktopLoadsActiproBarsControlThemes()
    {
        var path = Path.Combine(RepositoryRoot, "src", "PayCalc24.Client.Avalonia", "App.axaml");
        var document = XDocument.Load(path);
        var modernTheme = Assert.Single(document.Descendants(), x => x.Name.LocalName == "ModernTheme");
        Assert.Equal("Pro", modernTheme.Attribute("Includes")?.Value);
        Assert.DoesNotContain(modernTheme.Elements(), x => x.Name.LocalName == "StyleInclude");
    }

    [Fact]
    public void FeatureModulesDoNotReferenceOtherFeatureModules()
    {
        var moduleProjects = Directory.GetFiles(
            Path.Combine(RepositoryRoot, "src", "Modules"),
            "*.csproj",
            SearchOption.AllDirectories);

        foreach (var projectPath in moduleProjects)
        {
            if (projectPath.Contains("PayrollCalculation", StringComparison.Ordinal))
            {
                continue;
            }

            var project = XDocument.Load(projectPath);
            Assert.DoesNotContain(ProjectReferences(project), reference =>
                reference is not "PayCalc24.Domain" and not "PayCalc24.Contracts");
        }
    }

    [Fact]
    public void PublicContractsDoNotLeakProviderImplementations()
    {
        AssertSourceExcludes(
            "src/PayCalc24.Contracts",
            "Pomelo.EntityFrameworkCore",
            "MySqlConnector",
            "Odoo",
            "TaxOnline",
            "iBHXH",
            "ezBooks");
    }

    [Fact]
    public void CoreContractsRemainTs24AdaptersAndDoNotDefineCompanyOrUserMasters()
    {
        var contractsDirectory = Path.Combine(RepositoryRoot, "src", "PayCalc24.Contracts");
        var sources = Directory.GetFiles(contractsDirectory, "*.cs", SearchOption.AllDirectories)
            .Select(File.ReadAllText)
            .ToArray();

        Assert.DoesNotContain(sources, source =>
            source.Contains("class Company", StringComparison.Ordinal) ||
            source.Contains("class User", StringComparison.Ordinal));
    }

    [Fact]
    public void TemporalFrameworkDoesNotIntroducePersistenceOrCompanyUserMasters()
    {
        AssertSourceExcludes(
            "src/PayCalc24.Application/Temporal",
            "Microsoft.EntityFrameworkCore",
            "Pomelo",
            "MySqlConnector",
            "DbContext");

        var temporalSources = Directory.GetFiles(
                Path.Combine(RepositoryRoot, "src", "PayCalc24.Contracts", "Temporal"),
                "*.cs", SearchOption.AllDirectories)
            .Select(File.ReadAllText);

        Assert.DoesNotContain(temporalSources, source =>
            source.Contains("class Company", StringComparison.Ordinal) ||
            source.Contains("class User", StringComparison.Ordinal));
    }

    [Fact]
    public void OrganizationDomainAndCanonicalContractsDoNotLeakLegacyPersistenceNames()
    {
        AssertSourceExcludes("src/Modules/PayCalc24.Organization", "nhanvien", "phongban", "vitricongviec", "MucLuongCB", "MucLuongBHXH");
        AssertSourceExcludes("src/PayCalc24.Contracts/Organization", "nhanvien", "phongban", "vitricongviec", "Microsoft.EntityFrameworkCore");
    }

    [Fact]
    public void PayrollInputIsAnIndependentAppendOnlyModule()
    {
        var project = LoadProject("src/Modules/PayCalc24.PayrollInput/PayCalc24.PayrollInput.csproj");
        Assert.Equal(["PayCalc24.Contracts", "PayCalc24.Domain"], ProjectReferences(project));
        Assert.Empty(PackageReferences(project));
        AssertSourceExcludes(
            "src/Modules/PayCalc24.PayrollInput",
            "Microsoft.EntityFrameworkCore", "Avalonia", "Odoo", "TaxOnline", "iBHXH",
            "UpdateLedgerValue", "DeleteLedgerEntry");

        var model = File.ReadAllText(Path.Combine(
            RepositoryRoot, "src", "Modules", "PayCalc24.PayrollInput", "Model", "PayrollInputModel.cs"));
        Assert.DoesNotContain("set; }", model[model.IndexOf("public sealed class PayrollInputLedgerEntry", StringComparison.Ordinal)..], StringComparison.Ordinal);
    }

    [Fact]
    public void PayrollInputPersistenceUsesTypedDecimalColumnsAndNoFloatingPoint()
    {
        AssertSourceExcludes("src/PayCalc24.Infrastructure.MariaDb/PayrollInput", "float", "double");
        var source = File.ReadAllText(Path.Combine(RepositoryRoot, "src", "PayCalc24.Infrastructure.MariaDb", "PayrollInput", "PayrollInputRows.cs"));
        Assert.Contains("HasPrecision(28,8)", source, StringComparison.Ordinal);
        Assert.Contains("DecimalValue", source, StringComparison.Ordinal);
        Assert.Contains("IntegerValue", source, StringComparison.Ordinal);
        Assert.Contains("BooleanValue", source, StringComparison.Ordinal);
        Assert.Contains("DateValue", source, StringComparison.Ordinal);
        Assert.Contains("TextValue", source, StringComparison.Ordinal);
    }

    [Fact]
    public void FormulaRepositoryIsDataOnlyAndHasNoRuntimeEvaluatorOrPublishedMutationApi()
    {
        var project = LoadProject("src/Modules/PayCalc24.FormulaRepository/PayCalc24.FormulaRepository.csproj");
        Assert.Equal(["PayCalc24.Contracts", "PayCalc24.Domain"], ProjectReferences(project));
        Assert.Empty(PackageReferences(project));
        AssertSourceExcludes(
            "src/Modules/PayCalc24.FormulaRepository",
            "Microsoft.EntityFrameworkCore", "Avalonia", "Microsoft.CodeAnalysis", "Python",
            "JavaScript", "eval(", "Compile(", "DynamicMethod", "System.Net.Http",
            "AttendanceRecord", "Odoo", "iBHXH", "TaxOnline", "UpdatePublished", "DeletePublished");

        var contracts = File.ReadAllText(Path.Combine(
            RepositoryRoot, "src", "PayCalc24.Contracts", "FormulaRepository", "FormulaRepositoryContracts.cs"));
        Assert.DoesNotContain("Evaluate", contracts, StringComparison.Ordinal);
        Assert.DoesNotContain("FunctionCatalog", contracts, StringComparison.Ordinal);
    }

    [Fact]
    public void FormulaRepositoryPersistenceUsesRelationalTablesUuidAndDecimalColumns()
    {
        var migration = File.ReadAllText(Path.Combine(
            RepositoryRoot, "src", "PayCalc24.Infrastructure.MariaDb", "Migrations", "20260813070000_Task07DynamicFormulaRepository.cs"));
        foreach (var table in new[] { "FormulaDefinitions", "FormulaVersions", "FormulaDependencies", "FormulaTestCases", "ParameterSetVersions", "ParameterValues", "LookupTableVersions", "LookupRows", "RuleSetVersions", "Rules" })
        {
            Assert.Contains($"CREATE TABLE {table}", migration, StringComparison.Ordinal);
        }
        Assert.Contains("char(36)", migration, StringComparison.Ordinal);
        Assert.Contains("decimal(28,8)", migration, StringComparison.Ordinal);
        Assert.DoesNotContain(" float", migration, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(" double", migration, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void PayrollSnapshotsAreRelationalImmutableAndCalculationFree()
    {
        AssertSourceExcludes("src/Modules/PayCalc24.PayrollCalculation", "Avalonia", "Microsoft.EntityFrameworkCore", "NationalId", "UpdateFrozenSnapshot", "DateTime.Today", "DateTime.Now", "CalculateGross", "CalculateNet");
        var migration=File.ReadAllText(Path.Combine(RepositoryRoot,"src","PayCalc24.Infrastructure.MariaDb","Migrations","20260813090000_Task09PayrollPeriodSnapshots.cs"));
        foreach(var table in new[]{"PayrollPeriods","PayrollPeriodLifecycleEvents","PayrollCalculationSnapshots","PayrollSnapshotSubjects","PayrollSnapshotInputs","PayrollSnapshotFormulaVersions","PayrollSnapshotParameterVersions","PayrollSnapshotLookupVersions","PayrollSnapshotRuleSetVersions"}) Assert.Contains($"CREATE TABLE {table}",migration,StringComparison.Ordinal);
        Assert.Contains("decimal(28,8)",migration,StringComparison.Ordinal);Assert.DoesNotContain(" float",migration,StringComparison.OrdinalIgnoreCase);Assert.DoesNotContain(" double",migration,StringComparison.OrdinalIgnoreCase);
        var contracts=File.ReadAllText(Path.Combine(RepositoryRoot,"src","PayCalc24.Contracts","PayrollCalculation","PayrollCalculationContracts.cs"));Assert.DoesNotContain("NationalId",contracts,StringComparison.Ordinal);Assert.Contains("SnapshotHistoricalFacts",contracts,StringComparison.Ordinal);Assert.Contains("SnapshotPolicyConfiguration",contracts,StringComparison.Ordinal);
    }

    [Fact]
    public void PayrollCalculationUsesOnlySnapshotAndSafeEngineBoundaries()
    {
        AssertSourceExcludes("src/Modules/PayCalc24.PayrollCalculation",
            "Avalonia", "Microsoft.EntityFrameworkCore", "System.Net.Http", "Odoo", "iBHXH",
            "DateTime.Now", "DateTime.Today", "GetCurrent(", "GetLatest(", "CalculateGross", "CalculateNet",
            "component.Code ==", "P3Fund", "P3Coverage", "P3Allocation", "fund.Code ==", "float ", "double ");
        var migration=File.ReadAllText(Path.Combine(RepositoryRoot,"src","PayCalc24.Infrastructure.MariaDb","Migrations","20260813100000_Task10PayrollCalculationResults.cs"));
        foreach(var table in new[]{"PayrollCalculationRuns","PayrollSubjectCalculationResults","PayComponentCalculationResults","CalculationInputProvenance"})Assert.Contains($"CREATE TABLE {table}",migration,StringComparison.Ordinal);
        Assert.Contains("decimal(28,8)",migration,StringComparison.Ordinal);Assert.DoesNotContain(" float",migration,StringComparison.OrdinalIgnoreCase);Assert.DoesNotContain(" double",migration,StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void PayrollFundsAreGenericPinnedDecimalAndStatutoryFree()
    {
        AssertSourceExcludes("src/Modules/PayCalc24.PayrollFunds",
            "Avalonia", "Microsoft.EntityFrameworkCore", "System.Net.Http", "Odoo", "iBHXH",
            "DateTime.Now", "DateTime.Today", "GetCurrent(", "GetLatest(", "P3Fund", "P3Coverage",
            "CalculateGross", "CalculateNet", "PIT", "BHXH", "float ", "double ");
        var migration=File.ReadAllText(Path.Combine(RepositoryRoot,"src","PayCalc24.Infrastructure.MariaDb","Migrations","20260813110000_Task11PayrollFunds.cs"));
        foreach(var table in new[]{"PayrollFundDefinitions","PayrollFundVersions","PayrollSnapshotFundVersions","PayrollFundAllocationResults","PayrollFundMemberAllocationResults"})Assert.Contains($"CREATE TABLE {table}",migration,StringComparison.Ordinal);
        Assert.Contains("decimal(28,8)",migration,StringComparison.Ordinal);Assert.DoesNotContain(" float",migration,StringComparison.OrdinalIgnoreCase);Assert.DoesNotContain(" double",migration,StringComparison.OrdinalIgnoreCase);
        var contracts=File.ReadAllText(Path.Combine(RepositoryRoot,"src","PayCalc24.Contracts","PayrollFunds","PayrollFundContracts.cs"));
        Assert.Contains("SnapshotPayrollFundVersion",contracts,StringComparison.Ordinal);Assert.Contains("FundAllocationResultDto",contracts,StringComparison.Ordinal);Assert.DoesNotContain("P3",contracts,StringComparison.Ordinal);
    }

    [Fact]
    public void AttendanceIsGenericAndOnlyPublishesThroughPayrollInputContracts()
    {
        var project=LoadProject("src/Modules/PayCalc24.Attendance/PayCalc24.Attendance.csproj");
        Assert.Equal(["PayCalc24.Contracts","PayCalc24.Domain"],ProjectReferences(project));
        AssertSourceExcludes("src/Modules/PayCalc24.Attendance","Microsoft.EntityFrameworkCore","PayCalc24.PayrollCalculation","PayCalc24.PayrollFunds","CalculateGross","CalculateNet","DateTime.Now","DateTime.Today","P1","P2","P3","KPI","float ","double ");
        var source=File.ReadAllText(Path.Combine(RepositoryRoot,"src","Modules","PayCalc24.Attendance","Services","AttendanceService.cs"));
        Assert.Contains("IPayrollInputLedgerService",source,StringComparison.Ordinal);
        Assert.Contains("ledger.SubmitAsync",source,StringComparison.Ordinal);
        Assert.Contains("ledger.CorrectAsync",source,StringComparison.Ordinal);
        var migration=File.ReadAllText(Path.Combine(RepositoryRoot,"src","PayCalc24.Infrastructure.MariaDb","Migrations","20260813120000_Task12Attendance.cs"));
        foreach(var table in new[]{"AttendanceSourceDefinitions","AttendanceMappingVersions","AttendancePolicyVersions","AttendanceImportBatches","AttendanceFacts","AttendanceDerivedInputProvenance"})Assert.Contains($"CREATE TABLE {table}",migration,StringComparison.Ordinal);
        Assert.Contains("decimal(28,8)",migration,StringComparison.Ordinal);Assert.DoesNotContain(" float",migration,StringComparison.OrdinalIgnoreCase);Assert.DoesNotContain(" double",migration,StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void PerformanceIsGenericPinnedAndOnlyPublishesThroughPayrollInputContracts()
    {
        var project=LoadProject("src/Modules/PayCalc24.Performance/PayCalc24.Performance.csproj");
        Assert.Equal(["PayCalc24.Contracts","PayCalc24.Domain"],ProjectReferences(project));
        AssertSourceExcludes("src/Modules/PayCalc24.Performance","Microsoft.EntityFrameworkCore","PayCalc24.Attendance","PayCalc24.PayrollCalculation","PayCalc24.PayrollFunds","CalculateGross","CalculateNet","DateTime.Now","DateTime.Today","GetCurrent(","GetLatest(","Kpi1","Kpi2","Kpi3","P1","P2","P3","float ","double ");
        var source=File.ReadAllText(Path.Combine(RepositoryRoot,"src","Modules","PayCalc24.Performance","Services","PerformanceService.cs"));
        Assert.Contains("IPayrollInputLedgerService",source,StringComparison.Ordinal);Assert.Contains("ledger.SubmitAsync",source,StringComparison.Ordinal);Assert.Contains("ledger.CorrectAsync",source,StringComparison.Ordinal);
        var migration=File.ReadAllText(Path.Combine(RepositoryRoot,"src","PayCalc24.Infrastructure.MariaDb","Migrations","20260813130000_Task13Performance.cs"));
        foreach(var table in new[]{"KpiDefinitions","KpiDefinitionVersions","KpiAssignments","KpiResults","PerformancePolicyDefinitions","PerformancePolicyVersions","PerformanceGates","PerformanceEvaluationResults","PerformanceEvaluationKpiDetails","PerformanceDerivedInputProvenance"})Assert.Contains($"CREATE TABLE {table}",migration,StringComparison.Ordinal);
        Assert.Contains("decimal(28,8)",migration,StringComparison.Ordinal);Assert.DoesNotContain(" float",migration,StringComparison.OrdinalIgnoreCase);Assert.DoesNotContain(" double",migration,StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void PayrollReviewIsReadOnlyProvenanceBasedAndCalculationFree()
    {
        var project=LoadProject("src/Modules/PayCalc24.PayrollReview/PayCalc24.PayrollReview.csproj");
        Assert.Equal(["PayCalc24.Contracts","PayCalc24.Domain"],ProjectReferences(project));
        Assert.Empty(PackageReferences(project));
        AssertSourceExcludes("src/Modules/PayCalc24.PayrollReview",
            "Avalonia","Microsoft.EntityFrameworkCore","PayCalc24.FormulaEngine","PayCalc24.Attendance",
            "PayCalc24.Performance","DateTime.Now","DateTime.Today","GetCurrent(","GetLatest(",
            "CalculateGross","CalculateNet","PIT","BHXH","Approval","ScenarioSnapshot","float ","double ",
            "UpdateCalculation","UpdateFund","P1","P2","P3");
        var contracts=File.ReadAllText(Path.Combine(RepositoryRoot,"src","PayCalc24.Contracts","PayrollReview","PayrollReviewContracts.cs"));
        Assert.Contains("IPayrollReviewSource",contracts,StringComparison.Ordinal);
        Assert.Contains("PayrollReviewDataset",contracts,StringComparison.Ordinal);
        Assert.DoesNotContain("NationalId",contracts,StringComparison.Ordinal);
    }

    [Fact]
    public void ReferencePolicyBusinessCodesNeverEnterGenericEnginesOrPersistence()
    {
        foreach (var boundary in new[]
        {
            "src/Modules/PayCalc24.Compensation",
            "src/Modules/PayCalc24.FormulaEngine",
            "src/Modules/PayCalc24.PayrollCalculation",
            "src/Modules/PayCalc24.PayrollFunds",
            "src/Modules/PayCalc24.Attendance",
            "src/Modules/PayCalc24.Performance",
            "src/PayCalc24.Infrastructure.MariaDb"
        })
        {
            AssertSourceExcludes(boundary, "P1Calculator", "P2Calculator", "P3Calculator", "P3Engine",
                "P3FundEngine", "TS24PayrollEngine", "Ts24ReferencePolicyBuilder", "component.Code == \"P1\"",
                "component.Code == \"P2\"", "component.Code == \"P3\"", "company == \"TS24\"");
        }

        AssertSourceExcludes("src/Modules/PayCalc24.PayrollCalculation", "PayCalc24.Attendance", "PayCalc24.Performance");
        AssertSourceExcludes("src/Modules/PayCalc24.PayrollFunds", "PayCalc24.Attendance", "PayCalc24.Performance");
    }

    [Fact]
    public void AvaloniaXamlDoesNotHardCodePresentationContentOrSvgPaths()
    {
        var clientDirectory = Path.Combine(RepositoryRoot, "src", "PayCalc24.Client.Avalonia");
        var presentationAttributes = new[] { "Text", "Content", "Header", "Title", "ToolTip.Tip" };

        foreach (var xamlPath in Directory.GetFiles(clientDirectory, "*.axaml", SearchOption.AllDirectories))
        {
            var document = XDocument.Load(xamlPath);
            foreach (var attribute in document.Descendants().Attributes())
            {
                if (presentationAttributes.Contains(attribute.Name.LocalName, StringComparer.Ordinal))
                {
                    Assert.StartsWith("{", attribute.Value, StringComparison.Ordinal);
                }

                Assert.DoesNotContain(".svg", attribute.Value, StringComparison.OrdinalIgnoreCase);
            }
        }
    }

    [Fact]
    public void ScenariosAreIsolatedOrchestrationAndContainNoAlternateEngine()
    {
        var project=LoadProject("src/Modules/PayCalc24.Scenarios/PayCalc24.Scenarios.csproj");
        Assert.Equal(["PayCalc24.Contracts","PayCalc24.Domain"],ProjectReferences(project));
        Assert.Empty(PackageReferences(project));
        AssertSourceExcludes("src/Modules/PayCalc24.Scenarios",
            "BackTestEngine","SimulationEngine","ReplayEngine","WhatIfEngine","SafeFormulaEngine",
            "Microsoft.EntityFrameworkCore","Avalonia","PayCalc24.Attendance","PayCalc24.Performance",
            "DateTime.Now","DateTime.Today","GetCurrent(","GetLatest(","P1","P2","P3",
            "Gross","NetPay","PIT","BHXH","float ","double ");
        var source=File.ReadAllText(Path.Combine(RepositoryRoot,"src","Modules","PayCalc24.Scenarios","Services","ScenarioService.cs"));
        Assert.Contains("IPayrollCalculationService",source,StringComparison.Ordinal);
        Assert.Contains("IPayrollFundCalculationService",source,StringComparison.Ordinal);
        Assert.Contains("IPayrollReviewService",source,StringComparison.Ordinal);
        var migration=File.ReadAllText(Path.Combine(RepositoryRoot,"src","PayCalc24.Infrastructure.MariaDb","Migrations","20260813160000_Task16Scenarios.cs"));
        foreach(var table in new[]{"ScenarioDefinitions","ScenarioSnapshots","ScenarioPolicyOverrides","ScenarioInputOverrides","ScenarioExecutions"}) Assert.Contains($"CREATE TABLE {table}",migration,StringComparison.Ordinal);
        Assert.Contains("CHAR(36)",migration,StringComparison.Ordinal);
        Assert.DoesNotContain(" FLOAT",migration,StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(" DOUBLE",migration,StringComparison.OrdinalIgnoreCase);
    }

    private static XDocument LoadProject(string relativePath) =>
        XDocument.Load(Path.Combine(RepositoryRoot, relativePath));

    private static string[] ProjectReferences(XDocument project) => project
        .Descendants("ProjectReference")
        .Select(element => element.Attribute("Include")?.Value.Replace('\\', '/'))
        .Select(path => Path.GetFileNameWithoutExtension(path))
        .Where(name => name is not null)
        .Cast<string>()
        .Order(StringComparer.Ordinal)
        .ToArray();

    private static string[] PackageReferences(XDocument project) => project
        .Descendants("PackageReference")
        .Select(element => element.Attribute("Include")?.Value)
        .Where(name => name is not null)
        .Cast<string>()
        .Order(StringComparer.Ordinal)
        .ToArray();

    private static void AssertSourceExcludes(string relativeDirectory, params string[] forbiddenTokens)
    {
        var directory = Path.Combine(RepositoryRoot, relativeDirectory);
        var sourceFiles = Directory.GetFiles(directory, "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal));

        foreach (var sourceFile in sourceFiles)
        {
            var source = File.ReadAllText(sourceFile);
            foreach (var forbiddenToken in forbiddenTokens)
            {
                Assert.DoesNotContain(forbiddenToken, source, StringComparison.Ordinal);
            }
        }
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "PayCalc24.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new DirectoryNotFoundException("Could not locate the PayCalc24 repository root.");
    }
    [Fact]
    public void Task19ReportingAndDesktopBoundariesRemainPortable()
    {
        var reporting = LoadProject("src/Modules/PayCalc24.Reporting/PayCalc24.Reporting.csproj");
        Assert.Equal(["PayCalc24.Contracts", "PayCalc24.Domain"], ProjectReferences(reporting));
        Assert.Empty(PackageReferences(reporting));
        AssertSourceExcludes("src/Modules/PayCalc24.Reporting", "Avalonia", "Microsoft.EntityFrameworkCore",
            "PayCalc24.Attendance", "PayCalc24.Performance", "FormulaEngine", "GetCurrent(", "GetLatest(", "P1", "P2", "P3", "float ", "double ");

        var desktop = LoadProject("src/PayCalc24.Client.Avalonia/PayCalc24.Client.Avalonia.csproj");
        Assert.Equal(["PayCalc24.Contracts"], ProjectReferences(desktop));
        AssertSourceExcludes("src/PayCalc24.Client.Avalonia", "SafeFormulaEngine", "PayrollFundCalculationService",
            "StatutoryServices", "PayrollCalculationService", "Microsoft.EntityFrameworkCore", "System.Drawing", "Registry", "Win32");
    }
}
