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

public class ReadmeMediaExtractorTests
{
    [Test]
    public void ExtractScreenshotUrlsFindsMarkdownAndHtmlImages()
    {
        const string readme = """
            # Project

            ![Main screenshot](docs/screenshot-main.png)
            <img src="assets/demo.jpg" width="900" />
            ![Logo](assets/logo.png)
            """;
        var extractor = new ReadmeMediaExtractor();

        var urls = extractor.ExtractScreenshotUrls(readme, "example", "UsefulTool", "main");

        Assert.That(
            urls,
            Is.EqualTo(
                new[]
                {
                    "https://raw.githubusercontent.com/example/UsefulTool/main/docs/screenshot-main.png",
                    "https://raw.githubusercontent.com/example/UsefulTool/main/assets/demo.jpg"
                }));
    }

    [Test]
    public void ExtractScreenshotUrlsKeepsAbsoluteUrls()
    {
        const string readme = "![Preview](https://example.com/preview.png)";
        var extractor = new ReadmeMediaExtractor();

        var urls = extractor.ExtractScreenshotUrls(readme, "example", "UsefulTool", "main");

        Assert.That(urls, Is.EqualTo(new[] { "https://example.com/preview.png" }));
    }

    [Test]
    public void ExtractScreenshotPathsResolvesLocalReadmeImages()
    {
        var root = Directory.CreateTempSubdirectory();
        try
        {
            Directory.CreateDirectory(Path.Combine(root.FullName, "img"));
            var screenshotPath = Path.Combine(root.FullName, "img", "ReviewG33k.png");
            File.WriteAllText(screenshotPath, "not a real image");
            const string readme = """
                ![Main window screenshot](img/ReviewG33k.png?raw=true "Main window screenshot")
                ![Logo](img/logo.png)
                """;
            var extractor = new ReadmeMediaExtractor();

            var paths = extractor.ExtractScreenshotPaths(readme, root);

            Assert.That(paths, Is.EqualTo(new[] { screenshotPath }));
        }
        finally
        {
            root.Delete(recursive: true);
        }
    }
}
