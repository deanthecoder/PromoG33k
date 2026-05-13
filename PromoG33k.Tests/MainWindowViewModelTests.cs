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
using PromoG33k.Settings;
using PromoG33k.ViewModels;
using System.Net;
using System.Net.Http.Headers;

namespace PromoG33k.Tests;

[TestFixture]
public sealed class MainWindowViewModelTests
{
    [Test]
    public void SelectingRepositoryRaisesGenerateCommandCanExecuteChanged()
    {
        var settings = new AppSettings
        {
            Repositories = []
        };
        var viewModel = new MainWindowViewModel(
            new ClipboardService(),
            new PromotionScoreService(),
            new LocalRepositoryScanner(),
            new GitHubSocialPreviewService(new HttpClient(new StaticHtmlHandler())),
            new OpenAiPostGenerationService(),
            new ProjectSummaryService(),
            settings);

        var canExecuteChangedCount = 0;
        viewModel.GenerateDraftCommand.CanExecuteChanged += (_, _) => canExecuteChangedCount++;

        Assert.That(viewModel.GenerateDraftCommand.CanExecute(null), Is.False);

        viewModel.SelectedRepository = new RepositoryProfile
        {
            Name = "PromoG33k",
            GitHubUrl = "https://github.com/deanthecoder/PromoG33k"
        };

        Assert.That(canExecuteChangedCount, Is.EqualTo(1));
        Assert.That(viewModel.GenerateDraftCommand.CanExecute(null), Is.True);
    }

    [Test]
    public async Task TestOpenAiCommandUsesUnsavedSettingsPaneApiKey()
    {
        var handler = new CapturingHttpMessageHandler();
        var settings = new AppSettings();
        settings.SetOpenAiApiKey("sk-saved");
        settings.OpenAiModel = "gpt-saved";
        var viewModel = new MainWindowViewModel(
            new ClipboardService(),
            new PromotionScoreService(),
            new LocalRepositoryScanner(),
            new GitHubSocialPreviewService(new HttpClient(new StaticHtmlHandler())),
            new OpenAiPostGenerationService(new HttpClient(handler)),
            new ProjectSummaryService(),
            settings)
        {
            SettingsOpenAiApiKey = "sk-visible",
            SettingsOpenAiModel = "gpt-visible"
        };

        viewModel.TestOpenAiCommand.Execute(null);
        await handler.RequestCaptured.Task.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.That(handler.Authorization, Is.EqualTo(new AuthenticationHeaderValue("Bearer", "sk-visible")));
        Assert.That(handler.RequestBody, Does.Contain("\"model\":\"gpt-visible\""));
        Assert.That(settings.GetOpenAiApiKey(), Is.EqualTo("sk-saved"));
        Assert.That(settings.OpenAiModel, Is.EqualTo("gpt-saved"));
    }

    [Test]
    public void ChangingSelectedPriorityRefreshesRepositoryListOrder()
    {
        var lowPriorityRepository = new RepositoryProfile
        {
            Name = "LowPriority",
            GitHubUrl = "https://github.com/example/LowPriority",
            Priority = RepositoryPriority.Normal
        };
        var selectedRepository = new RepositoryProfile
        {
            Name = "Selected",
            GitHubUrl = "https://github.com/example/Selected",
            Priority = RepositoryPriority.Normal
        };
        var settings = new AppSettings
        {
            Repositories = [lowPriorityRepository, selectedRepository],
            PromotionHistory = []
        };
        var viewModel = new MainWindowViewModel(
            new ClipboardService(),
            new PromotionScoreService(),
            new LocalRepositoryScanner(),
            new GitHubSocialPreviewService(new HttpClient(new StaticHtmlHandler())),
            new OpenAiPostGenerationService(),
            new ProjectSummaryService(),
            settings);
        viewModel.SelectedRepository = selectedRepository;

        viewModel.SelectedPriorityIndex = 0;

        Assert.That(viewModel.Repositories.Select(repository => repository.Name).ToArray(), Is.EqualTo(new[] { "Selected", "LowPriority" }));
        Assert.That(viewModel.SelectedRepository, Is.SameAs(selectedRepository));
    }

