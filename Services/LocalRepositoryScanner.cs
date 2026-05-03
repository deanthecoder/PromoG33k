// Code authored by Dean Edis (DeanTheCoder).
// Anyone is free to copy, modify, use, compile, or distribute this software,
// either in source code form or as a compiled binary, for any purpose.
//
// If you modify the code, please retain this copyright header,
// and consider contributing back to the repository or letting us know
// about your modifications. Your contributions are valued!
//
// THE SOFTWARE IS PROVIDED AS IS, WITHOUT WARRANTY OF ANY KIND.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using PromoG33k.Models;

namespace PromoG33k.Services;

/// <summary>
/// Builds repository profiles from local git checkouts.
/// </summary>
/// <remarks>
/// Only repositories with a GitHub remote are included, because generated posts need a usable project URL.
/// </remarks>
public sealed class LocalRepositoryScanner
{
    private static readonly Regex HttpsGitHubRegex = new Regex(
        @"^https://github\.com/(?<owner>[^/]+)/(?<repo>[^/]+?)(?:\.git)?/?$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex ScpGitHubRegex = new Regex(
        @"^git@github\.com:(?<owner>[^/]+)/(?<repo>[^/]+?)(?:\.git)?$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex SshGitHubRegex = new Regex(
        @"^ssh://git@github\.com/(?<owner>[^/]+)/(?<repo>[^/]+?)(?:\.git)?$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private readonly ReadmeMediaExtractor m_readmeMediaExtractor;

    public LocalRepositoryScanner()
        : this(new ReadmeMediaExtractor())
    {
    }

    public LocalRepositoryScanner(ReadmeMediaExtractor readmeMediaExtractor) =>
        m_readmeMediaExtractor = readmeMediaExtractor ?? throw new ArgumentNullException(nameof(readmeMediaExtractor));

    public IReadOnlyList<RepositoryProfile> GetRepositories(DirectoryInfo rootDirectory)
    {
        if (rootDirectory?.Exists != true)
            return [];

        return rootDirectory
            .EnumerateDirectories()
            .Select(CreateRepositoryProfile)
            .Where(repository => repository != null)
            .OrderByDescending(repository => repository.UpdatedAtUtc)
            .ThenBy(repository => repository.Name)
            .ToArray();
    }

    public static bool TryNormalizeGitHubUrl(string remoteUrl, out string githubUrl, out string owner, out string repositoryName)
    {
        githubUrl = null;
        owner = null;
        repositoryName = null;

        if (string.IsNullOrWhiteSpace(remoteUrl))
            return false;

        var match = HttpsGitHubRegex.Match(remoteUrl.Trim());
        if (!match.Success)
            match = ScpGitHubRegex.Match(remoteUrl.Trim());
        if (!match.Success)
            match = SshGitHubRegex.Match(remoteUrl.Trim());
        if (!match.Success)
            return false;

        owner = match.Groups["owner"].Value;
        repositoryName = match.Groups["repo"].Value;
        githubUrl = $"https://github.com/{owner}/{repositoryName}";
        return true;
    }

    private RepositoryProfile CreateRepositoryProfile(DirectoryInfo repositoryDirectory)
    {
        var gitDirectory = ResolveGitDirectory(repositoryDirectory);
        if (gitDirectory == null)
            return null;

        var gitConfigFile = new FileInfo(Path.Combine(gitDirectory.FullName, "config"));
        if (!gitConfigFile.Exists)
            return null;

        var remoteUrl = ReadOriginRemoteUrl(gitConfigFile);
        if (!TryNormalizeGitHubUrl(remoteUrl, out var githubUrl, out var owner, out var repositoryName))
            return null;

        var readmeFile = GetReadmeFile(repositoryDirectory);
        var readmeText = readmeFile?.Exists == true ? File.ReadAllText(readmeFile.FullName) : string.Empty;
        var isAvaloniaProject = IsAvaloniaProject(repositoryDirectory);
        return new RepositoryProfile
        {
            Name = repositoryName,
            Owner = owner,
            GitHubUrl = githubUrl,
            Description = ExtractDescription(readmeText),
            DefaultBranch = ReadDefaultBranch(gitConfigFile),
            CreatedAtUtc = repositoryDirectory.CreationTimeUtc,
            UpdatedAtUtc = GetUpdatedAtUtc(repositoryDirectory, gitDirectory),
            Priority = RepositoryPriority.Normal,
            ScreenshotUrls = m_readmeMediaExtractor.ExtractScreenshotPaths(readmeText, repositoryDirectory).ToList(),
            ReadmeHeadings = ExtractReadmeHeadings(readmeText).ToList(),
            ReadmeHighlights = ExtractReadmeHighlights(readmeText).ToList(),
            Language = DetectPrimaryLanguage(repositoryDirectory),
            IsAvaloniaProject = isAvaloniaProject,
            IsCrossPlatform = isAvaloniaProject || MentionsCrossPlatform(readmeText)
        };
    }

    private static DirectoryInfo ResolveGitDirectory(DirectoryInfo repositoryDirectory)
    {
        var dotGitDirectory = new DirectoryInfo(Path.Combine(repositoryDirectory.FullName, ".git"));
        if (dotGitDirectory.Exists)
            return dotGitDirectory;

        var dotGitFile = new FileInfo(Path.Combine(repositoryDirectory.FullName, ".git"));
        if (!dotGitFile.Exists)
            return null;

        var gitDirLine = File.ReadLines(dotGitFile.FullName).FirstOrDefault() ?? string.Empty;
        const string prefix = "gitdir:";
        if (!gitDirLine.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            return null;

        var gitDirPath = gitDirLine[prefix.Length..].Trim();
        if (!Path.IsPathRooted(gitDirPath))
            gitDirPath = Path.Combine(repositoryDirectory.FullName, gitDirPath);

        var gitDirectory = new DirectoryInfo(Path.GetFullPath(gitDirPath));
        return gitDirectory.Exists ? gitDirectory : null;
    }

    private static string ReadOriginRemoteUrl(FileInfo gitConfigFile)
    {
        var inOriginRemote = false;
        foreach (var line in File.ReadLines(gitConfigFile.FullName))
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith("[", StringComparison.Ordinal))
            {
                inOriginRemote = trimmed.Equals("[remote \"origin\"]", StringComparison.OrdinalIgnoreCase);
                continue;
            }

            if (!inOriginRemote || !trimmed.StartsWith("url =", StringComparison.OrdinalIgnoreCase))
                continue;

            return trimmed["url =".Length..].Trim();
        }

        return string.Empty;
    }

