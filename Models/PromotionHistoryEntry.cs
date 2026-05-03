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

namespace PromoG33k.Models;

/// <summary>
/// A generated or used promotional post for a repository.
/// </summary>
/// <remarks>
/// Promotion history keeps generated posts from becoming repetitive and lets scheduling penalize recent use.
/// </remarks>
public sealed class PromotionHistoryEntry
{
    public string RepositoryName { get; set; } = string.Empty;
    public PostStyle Style { get; set; } = PostStyle.Showcase;
    public DateTime GeneratedAtUtc { get; set; }
    public DateTime? UsedAtUtc { get; set; }
    public string Text { get; set; } = string.Empty;
}
