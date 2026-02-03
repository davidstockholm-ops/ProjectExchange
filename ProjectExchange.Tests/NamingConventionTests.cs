using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace ProjectExchange.Tests;

/// <summary>
/// Verifies naming conventions across ProjectExchange.* assemblies and the repository file system:
/// - All namespaces must start with "ProjectExchange" (exact casing).
/// - All classes must be in PascalCase.
/// - No folders or .csproj files may use lowercase "projectexchange".
/// </summary>
public class NamingConventionTests
{
    private static readonly Regex PascalCaseRegex = new Regex("^[A-Z][a-zA-Z0-9]*$", RegexOptions.Compiled);

    /// <summary>
    /// Ensure ProjectExchange.* assemblies are loaded so reflection can see them.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static Assembly[] GetProjectExchangeAssemblies()
    {
        _ = typeof(ProjectExchange.Core.Markets.MarketService).Assembly;
        _ = typeof(ProjectExchange.Accounting.Domain.Services.LedgerService).Assembly;
        return AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => a.GetName().Name?.StartsWith("ProjectExchange", StringComparison.Ordinal) == true)
            .ToArray();
    }

    [Fact]
    public void All_Namespaces_Must_Start_With_ProjectExchange_Exact_Casing()
    {
        var assemblies = GetProjectExchangeAssemblies();
        var violations = new List<string>();

        foreach (var assembly in assemblies)
        {
            foreach (var type in GetExportedTypes(assembly))
            {
                var ns = type.Namespace;
                if (string.IsNullOrEmpty(ns))
                    continue;
                if (!ns.StartsWith("ProjectExchange", StringComparison.Ordinal))
                    violations.Add($"[{assembly.GetName().Name}] {type.FullName} -> Namespace must start with 'ProjectExchange' (found: '{ns}')");
            }
        }

        AssertViolations("Namespaces must start with 'ProjectExchange' (exact casing):", violations);
    }

    [Fact]
    public void All_Classes_Must_Be_PascalCase()
    {
        var assemblies = GetProjectExchangeAssemblies();
        var violations = new List<string>();

        foreach (var assembly in assemblies)
        {
            foreach (var type in GetExportedTypes(assembly))
            {
                if (type.Name.StartsWith("<", StringComparison.Ordinal))
                    continue;
                var name = type.Name;
                var baseName = name.Contains('`') ? name.Substring(0, name.IndexOf('`')) : name;
                if (string.IsNullOrEmpty(baseName))
                    continue;
                if (!PascalCaseRegex.IsMatch(baseName))
                    violations.Add($"[{assembly.GetName().Name}] {type.FullName} -> Class name must be PascalCase (found: '{baseName}')");
            }
        }

        AssertViolations("Class names must be PascalCase:", violations);
    }

    [Fact]
    public void No_Folders_Or_Csproj_Files_Use_Lowercase_ProjectExchange_And_All_Folders_PascalCase()
    {
        var root = FindSolutionRoot();
        if (root == null)
        {
            // Could not find solution root (e.g. running from different cwd); skip file-system check or fail softly
            return;
        }

        var violations = new List<string>();
        var sep = Path.DirectorySeparatorChar;
        var skipSegments = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "bin", "obj", ".git", ".vs", "node_modules",
            ".github", "scripts"
        };

        foreach (var dir in Directory.GetDirectories(root, "*", SearchOption.AllDirectories))
        {
            var relative = dir.Replace(root, "").TrimStart(sep);
            var pathSegments = relative.Split(sep);
            if (pathSegments.Any(s => skipSegments.Contains(s)))
                continue;
            var segment = Path.GetFileName(dir);
            if (segment.Contains("projectexchange", StringComparison.Ordinal))
                violations.Add($"Folder (wrong 'ProjectExchange' casing): {relative}");
            else if (!IsValidFolderNamePascalCase(segment))
                violations.Add($"Folder (must be PascalCase, no spaces/hyphens): {relative}");
        }

        foreach (var file in Directory.GetFiles(root, "*.csproj", SearchOption.AllDirectories))
        {
            var relative = file.Replace(root, "").TrimStart(sep);
            if (relative.Split(sep).Any(s => skipSegments.Contains(s)))
                continue;
            var fileName = Path.GetFileName(file);
            if (fileName.Contains("projectexchange", StringComparison.Ordinal))
                violations.Add($".csproj (wrong 'ProjectExchange' casing): {relative}");
        }

        AssertViolations("Folder/.csproj naming: use 'ProjectExchange' exact casing; all folders must be PascalCase (no lowercase start, no spaces/hyphens):", violations);
    }

    private static bool IsValidFolderNamePascalCase(string segment)
    {
        if (string.IsNullOrEmpty(segment) || segment.Contains(' ', StringComparison.Ordinal) || segment.Contains('-', StringComparison.Ordinal))
            return false;
        return segment.Split('.').All(part => PascalCaseRegex.IsMatch(part));
    }

    private static IEnumerable<Type> GetExportedTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetExportedTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            return ex.Types.Where(t => t != null)!;
        }
    }

    private static string? FindSolutionRoot()
    {
        var start = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)
            ?? Directory.GetCurrentDirectory();
        var dir = new DirectoryInfo(start);
        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "ProjectExchange.sln")))
                return dir.FullName;
            dir = dir.Parent;
        }
        if (File.Exists(Path.Combine(Directory.GetCurrentDirectory(), "ProjectExchange.sln")))
            return Directory.GetCurrentDirectory();
        return null;
    }

    private static void AssertViolations(string header, List<string> violations)
    {
        if (violations.Count == 0)
            return;

        var message = header + Environment.NewLine
            + string.Join(Environment.NewLine, violations.Select(v => "  - " + v));
        Assert.Fail(message);
    }
}
