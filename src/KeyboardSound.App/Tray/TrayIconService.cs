using System.Windows.Forms;
using KeyboardSound.App.Ui;
using KeyboardSound.Core.AppState;

namespace KeyboardSound.App.Tray;

/// <summary>
/// Windows system-tray integration. Kept deliberately dumb: it reflects ApplicationState and
/// forwards clicks back via events/callbacks, it doesn't own any app logic itself.
/// </summary>
public sealed class TrayIconService : IDisposable
{
    private readonly NotifyIcon _notifyIcon;
    private readonly ApplicationState _appState;
    private readonly ToolStripMenuItem _currentPackItem;
    private readonly ToolStripMenuItem _soundToggleItem;

    public event Action? OpenRequested;
    public event Action? ExitRequested;

    public TrayIconService(ApplicationState appState)
    {
        _appState = appState;

        _currentPackItem = new ToolStripMenuItem("Current: -") { Enabled = false };
        _soundToggleItem = new ToolStripMenuItem("Sound: On");
        _soundToggleItem.Click += (_, _) => ToggleSound();

        var openItem = new ToolStripMenuItem("Open");
        openItem.Click += (_, _) => OpenRequested?.Invoke();

        var exitItem = new ToolStripMenuItem("Exit");
        exitItem.Click += (_, _) => ExitRequested?.Invoke();

        var menu = new ContextMenuStrip();
        menu.Items.Add(_currentPackItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(_soundToggleItem);
        menu.Items.Add(openItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(exitItem);

        _notifyIcon = new NotifyIcon
        {
            Icon = IconFactory.CreateTrayIcon(),
            Text = "Keyboard Sound",
            Visible = true,
            ContextMenuStrip = menu
        };
        _notifyIcon.DoubleClick += (_, _) => OpenRequested?.Invoke();

        _appState.ActivePackChanged += _ => RefreshLabels();
        RefreshLabels();
    }

    private void ToggleSound()
    {
        _appState.SetSoundEnabled(!_appState.Settings.Current.SoundEnabled);
        RefreshLabels();
    }

    public void RefreshLabels()
    {
        _currentPackItem.Text = $"Current: {_appState.ActivePack?.Metadata.Name ?? "-"}";
        _soundToggleItem.Text = _appState.Settings.Current.SoundEnabled ? "Sound: On" : "Sound: Off";
    }

    public void Dispose()
    {
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
    }
}
