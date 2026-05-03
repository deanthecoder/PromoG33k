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

public class OpenAiApiKeyCodecTests
{
    [Test]
    public void EncodeDoesNotStorePlainText()
    {
        const string apiKey = "sk-test-key";

        var encoded = OpenAiApiKeyCodec.Encode(apiKey);

        Assert.That(encoded, Is.Not.Empty);
        Assert.That(Convert.ToBase64String(encoded), Does.Not.Contain(apiKey));
    }

    [Test]
    public void DecodeRoundTripsEncodedKey()
    {
        const string apiKey = "sk-test-key";

        var decoded = OpenAiApiKeyCodec.Decode(OpenAiApiKeyCodec.Encode(apiKey));

        Assert.That(decoded, Is.EqualTo(apiKey));
    }

    [Test]
    public void EmptyKeyEncodesAsEmptyBytes()
    {
        var encoded = OpenAiApiKeyCodec.Encode(" ");

        Assert.That(encoded, Is.Empty);
        Assert.That(OpenAiApiKeyCodec.Decode(encoded), Is.EqualTo(string.Empty));
    }
}
