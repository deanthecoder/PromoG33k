// Code authored by Dean Edis (DeanTheCoder).
// Anyone is free to copy, modify, use, compile, or distribute this software,
// either in source code form or as a compiled binary, for any purpose.
//
// If you modify the code, please retain this copyright header,
// and consider contributing back to the repository or letting us know
// about your modifications. Your contributions are valued!
//
// THE SOFTWARE IS PROVIDED AS IS, WITHOUT WARRANTY OF ANY KIND.

namespace PromoG33k.Models;

/// <summary>
/// Available sort orders for the public repository queue.
/// </summary>
/// <remarks>
/// Sorting lets users switch between browsing alphabetically and following PromoG33k's recommendation score.
/// </remarks>
public enum RepositorySortMode
{
    PostPriority,
    Updated,
    Name
}
