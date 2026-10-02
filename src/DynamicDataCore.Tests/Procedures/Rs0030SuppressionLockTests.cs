using System.Text.RegularExpressions;

namespace DynamicDataCore.Tests.Procedures;

/// <summary>
/// Source-level lock on the raw-SQL analyzer: the only RS0030 suppression allowed in the library sources is the
/// single <c>#pragma warning disable RS0030</c> in SqlProcedureExecutor.cs. Any other pragma (any spacing/case,
/// including a generic list that contains RS0030) or SuppressMessage for RS0030 fails this test.
/// </summary>
public class Rs0030SuppressionLockTests
{
    private const string AllowedFile = "SqlProcedureExecutor.cs";

    private static readonly Regex PragmaDisable = new(
        @"#\s*pragma\s+warning\s+disable\b[^\r\n]*\bRS0030\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex SuppressMessage = new(
        @"SuppressMessage\s*\([^\)]*\bRS0030\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    [Fact]
    public void Only_SqlProcedureExecutor_Suppresses_RS0030_Exactly_Once()
    {
        var srcRoot = Path.Combine(FindRepoRoot(), "src");
        var sep = Path.DirectorySeparatorChar;

        var files = Directory.EnumerateFiles(srcRoot, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{sep}DynamicDataCore.Tests{sep}"))
            .Where(f => !f.Contains($"{sep}obj{sep}") && !f.Contains($"{sep}bin{sep}"))
            .ToList();

        Assert.NotEmpty(files);

        var offenders = new List<string>();
        var allowedCount = 0;
        foreach (var file in files)
        {
            var text = File.ReadAllText(file);
            var pragmas = PragmaDisable.Matches(text).Count;
            var attrs = SuppressMessage.Matches(text).Count;

            if (Path.GetFileName(file) == AllowedFile && attrs == 0)
                allowedCount += pragmas;
            else if (pragmas + attrs > 0)
                offenders.Add($"{Path.GetRelativePath(srcRoot, file)} ({pragmas} pragma, {attrs} SuppressMessage)");
        }

        Assert.True(offenders.Count == 0, "RS0030 suppression outside the allowed place: " + string.Join("; ", offenders));
        Assert.True(allowedCount == 1, $"{AllowedFile} must contain exactly one '#pragma warning disable RS0030' but has {allowedCount}.");
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (dir.EnumerateFiles("*.sln").Any() || dir.EnumerateFiles("*.slnx").Any())
                return dir.FullName;
            dir = dir.Parent;
        }
        throw new InvalidOperationException("Repository root (.sln) not found above " + AppContext.BaseDirectory);
    }
}
