using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using KeyboardSound.App.Configuration;
using KeyboardSound.Core.AppState;
using KeyboardSound.Core.Input;
using KeyboardSound.Core.Settings;
using KeyboardSound.Core.SoundPacks;

namespace KeyboardSound.App.Ui;

/// <summary>
/// Main window: current pack, volume, key mode, soundpack list with favorites, and basic
/// settings. Plain code-behind (no MVVM framework) — the app is small enough that the extra
/// abstraction wouldn't earn its keep. Closing the window hides it instead of exiting the app;
/// the app only actually exits via the tray "Exit" command.
/// </summary>
public partial class MainWindow : Window
{
    // A compact, common subset of keys for quick custom-key selection. The underlying
    // architecture (LogicalKey + AppSettings.EnabledKeys) supports any key; this list is just
    // what the foundation-loop UI exposes for now instead of a full virtual keyboard.
    private static readonly (LogicalKey Key, string Label)[] QuickSelectKeys =
    {
        (LogicalKey.W, "W"), (LogicalKey.A, "A"), (LogicalKey.S, "S"), (LogicalKey.D, "D"),
        (LogicalKey.Space, "Space"), (LogicalKey.Enter, "Enter"), (LogicalKey.Backspace, "Backspace"),
        (LogicalKey.Tab, "Tab"), (LogicalKey.ShiftLeft, "Shift"), (LogicalKey.CtrlLeft, "Ctrl"),
        (LogicalKey.AltLeft, "Alt"), (LogicalKey.Escape, "Esc"),
    };

    private readonly ApplicationState _appState;
    private bool _isInitializing;
    private bool _showFavoriteSoundsOnly;

    public MainWindow(ApplicationState appState)
    {
        InitializeComponent();
        _appState = appState;

        Icon = IconFactory.CreateWindowIconSource();

        BuildCustomKeysPanel();
        LoadFromState();

        _appState.ActivePackChanged += _ => Dispatcher.Invoke(LoadFromState);
    }

    /// <summary>Set by App when a real shutdown (not just closing the window) is happening,
    /// so Window_Closing lets the close proceed instead of just hiding.</summary>
    public bool IsExiting { get; set; }

    private void LoadFromState()
    {
        _isInitializing = true;
        try
        {
            var settings = _appState.Settings.Current;

            CurrentPackNameText.Text = _appState.ActivePack?.Metadata.Name ?? "No soundpack available";
            CurrentPackDescriptionText.Text = _appState.ActivePack?.Metadata.Description ?? "";

            VolumeSlider.Value = settings.Volume;
            VolumeValueText.Text = $"{settings.Volume * 100:0}%";

            SoundEnabledCheckBox.IsChecked = settings.SoundEnabled;

            AllKeysRadio.IsChecked = settings.KeyMode == KeyMode.AllKeys;
            CustomKeysRadio.IsChecked = settings.KeyMode == KeyMode.CustomKeys;
            CustomKeysPanel.Visibility = settings.KeyMode == KeyMode.CustomKeys ? Visibility.Visible : Visibility.Collapsed;
            RefreshCustomKeysPanel();

            StartWithWindowsCheckBox.IsChecked = StartupManager.IsEnabled();
            WidgetVisibleCheckBox.IsChecked = settings.WidgetVisible;
            DebugLoggingCheckBox.IsChecked = settings.DebugLogging;

            RefreshPackList();
            RefreshSoundList();
        }
        finally
        {
            _isInitializing = false;
        }
    }

    private void RefreshSoundList()
    {
        var settings = _appState.Settings.Current;
        var sounds = _appState.GetActivePackSounds(SoundCategory.Normal);

        var rows = sounds
            .Select(s => new SoundRow
            {
                Id = s.Id,
                DisplayName = s.DisplayName,
                IsFavorite = settings.FavoriteSoundIds.Contains(s.Id),
                IsSelected = settings.SelectedSoundId == s.Id
            })
            .Where(r => !_showFavoriteSoundsOnly || r.IsFavorite)
            .OrderByDescending(r => r.IsFavorite)
            .ThenBy(r => r.DisplayName)
            .ToList();

        SoundList.ItemsSource = rows;
        NoSoundsText.Visibility = (rows.Count == 0 && _showFavoriteSoundsOnly) ? Visibility.Visible : Visibility.Collapsed;

        var accentStyle = (Style)System.Windows.Application.Current.Resources["AccentButtonStyle"];
        var ghostStyle = (Style)System.Windows.Application.Current.Resources["GhostButtonStyle"];
        ShowAllSoundsButton.Style = _showFavoriteSoundsOnly ? ghostStyle : accentStyle;
        ShowFavoriteSoundsButton.Style = _showFavoriteSoundsOnly ? accentStyle : ghostStyle;
    }

    private void ShowAllSounds_Click(object sender, RoutedEventArgs e)
    {
        _showFavoriteSoundsOnly = false;
        RefreshSoundList();
    }

