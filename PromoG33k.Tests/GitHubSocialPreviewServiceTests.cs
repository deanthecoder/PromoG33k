// Code authored by Dean Edis (DeanTheCoder).
// Anyone is free to copy, modify, use, compile, or distribute this software,
// either in source code form or as a compiled binary, for any purpose.
//
// If you modify the code, please retain this copyright header,
// and consider contributing back to the repository or letting us know
// about your modifications. Your contributions are valued!
//
// THE SOFTWARE IS PROVIDED AS IS, WITHOUT WARRANTY OF ANY KIND.

using System.Net;
using PromoG33k.Services;

namespace PromoG33k.Tests;

public sealed class GitHubSocialPreviewServiceTests
{
    [Test]
    public async Task CheckAsyncDetectsCustomRepositoryImageFromOpenGraphTag()
    {
        var service = new GitHubSocialPreviewService(
            new HttpClient(
                new StaticHtmlHandler(
                    """
                    <meta property="og:image" content="https://repository-images.githubusercontent.com/123/preview" />
                    """)));

        var result = await service.CheckAsync("https://github.com/example/UsefulTool");

        Assert.That(result.HasCustomImage, Is.True);
        Assert.That(result.ImageUrl, Is.EqualTo("https://repository-images.githubusercontent.com/123/preview"));
    }

    [Test]
    public async Task CheckAsyncTreatsGeneratedGitHubOpenGraphImageAsMissingCustomPreview()
    {
        var service = new GitHubSocialPreviewService(
            new HttpClient(
                new StaticHtmlHandler(
                    """
                    <meta property="og:image" content="https://opengraph.githubassets.com/hash/example/UsefulTool" />
                    """)));

        var result = await service.CheckAsync("https://github.com/example/UsefulTool");

        Assert.That(result.HasCustomImage, Is.False);
        Assert.That(result.ImageUrl, Is.EqualTo("https://opengraph.githubassets.com/hash/example/UsefulTool"));
    }

    private sealed class StaticHtmlHandler(string html) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(
                new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(html)
                });
    }
}
