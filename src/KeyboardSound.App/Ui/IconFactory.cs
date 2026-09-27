using System.Drawing;
using System.Windows.Media.Imaging;

namespace KeyboardSound.App.Ui;

/// <summary>
/// Loads the app's real multi-resolution icon (Resources/app.ico, also embedded as the EXE's
/// Win32 icon via ApplicationIcon in the csproj) for the window, tray, and taskbar, so every
/// place Windows shows an icon for this app shows the same artwork.
/// </summary>
internal static class IconFactory
{
    private static readonly Uri ResourceRelativeUri = new("Resources/app.ico", UriKind.Relative);
    private static readonly Uri PackUri = new("pack://application:,,,/Resources/app.ico");

    public static Icon CreateTrayIcon()
    {
        var info = System.Windows.Application.GetResourceStream(ResourceRelativeUri)
            ?? throw new InvalidOperationException("Resources/app.ico was not found as an embedded resource.");
        using var stream = info.Stream;
        return new Icon(stream, new System.Drawing.Size(32, 32));
    }

    public static BitmapSource CreateWindowIconSource()
    {
        return new BitmapImage(PackUri);
    }
}
