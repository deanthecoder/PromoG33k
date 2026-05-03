// Code authored by Dean Edis (DeanTheCoder).
// Anyone is free to copy, modify, use, compile, or distribute this software,
// either in source code form or as a compiled binary, for any purpose.
//
// If you modify the code, please retain this copyright header,
// and consider contributing back to the repository or letting us know
// about your modifications. Your contributions are valued!
//
// THE SOFTWARE IS PROVIDED AS IS, WITHOUT WARRANTY OF ANY KIND.

using DTC.Core.UI;

namespace PromoG33k.ViewModels;

/// <summary>
/// Application-level view model for native menu commands.
/// </summary>
/// <remarks>
/// Keeps shell-level concerns, such as the About dialog, separate from the main promotion dashboard.
/// </remarks>
public sealed class AppViewModel : AppViewModelBase
{
    public AppViewModel()
        : base(AboutInfoProvider.Info)
    {
    }
}
