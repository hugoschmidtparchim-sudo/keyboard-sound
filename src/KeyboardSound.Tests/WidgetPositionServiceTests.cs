using KeyboardSound.Core.Settings;
using KeyboardSound.Core.Widget;

namespace KeyboardSound.Tests;

public class WidgetPositionServiceTests
{
    private static readonly ScreenBounds PrimaryScreen = new(0, 0, 1920, 1080);

    [Fact]
    public void Resolve_NoSavedPosition_ReturnsDefaultInsidePrimaryScreen()
    {
        var result = WidgetPositionService.Resolve(null, 80, 80, new[] { PrimaryScreen });

        Assert.InRange(result.X, 0, 1920 - 80);
        Assert.InRange(result.Y, 0, 1080 - 80);
    }

    [Fact]
    public void Resolve_SavedPositionOnScreen_IsKeptUnchanged()
    {
        var saved = new WidgetPosition { X = 500, Y = 300 };

        var result = WidgetPositionService.Resolve(saved, 80, 80, new[] { PrimaryScreen });

        Assert.Equal(500, result.X);
        Assert.Equal(300, result.Y);
    }

    [Fact]
    public void Resolve_SavedPositionOffScreen_IsClampedBackIntoView()
    {
        // Resolution shrank since the position was saved (e.g. 4K -> 1080p).
        var saved = new WidgetPosition { X = 3800, Y = 2100 };

        var result = WidgetPositionService.Resolve(saved, 80, 80, new[] { PrimaryScreen });

        Assert.InRange(result.X, 0, 1920 - 80);
        Assert.InRange(result.Y, 0, 1080 - 80);
    }

    [Fact]
    public void Resolve_SavedPositionPartiallyVisible_IsKept()
    {
        // Widget mostly hangs off the right edge but a meaningful sliver remains (50px, above
        // the minimum-visible-margin threshold) — that's a deliberate user placement (e.g.
        // tucked at the screen edge), not a lost widget.
        var saved = new WidgetPosition { X = 1870, Y = 500 };

        var result = WidgetPositionService.Resolve(saved, 80, 80, new[] { PrimaryScreen });

        Assert.Equal(1870, result.X);
    }

    [Fact]
    public void Resolve_MultiMonitor_KeepsPositionOnSecondaryScreen()
    {
        var secondary = new ScreenBounds(1920, 0, 1280, 1024);
        var saved = new WidgetPosition { X = 2500, Y = 200 };

        var result = WidgetPositionService.Resolve(saved, 80, 80, new[] { PrimaryScreen, secondary });

        Assert.Equal(2500, result.X);
        Assert.Equal(200, result.Y);
    }

    [Fact]
    public void Resolve_NoScreensAvailable_DoesNotThrow()
    {
        var result = WidgetPositionService.Resolve(null, 80, 80, Array.Empty<ScreenBounds>());

        Assert.NotNull(result);
    }
}
