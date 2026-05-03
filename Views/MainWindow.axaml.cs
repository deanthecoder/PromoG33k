// Code authored by Dean Edis (DeanTheCoder).
// Anyone is free to copy, modify, use, compile, or distribute this software,
// either in source code form or as a compiled binary, for any purpose.
//
// If you modify the code, please retain this copyright header,
// and consider contributing back to the repository or letting us know
// about your modifications. Your contributions are valued!
//
// THE SOFTWARE IS PROVIDED AS IS, WITHOUT WARRANTY OF ANY KIND.

using Avalonia.Controls;
using Avalonia.Input;
using PromoG33k.ViewModels;

namespace PromoG33k.Views;

/// <summary>
/// Main desktop window for the promotion queue.
/// </summary>
/// <remarks>
/// The code-behind remains intentionally thin; behavior lives in the view model and services.
/// </remarks>
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        Loaded += OnLoadedAsync;
    }

    private async void OnLoadedAsync(object sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Loaded -= OnLoadedAsync;
        if (DataContext is MainWindowViewModel { IsStartupInitializationEnabled: true } viewModel)
            await viewModel.InitializeAsync();
    }

    private async void OnScreenshotCopyButtonClick(object sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is not Button { DataContext: ScreenshotPreviewViewModel preview } ||
            preview.ImageBytes == null ||
            preview.ImageBytes.Length == 0)
            return;

        if (Clipboard == null)
        {
            preview.StatusText = "Clipboard is unavailable.";
            return;
        }

        var dataObject = new DataObject();
        dataObject.Set("image/png", preview.ImageBytes);
        dataObject.Set("public.png", preview.ImageBytes);
        dataObject.Set("PNG", preview.ImageBytes);
        await Clipboard.SetDataObjectAsync(dataObject);
        preview.StatusText = "Image data copied to clipboard.";
    }
}
