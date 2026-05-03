// Code authored by Dean Edis (DeanTheCoder).
// Anyone is free to copy, modify, use, compile, or distribute this software,
// either in source code form or as a compiled binary, for any purpose.
//
// If you modify the code, please retain this copyright header,
// and consider contributing back to the repository or letting us know
// about your modifications. Your contributions are valued!
//
// THE SOFTWARE IS PROVIDED AS IS, WITHOUT WARRANTY OF ANY KIND.

using Avalonia.Media.Imaging;

namespace PromoG33k.ViewModels;

/// <summary>
/// Preview state for a screenshot candidate.
/// </summary>
/// <remarks>
/// Keeps downloaded image data near the UI so screenshots can be previewed and copied without re-fetching.
/// </remarks>
public sealed class ScreenshotPreviewViewModel : ViewModelBase
{
    private Bitmap m_image;
    private string m_statusText;

    public ScreenshotPreviewViewModel(string url)
    {
        Url = url;
        m_statusText = "Loading screenshot...";
    }

    public string Url { get; }
    public byte[] ImageBytes { get; set; }

    public Bitmap Image
    {
        get => m_image;
        set => SetProperty(ref m_image, value);
    }

    public string StatusText
    {
        get => m_statusText;
        set => SetProperty(ref m_statusText, value);
    }

    public bool HasImage => Image != null;

    public void MarkImageLoaded(Bitmap image, byte[] imageBytes)
    {
        Image = image;
        ImageBytes = imageBytes;
        StatusText = string.Empty;
        OnPropertyChanged(nameof(HasImage));
    }
}
