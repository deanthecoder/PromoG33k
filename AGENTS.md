# AGENTS.md

## Summary

PromoG33k is a cross-platform Avalonia desktop app that helps developers promote their GitHub projects without becoming spammy.

The app should scan selected GitHub repositories, extract useful README content and media, generate reviewable social post drafts, and track promotion history so suggestions stay tasteful and varied.

## Product Guidance

- Keep the user in control: generate, review, edit, copy, and manually post.
- Do not directly post to X, LinkedIn, Mastodon, or other networks in the first version.
- Prefer honest post content grounded in actual repository data.
- Emphasize newer and recently updated repositories, but let older useful projects surface occasionally.
- Avoid suggesting too many posts too close together.
- Excluded repositories must never be suggested.

## Technical Direction

- Use C#, .NET 9 or later, Avalonia, and a simple MVVM structure.
- Keep Windows and macOS as first-class targets.
- Use `DTC.Core` helpers where they fit, especially settings, file/path helpers, compression, commands, and view model helpers.
- Use `DTC.Core.Settings.UserSettingsBase` for local preferences.
- Store the OpenAI API key in settings as LZ4-compressed bytes. Treat this as light obfuscation, not encryption.
- Use `TextCopy` for cross-platform clipboard text copying.
- Use the `Installer` submodule from `DTC.Installer` for packaging.
- Use NUnit for tests.

## Project Structure

Prefer these folders:

- `Models`
- `Services`
- `Settings`
- `ViewModels`
- `Views`

## UI Direction

- Default first screen should be a repo queue/dashboard.
- Make it easy to see which repositories are promotable next and why.
- Keep generated post text editable and copyable.
- Show extracted screenshots and demo links near the generated post.
- Avoid marketing-page chrome; this is a working desktop tool.
