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
using System.Linq;
using System.Text;
using PromoG33k.Models;

namespace PromoG33k.Services;

/// <summary>
/// Builds a Markdown catalogue of locally scanned repositories.
/// </summary>
/// <remarks>
/// The summary is deliberately deterministic and fact-based so it can be copied without spending API credits.
/// </remarks>
public sealed class ProjectSummaryService
{
    public string CreateMarkdownSummary(IEnumerable<RepositoryProfile> repositories, DateTime utcNow)
    {
        if (repositories == null)
            throw new ArgumentNullException(nameof(repositories));

        var repositoryList = repositories
            .OrderBy(repository => repository.IsExcluded)
            .ThenByDescending(repository => repository.UpdatedAtUtc)
            .ThenBy(repository => repository.Name)
            .ToArray();

        var builder = new StringBuilder();
        builder.AppendLine("# Local project summary");
        builder.AppendLine();
        builder.AppendLine($"Generated: {utcNow.ToLocalTime():yyyy-MM-dd HH:mm}");
        builder.AppendLine($"Projects: {repositoryList.Length}");

        foreach (var repository in repositoryList)
        {
            builder.AppendLine();
            builder.AppendLine($"## {Fallback(repository.Name, "Unnamed repository")}");
            AppendLineIfPresent(builder, "GitHub", repository.GitHubUrl);
            AppendLineIfPresent(builder, "Description", repository.Description);
            if (repository.IsExcluded)
                builder.AppendLine("Priority: Excluded");

            var signals = GetSignals(repository).ToArray();
            if (signals.Length > 0)
            {
                builder.AppendLine("Signals:");
                foreach (var signal in signals)
                    builder.AppendLine($"- {signal}");
            }

            var highlights = repository.ReadmeHighlights
                .Where(highlight => !string.IsNullOrWhiteSpace(highlight))
                .Take(3)
                .ToArray();
            if (highlights.Length > 0)
            {
                builder.AppendLine("README highlights:");
                foreach (var highlight in highlights)
                    builder.AppendLine($"- {highlight}");
            }

            if (repository.ReadinessWarnings.Count > 0)
            {
                builder.AppendLine("Readiness notes:");
                foreach (var warning in repository.ReadinessWarnings)
                    builder.AppendLine($"- {warning}");
            }
        }

        return builder.ToString().TrimEnd();
    }

    private static IEnumerable<string> GetSignals(RepositoryProfile repository)
    {
        if (repository.IsAvaloniaProject)
            yield return "Avalonia project";
        if (repository.IsCrossPlatform)
            yield return "Cross-platform";
        if (repository.ScreenshotUrls.Count > 0)
            yield return $"{repository.ScreenshotUrls.Count} screenshot candidate{Plural(repository.ScreenshotUrls.Count)}";
        if (repository.DemoUrls.Count > 0)
            yield return $"{repository.DemoUrls.Count} demo link{Plural(repository.DemoUrls.Count)}";
        if (repository.Topics.Count > 0)
            yield return $"Topics: {repository.TopicText}";
    }

    private static void AppendLineIfPresent(StringBuilder builder, string label, string value)
    {
        if (!string.IsNullOrWhiteSpace(value))
            builder.AppendLine($"{label}: {value.Trim()}");
    }

    private static string Fallback(string value, string fallback) =>
        string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();

    private static string Plural(int count) =>
        count == 1 ? string.Empty : "s";
}
