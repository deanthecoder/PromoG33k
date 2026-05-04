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
