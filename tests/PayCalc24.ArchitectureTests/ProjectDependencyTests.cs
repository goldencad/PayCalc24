using System.Xml.Linq;

namespace PayCalc24.ArchitectureTests;

public sealed class ProjectDependencyTests
{
    private static readonly string RepositoryRoot = FindRepositoryRoot();

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
}
