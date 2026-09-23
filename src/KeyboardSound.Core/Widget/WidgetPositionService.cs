using KeyboardSound.Core.Settings;

namespace KeyboardSound.Core.Widget;

/// <summary>
/// Resolves where the widget should appear: keeps a saved position if it's still on-screen,
/// falls back to a sensible default on first run, and re-clamps into view if the saved spot
/// fell outside every current monitor (resolution change, monitor unplugged, etc.).
/// </summary>
public static class WidgetPositionService
{
    /// <summary>Minimum width/height of the widget that must remain on some screen for a
    /// saved position to be considered still valid/reachable by the user.</summary>
    private const double MinVisibleMargin = 24;

    public static WidgetPosition Resolve(
        WidgetPosition? saved,
        double widgetWidth,
        double widgetHeight,
        IReadOnlyList<ScreenBounds> screens)
    {
        if (screens.Count == 0)
            return saved ?? new WidgetPosition { X = 40, Y = 40 };

        var primary = screens[0];

        if (saved is not null && IsSufficientlyVisible(saved, widgetWidth, widgetHeight, screens))
            return saved;

        if (saved is not null)
            return ClampToScreen(saved, widgetWidth, widgetHeight, primary);

        return DefaultPosition(widgetWidth, widgetHeight, primary);
    }

    private static WidgetPosition DefaultPosition(double w, double h, ScreenBounds primary) => new()
    {
        X = primary.X + primary.Width - w - 40,
        Y = primary.Y + primary.Height - h - 80
    };

    private static bool IsSufficientlyVisible(WidgetPosition pos, double w, double h, IReadOnlyList<ScreenBounds> screens)
    {
        foreach (var screen in screens)
        {
            var overlapW = Math.Min(pos.X + w, screen.X + screen.Width) - Math.Max(pos.X, screen.X);
            var overlapH = Math.Min(pos.Y + h, screen.Y + screen.Height) - Math.Max(pos.Y, screen.Y);
            if (overlapW >= MinVisibleMargin && overlapH >= MinVisibleMargin)
                return true;
        }
        return false;
    }

    private static WidgetPosition ClampToScreen(WidgetPosition pos, double w, double h, ScreenBounds screen) => new()
    {
        X = Math.Clamp(pos.X, screen.X, Math.Max(screen.X, screen.X + screen.Width - w)),
        Y = Math.Clamp(pos.Y, screen.Y, Math.Max(screen.Y, screen.Y + screen.Height - h))
    };
}
