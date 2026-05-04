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

namespace PromoG33k.Tests;

public class RepositoryProfileTests
{
    [Test]
    public void ShortDescriptionTruncatesLongDescriptions()
    {
        var repository = new RepositoryProfile
        {
            Description = new string('a', 90)
        };

        Assert.That(repository.ShortDescription, Is.EqualTo($"{new string('a', 80)}..."));
    }

    [Test]
    public void ShortDescriptionLeavesShortDescriptionsAlone()
    {
        var repository = new RepositoryProfile
        {
            Description = "Short description."
        };

        Assert.That(repository.ShortDescription, Is.EqualTo("Short description."));
    }

    [Test]
    public void ReadinessWarningsDescribeMissingSharingSignals()
    {
        var repository = new RepositoryProfile
        {
            HasReadme = true,
            ReadmeMentionsLicense = false,
            ScreenshotUrls = []
        };

        Assert.That(repository.HasReadinessWarnings, Is.True);
        Assert.That(repository.ReadinessWarningText, Does.Contain("README does not mention a license."));
        Assert.That(repository.ReadinessWarningText, Does.Contain("No screenshots found."));
    }

    [Test]
    public void ReadinessWarningsDoNotDuplicateLicenseWarningWhenReadmeIsMissing()
    {
        var repository = new RepositoryProfile
        {
            HasReadme = false,
            ReadmeMentionsLicense = false
        };

        Assert.That(repository.ReadinessWarningText, Does.Contain("No README found."));
        Assert.That(repository.ReadinessWarningText, Does.Not.Contain("license"));
    }
}
