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
    public DateTime? LastPromotedAtUtc { get; set; }
    public RepositoryPriority Priority { get; set; } = RepositoryPriority.Normal;
    public List<string> ScreenshotUrls { get; set; } = [];
    public List<string> DemoUrls { get; set; } = [];
    public List<string> Topics { get; set; } = [];
    public List<string> ReadmeHeadings { get; set; } = [];
    public List<string> ReadmeHighlights { get; set; } = [];
    public bool HasReadme { get; set; }
    public bool ReadmeMentionsLicense { get; set; }
    public bool IsEmptyRepository { get; set; }
    public bool? HasGitHubSocialPreviewImage { get; set; }
    public string GitHubSocialPreviewImageUrl { get; set; } = string.Empty;
    public string Language { get; set; } = string.Empty;
    public int StargazersCount { get; set; }
    public string SocialPreviewText { get; set; } = string.Empty;
    public bool IsAvaloniaProject { get; set; }
    public bool IsCrossPlatform { get; set; }

    public bool IsExcluded => Priority == RepositoryPriority.Excluded;
    public bool HasMedia => ScreenshotUrls.Count > 0 || DemoUrls.Count > 0;
    public bool HasReadinessWarnings => ReadinessWarnings.Count > 0;

    public IReadOnlyList<string> ReadinessWarnings
    {
        get
        {
            var warnings = new List<string>();
            if (IsEmptyRepository)
                warnings.Add("Repository appears empty.");
            if (!HasReadme)
                warnings.Add("No README found.");
            else if (!ReadmeMentionsLicense)
                warnings.Add("README does not mention a license.");
            if (ScreenshotUrls.Count == 0)
                warnings.Add("No screenshots found.");
            if (HasGitHubSocialPreviewImage == false)
                warnings.Add("No custom GitHub social preview image set.");
            return warnings;
        }
    }

    public string ReadinessWarningText => string.Join(Environment.NewLine, ReadinessWarnings);

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

    public string UpdatedText => UpdatedAtUtc == default ? "Updated date unknown" : $"Updated {GetRelativeAgeText(UpdatedAtUtc, DateTime.UtcNow)}";

    public string LastPromotedText => LastPromotedAtUtc.HasValue
        ? $"Used {GetRelativeAgeText(LastPromotedAtUtc.Value, DateTime.UtcNow)}"
        : "Never used";

    public string ScreenshotSummary =>
        ScreenshotUrls.Count == 0
            ? "No screenshots found yet."
            : $"{ScreenshotUrls.Count} screenshot candidates found.";

    private static string GetRelativeAgeText(DateTime utcDateTime, DateTime utcNow)
    {
        var elapsed = utcNow - utcDateTime;
        if (elapsed < TimeSpan.Zero)
            elapsed = TimeSpan.Zero;

        if (elapsed.TotalMinutes < 1)
            return "just now";
        if (elapsed.TotalHours < 1)
            return $"{Math.Max(1, (int)elapsed.TotalMinutes)} minutes ago";
        if (elapsed.TotalDays < 1)
        {
            var hours = Math.Max(1, (int)elapsed.TotalHours);
            return hours == 1 ? "1 hour ago" : $"{hours} hours ago";
        }
        if (elapsed.TotalDays < 14)
        {
            var days = Math.Max(1, (int)elapsed.TotalDays);
            return days == 1 ? "1 day ago" : $"{days} days ago";
        }
        if (elapsed.TotalDays < 60)
        {
            var weeks = Math.Max(1, (int)(elapsed.TotalDays / 7));
            return weeks == 1 ? "1 week ago" : $"{weeks} weeks ago";
        }
        if (elapsed.TotalDays < 730)
        {
            var months = Math.Max(1, (int)(elapsed.TotalDays / 30));
            return months == 1 ? "1 month ago" : $"{months} months ago";
        }

        var years = Math.Max(1, (int)(elapsed.TotalDays / 365));
        return years == 1 ? "1 year ago" : $"{years} years ago";
    }
}
