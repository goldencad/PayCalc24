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

        Assert.Equal(["PayCalc24.Domain"], ProjectReferences(project));
        Assert.Empty(PackageReferences(project));
        AssertSourceExcludes(
            "src/Modules/PayCalc24.FormulaEngine",
            "Microsoft.EntityFrameworkCore",
            "Avalonia",
            "System.Net.Http",
            "PayCalc24.Infrastructure");
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
