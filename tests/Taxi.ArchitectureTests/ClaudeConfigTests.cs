using System.Text.Json;
using System.Text.RegularExpressions;

using Xunit;

namespace Taxi.ArchitectureTests;

/// <summary>
/// Keeps the Claude Code set-up honest. <c>CLAUDE.md</c> and <c>AGENTS.md</c> are loaded in every chat,
/// so every line of them is paid for again and again: they stay small, and everything else is read on
/// demand. These tests stop them growing back, and stop a rule, a skill or the settings from pointing
/// at nothing. Raise a limit deliberately, never by drift.
/// </summary>
public partial class ClaudeConfigTests
{
    private const int MaxClaudeMdLines = 90;
    private const int MaxAgentsMdLines = 220;

    private static readonly string Root = FindRoot();

    [Fact]
    public void ClaudeMd_StaysSmall_AndImportsExistingFiles()
    {
        var lines = ReadLines("CLAUDE.md");

        Assert.True(lines.Length <= MaxClaudeMdLines, $"CLAUDE.md has {lines.Length} lines (limit {MaxClaudeMdLines}); it is loaded in every chat.");

        var imports = lines.Where(l => l.StartsWith('@')).Select(l => l[1..].Trim()).ToList();
        Assert.Contains("AGENTS.md", imports);
        foreach (var import in imports)
        {
            Assert.True(File.Exists(Path.Combine(Root, import)), $"CLAUDE.md imports '{import}', which does not exist.");
        }
    }

    [Fact]
    public void AgentsMd_StaysADigest()
    {
        var lines = ReadLines("AGENTS.md");

        Assert.True(lines.Length <= MaxAgentsMdLines, $"AGENTS.md has {lines.Length} lines (limit {MaxAgentsMdLines}); move detail to docs/AGENT_REFERENCE.md.");
    }

    [Fact]
    public void ScriptsMentionedByClaudeFiles_Exist()
    {
        var sources = new List<string> { "CLAUDE.md", "AGENTS.md", ".claude/settings.json" };
        sources.AddRange(Directory.GetFiles(Path.Combine(Root, ".claude", "skills"), "SKILL.md", SearchOption.AllDirectories)
            .Select(f => Path.GetRelativePath(Root, f)));

        var missing = new List<string>();
        foreach (var source in sources)
        {
            foreach (Match match in ToolScript().Matches(File.ReadAllText(Path.Combine(Root, source))))
            {
                if (!File.Exists(Path.Combine(Root, match.Value)))
                {
                    missing.Add($"{source} -> {match.Value}");
                }
            }
        }

        Assert.True(missing.Count == 0, $"Scripts that do not exist: {string.Join("; ", missing)}");
    }

    [Fact]
    public void Settings_AreValidJson_WithExistingHookScripts()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(Root, ".claude", "settings.json")));

        Assert.True(document.RootElement.TryGetProperty("permissions", out _), "settings.json needs a permissions section.");

        var commands = document.RootElement.GetProperty("hooks").EnumerateObject()
            .SelectMany(e => e.Value.EnumerateArray())
            .SelectMany(m => m.GetProperty("hooks").EnumerateArray())
            .Select(h => h.GetProperty("command").GetString()!)
            .ToList();

        Assert.NotEmpty(commands);
        foreach (var command in commands)
        {
            var script = ToolScript().Match(command);
            Assert.True(script.Success && File.Exists(Path.Combine(Root, script.Value)), $"Hook '{command}' does not point at an existing tool script.");
        }
    }

    [Fact]
    public void Rules_HavePaths_ThatPointAtRealFolders()
    {
        var rules = Directory.GetFiles(Path.Combine(Root, ".claude", "rules"), "*.md");
        Assert.NotEmpty(rules);

        foreach (var rule in rules)
        {
            var name = Path.GetFileName(rule);
            var lines = File.ReadAllLines(rule);
            Assert.Equal("---", lines[0]);

            var end = Array.IndexOf(lines, "---", 1);
            Assert.True(end > 1, $"{name}: front matter must close.");

            var globs = lines[1..end]
                .Where(l => l.TrimStart().StartsWith("- ", StringComparison.Ordinal))
                .Select(l => l.Trim()[2..].Trim('"'))
                .ToList();
            Assert.True(globs.Count > 0, $"{name}: a rule without paths loads in every chat.");

            foreach (var glob in globs)
            {
                var folder = glob.Split('*')[0].TrimEnd('/');
                Assert.True(
                    Directory.Exists(Path.Combine(Root, folder)) || File.Exists(Path.Combine(Root, folder)),
                    $"{name}: '{glob}' starts at '{folder}', which does not exist.");
            }
        }
    }

    [Fact]
    public void Skills_HaveMatchingNameAndDescription()
    {
        var skills = Directory.GetDirectories(Path.Combine(Root, ".claude", "skills"));
        Assert.NotEmpty(skills);

        foreach (var folder in skills)
        {
            var expected = Path.GetFileName(folder);
            var file = Path.Combine(folder, "SKILL.md");
            Assert.True(File.Exists(file), $"{expected}: SKILL.md is missing.");

            var lines = File.ReadAllLines(file);
            Assert.Equal("---", lines[0]);

            var end = Array.IndexOf(lines, "---", 1);
            Assert.True(end > 1, $"{expected}: front matter must close.");

            var front = lines[1..end];
            Assert.Contains($"name: {expected}", front);
            Assert.Contains(front, l => l.StartsWith("description: ", StringComparison.Ordinal) && l.Length > "description: ".Length);
        }
    }

    [GeneratedRegex(@"tool/[A-Za-z0-9\-]+\.ps1")]
    private static partial Regex ToolScript();

    private static string[] ReadLines(string relative) => File.ReadAllLines(Path.Combine(Root, relative));

    private static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && directory.GetFiles("*.slnx").Length == 0)
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Repository root (the folder with the .slnx file) was not found.");
    }
}
