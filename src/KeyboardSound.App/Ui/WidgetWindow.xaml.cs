using System.Windows;
using System.Windows.Input;
using Brush = System.Windows.Media.Brush;
using KeyboardSound.Core.AppState;
using KeyboardSound.Core.Settings;
using KeyboardSound.Core.Widget;
using Forms = System.Windows.Forms;

namespace KeyboardSound.App.Ui;

/// <summary>
/// The small always-on-top desktop widget: draggable, shows current sound-on/off status via
/// its ring color, and opens the main window on a plain click. A click vs. a drag is
/// distinguished by comparing the window position before and after <see cref="DragMove"/>.
/// </summary>
public partial class WidgetWindow : Window
{
    private readonly ApplicationState _appState;

    public event Action? OpenRequested;

    public WidgetWindow(ApplicationState appState)
    {
        InitializeComponent();
        _appState = appState;

        Icon = IconFactory.CreateWindowIconSource();

        PositionFromSettings();
        RefreshStatus();

        _appState.Settings.Saved += _ => RefreshStatus();
        Microsoft.Win32.SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
        Closed += (_, _) => Microsoft.Win32.SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
    }

    private void PositionFromSettings()
    {
        var screens = GetScreenBounds();
        var resolved = WidgetPositionService.Resolve(_appState.Settings.Current.WidgetPosition, Width, Height, screens);
        Left = resolved.X;
        Top = resolved.Y;
    }

    private static IReadOnlyList<ScreenBounds> GetScreenBounds()
    {
        // Known simplification: this treats Windows Forms' physical-pixel screen bounds as
        // WPF device-independent units (i.e. assumes ~96 DPI / 100% scaling). On a scaled
        // display the widget can therefore land slightly off from where a mathematically
        // perfect per-monitor-DPI conversion would put it; it still always resolves to a
        // position within IsSufficientlyVisible tolerance, so it can never end up unreachable.
        return Forms.Screen.AllScreens
            .Select(s => new ScreenBounds(s.Bounds.X, s.Bounds.Y, s.Bounds.Width, s.Bounds.Height))
            .ToList();
    }

    private void OnDisplaySettingsChanged(object? sender, EventArgs e)
    {
        Dispatcher.Invoke(PositionFromSettings);
    }

    public void RefreshStatus()
    {
        var enabled = _appState.Settings.Current.SoundEnabled;
        RootBorder.BorderBrush = (Brush)FindResource(enabled ? "AccentBrush" : "BorderBrush");
        StatusDot.Fill = (Brush)FindResource(enabled ? "AccentBrush" : "TextSecondaryBrush");
    }

    private void RootBorder_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        var startLeft = Left;
        var startTop = Top;

        try
        {
            DragMove();
        }
        catch (InvalidOperationException)
        {
            // DragMove throws if the button was already released by the time it's called
            // (e.g. a very fast click) — that's simply a click, not a drag.
        }

        var moved = Math.Abs(Left - startLeft) > 2 || Math.Abs(Top - startTop) > 2;
        if (moved)
        {
            SavePosition();
        }
        else
        {
            OpenRequested?.Invoke();
        }
    }

    private void SavePosition()
    {
        _appState.Settings.Current.WidgetPosition = new WidgetPosition { X = Left, Y = Top };
        _appState.Settings.Save();
    }
}
