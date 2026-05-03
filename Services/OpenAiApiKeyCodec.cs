// Code authored by Dean Edis (DeanTheCoder).
// Anyone is free to copy, modify, use, compile, or distribute this software,
// either in source code form or as a compiled binary, for any purpose.
//
// If you modify the code, please retain this copyright header,
// and consider contributing back to the repository or letting us know
// about your modifications. Your contributions are valued!
//
// THE SOFTWARE IS PROVIDED AS IS, WITHOUT WARRANTY OF ANY KIND.

using DTC.Core.Extensions;

namespace PromoG33k.Services;

/// <summary>
/// Converts OpenAI API keys to and from the app's local compressed representation.
/// </summary>
/// <remarks>
/// LZ4 compression is only light obfuscation. It exists so the key is not stored as plain JSON text.
/// </remarks>
public static class OpenAiApiKeyCodec
{
    public static byte[] Encode(string apiKey) =>
        string.IsNullOrWhiteSpace(apiKey) ? [] : apiKey.Trim().Compress();

    public static string Decode(byte[] compressedApiKey) =>
        compressedApiKey == null || compressedApiKey.Length == 0
            ? string.Empty
            : compressedApiKey.DecompressToString();
}