    [Test]
    public void LastUsedSortShowsMostRecentlyUsedRepositoriesFirst()
    {
        var olderRepository = new RepositoryProfile
        {
            Name = "Older",
            GitHubUrl = "https://github.com/example/Older"
        };
        var newerRepository = new RepositoryProfile
        {
            Name = "Newer",
            GitHubUrl = "https://github.com/example/Newer"
        };
        var neverUsedRepository = new RepositoryProfile
        {
            Name = "Never",
            GitHubUrl = "https://github.com/example/Never"
        };
        var now = DateTime.UtcNow;
        var settings = new AppSettings
        {
            Repositories = [olderRepository, neverUsedRepository, newerRepository],
            PromotionHistory =
            [
                new PromotionHistoryEntry { RepositoryName = "Older", UsedAtUtc = now.AddDays(-8) },
                new PromotionHistoryEntry { RepositoryName = "Newer", UsedAtUtc = now.AddDays(-2) }
            ]
        };
        var viewModel = new MainWindowViewModel(
            new ClipboardService(),
            new PromotionScoreService(),
            new LocalRepositoryScanner(),
            new GitHubSocialPreviewService(new HttpClient(new StaticHtmlHandler())),
            new OpenAiPostGenerationService(),
            new ProjectSummaryService(),
            settings);

        viewModel.SortModeIndex = 2;

        Assert.That(viewModel.Repositories.Select(repository => repository.Name).ToArray(), Is.EqualTo(new[] { "Newer", "Older", "Never" }));
        Assert.That(newerRepository.LastPromotedText, Does.StartWith("Used 2 days ago").Or.StartWith("Used 1 day ago"));
        Assert.That(neverUsedRepository.LastPromotedText, Is.EqualTo("Never used"));
    }

    [Test]
    public async Task CopyProjectSummaryCommandCopiesMarkdownSummary()
    {
        var clipboardService = new CapturingClipboardService();
        var settings = new AppSettings
        {
            Repositories =
            [
                new RepositoryProfile
                {
                    Name = "PromoG33k",
                    GitHubUrl = "https://github.com/deanthecoder/PromoG33k",
                    Description = "A desktop app for tasteful project promotion.",
                    Language = "C#",
                    UpdatedAtUtc = new DateTime(2026, 5, 8, 12, 0, 0, DateTimeKind.Utc),
                    ScreenshotUrls = ["img/PromoG33k.png"],
                    ReadmeHighlights = ["Generate reviewable social drafts."],
                    IsAvaloniaProject = true,
                    IsCrossPlatform = true
                }
            ],
            PromotionHistory = []
        };
        var viewModel = new MainWindowViewModel(
            clipboardService,
            new PromotionScoreService(),
            new LocalRepositoryScanner(),
            new GitHubSocialPreviewService(new HttpClient(new StaticHtmlHandler())),
            new OpenAiPostGenerationService(),
            new ProjectSummaryService(),
            settings);

        viewModel.CopyProjectSummaryCommand.Execute(null);
        await clipboardService.TextCopied.Task.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.That(clipboardService.Text, Does.StartWith("# Local project summary"));
        Assert.That(clipboardService.Text, Does.Contain("## PromoG33k"));
        Assert.That(clipboardService.Text, Does.Contain("GitHub: https://github.com/deanthecoder/PromoG33k"));
        Assert.That(clipboardService.Text, Does.Not.Contain("Language: C#"));
        Assert.That(clipboardService.Text, Does.Not.Contain("Updated: 2026-05-08"));
        Assert.That(clipboardService.Text, Does.Contain("- Avalonia project"));
        Assert.That(clipboardService.Text, Does.Contain("- Generate reviewable social drafts."));
        Assert.That(viewModel.StatusText, Is.EqualTo("Copied summary for 1 project."));
    }

    private sealed class CapturingHttpMessageHandler : HttpMessageHandler
    {
        public TaskCompletionSource RequestCaptured { get; } = new();
        public AuthenticationHeaderValue Authorization { get; private set; }
        public string RequestBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Authorization = request.Headers.Authorization;
            RequestBody = await request.Content.ReadAsStringAsync(cancellationToken);
            RequestCaptured.SetResult();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"output_text":"OK"}""")
            };
        }
    }

    private sealed class CapturingClipboardService : IClipboardService
    {
        public TaskCompletionSource TextCopied { get; } = new();
        public string Text { get; private set; }

        public Task CopyTextAsync(string text)
        {
            Text = text;
            TextCopied.SetResult();
            return Task.CompletedTask;
        }
    }

    private sealed class StaticHtmlHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(
                new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("<html></html>")
                });
    }
}
