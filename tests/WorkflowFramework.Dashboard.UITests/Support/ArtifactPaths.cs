using System.Security.Cryptography;
using System.Text;
using Reqnroll;

namespace WorkflowFramework.Dashboard.UITests.Support;

public static class ArtifactPaths
{
    private static readonly string RepoRoot = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));

    public static string ArtifactRoot => Path.Combine(RepoRoot, "TestResults", "dashboard-ui", "latest");
    public static string ScenarioRoot => Path.Combine(ArtifactRoot, "scenarios");
    public static string DocsPromotableRoot => Path.Combine(ArtifactRoot, "docs-promotable");

    public static void InitializeRun()
    {
        if (Directory.Exists(ArtifactRoot))
            Directory.Delete(ArtifactRoot, recursive: true);

        Directory.CreateDirectory(ScenarioRoot);
        Directory.CreateDirectory(DocsPromotableRoot);
    }

    public static string GetScenarioDirectory(ScenarioContext scenarioContext)
    {
        var safeName = CreateSafeScenarioName(scenarioContext.ScenarioInfo.Title);
        var directory = Path.Combine(ScenarioRoot, safeName);
        Directory.CreateDirectory(directory);
        return directory;
    }

    public static string GetScenarioArtifactPath(ScenarioContext scenarioContext, string category, string fileName)
    {
        var directory = Path.Combine(GetScenarioDirectory(scenarioContext), category);
        Directory.CreateDirectory(directory);
        return Path.Combine(directory, fileName);
    }

    public static string GetDocsPromotablePath(string fileName)
    {
        Directory.CreateDirectory(DocsPromotableRoot);
        return Path.Combine(DocsPromotableRoot, fileName);
    }

    private static string CreateSafeScenarioName(string title)
    {
        var builder = new StringBuilder(title.Length);
        foreach (var character in title)
            builder.Append(char.IsLetterOrDigit(character) ? character : '-');

        var normalized = builder.ToString().Trim('-');
        while (normalized.Contains("--", StringComparison.Ordinal))
            normalized = normalized.Replace("--", "-", StringComparison.Ordinal);

        normalized = string.IsNullOrWhiteSpace(normalized) ? "scenario" : normalized.ToLowerInvariant();
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(title))).ToLowerInvariant()[..8];
        return $"{normalized}-{hash}";
    }
}
