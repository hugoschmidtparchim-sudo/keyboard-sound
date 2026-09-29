using System.Windows;
using System.Windows.Controls;
using KeyboardSound.Core.AppState;
using KeyboardSound.Core.Input;
using KeyboardSound.Core.SoundPacks;
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;
using Orientation = System.Windows.Controls.Orientation;

namespace KeyboardSound.App.Ui;

/// <summary>
/// Visual keyboard for assigning individual sounds to individual keys (per-key overrides on top
/// of the existing global/category sound selection - see
/// <see cref="Core.Settings.AppSettings.CustomKeySounds"/> and
/// <see cref="Core.Routing.InputRouter"/> for the priority order). Plain code-behind, generated
/// from the data-driven <see cref="KeyboardLayout"/> rather than hand-authored per-key XAML.
/// </summary>
public partial class KeyboardEditorWindow : Window
{
    private const double KeyUnit = 42;
    private const double KeyGap = 4;
    private const double KeyHeight = 42;

    private readonly ApplicationState _appState;
    private readonly HashSet<LogicalKey> _selectedKeys = new();
    private readonly Dictionary<LogicalKey, Border> _keyElements = new();
    private readonly List<LogicalKey> _allLayoutKeys = new();

    public KeyboardEditorWindow(ApplicationState appState)
    {
        InitializeComponent();
        _appState = appState;

        BuildKeyboard();
        RefreshSoundList();
        RefreshSelectionUi();
    }

    private void BuildKeyboard()
    {
        var mainColumn = new StackPanel { Orientation = Orientation.Vertical };
        foreach (var row in KeyboardLayout.MainRows)
            mainColumn.Children.Add(BuildRow(row));
        KeyboardHost.Children.Add(mainColumn);

        var clusterColumn = new StackPanel { Orientation = Orientation.Vertical, Margin = new Thickness(18, 0, 0, 0) };
        foreach (var row in KeyboardLayout.NavRows)
            clusterColumn.Children.Add(BuildRow(row));
        clusterColumn.Children.Add(new Border { Height = 8 });
        foreach (var row in KeyboardLayout.ArrowRows)
            clusterColumn.Children.Add(BuildArrowRow(row));
        KeyboardHost.Children.Add(clusterColumn);

        var numPadColumn = new StackPanel { Orientation = Orientation.Vertical, Margin = new Thickness(18, 0, 0, 0) };
        foreach (var row in KeyboardLayout.NumPadRows)
            numPadColumn.Children.Add(BuildRow(row));
        KeyboardHost.Children.Add(numPadColumn);
    }

