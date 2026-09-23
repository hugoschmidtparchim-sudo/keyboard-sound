using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Interop;
using System.Windows.Media.Imaging;

namespace KeyboardSound.App.Ui;

/// <summary>
/// Draws the app's icon in code instead of shipping a bundled .ico asset — there is no final
/// branding yet (see the placeholder-soundpack note), and a generated glyph is enough for the
/// window/taskbar/tray icon during this foundation phase. Swap this for a real .ico later
/// without touching any caller.
/// </summary>
internal static class IconFactory
{
    public static Icon CreateTrayIcon()
    {
        using var bitmap = Draw(32);
        var handle = bitmap.GetHicon();
        return Icon.FromHandle(handle);
    }

    public static BitmapSource CreateWindowIconSource()
    {
        using var bitmap = Draw(64);
        var handle = bitmap.GetHicon();
        try
        {
            return Imaging.CreateBitmapSourceFromHIcon(handle, System.Windows.Int32Rect.Empty,
                BitmapSizeOptions.FromEmptyOptions());
        }
        finally
        {
            NativeMethods.DestroyIcon(handle);
        }
    }

    private static Bitmap Draw(int size)
    {
        var bitmap = new Bitmap(size, size);
        using var g = Graphics.FromImage(bitmap);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Color.Transparent);

        using var bg = new SolidBrush(Color.FromArgb(255, 0x5B, 0x8C, 0xFF));
        g.FillRoundedRectangle(bg, 0, 0, size, size, size / 4);

        using var pen = new Pen(Color.White, Math.Max(1.5f, size / 16f));
        var margin = size * 0.28f;
        var keyH = size * 0.2f;
        var top = (size - keyH) / 2f;
        g.DrawRoundedRectangle(pen, margin, top, size - margin * 2, keyH, size * 0.06f);

        return bitmap;
    }

    private static class NativeMethods
    {
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        public static extern bool DestroyIcon(nint hIcon);
    }
}

file static class GraphicsExtensions
{
    public static void FillRoundedRectangle(this Graphics g, Brush brush, float x, float y, float w, float h, float radius)
    {
        using var path = RoundedRect(x, y, w, h, radius);
        g.FillPath(brush, path);
    }

    public static void DrawRoundedRectangle(this Graphics g, Pen pen, float x, float y, float w, float h, float radius)
    {
        using var path = RoundedRect(x, y, w, h, radius);
        g.DrawPath(pen, path);
    }

    private static GraphicsPath RoundedRect(float x, float y, float w, float h, float radius)
    {
        var d = radius * 2;
        var path = new GraphicsPath();
        path.AddArc(x, y, d, d, 180, 90);
        path.AddArc(x + w - d, y, d, d, 270, 90);
        path.AddArc(x + w - d, y + h - d, d, d, 0, 90);
        path.AddArc(x, y + h - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }
}