    private void ShowFavoriteSounds_Click(object sender, RoutedEventArgs e)
    {
        _showFavoriteSoundsOnly = true;
        RefreshSoundList();
    }

    private void FavoriteSoundButton_Click(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).Tag is not string id) return;
        _appState.ToggleFavoriteSound(id);
        RefreshSoundList();
    }

    private void SelectSoundButton_Click(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).Tag is not string id) return;
        _appState.SelectSound(id);
        RefreshSoundList();
    }

    private void SoundName_MouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (((FrameworkElement)sender).Tag is not string id) return;
        _appState.SelectSound(id);
        RefreshSoundList();
    }

    private void RefreshPackList()
    {
        var settings = _appState.Settings.Current;
        var rows = _appState.AvailablePacks
            .Select(p => new SoundPackRow
            {
                Id = p.Id,
                Name = p.Metadata.Name,
                Description = p.Metadata.Description,
                SampleCount = p.TotalSampleCount,
                IsActive = p.Id == _appState.ActivePack?.Id,
                IsFavorite = settings.FavoriteSoundPackIds.Contains(p.Id)
            })
            // Favorites first, matching the "Favorites" grouping from the spec without a
            // separate duplicated list.
            .OrderByDescending(r => r.IsFavorite)
            .ThenBy(r => r.Name)
            .ToList();

        SoundPackList.ItemsSource = rows;
    }

    private void BuildCustomKeysPanel()
    {
        foreach (var (key, label) in QuickSelectKeys)
        {
            var toggle = new ToggleButton
            {
                Content = label,
                Tag = key,
                Margin = new Thickness(0, 0, 6, 6),
                Padding = new Thickness(10, 5, 10, 5),
                Style = (Style)System.Windows.Application.Current.Resources["KeyToggleStyle"]
            };
            toggle.Checked += CustomKeyToggle_Changed;
            toggle.Unchecked += CustomKeyToggle_Changed;
            CustomKeysWrapPanel.Children.Add(toggle);
        }
    }

    private void RefreshCustomKeysPanel()
    {
        var enabled = _appState.Settings.Current.EnabledKeys;
        foreach (var child in CustomKeysWrapPanel.Children)
        {
            if (child is ToggleButton { Tag: LogicalKey key } toggle)
                toggle.IsChecked = enabled.Contains(key);
        }
    }

    private void CustomKeyToggle_Changed(object sender, RoutedEventArgs e)
    {
        if (_isInitializing) return;

        var enabled = new HashSet<LogicalKey>(_appState.Settings.Current.EnabledKeys);
        foreach (var child in CustomKeysWrapPanel.Children)
        {
            if (child is ToggleButton { Tag: LogicalKey key } toggle)
            {
                if (toggle.IsChecked == true) enabled.Add(key);
                else enabled.Remove(key);
            }
        }
        _appState.SetEnabledKeys(enabled);
    }

    private void VolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        VolumeValueText.Text = $"{e.NewValue * 100:0}%";
        if (_isInitializing) return;
        _appState.SetVolume(e.NewValue);
    }

    private void SoundEnabledCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        if (_isInitializing) return;
        _appState.SetSoundEnabled(SoundEnabledCheckBox.IsChecked == true);
    }

    private void KeyModeRadio_Changed(object sender, RoutedEventArgs e)
    {
        if (_isInitializing) return;
        var mode = CustomKeysRadio.IsChecked == true ? KeyMode.CustomKeys : KeyMode.AllKeys;
        _appState.SetKeyMode(mode);
        CustomKeysPanel.Visibility = mode == KeyMode.CustomKeys ? Visibility.Visible : Visibility.Collapsed;
    }

    private void FavoriteButton_Click(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).Tag is not string id) return;
        _appState.ToggleFavorite(id);
        RefreshPackList();
    }

    private void ActivateButton_Click(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).Tag is not string id) return;
        _appState.ActivatePack(id);
        // ActivePackChanged triggers LoadFromState via the event subscription above.
    }

    private void StartWithWindowsCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        if (_isInitializing) return;
        var enabled = StartWithWindowsCheckBox.IsChecked == true;
        StartupManager.SetEnabled(enabled);
        _appState.Settings.Current.StartWithWindows = enabled;
        _appState.Settings.Save();
    }

    private void WidgetVisibleCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        if (_isInitializing) return;
        _appState.Settings.Current.WidgetVisible = WidgetVisibleCheckBox.IsChecked == true;
        _appState.Settings.Save();
        WidgetVisibilityRequested?.Invoke(WidgetVisibleCheckBox.IsChecked == true);
    }

    private void DebugLoggingCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        if (_isInitializing) return;
        var enabled = DebugLoggingCheckBox.IsChecked == true;
        _appState.Settings.Current.DebugLogging = enabled;
        _appState.Settings.Save();
        Core.Diagnostics.Log.DebugEnabled = enabled;
    }

    public event Action<bool>? WidgetVisibilityRequested;

    private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
    {
        if (IsExiting) return;
        e.Cancel = true;
        Hide();
    }
}
