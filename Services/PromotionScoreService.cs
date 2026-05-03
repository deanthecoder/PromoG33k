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
using PromoG33k.Models;

namespace PromoG33k.Services;

/// <summary>
/// Scores repositories for the promotion queue.
/// </summary>
/// <remarks>
/// The queue should favor fresh and well-presented work while avoiding repeated promotion of the same repo.
/// </remarks>
public sealed class PromotionScoreService
{
    public double CalculateScore(RepositoryProfile repository, IReadOnlyCollection<PromotionHistoryEntry> history, DateTime utcNow)
    {
        if (repository == null)
            throw new ArgumentNullException(nameof(repository));
        if (history == null)
            throw new ArgumentNullException(nameof(history));
        if (repository.IsExcluded)
            return double.NegativeInfinity;

        var score = 0.0;
        score += GetPriorityScore(repository.Priority);
        score += GetAgeScore(repository.CreatedAtUtc, utcNow);
        score += GetActivityScore(repository.UpdatedAtUtc, utcNow);
        score += repository.ScreenshotUrls.Count * 4;
        score += repository.DemoUrls.Count * 6;
        score += Math.Min(repository.Topics.Count, 5);
        score -= GetRecentPromotionPenalty(repository, history, utcNow);
        return score;
    }

    public IReadOnlyList<RepositoryProfile> Rank(
        IEnumerable<RepositoryProfile> repositories,
        IReadOnlyCollection<PromotionHistoryEntry> history,
        DateTime utcNow) =>
        repositories
            .Where(repository => !repository.IsExcluded)
            .OrderByDescending(repository => CalculateScore(repository, history, utcNow))
            .ThenBy(repository => repository.Name)
            .ToArray();

    private static double GetPriorityScore(RepositoryPriority priority) =>
        priority switch
        {
            RepositoryPriority.High => 30,
            RepositoryPriority.Normal => 15,
            RepositoryPriority.Low => 2,
            _ => 0
        };

    private static double GetAgeScore(DateTime createdAtUtc, DateTime utcNow)
    {
        if (createdAtUtc == default)
            return 0;

        var ageDays = Math.Max(0, (utcNow - createdAtUtc).TotalDays);
        if (ageDays <= 30)
            return 35;
        if (ageDays <= 90)
            return 25;
        if (ageDays <= 365)
            return 12;
        return 4;
    }

    private static double GetActivityScore(DateTime updatedAtUtc, DateTime utcNow)
    {
        if (updatedAtUtc == default)
            return 0;

        var ageDays = Math.Max(0, (utcNow - updatedAtUtc).TotalDays);
        if (ageDays <= 14)
            return 25;
        if (ageDays <= 60)
            return 15;
        if (ageDays <= 180)
            return 8;
        return 1;
    }

    private static double GetRecentPromotionPenalty(
        RepositoryProfile repository,
        IReadOnlyCollection<PromotionHistoryEntry> history,
        DateTime utcNow)
    {
        var lastUsed = history
            .Where(entry => entry.RepositoryName.Equals(repository.Name, StringComparison.OrdinalIgnoreCase))
            .Select(entry => entry.UsedAtUtc ?? entry.GeneratedAtUtc)
            .DefaultIfEmpty()
            .Max();

        if (lastUsed == default)
            return 0;

        var daysSinceUse = (utcNow - lastUsed).TotalDays;
        if (daysSinceUse < 7)
            return 40;
        if (daysSinceUse < 30)
            return 20;
        if (daysSinceUse < 90)
            return 8;
        return 0;
    }
}