    private StackPanel BuildRow(IReadOnlyList<KeyDef> row)
    {
        var panel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, KeyGap) };
        foreach (var def in row)
            panel.Children.Add(BuildKeyElement(def));
        return panel;
    }

    private StackPanel BuildArrowRow(IReadOnlyList<KeyDef?> row)
    {
        var panel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, KeyGap) };
        foreach (var def in row)
            panel.Children.Add(def is null
                ? new Border { Width = KeyUnit, Height = KeyHeight, Margin = new Thickness(0, 0, KeyGap, 0), Background = Brushes.Transparent }
                : BuildKeyElement(def));
        return panel;
    }

    private Border BuildKeyElement(KeyDef def)
    {
        _allLayoutKeys.Add(def.Key);

        var border = new Border
        {
            Width = def.Width * KeyUnit + (def.Width - 1) * KeyGap,
            Height = KeyHeight,
            Margin = new Thickness(0, 0, KeyGap, 0),
            CornerRadius = new CornerRadius(7),
            Cursor = System.Windows.Input.Cursors.Hand,
            Tag = def.Key
        };
        var text = new TextBlock
        {
            Text = def.Label,
            FontSize = 12,
            HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
            VerticalAlignment = System.Windows.VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        border.Child = text;
        border.MouseLeftButtonDown += (_, e) =>
        {
            var mods = System.Windows.Input.Keyboard.Modifiers;
            var extend = (mods & System.Windows.Input.ModifierKeys.Control) != 0 || (mods & System.Windows.Input.ModifierKeys.Shift) != 0;
            ToggleSelection(def.Key, extend);
            e.Handled = true;
        };

        _keyElements[def.Key] = border;
        ApplyKeyVisualState(def.Key);
        return border;
    }

    private void ToggleSelection(LogicalKey key, bool extend)
    {
        if (!extend)
        {
            var wasOnlySelected = _selectedKeys.Count == 1 && _selectedKeys.Contains(key);
            _selectedKeys.Clear();
            if (!wasOnlySelected)
                _selectedKeys.Add(key);
        }
        else
        {
            if (!_selectedKeys.Add(key))
                _selectedKeys.Remove(key);
        }

        foreach (var k in _keyElements.Keys)
            ApplyKeyVisualState(k);
        RefreshSelectionUi();
        RefreshSoundList();
    }

    private void ApplyKeyVisualState(LogicalKey key)
    {
        if (!_keyElements.TryGetValue(key, out var border)) return;

        var isSelected = _selectedKeys.Contains(key);
        var hasCustom = _appState.GetCustomKeySound(key) is not null;

        var resources = System.Windows.Application.Current.Resources;
        if (isSelected)
        {
            border.Background = (Brush)resources["AccentBrush"];
            border.BorderBrush = (Brush)resources["AccentBrush"];
            border.BorderThickness = new Thickness(2);
            ((TextBlock)border.Child).Foreground = Brushes.White;
        }
        else
        {
            border.Background = (Brush)resources["SurfaceRaisedBrush"];
            border.BorderBrush = hasCustom
                ? (Brush)resources["AccentBrush"]
                : (Brush)resources["BorderBrush"];
            border.BorderThickness = new Thickness(hasCustom ? 2 : 1);
            ((TextBlock)border.Child).Foreground = (Brush)resources["TextPrimaryBrush"];
        }
    }

    private void RefreshSelectionUi()
    {
        if (_selectedKeys.Count == 0)
        {
            SelectionSummaryText.Text = "No keys selected. Click a key to start.";
        }
        else if (_selectedKeys.Count == 1)
        {
            var key = _selectedKeys.First();
            var soundId = _appState.GetCustomKeySound(key);
            var soundName = soundId is null ? "Global sound" : DisplayNameFor(soundId) ?? soundId;
            SelectionSummaryText.Text = $"Selected key: {key}\nSound: {soundName}";
        }
        else
        {
            SelectionSummaryText.Text = $"{_selectedKeys.Count} keys selected: {string.Join(", ", _selectedKeys.Select(k => k.ToString()))}";
        }

        NoSelectionHintText.Visibility = _selectedKeys.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        AssignSoundList.Visibility = _selectedKeys.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
    }

    private string? DisplayNameFor(string soundId) =>
        _appState.GetActivePackSounds(SoundCategory.Normal).FirstOrDefault(s => s.Id == soundId)?.DisplayName;

    private void RefreshSoundList()
    {
        // "Already assigned to every currently-selected key" - not just "the global pin" like
        // the main Sounds list uses IsSelected for; this list is scoped to the editor's own
        // selection, reusing the same SoundRow/converters for visual consistency only.
        var uniformSoundId = _selectedKeys.Count > 0
            ? _selectedKeys.Select(_appState.GetCustomKeySound).Distinct().Count() == 1
                ? _appState.GetCustomKeySound(_selectedKeys.First())
                : "__mixed__"
            : null;

        AssignSoundList.ItemsSource = _appState.GetActivePackSounds(SoundCategory.Normal)
            .Select(s => new SoundRow
            {
                Id = s.Id,
                DisplayName = s.DisplayName,
                IsFavorite = false,
                IsSelected = uniformSoundId == s.Id
            })
            .ToList();
    }

    private void EditorPreview_Click(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).Tag is not string id) return;
        _appState.PreviewSound(id);
    }

    private void EditorApply_Click(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).Tag is not string id) return;
        if (_selectedKeys.Count == 0) return;

        _appState.SetCustomKeySounds(_selectedKeys, id);
        foreach (var k in _selectedKeys) ApplyKeyVisualState(k);
        RefreshSelectionUi();
        RefreshSoundList();
    }

    private void UseGlobalSound_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedKeys.Count == 0) return;

        _appState.ClearCustomKeySounds(_selectedKeys);
        foreach (var k in _selectedKeys) ApplyKeyVisualState(k);
        RefreshSelectionUi();
        RefreshSoundList();
    }

    private void SelectAll_Click(object sender, RoutedEventArgs e)
    {
        _selectedKeys.Clear();
        foreach (var k in _allLayoutKeys) _selectedKeys.Add(k);
        foreach (var k in _keyElements.Keys) ApplyKeyVisualState(k);
        RefreshSelectionUi();
        RefreshSoundList();
    }

    private void ClearSelection_Click(object sender, RoutedEventArgs e)
    {
        _selectedKeys.Clear();
        foreach (var k in _keyElements.Keys) ApplyKeyVisualState(k);
        RefreshSelectionUi();
        RefreshSoundList();
    }

    private void ResetCustomKeys_Click(object sender, RoutedEventArgs e)
    {
        var result = System.Windows.MessageBox.Show(
            this,
            "Reset every custom key sound? Keys will go back to using your global sound. Favorites and other settings are not affected.",
            "Reset Custom Keys",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);
        if (result != MessageBoxResult.Yes) return;

        _appState.ResetCustomKeySounds();
        foreach (var k in _keyElements.Keys) ApplyKeyVisualState(k);
        RefreshSelectionUi();
        RefreshSoundList();
    }
}