    private static string ReadDefaultBranch(FileInfo gitConfigFile)
    {
        var branchName = string.Empty;
        foreach (var line in File.ReadLines(gitConfigFile.FullName))
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith("[branch ", StringComparison.OrdinalIgnoreCase))
            {
                branchName = trimmed
                    .Replace("[branch ", string.Empty, StringComparison.OrdinalIgnoreCase)
                    .Trim(']', '"', ' ');
            }
        }

        return string.IsNullOrWhiteSpace(branchName) ? "main" : branchName;
    }

    private static FileInfo GetReadmeFile(DirectoryInfo repositoryDirectory) =>
        repositoryDirectory
            .EnumerateFiles("README.*", SearchOption.TopDirectoryOnly)
            .OrderByDescending(file => file.Name.Equals("README.md", StringComparison.OrdinalIgnoreCase))
            .ThenBy(file => file.Name)
            .FirstOrDefault();

    private static string ExtractDescription(string readmeText)
    {
        if (string.IsNullOrWhiteSpace(readmeText))
            return string.Empty;

        return readmeText
            .Split('\n')
            .Select(line => line.Trim())
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Where(line => !line.StartsWith("#", StringComparison.Ordinal))
            .Where(line => !line.StartsWith("[!", StringComparison.Ordinal))
            .Where(line => !line.StartsWith("![", StringComparison.Ordinal))
            .Where(line => !line.StartsWith("<", StringComparison.Ordinal))
            .FirstOrDefault() ?? string.Empty;
    }

    private static IEnumerable<string> ExtractReadmeHeadings(string readmeText)
    {
        if (string.IsNullOrWhiteSpace(readmeText))
            yield break;

        foreach (var line in readmeText.Split('\n'))
        {
            var trimmed = line.Trim();
            if (!trimmed.StartsWith("##", StringComparison.Ordinal) ||
                trimmed.StartsWith("#######", StringComparison.Ordinal))
                continue;

            var heading = CleanReadmeLine(trimmed.TrimStart('#').Trim());
            if (!string.IsNullOrWhiteSpace(heading))
                yield return heading;
        }
    }

    private static IEnumerable<string> ExtractReadmeHighlights(string readmeText)
    {
        if (string.IsNullOrWhiteSpace(readmeText))
            yield break;

        foreach (var line in readmeText.Split('\n'))
        {
            var trimmed = line.Trim();
            if (!IsFeatureLikeLine(trimmed))
                continue;

            var highlight = CleanReadmeLine(trimmed.TrimStart('-', '*', '+', ' '));
            if (!string.IsNullOrWhiteSpace(highlight))
                yield return highlight;
        }
    }

    private static bool IsFeatureLikeLine(string line) =>
        line.StartsWith("- ", StringComparison.Ordinal) ||
        line.StartsWith("* ", StringComparison.Ordinal) ||
        line.StartsWith("+ ", StringComparison.Ordinal);

    private static string CleanReadmeLine(string line)
    {
        var value = Regex.Replace(line, @"!\[[^\]]*\]\([^)]+\)", string.Empty);
        value = Regex.Replace(value, @"\[(?<text>[^\]]+)\]\([^)]+\)", "${text}");
        value = Regex.Replace(value, @"[`*_>#]", string.Empty);
        return value.Trim();
    }

    private static DateTime GetUpdatedAtUtc(DirectoryInfo repositoryDirectory, DirectoryInfo gitDirectory)
    {
        var gitHead = new FileInfo(Path.Combine(gitDirectory.FullName, "HEAD"));
        if (gitHead.Exists)
            return gitHead.LastWriteTimeUtc;

        return repositoryDirectory.LastWriteTimeUtc;
    }

    private static string DetectPrimaryLanguage(DirectoryInfo repositoryDirectory)
    {
        if (repositoryDirectory.EnumerateFiles("*.cs", SearchOption.AllDirectories).Any(IsSourceFile))
            return "C#";
        if (repositoryDirectory.EnumerateFiles("*.ts", SearchOption.AllDirectories).Any(IsSourceFile))
            return "TypeScript";
        if (repositoryDirectory.EnumerateFiles("*.js", SearchOption.AllDirectories).Any(IsSourceFile))
            return "JavaScript";
        if (repositoryDirectory.EnumerateFiles("*.py", SearchOption.AllDirectories).Any(IsSourceFile))
            return "Python";
        return string.Empty;
    }

    private static bool IsAvaloniaProject(DirectoryInfo repositoryDirectory) =>
        repositoryDirectory
            .EnumerateFiles("*.csproj", SearchOption.AllDirectories)
            .Where(IsSourceFile)
            .Any(file => File.ReadAllText(file.FullName).Contains("Avalonia", StringComparison.OrdinalIgnoreCase));

    private static bool MentionsCrossPlatform(string readmeText)
    {
        if (string.IsNullOrWhiteSpace(readmeText))
            return false;

        return readmeText.Contains("cross-platform", StringComparison.OrdinalIgnoreCase) ||
               readmeText.Contains("cross platform", StringComparison.OrdinalIgnoreCase) ||
               readmeText.Contains("Windows and Mac", StringComparison.OrdinalIgnoreCase) ||
               readmeText.Contains("Windows and macOS", StringComparison.OrdinalIgnoreCase) ||
               readmeText.Contains("Windows, Mac", StringComparison.OrdinalIgnoreCase) ||
               readmeText.Contains("Windows, macOS", StringComparison.OrdinalIgnoreCase) ||
               readmeText.Contains("Windows and Linux", StringComparison.OrdinalIgnoreCase) ||
               readmeText.Contains("macOS and Linux", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsSourceFile(FileInfo file) =>
        !file.FullName.Contains($"{Path.DirectorySeparatorChar}.git{Path.DirectorySeparatorChar}", StringComparison.Ordinal) &&
        !file.FullName.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase) &&
        !file.FullName.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase) &&
        !file.FullName.Contains($"{Path.DirectorySeparatorChar}node_modules{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase);
}
