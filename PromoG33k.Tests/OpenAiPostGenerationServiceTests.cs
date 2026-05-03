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
using PromoG33k.Models;
using PromoG33k.Services;

namespace PromoG33k.Tests;

public class OpenAiPostGenerationServiceTests
{
    [Test]
    public void CreatePromptIncludesRepositoryFacts()
    {
        var repository = CreateRepository();

        var prompt = OpenAiPostGenerationService.CreatePrompt(repository, PostStyle.Showcase);

        Assert.That(prompt, Does.Contain("UsefulTool"));
        Assert.That(prompt, Does.Contain("A useful tool."));
        Assert.That(prompt, Does.Contain("https://github.com/example/UsefulTool"));
        Assert.That(prompt, Does.Contain("dotnet, avalonia"));
        Assert.That(prompt, Does.Contain("README headings: What it can do"));
        Assert.That(prompt, Does.Contain("README highlights: Review local changes"));
        Assert.That(prompt, Does.Contain("Avalonia project: True"));
        Assert.That(prompt, Does.Contain("Cross-platform: True"));
        Assert.That(prompt, Does.Contain("#avaloniaui"));
        Assert.That(prompt, Does.Contain("Do not invent"));
        Assert.That(prompt, Does.Contain("Use first person"));
        Assert.That(prompt, Does.Contain("Do not start with 'I built'"));
        Assert.That(prompt, Does.Contain("Vary the opening line"));
        Assert.That(prompt, Does.Contain("short lines separated by blank lines"));
    }

    [Test]
    public void CreatePromptIncludesDistinctStyleGuidance()
    {
        var repository = CreateRepository();

        var showcasePrompt = OpenAiPostGenerationService.CreatePrompt(repository, PostStyle.Showcase);
        var technicalPrompt = OpenAiPostGenerationService.CreatePrompt(repository, PostStyle.TechnicalNugget);
        var problemPrompt = OpenAiPostGenerationService.CreatePrompt(repository, PostStyle.ProblemSolution);

        Assert.That(showcasePrompt, Does.Contain("why it exists"));
        Assert.That(technicalPrompt, Does.Contain("concrete technical detail"));
        Assert.That(problemPrompt, Does.Contain("developer pain"));
    }

    [Test]
    public void CreatePromptIncludesExtraInstruction()
    {
        var repository = CreateRepository();

        var prompt = OpenAiPostGenerationService.CreatePrompt(
            repository,
            PostStyle.Showcase,
            "Make it shorter.",
            "Existing social preview text.");

        Assert.That(prompt, Does.Contain("Extra instruction from the user: Make it shorter."));
        Assert.That(prompt, Does.Contain("Current social preview text to revise or respond to: Existing social preview text."));
        Assert.That(prompt, Does.Contain("do not invent unsupported claims"));
    }

    [Test]
    public void ExtractOutputTextReadsNestedResponsesOutputText()
    {
        const string responseJson = """
            {
              "output": [
                {
                  "type": "message",
                  "content": [
                    { "type": "output_text", "text": "Generated post" }
                  ]
                }
              ]
            }
            """;

        var output = OpenAiPostGenerationService.ExtractOutputText(responseJson);

        Assert.That(output, Is.EqualTo("Generated post"));
    }

    [Test]
    public async Task GenerateDraftAsyncPostsResponsesRequestWithoutStoringResponse()
    {
        var handler = new CapturingHandler(
            """
            {
              "output": [
                {
                  "type": "message",
                  "content": [
                    { "type": "output_text", "text": "Generated post" }
                  ]
                }
              ]
            }
            """);
        var service = new OpenAiPostGenerationService(new HttpClient(handler));

        var draft = await service.GenerateDraftAsync("sk-test", "gpt-test", CreateRepository(), PostStyle.Showcase);

        Assert.That(draft, Is.EqualTo("Generated post"));
        Assert.That(handler.RequestUri?.AbsoluteUri, Is.EqualTo("https://api.openai.com/v1/responses"));
        Assert.That(handler.AuthorizationHeader, Is.EqualTo("Bearer sk-test"));
        Assert.That(handler.RequestJson, Does.Contain("\"model\":\"gpt-test\""));
        Assert.That(handler.RequestJson, Does.Contain("\"store\":false"));
        Assert.That(handler.RequestJson, Does.Contain("UsefulTool"));
    }

    private static RepositoryProfile CreateRepository() =>
        new RepositoryProfile
        {
            Name = "UsefulTool",
            GitHubUrl = "https://github.com/example/UsefulTool",
            Description = "A useful tool.",
            Language = "C#",
            Topics = ["dotnet", "avalonia"],
            ReadmeHeadings = ["What it can do"],
            ReadmeHighlights = ["Review local changes", "Copy results to the clipboard"],
            ScreenshotUrls = ["https://example.com/screenshot.png"],
            IsAvaloniaProject = true,
            IsCrossPlatform = true
        };

    private sealed class CapturingHandler : HttpMessageHandler
    {
        private readonly string m_responseJson;

        public CapturingHandler(string responseJson) =>
            m_responseJson = responseJson;

        public Uri RequestUri { get; private set; }
        public string AuthorizationHeader { get; private set; }
        public string RequestJson { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestUri = request.RequestUri;
            AuthorizationHeader = request.Headers.Authorization?.ToString();
            RequestJson = await request.Content.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(m_responseJson)
            };
        }
    }
}
