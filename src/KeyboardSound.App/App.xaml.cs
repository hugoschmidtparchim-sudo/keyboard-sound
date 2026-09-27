using System.IO;
using System.Threading;
using System.Windows;
using KeyboardSound.App.Tray;
using KeyboardSound.App.Ui;
using KeyboardSound.Core.AppState;
using KeyboardSound.Core.Audio;
using KeyboardSound.Core.Configuration;
using KeyboardSound.Core.Diagnostics;
using KeyboardSound.Core.Input;
using KeyboardSound.Core.Settings;

namespace KeyboardSound.App;

/// <summary>
/// Composition root and lifecycle owner. Startup order follows the spec's app-lifecycle rules:
/// load config -> prepare audio -> start global input -> show widget -> UI ready. Shutdown is
/// the exact reverse, triggered only from the tray "Exit" command (closing the main window or
/// the widget never quits the app — see their Closing handlers).
/// </summary>
public partial class App : System.Windows.Application
{
    private const string SingleInstanceMutexName = "Local\\KeyboardSound-SingleInstance";

    private Mutex? _singleInstanceMutex;
    private ApplicationState? _appState;
    private WidgetWindow? _widgetWindow;
    private MainWindow? _mainWindow;
    private TrayIconService? _trayIcon;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // A second launch (e.g. double-clicking the desktop shortcut while the tray icon is
        // already running) would otherwise install its own global hook and play every sound
        // twice, including after the user thinks they've "closed" the app because the first,
        // still-running instance keeps going in the background. Only the first instance
        // proceeds; a later one exits immediately without touching audio/input/tray.
        _singleInstanceMutex = new Mutex(true, SingleInstanceMutexName, out var createdNew);
        if (!createdNew)
        {
            Shutdown();
            return;
        }

        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;

        var settings = new SettingsService(AppPaths.SettingsFilePath);
        Log.Initialize(AppPaths.LogFilePath, settings.Current.DebugLogging);
        Log.Info("Starting Keyboard Sound.");

        Directory.CreateDirectory(AppPaths.UserSoundPacksDirectory);

        IAudioEngine audioEngine = new NAudioEngine();
        IGlobalKeyboardHook hook = new LowLevelKeyboardHook();

        _appState = new ApplicationState(
            settings,
            audioEngine,
            hook,
            new[] { AppPaths.BuiltInSoundPacksDirectory, AppPaths.UserSoundPacksDirectory });

        // The LL keyboard hook must be installed on a thread pumping Win32 messages;
        // the WPF UI thread's Dispatcher already does this once the app is running.
        _appState.Start();

        _mainWindow = new MainWindow(_appState);
        _mainWindow.WidgetVisibilityRequested += visible => SetWidgetVisible(visible);

        _widgetWindow = new WidgetWindow(_appState);
        _widgetWindow.OpenRequested += ShowMainWindow;
        if (settings.Current.WidgetVisible)
            _widgetWindow.Show();

        _trayIcon = new TrayIconService(_appState);
        _trayIcon.OpenRequested += ShowMainWindow;
        _trayIcon.ExitRequested += Shutdown;
    }

    private void SetWidgetVisible(bool visible)
    {
        if (_widgetWindow is null) return;
        if (visible) _widgetWindow.Show();
        else _widgetWindow.Hide();
    }

    private void ShowMainWindow()
    {
        if (_mainWindow is null) return;
        _mainWindow.Show();
        _mainWindow.WindowState = WindowState.Normal;
        _mainWindow.Activate();
    }

    /// <summary>Actual application exit — reached via the tray "Exit" item, or immediately at
    /// startup for a second instance that lost the single-instance mutex (see OnStartup).</summary>
    private new void Shutdown()
    {
        if (_mainWindow is not null) _mainWindow.IsExiting = true;

        _trayIcon?.Dispose();
        _widgetWindow?.Close();
        _mainWindow?.Close();
        _appState?.Shutdown();
        _singleInstanceMutex?.Dispose();

        base.Shutdown();
    }

    private void OnDispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
    {
        Log.Error("Unhandled UI exception.", e.Exception);
        // A single bad UI interaction must not take down a background utility app.
        e.Handled = true;
    }

    private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        Log.Error("Unhandled exception.", e.ExceptionObject as Exception);
        Log.Flush();
    }
}
