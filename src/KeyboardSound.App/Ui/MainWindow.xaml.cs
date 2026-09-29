using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using KeyboardSound.App.Configuration;
using KeyboardSound.Core.AppState;
using KeyboardSound.Core.Input;
using KeyboardSound.Core.Persistence;
using KeyboardSound.Core.Settings;
using KeyboardSound.Core.SoundPacks;

namespace KeyboardSound.App.Ui;

/// <summary>
/// Main window: current pack, volume, key mode, soundpack list with favorites, and basic
/// settings. Plain code-behind (no MVVM framework) — the app is small enough that the extra
/// abstraction wouldn't earn its keep. Closing the window minimizes it instead of exiting the
/// app; the app only actually exits via the tray "Exit" command.
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

        var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
        VersionText.Text = $"KeyboardSound v{(version is null ? "1.0.0" : version.ToString(3))}";

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
            RefreshCustomKeySoundsSummary();

            StartWithWindowsCheckBox.IsChecked = StartupManager.IsEnabled();
            WidgetVisibleCheckBox.IsChecked = settings.WidgetVisible;
            DebugLoggingCheckBox.IsChecked = settings.DebugLogging;

            RefreshPackList();
            RefreshSoundList();
            RefreshStatistics();
        }
        finally
        {
            _isInitializing = false;
        }
    }

    private void RefreshStatistics()
    {
        var stats = _appState.Stats;
        StatKeyPressesText.Text = stats.Current.TotalKeyPresses.ToString("N0");
        StatMostUsedKeyText.Text = stats.MostUsedKey ?? "-";

        var usage = stats.TotalUsageTime;
        StatUsageTimeText.Text = usage.TotalHours >= 1
            ? $"{(int)usage.TotalHours}h {usage.Minutes}m"
            : $"{(int)usage.TotalMinutes}m";

        var favoritePackId = _appState.Settings.Current.FavoriteSoundPackIds.FirstOrDefault();
        var favoritePack = favoritePackId is null
            ? null
            : _appState.AvailablePacks.FirstOrDefault(p => p.Id == favoritePackId);
        StatFavoritePackText.Text = favoritePack?.Metadata.Name ?? "None yet";
    }

    private void ResetStatistics_Click(object sender, RoutedEventArgs e)
    {
        var result = System.Windows.MessageBox.Show(
            this,
            "Reset key press count, most used key, and usage time? This never affects your sounds, packs, favorites, or other settings.",
            "Reset Statistics",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);
        if (result != MessageBoxResult.Yes) return;

        _appState.Stats.Reset();
        RefreshStatistics();
    }

    private void RunSetupAgain_Click(object sender, RoutedEventArgs e)
    {
        var onboarding = new OnboardingWindow(_appState) { Owner = this };
        onboarding.WidgetVisibilityRequested += visible => WidgetVisibilityRequested?.Invoke(visible);
        onboarding.ShowDialog();
        LoadFromState();
    }

    private void RefreshCustomKeySoundsSummary()
    {
        var count = _appState.Settings.Current.CustomKeySounds.Count;
        CustomKeySoundsSummaryText.Text = count == 0
            ? "Give any key its own sound - it'll play that instead of your global sound."
            : $"{count} key{(count == 1 ? "" : "s")} currently {(count == 1 ? "has" : "have")} its own sound.";
    }

    private void OpenKeyboardEditor_Click(object sender, RoutedEventArgs e)
    {
        var editor = new KeyboardEditorWindow(_appState) { Owner = this };
        editor.ShowDialog();
        RefreshCustomKeySoundsSummary();
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

    private void PreviewSoundButton_Click(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).Tag is not string id) return;
        _appState.PreviewSound(id);
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

    private void ExportSettings_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Title = "Export KeyboardSound Settings",
            Filter = "KeyboardSound backup (*.json)|*.json",
            FileName = $"KeyboardSound-backup-{DateTime.Now:yyyy-MM-dd}.json"
        };
        if (dialog.ShowDialog() != true) return;

        // Reuses the same atomic-write persistence the live settings file already uses, just
        // pointed at the chosen path - no separate serialization logic to keep in sync.
        new JsonFileStore<AppSettings>(dialog.FileName).Save(_appState.Settings.Current);
        ShowBackupStatus($"Exported to {System.IO.Path.GetFileName(dialog.FileName)}.");
    }

    private void ImportSettings_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Import KeyboardSound Settings",
            Filter = "KeyboardSound backup (*.json)|*.json"
        };
        if (dialog.ShowDialog() != true) return;

        AppSettings? imported;
        try
        {
            var json = System.IO.File.ReadAllText(dialog.FileName);
            imported = JsonSerializer.Deserialize<AppSettings>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                Converters = { new JsonStringEnumConverter() }
            });
        }
        catch (Exception ex)
        {
            Core.Diagnostics.Log.Error($"Failed to read settings backup '{dialog.FileName}'.", ex);
            imported = null;
        }

        if (imported is null)
        {
            ShowBackupStatus("Import failed - that file isn't a valid KeyboardSound backup.");
            return;
        }

        // Applied onto the live settings object in place (rather than replacing the reference)
        // so every other component already holding onto Settings.Current sees the update.
        var current = _appState.Settings.Current;
        current.Volume = Math.Clamp(imported.Volume, 0.0, 1.0);
        current.SoundEnabled = imported.SoundEnabled;
        current.KeyMode = imported.KeyMode;
        current.EnabledKeys = imported.EnabledKeys ?? new();
        current.FavoriteSoundPackIds = imported.FavoriteSoundPackIds ?? new();
        current.FavoriteSoundIds = imported.FavoriteSoundIds ?? new();
        current.SelectedSoundId = imported.SelectedSoundId;
        current.WidgetPosition = imported.WidgetPosition;
        current.WidgetVisible = imported.WidgetVisible;
        current.StartWithWindows = imported.StartWithWindows;
        current.DebugLogging = imported.DebugLogging;
        if (!string.IsNullOrWhiteSpace(imported.ActiveSoundPackId))
            current.ActiveSoundPackId = imported.ActiveSoundPackId;
        _appState.Settings.Save();

        // Sync the handful of things that live outside the settings file itself.
        _appState.AudioEngine.Volume = current.Volume;
        Core.Diagnostics.Log.DebugEnabled = current.DebugLogging;
        StartupManager.SetEnabled(current.StartWithWindows);
        if (_appState.AvailablePacks.Any(p => p.Id == current.ActiveSoundPackId))
            _appState.ActivatePack(current.ActiveSoundPackId);
        WidgetVisibilityRequested?.Invoke(current.WidgetVisible);

        LoadFromState();
        ShowBackupStatus("Settings imported successfully.");
    }

    private void ShowBackupStatus(string message)
    {
        BackupStatusText.Text = message;
        BackupStatusText.Visibility = Visibility.Visible;
    }

    private void AddSoundpack_Click(object sender, RoutedEventArgs e)
    {
        using var dialog = new System.Windows.Forms.FolderBrowserDialog
        {
            Description = "Select a soundpack folder (a pack.json plus category subfolders like 'normal', 'space', etc.)",
            UseDescriptionForTitle = true
        };
        if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;

        var sourceDir = dialog.SelectedPath;
        var folderName = System.IO.Path.GetFileName(sourceDir.TrimEnd(System.IO.Path.DirectorySeparatorChar));
        if (string.IsNullOrWhiteSpace(folderName))
        {
            ShowSoundpackImportStatus("Couldn't add that folder - invalid folder name.");
            return;
        }

        var destDir = System.IO.Path.Combine(KeyboardSound.Core.Configuration.AppPaths.UserSoundPacksDirectory, folderName);
        if (System.IO.Directory.Exists(destDir))
        {
            ShowSoundpackImportStatus($"A soundpack named '{folderName}' is already installed.");
            return;
        }

        try
        {
            CopyDirectory(sourceDir, destDir);
        }
        catch (Exception ex)
        {
            Core.Diagnostics.Log.Error($"Failed to copy soundpack from '{sourceDir}' to '{destDir}'.", ex);
            ShowSoundpackImportStatus("Couldn't add that soundpack - see the log for details.");
            TryDeleteDirectory(destDir);
            return;
        }

        // Discovery (not this handler) is the single source of truth for what counts as a valid
        // pack - reusing it here means a folder is only accepted if SoundPackManager would also
        // accept it later, with no separate validation rules to keep in sync.
        _appState.RefreshPacks();

        if (_appState.AvailablePacks.Any(p => p.RootPath == destDir))
        {
            _appState.Stats.RecordSoundpackImported();
            RefreshPackList();
            ShowSoundpackImportStatus($"Added soundpack '{folderName}'.");
        }
        else
        {
            TryDeleteDirectory(destDir);
            _appState.RefreshPacks();
            ShowSoundpackImportStatus("That folder doesn't look like a valid soundpack - no category subfolders with .wav/.ogg files were found.");
        }
    }

    private static void CopyDirectory(string sourceDir, string destDir)
    {
        System.IO.Directory.CreateDirectory(destDir);
        foreach (var dir in System.IO.Directory.GetDirectories(sourceDir, "*", System.IO.SearchOption.AllDirectories))
            System.IO.Directory.CreateDirectory(dir.Replace(sourceDir, destDir));
        foreach (var file in System.IO.Directory.GetFiles(sourceDir, "*", System.IO.SearchOption.AllDirectories))
            System.IO.File.Copy(file, file.Replace(sourceDir, destDir), overwrite: false);
    }

    private static void TryDeleteDirectory(string dir)
    {
        try
        {
            if (System.IO.Directory.Exists(dir))
                System.IO.Directory.Delete(dir, recursive: true);
        }
        catch
        {
            // Best-effort cleanup only - a leftover partial folder is harmless since it either
            // won't parse as a pack (ignored by discovery) or the user can delete it manually.
        }
    }

    private void ShowSoundpackImportStatus(string message)
    {
        SoundpackImportStatusText.Text = message;
        SoundpackImportStatusText.Visibility = Visibility.Visible;
    }

    public event Action<bool>? WidgetVisibilityRequested;

    private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
    {
        if (IsExiting) return;
        e.Cancel = true;
        // Minimize rather than Hide() so the window keeps its taskbar button (clicking it
        // restores the window) instead of disappearing from the taskbar entirely - only the
        // tray "Exit" command should ever make KeyboardSound's taskbar presence go away.
        WindowState = WindowState.Minimized;
    }
}
