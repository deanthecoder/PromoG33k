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

namespace PromoG33k.Models;

/// <summary>
/// Local metadata and extracted promotional signals for a GitHub repository.
/// </summary>
/// <remarks>
/// This model is deliberately independent of GitHub API DTOs so repository discovery, README parsing,
/// and manual edits can all feed the same promotion queue.
/// </remarks>
public sealed class RepositoryProfile
{
    public string Name { get; set; } = string.Empty;
    public string Owner { get; set; } = string.Empty;
    public string GitHubUrl { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string DefaultBranch { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public RepositoryPriority Priority { get; set; } = RepositoryPriority.Normal;
    public List<string> ScreenshotUrls { get; set; } = [];
    public List<string> DemoUrls { get; set; } = [];
    public List<string> Topics { get; set; } = [];
    public List<string> ReadmeHeadings { get; set; } = [];
    public List<string> ReadmeHighlights { get; set; } = [];
    public string Language { get; set; } = string.Empty;
    public int StargazersCount { get; set; }
    public string SocialPreviewText { get; set; } = string.Empty;
    public bool IsAvaloniaProject { get; set; }
    public bool IsCrossPlatform { get; set; }

    public bool IsExcluded => Priority == RepositoryPriority.Excluded;
    public bool HasMedia => ScreenshotUrls.Count > 0 || DemoUrls.Count > 0;

    public string PromotionMaterialSummary
    {
        get
        {
            var signals = new List<string>();
            if (ScreenshotUrls.Count > 0)
                signals.Add($"{ScreenshotUrls.Count} screenshots");
            if (DemoUrls.Count > 0)
                signals.Add($"{DemoUrls.Count} demos");
            if (!string.IsNullOrWhiteSpace(Language))
                signals.Add(Language);
            if (Topics.Count > 0)
                signals.Add($"{Topics.Count} topics");
            return signals.Count == 0 ? "Repo metadata only" : string.Join(", ", signals);
        }
    }

    public string TopicText => string.Join(", ", Topics.Take(5));

    public string ShortDescription =>
        Description.Length <= 80 ? Description : $"{Description[..80].TrimEnd()}...";

    public string UpdatedText => UpdatedAtUtc == default ? "Updated date unknown" : $"Updated {UpdatedAtUtc:yyyy-MM-dd}";

    public string ScreenshotSummary =>
        ScreenshotUrls.Count == 0
            ? "No screenshots found yet."
            : $"{ScreenshotUrls.Count} screenshot candidates found.";
}
