// Code authored by Dean Edis (DeanTheCoder).
// Anyone is free to copy, modify, use, compile, or distribute this software,
// either in source code form or as a compiled binary, for any purpose.
//
// If you modify the code, please retain this copyright header,
// and consider contributing back to the repository or letting us know
// about your modifications. Your contributions are valued!
//
// THE SOFTWARE IS PROVIDED AS IS, WITHOUT WARRANTY OF ANY KIND.

using System.Collections.Generic;
using DTC.Core.Settings;
using PromoG33k.Models;
using PromoG33k.Services;

namespace PromoG33k.Settings;

/// <summary>
/// Persistent user preferences for PromoG33k.
/// </summary>
/// <remarks>
/// Settings are local to the desktop app and intentionally include editable repository state and history.
/// </remarks>
public sealed class AppSettings : UserSettingsBase
{
    public static AppSettings Instance { get; } = new AppSettings();

    protected override string SettingsFileName => "promog33k-settings.json";

    public string OpenAiModel
    {
        get => Get<string>();
        set => Set(value);
    }

    public string LocalRepositoryRootPath
    {
        get => Get<string>();
        set => Set(value);
    }

    public byte[] CompressedOpenAiApiKey
    {
        get => Get<byte[]>();
        set => Set(value);
    }

    public List<RepositoryProfile> Repositories
    {
        get => Get<List<RepositoryProfile>>();
        set => Set(value);
    }

    public List<PromotionHistoryEntry> PromotionHistory
    {
        get => Get<List<PromotionHistoryEntry>>();
        set => Set(value);
    }

    public string GetOpenAiApiKey() =>
        OpenAiApiKeyCodec.Decode(CompressedOpenAiApiKey);

    public void SetOpenAiApiKey(string apiKey) =>
        CompressedOpenAiApiKey = OpenAiApiKeyCodec.Encode(apiKey);

    protected override void ApplyDefaults()
    {
        OpenAiModel = "gpt-5.4-mini";
        LocalRepositoryRootPath = string.Empty;
        CompressedOpenAiApiKey = [];
        Repositories = [];
        PromotionHistory = [];
    }
}
