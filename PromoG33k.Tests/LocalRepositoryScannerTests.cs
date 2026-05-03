// Code authored by Dean Edis (DeanTheCoder).
// Anyone is free to copy, modify, use, compile, or distribute this software,
// either in source code form or as a compiled binary, for any purpose.
//
// If you modify the code, please retain this copyright header,
// and consider contributing back to the repository or letting us know
// about your modifications. Your contributions are valued!
//
// THE SOFTWARE IS PROVIDED AS IS, WITHOUT WARRANTY OF ANY KIND.

using PromoG33k.Services;

namespace PromoG33k.Tests;

public class LocalRepositoryScannerTests
{
    [TestCase("https://github.com/deanthecoder/ReviewG33k.git")]
    [TestCase("git@github.com:deanthecoder/ReviewG33k.git")]
    [TestCase("ssh://git@github.com/deanthecoder/ReviewG33k.git")]
    public void TryNormalizeGitHubUrlSupportsCommonRemoteFormats(string remoteUrl)
    {
        var success = LocalRepositoryScanner.TryNormalizeGitHubUrl(remoteUrl, out var githubUrl, out var owner, out var repositoryName);

        Assert.That(success, Is.True);
        Assert.That(githubUrl, Is.EqualTo("https://github.com/deanthecoder/ReviewG33k"));
        Assert.That(owner, Is.EqualTo("deanthecoder"));
        Assert.That(repositoryName, Is.EqualTo("ReviewG33k"));
    }

    [Test]
    public void GetRepositoriesReadsLocalGitMetadataAndReadmeMedia()
    {
        var root = Directory.CreateTempSubdirectory();
        try
        {
            var repositoryDirectory = CreateRepository(root, "ReviewG33k", "https://github.com/deanthecoder/ReviewG33k.git");
            Directory.CreateDirectory(Path.Combine(repositoryDirectory.FullName, "img"));
            var screenshotPath = Path.Combine(repositoryDirectory.FullName, "img", "ReviewG33k.png");
            File.WriteAllText(screenshotPath, "not a real image");
            File.WriteAllText(
                Path.Combine(repositoryDirectory.FullName, "README.md"),
                """
                # ReviewG33k
                ReviewG33k is a lightweight desktop app for fast, practical code reviews.
                ## What it can do
                - Review Bitbucket pull requests.
                - Review local committed changes.
                ![Main window screenshot](img/ReviewG33k.png)
                """);
            File.WriteAllText(Path.Combine(repositoryDirectory.FullName, "Program.cs"), "class Program { }");
            File.WriteAllText(
                Path.Combine(repositoryDirectory.FullName, "ReviewG33k.csproj"),
                """
                <Project Sdk="Microsoft.NET.Sdk">
                  <ItemGroup>
                    <PackageReference Include="Avalonia" Version="11.2.7" />
                  </ItemGroup>
                </Project>
                """);
            var scanner = new LocalRepositoryScanner();

            var repositories = scanner.GetRepositories(root);

            Assert.That(repositories, Has.Count.EqualTo(1));
            Assert.That(repositories[0].Name, Is.EqualTo("ReviewG33k"));
            Assert.That(repositories[0].Owner, Is.EqualTo("deanthecoder"));
            Assert.That(repositories[0].GitHubUrl, Is.EqualTo("https://github.com/deanthecoder/ReviewG33k"));
            Assert.That(repositories[0].Description, Does.StartWith("ReviewG33k is a lightweight"));
            Assert.That(repositories[0].ScreenshotUrls, Is.EqualTo(new[] { screenshotPath }));
            Assert.That(repositories[0].ReadmeHeadings, Is.EqualTo(new[] { "What it can do" }));
            Assert.That(
                repositories[0].ReadmeHighlights,
                Is.EqualTo(new[] { "Review Bitbucket pull requests.", "Review local committed changes." }));
            Assert.That(repositories[0].Language, Is.EqualTo("C#"));
            Assert.That(repositories[0].IsAvaloniaProject, Is.True);
            Assert.That(repositories[0].IsCrossPlatform, Is.True);
        }
        finally
        {
            root.Delete(recursive: true);
        }
    }

    [Test]
    public void GetRepositoriesSkipsReposWithoutGitHubRemote()
    {
        var root = Directory.CreateTempSubdirectory();
        try
        {
            CreateRepository(root, "PrivateTool", "https://example.com/dean/PrivateTool.git");
            var scanner = new LocalRepositoryScanner();

            var repositories = scanner.GetRepositories(root);

            Assert.That(repositories, Is.Empty);
        }
        finally
        {
            root.Delete(recursive: true);
        }
    }

    private static DirectoryInfo CreateRepository(DirectoryInfo root, string name, string remoteUrl)
    {
        var repositoryDirectory = Directory.CreateDirectory(Path.Combine(root.FullName, name));
        var gitDirectory = Directory.CreateDirectory(Path.Combine(repositoryDirectory.FullName, ".git"));
        File.WriteAllText(Path.Combine(gitDirectory.FullName, "HEAD"), "ref: refs/heads/main");
        File.WriteAllText(
            Path.Combine(gitDirectory.FullName, "config"),
            $$"""
            [core]
                repositoryformatversion = 0
            [remote "origin"]
                url = {{remoteUrl}}
                fetch = +refs/heads/*:refs/remotes/origin/*
            [branch "main"]
                remote = origin
                merge = refs/heads/main
            """);
        return repositoryDirectory;
    }
}
