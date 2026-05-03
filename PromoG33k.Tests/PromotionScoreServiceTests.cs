// Code authored by Dean Edis (DeanTheCoder).
// Anyone is free to copy, modify, use, compile, or distribute this software,
// either in source code form or as a compiled binary, for any purpose.
//
// If you modify the code, please retain this copyright header,
// and consider contributing back to the repository or letting us know
// about your modifications. Your contributions are valued!
//
// THE SOFTWARE IS PROVIDED AS IS, WITHOUT WARRANTY OF ANY KIND.

using PromoG33k.Models;
using PromoG33k.Services;

namespace PromoG33k.Tests;

public class PromotionScoreServiceTests
{
    [Test]
    public void RankPrefersNewerRepositoryWhenOtherSignalsAreSimilar()
    {
        var now = new DateTime(2026, 5, 2, 12, 0, 0, DateTimeKind.Utc);
        var service = new PromotionScoreService();
        var oldRepo = CreateRepo("OldRepo", now.AddYears(-4), now.AddMonths(-8));
        var newRepo = CreateRepo("NewRepo", now.AddDays(-20), now.AddDays(-3));

        var ranked = service.Rank([oldRepo, newRepo], [], now);

        Assert.That(ranked.First().Name, Is.EqualTo("NewRepo"));
    }

    [Test]
    public void RankExcludesExcludedRepositories()
    {
        var now = new DateTime(2026, 5, 2, 12, 0, 0, DateTimeKind.Utc);
        var service = new PromotionScoreService();
        var excludedRepo = CreateRepo("ExcludedRepo", now.AddDays(-1), now.AddDays(-1));
        excludedRepo.Priority = RepositoryPriority.Excluded;

        var ranked = service.Rank([excludedRepo], [], now);

        Assert.That(ranked, Is.Empty);
    }

    [Test]
    public void RecentPromotionPenalizesRepository()
    {
        var now = new DateTime(2026, 5, 2, 12, 0, 0, DateTimeKind.Utc);
        var service = new PromotionScoreService();
        var repo = CreateRepo("PromoRepo", now.AddDays(-10), now.AddDays(-2));

        var cleanScore = service.CalculateScore(repo, [], now);
        var recentScore = service.CalculateScore(
            repo,
            [new PromotionHistoryEntry { RepositoryName = "PromoRepo", GeneratedAtUtc = now.AddDays(-1) }],
            now);

        Assert.That(recentScore, Is.LessThan(cleanScore));
    }

    private static RepositoryProfile CreateRepo(string name, DateTime createdAtUtc, DateTime updatedAtUtc) =>
        new RepositoryProfile
        {
            Name = name,
            GitHubUrl = $"https://github.com/deanthecoder/{name}",
            Description = "A project.",
            CreatedAtUtc = createdAtUtc,
            UpdatedAtUtc = updatedAtUtc,
            Priority = RepositoryPriority.Normal,
            ScreenshotUrls = ["screenshot.png"],
            Topics = ["dotnet"]
        };
}
