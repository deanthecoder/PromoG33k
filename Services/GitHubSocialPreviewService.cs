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
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace PromoG33k.Services;

/// <summary>
/// Checks the public GitHub repository page for a custom social preview image.
/// </summary>
/// <remarks>
/// GitHub exposes uploaded repository social previews through normal Open Graph HTML tags.
/// Generated fallback images use opengraph.githubassets.com, while custom uploads use
/// repository-images.githubusercontent.com.
/// </remarks>
public sealed class GitHubSocialPreviewService
{
    private static readonly Regex MetaTagRegex = new Regex(
        @"<meta\b[^>]*>",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex AttributeRegex = new Regex(
        @"(?<name>[a-zA-Z_:][-a-zA-Z0-9_:.]*)\s*=\s*(?:""(?<value>[^""]*)""|'(?<value>[^']*)')",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private readonly HttpClient m_httpClient;

    public GitHubSocialPreviewService()
        : this(CreateDefaultHttpClient())
    {
    }

    public GitHubSocialPreviewService(HttpClient httpClient) =>
        m_httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));

    public async Task<GitHubSocialPreviewResult> CheckAsync(string repositoryUrl, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(repositoryUrl) ||
            !Uri.TryCreate(repositoryUrl, UriKind.Absolute, out var uri) ||
            !uri.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase))
            return GitHubSocialPreviewResult.Unknown;

        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        request.Headers.UserAgent.ParseAdd("PromoG33k");
        using var response = await m_httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
            return GitHubSocialPreviewResult.Unknown;

        var html = await response.Content.ReadAsStringAsync(cancellationToken);
        var imageUrl = ExtractOpenGraphImageUrl(html);
        if (string.IsNullOrWhiteSpace(imageUrl))
            return GitHubSocialPreviewResult.Unknown;

        return new GitHubSocialPreviewResult(IsCustomSocialPreviewImage(imageUrl), imageUrl);
    }

    public static string ExtractOpenGraphImageUrl(string html)
    {
        if (string.IsNullOrWhiteSpace(html))
            return string.Empty;

        foreach (Match match in MetaTagRegex.Matches(html))
        {
            var attributes = AttributeRegex
                .Matches(match.Value)
                .Cast<Match>()
                .ToDictionary(
                    attribute => attribute.Groups["name"].Value,
                    attribute => WebUtility.HtmlDecode(attribute.Groups["value"].Value),
                    StringComparer.OrdinalIgnoreCase);

            if (!attributes.TryGetValue("content", out var content) ||
                string.IsNullOrWhiteSpace(content))
                continue;

            attributes.TryGetValue("property", out var property);
            attributes.TryGetValue("name", out var name);
            if (string.Equals(property, "og:image", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(name, "twitter:image", StringComparison.OrdinalIgnoreCase))
                return content;
        }

        return string.Empty;
    }

    private static bool IsCustomSocialPreviewImage(string imageUrl) =>
        Uri.TryCreate(imageUrl, UriKind.Absolute, out var uri) &&
        uri.Host.Equals("repository-images.githubusercontent.com", StringComparison.OrdinalIgnoreCase);

    private static HttpClient CreateDefaultHttpClient() =>
        new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(5)
        };
}
