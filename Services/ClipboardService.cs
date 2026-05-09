// Code authored by Dean Edis (DeanTheCoder).
// Anyone is free to copy, modify, use, compile, or distribute this software,
// either in source code form or as a compiled binary, for any purpose.
//
// If you modify the code, please retain this copyright header,
// and consider contributing back to the repository or letting us know
// about your modifications. Your contributions are valued!
//
// THE SOFTWARE IS PROVIDED AS IS, WITHOUT WARRANTY OF ANY KIND.

using System.Threading.Tasks;
using TextCopy;

namespace PromoG33k.Services;

public interface IClipboardService
{
    Task CopyTextAsync(string text);
}

/// <summary>
/// Cross-platform clipboard helper for generated post text.
/// </summary>
/// <remarks>
/// Keeping clipboard access behind a service lets the UI later support screenshots without changing view models.
/// </remarks>
public sealed class ClipboardService : IClipboardService
{
    public Task CopyTextAsync(string text)
    {
        global::TextCopy.ClipboardService.SetText(text ?? string.Empty);
        return Task.CompletedTask;
    }
}
