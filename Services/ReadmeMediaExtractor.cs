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

namespace PromoG33k.Services;

/// <summary>
/// Extracts promotable media links from README markdown.
/// </summary>
/// <remarks>
/// This first pass is deliberately text-based so repo scanning stays quick and works without cloning repositories.
/// </remarks>
public sealed class ReadmeMediaExtractor
{
    private static readonly Regex MarkdownImageRegex = new Regex(
        @"!\[(?<alt>[^\]]*)\]\((?<url>[^)\s]+)(?:\s+""[^""]*"")?\)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex HtmlImageRegex = new Regex(
        @"<img\b[^>]*\bsrc\s*=\s*[""'](?<url>[^""']+)[""'][^>]*>",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public IReadOnlyList<string> ExtractScreenshotUrls(
        string readmeMarkdown,
        string owner,
        string repositoryName,
        string defaultBranch)
    {
        if (string.IsNullOrWhiteSpace(readmeMarkdown))
            return [];

        return ExtractImageUrls(readmeMarkdown)
            .Where(IsSupportedImageUrl)
            .Where(IsLikelyScreenshotUrl)
            .Select(url => ResolveUrl(url, owner, repositoryName, defaultBranch))
            .Where(url => !string.IsNullOrWhiteSpace(url))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public IReadOnlyList<string> ExtractScreenshotPaths(string readmeMarkdown, DirectoryInfo repositoryDirectory)
    {
        if (string.IsNullOrWhiteSpace(readmeMarkdown) || repositoryDirectory?.Exists != true)
            return [];

        return ExtractImageUrls(readmeMarkdown)
            .Where(IsSupportedImageUrl)
            .Where(IsLikelyScreenshotUrl)
            .Select(url => ResolveLocalPath(url, repositoryDirectory))
            .Where(path => !string.IsNullOrWhiteSpace(path) && File.Exists(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static IEnumerable<string> ExtractImageUrls(string readmeMarkdown)
    {
        foreach (Match match in MarkdownImageRegex.Matches(readmeMarkdown))
            yield return match.Groups["url"].Value;
        foreach (Match match in HtmlImageRegex.Matches(readmeMarkdown))
            yield return match.Groups["url"].Value;
    }

    private static bool IsSupportedImageUrl(string url)
    {
        var path = StripQueryAndFragment(url);
        return path.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
               path.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
               path.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase) ||
               path.EndsWith(".webp", StringComparison.OrdinalIgnoreCase) ||
               path.EndsWith(".gif", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsLikelyScreenshotUrl(string url)
    {
        var lower = StripQueryAndFragment(url).ToLowerInvariant();
        if (lower.Contains("screenshot", StringComparison.Ordinal) ||
            lower.Contains("screen-shot", StringComparison.Ordinal) ||
            lower.Contains("screenshots/", StringComparison.Ordinal) ||
            lower.Contains("demo", StringComparison.Ordinal) ||
            lower.Contains("preview", StringComparison.Ordinal))
            return true;

        return !lower.Contains("logo", StringComparison.Ordinal) &&
               !lower.Contains("icon", StringComparison.Ordinal) &&
               !lower.Contains("badge", StringComparison.Ordinal) &&
               !lower.Contains("shield", StringComparison.Ordinal);
    }

    private static string ResolveUrl(string url, string owner, string repositoryName, string defaultBranch)
    {
        if (Uri.TryCreate(url, UriKind.Absolute, out _))
            return url;

        if (string.IsNullOrWhiteSpace(owner) ||
            string.IsNullOrWhiteSpace(repositoryName) ||
            string.IsNullOrWhiteSpace(defaultBranch))
            return string.Empty;

        url = url.TrimStart('/');
        return $"https://raw.githubusercontent.com/{owner}/{repositoryName}/{defaultBranch}/{url}";
    }

    private static string ResolveLocalPath(string url, DirectoryInfo repositoryDirectory)
    {
        if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return uri.IsFile ? uri.LocalPath : string.Empty;

        url = StripQueryAndFragment(url).TrimStart('/');
        return Path.GetFullPath(Path.Combine(repositoryDirectory.FullName, url.Replace('/', Path.DirectorySeparatorChar)));
    }

    private static string StripQueryAndFragment(string url)
    {
        var queryIndex = url.IndexOfAny(['?', '#']);
        return queryIndex < 0 ? url : url[..queryIndex];
    }
}
