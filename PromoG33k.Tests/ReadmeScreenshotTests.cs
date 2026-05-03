// Code authored by Dean Edis (DeanTheCoder).
// Anyone is free to copy, modify, use, compile, or distribute this software,
// either in source code form or as a compiled binary, for any purpose.
//
// If you modify the code, please retain this copyright header,
// and consider contributing back to the repository or letting us know
// about your modifications. Your contributions are valued!
//
// THE SOFTWARE IS PROVIDED AS IS, WITHOUT WARRANTY OF ANY KIND.

using System.Reflection;
using System.Runtime.CompilerServices;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using PromoG33k.Models;
using PromoG33k.ViewModels;
using PromoG33k.Views;

namespace PromoG33k.Tests;

[TestFixture]
public sealed class ReadmeScreenshotTests
{
    private static readonly DirectoryInfo RepositoryDirectory = new(GetRepositoryRootPath());
    private static readonly DirectoryInfo ImageDirectory = new(Path.Combine(RepositoryDirectory.FullName, "img"));

    [Test]
    public async Task CaptureMainWindowScreenshot()
    {
        var session = HeadlessUnitTestSession.GetOrStartForAssembly(Assembly.GetExecutingAssembly());

        await session.Dispatch(async () =>
        {
            ImageDirectory.Create();

            var viewModel = CreateViewModel();
            viewModel.IsStartupInitializationEnabled = false;
            var window = new MainWindow
            {
                Width = 1120,
                Height = 760,
                DataContext = viewModel
            };

            window.Show();

            try
            {
                await WaitForRenderAsync();
                Assert.That(viewModel.IsSettingsOpen, Is.False, "README screenshot should show the main repo workspace, not Settings.");
                await WaitForRenderAsync();
                SaveScreenshot(window, "PromoG33k.png");
            }
            finally
            {
                window.Close();
            }

            return true;
        }, CancellationToken.None);
    }

    private static MainWindowViewModel CreateViewModel()
    {
        var viewModel = new MainWindowViewModel();
        viewModel.Repositories.Clear();

        var promoG33k = new RepositoryProfile
        {
            Name = "PromoG33k",
            Owner = "deanthecoder",
            GitHubUrl = "https://github.com/deanthecoder/PromoG33k",
            Description = "Create editable social previews for local GitHub projects.",
            CreatedAtUtc = new DateTime(2026, 4, 20, 12, 0, 0, DateTimeKind.Utc),
            UpdatedAtUtc = new DateTime(2026, 5, 3, 12, 0, 0, DateTimeKind.Utc),
            Priority = RepositoryPriority.High,
            Language = "C#",
            Topics = ["dotnet", "avalonia", "opensource"],
            ReadmeHeadings = ["What It Does", "AI Generation", "Screenshots"],
            ReadmeHighlights =
            [
                "Scans local GitHub repos.",
                "Extracts README context and screenshots.",
                "Generates editable social preview text."
            ],
            SocialPreviewText = """
                I keep building useful tools and then forgetting to talk about them.

                PromoG33k helps turn local repo metadata, README notes, and screenshots into a short editable social preview.

                https://github.com/deanthecoder/PromoG33k

                #dotnet #avalonia #opensource
                """
        };

        viewModel.Repositories.Add(promoG33k);
        viewModel.Repositories.Add(
            new RepositoryProfile
            {
                Name = "ReviewG33k",
                Owner = "deanthecoder",
                GitHubUrl = "https://github.com/deanthecoder/ReviewG33k",
                Description = "A lightweight desktop app for fast, practical code reviews.",
                CreatedAtUtc = new DateTime(2026, 1, 10, 12, 0, 0, DateTimeKind.Utc),
                UpdatedAtUtc = new DateTime(2026, 4, 28, 12, 0, 0, DateTimeKind.Utc),
                Priority = RepositoryPriority.Normal,
                Language = "C#",
                Topics = ["dotnet", "codereview"]
            });
        viewModel.Repositories.Add(
            new RepositoryProfile
            {
                Name = "DTC.AsciiTheme",
                Owner = "deanthecoder",
                GitHubUrl = "https://github.com/deanthecoder/DTC.AsciiTheme",
                Description = "Retro ASCII-style UI for Avalonia.",
                CreatedAtUtc = new DateTime(2025, 11, 3, 12, 0, 0, DateTimeKind.Utc),
                UpdatedAtUtc = new DateTime(2026, 4, 12, 12, 0, 0, DateTimeKind.Utc),
                Priority = RepositoryPriority.Normal,
                Language = "C#",
                Topics = ["dotnet", "avalonia", "theme"]
            });

        viewModel.SelectedRepository = promoG33k;
        viewModel.DraftText = promoG33k.SocialPreviewText;
        return viewModel;
    }

    private static void SaveScreenshot(Window window, string fileName)
    {
        using var frame = window.CaptureRenderedFrame();
        Assert.That(frame, Is.Not.Null, $"Expected a rendered frame for screenshot '{fileName}'.");

        var file = new FileInfo(Path.Combine(ImageDirectory.FullName, fileName));
        using (var stream = file.Open(FileMode.Create, FileAccess.Write, FileShare.None))
        {
            frame!.Save(stream);
        }

        file.Refresh();
        Assert.That(file.Exists, Is.True, $"Expected screenshot '{file.FullName}' to be written.");
        Assert.That(file.Length, Is.GreaterThan(0L), $"Expected screenshot '{file.FullName}' to contain data.");
    }

    private static async Task WaitForRenderAsync()
    {
        await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Render);
        await Task.Delay(50);
    }

    private static string GetRepositoryRootPath([CallerFilePath] string sourceFilePath = "")
    {
        var sourceDirectory = Path.GetDirectoryName(sourceFilePath) ?? string.Empty;
        return Path.GetFullPath(Path.Combine(sourceDirectory, ".."));
    }
}
