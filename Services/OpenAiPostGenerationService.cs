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
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using PromoG33k.Models;

namespace PromoG33k.Services;

/// <summary>
/// Generates promotional post drafts using the OpenAI Responses API.
/// </summary>
/// <remarks>
/// Calls are explicit and user-triggered so PromoG33k never spends API credits in the background.
/// </remarks>
public sealed class OpenAiPostGenerationService
{
    private static readonly Uri ResponsesUri = new Uri("https://api.openai.com/v1/responses");
    private static readonly JsonSerializerOptions SerializerOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);
    private readonly HttpClient m_httpClient;

    public OpenAiPostGenerationService()
        : this(new HttpClient())
    {
    }

    public OpenAiPostGenerationService(HttpClient httpClient) =>
        m_httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));

    public async Task<string> GenerateDraftAsync(
        string apiKey,
        string model,
        RepositoryProfile repository,
        PostStyle postStyle,
        CancellationToken cancellationToken = default)
    {
        return await GenerateDraftAsync(apiKey, model, repository, postStyle, null, cancellationToken);
    }

    public async Task<string> GenerateDraftAsync(
        string apiKey,
        string model,
        RepositoryProfile repository,
        PostStyle postStyle,
        string extraInstruction,
        CancellationToken cancellationToken = default)
    {
        return await GenerateDraftAsync(apiKey, model, repository, postStyle, extraInstruction, null, cancellationToken);
    }

    public async Task<string> GenerateDraftAsync(
        string apiKey,
        string model,
        RepositoryProfile repository,
        PostStyle postStyle,
        string extraInstruction,
        string currentDraftText,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new ArgumentException("OpenAI API key is required.", nameof(apiKey));
        if (string.IsNullOrWhiteSpace(model))
            throw new ArgumentException("OpenAI model is required.", nameof(model));
        if (repository == null)
            throw new ArgumentNullException(nameof(repository));

        var input = CreatePrompt(repository, postStyle, extraInstruction, currentDraftText);
        return await CreateTextResponseAsync(apiKey, model, input, 280, cancellationToken);
    }

    public async Task<bool> TestConnectionAsync(string apiKey, string model, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new ArgumentException("OpenAI API key is required.", nameof(apiKey));
        if (string.IsNullOrWhiteSpace(model))
            throw new ArgumentException("OpenAI model is required.", nameof(model));

        var response = await CreateTextResponseAsync(apiKey, model, "Reply with exactly: OK", 20, cancellationToken);
        return response.Contains("OK", StringComparison.OrdinalIgnoreCase);
    }

    public static string CreatePrompt(RepositoryProfile repository, PostStyle postStyle) =>
        CreatePrompt(repository, postStyle, null);

    public static string CreatePrompt(RepositoryProfile repository, PostStyle postStyle, string extraInstruction)
    {
        return CreatePrompt(repository, postStyle, extraInstruction, null);
    }

    public static string CreatePrompt(RepositoryProfile repository, PostStyle postStyle, string extraInstruction, string currentDraftText)
    {
        var lines = new List<string>
        {
            "Write one copy/paste-ready social post for a developer sharing one of their GitHub projects.",
            "Use first person, as if the repository owner wrote it, but avoid formulaic openings.",
            "Do not start with 'I built' unless it is genuinely the strongest opening for this specific post.",
            "Vary the opening line so repeated generations feel human, curious, and conversational.",
            "Good openings can mention a problem, a small lesson, a tradeoff, a useful workflow, or why the project exists.",
            "Format it as 3 or 4 short lines separated by blank lines: human opener, useful detail, GitHub URL, hashtags.",
            "Do not return a single paragraph.",
            "Keep it honest, specific, non-hypey, concise, and lightly personal.",
            "Include the GitHub URL.",
            "Use 2 to 5 relevant hashtags.",
            "Do not invent features, metrics, screenshots, or claims.",
            $"Post style: {postStyle}.",
            $"Style guidance: {GetStyleGuidance(postStyle)}",
            $"Repository: {repository.Name}.",
            $"Description: {Fallback(repository.Description, "No repository description provided.")}",
            $"Language: {Fallback(repository.Language, "Unknown")}",
            $"Topics: {Fallback(repository.TopicText, "None")}",
            $"Avalonia project: {repository.IsAvaloniaProject}",
            $"Cross-platform: {repository.IsCrossPlatform}",
            $"GitHub URL: {repository.GitHubUrl}",
            $"Screenshots found: {repository.ScreenshotUrls.Count}",
            $"Demo links found: {repository.DemoUrls.Count}"
        };

        if (repository.ReadmeHeadings.Count > 0)
            lines.Add($"README headings: {string.Join("; ", repository.ReadmeHeadings.Take(8))}");
        if (repository.ReadmeHighlights.Count > 0)
            lines.Add($"README highlights: {string.Join("; ", repository.ReadmeHighlights.Take(10))}");
        if (repository.IsCrossPlatform)
            lines.Add("If it feels natural, mention that the project is cross-platform.");
        if (repository.IsAvaloniaProject)
            lines.Add("If hashtags are useful, consider #avaloniaui alongside relevant C#/.NET tags.");
        if (!string.IsNullOrWhiteSpace(extraInstruction))
        {
            lines.Add($"Extra instruction from the user: {extraInstruction.Trim()}");
            if (!string.IsNullOrWhiteSpace(currentDraftText))
                lines.Add($"Current social preview text to revise or respond to: {currentDraftText.Trim()}");
            lines.Add("Follow the extra instruction when it improves the post, but do not invent unsupported claims.");
        }
        if (repository.ScreenshotUrls.Count > 0)
            lines.Add($"Screenshot URLs: {string.Join(", ", repository.ScreenshotUrls.Take(3))}");
        if (repository.DemoUrls.Count > 0)
            lines.Add($"Demo URLs: {string.Join(", ", repository.DemoUrls.Take(3))}");

        return string.Join("\n", lines);
    }

    private async Task<string> CreateTextResponseAsync(
        string apiKey,
        string model,
        string input,
        int maxOutputTokens,
        CancellationToken cancellationToken)
    {
        var payload = new
        {
            model,
            input,
            instructions = "You are helping a developer share useful project updates without sounding spammy.",
            max_output_tokens = maxOutputTokens,
            store = false
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, ResponsesUri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey.Trim());
        request.Content = new StringContent(JsonSerializer.Serialize(payload, SerializerOptions), Encoding.UTF8, "application/json");

        using var response = await m_httpClient.SendAsync(request, cancellationToken);
        var responseText = await response.Content.ReadAsStringAsync(cancellationToken);
        response.EnsureSuccessStatusCode();
        return ExtractOutputText(responseText);
    }

    public static string ExtractOutputText(string responseJson)
    {
        using var document = JsonDocument.Parse(responseJson);
        if (TryFindStringProperty(document.RootElement, "output_text", out var outputText))
            return outputText.Trim();

        var textParts = new List<string>();
        CollectOutputText(document.RootElement, textParts);
        return string.Join("\n", textParts.Where(part => !string.IsNullOrWhiteSpace(part))).Trim();
    }

    private static void CollectOutputText(JsonElement element, List<string> textParts)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                if (element.TryGetProperty("type", out var typeProperty) &&
                    typeProperty.GetString()?.Equals("output_text", StringComparison.OrdinalIgnoreCase) == true &&
                    element.TryGetProperty("text", out var textProperty) &&
                    textProperty.ValueKind == JsonValueKind.String)
                    textParts.Add(textProperty.GetString());

                foreach (var property in element.EnumerateObject())
                    CollectOutputText(property.Value, textParts);
                break;
            case JsonValueKind.Array:
                foreach (var child in element.EnumerateArray())
                    CollectOutputText(child, textParts);
                break;
        }
    }

    private static bool TryFindStringProperty(JsonElement element, string propertyName, out string value)
    {
        value = null;
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                if (element.TryGetProperty(propertyName, out var property) &&
                    property.ValueKind == JsonValueKind.String)
                {
                    value = property.GetString();
                    return true;
                }

                foreach (var childProperty in element.EnumerateObject())
                {
                    if (TryFindStringProperty(childProperty.Value, propertyName, out value))
                        return true;
                }
                break;
            case JsonValueKind.Array:
                foreach (var child in element.EnumerateArray())
                {
                    if (TryFindStringProperty(child, propertyName, out value))
                        return true;
                }
                break;
        }

        return false;
    }

    private static string Fallback(string value, string fallback) =>
        string.IsNullOrWhiteSpace(value) ? fallback : value;

    private static string GetStyleGuidance(PostStyle postStyle) =>
        postStyle switch
        {
            PostStyle.ProgressUpdate => "Make it feel like a fresh dev update: what improved, what changed, or what is now nicer to use.",
            PostStyle.TechnicalNugget => "Lead with a concrete technical detail, implementation choice, or small lesson from the project.",
            PostStyle.ProblemSolution => "Open with the developer pain or workflow problem, then show how the project helps.",
            PostStyle.DemoVideo => "Write as if sharing a visual/demo moment; mention screenshots or demos only if provided in the repo facts.",
            _ => "Introduce the project through why it exists or who it helps, without sounding like a product launch."
        };
}
