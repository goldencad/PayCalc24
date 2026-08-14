namespace PayCalc24.ArchitectureTests;

public sealed class Task20AArchitectureTests
{
    private static readonly string Root = FindRoot();

    [Fact]
    public void ApiContainsNoAiSpecificBusinessEndpointOrNaturalLanguageParser()
    {
        var source = ReadTree("src/PayCalc24.Api");
        Assert.DoesNotContain("AiCalculatePayroll", source, StringComparison.Ordinal);
        Assert.DoesNotContain("AgentApprovePayroll", source, StringComparison.Ordinal);
        Assert.DoesNotContain("HumanCalculatePayroll", source, StringComparison.Ordinal);
        Assert.DoesNotContain("natural language", source, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void NoParallelAgentPermissionModelExists()
    {
        var source = ReadTree("src");
        Assert.DoesNotContain("AgentPermissions", source, StringComparison.Ordinal);
        Assert.DoesNotContain("AgentAcl", source, StringComparison.Ordinal);
        Assert.DoesNotContain("AiRoles", source, StringComparison.Ordinal);
    }

    [Fact]
    public void ApplicationAndDomainDoNotDependOnAspNetCore()
    {
        Assert.DoesNotContain("Microsoft.AspNetCore", ReadTree("src/PayCalc24.Application"), StringComparison.Ordinal);
        Assert.DoesNotContain("Microsoft.AspNetCore", ReadTree("src/PayCalc24.Domain"), StringComparison.Ordinal);
    }

    private static string ReadTree(string relative) => string.Join('\n', Directory.EnumerateFiles(Path.Combine(Root, relative), "*.cs", SearchOption.AllDirectories).Select(File.ReadAllText));
    private static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "PayCalc24.sln"))) directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }
}
