using System.Windows;
using KeyboardSound.App.Configuration;
using KeyboardSound.Core.AppState;
using KeyboardSound.Core.Settings;
using KeyboardSound.Core.SoundPacks;

namespace KeyboardSound.App.Ui;

/// <summary>
/// First-run (or "Run Setup Again") wizard: choose a sound, keyboard mode, widget visibility,
/// and autostart. Plain step-panel-visibility navigation, consistent with the rest of the app's
/// deliberately simple code-behind style. Shown modally (ShowDialog) so it fully completes
/// before the rest of startup continues, but only ever on first run - see AppSettings.OnboardingCompleted.
/// </summary>
public partial class OnboardingWindow : Window
{
    private readonly ApplicationState _appState;
    private readonly FrameworkElement[] _steps;
    private int _stepIndex;

    public event Action<bool>? WidgetVisibilityRequested;

    public OnboardingWindow(ApplicationState appState)
    {
        InitializeComponent();
        _appState = appState;
        _steps = new FrameworkElement[] { StepWelcome, StepSound, StepKeyMode, StepWidget, StepStartup };

        RefreshSoundList();
        ShowStep(0);
    }

    private void RefreshSoundList()
    {
        OnboardingSoundList.ItemsSource = _appState.GetActivePackSounds(SoundCategory.Normal)
            .Select(s => new SoundRow
            {
                Id = s.Id,
                DisplayName = s.DisplayName,
                IsFavorite = false,
                IsSelected = _appState.IsSoundSelected(s.Id)
            })
            .ToList();
    }

    private void ShowStep(int index)
    {
        _stepIndex = index;
        for (var i = 0; i < _steps.Length; i++)
            _steps[i].Visibility = i == index ? Visibility.Visible : Visibility.Collapsed;

        BackButton.Visibility = index == 0 ? Visibility.Collapsed : Visibility.Visible;
        NextButton.Content = index == _steps.Length - 1 ? "Finish" : "Next";
        StepIndicatorText.Text = $"{index + 1} / {_steps.Length}";
    }

    private void Back_Click(object sender, RoutedEventArgs e)
    {
        if (_stepIndex > 0) ShowStep(_stepIndex - 1);
    }

    private void Next_Click(object sender, RoutedEventArgs e)
    {
        if (_stepIndex < _steps.Length - 1)
        {
            ShowStep(_stepIndex + 1);
            return;
        }
        Finish();
    }

    private void OnboardingPreview_Click(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).Tag is not string id) return;
        _appState.PreviewSound(id);
    }

    private void OnboardingSelect_Click(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).Tag is not string id) return;
        _appState.SelectSound(id);
        RefreshSoundList();
    }

    private void Finish()
    {
        _appState.SetKeyMode(OnboardingCustomKeysRadio.IsChecked == true ? KeyMode.CustomKeys : KeyMode.AllKeys);

        var widgetVisible = OnboardingWidgetShowRadio.IsChecked == true;
        _appState.Settings.Current.WidgetVisible = widgetVisible;
        WidgetVisibilityRequested?.Invoke(widgetVisible);

        var startWithWindows = OnboardingStartWithWindowsRadio.IsChecked == true;
        StartupManager.SetEnabled(startWithWindows);
        _appState.Settings.Current.StartWithWindows = startWithWindows;

        _appState.Settings.Current.OnboardingCompleted = true;
        _appState.Settings.Save();

        Close();
    }
}
