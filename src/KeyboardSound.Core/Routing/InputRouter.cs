using KeyboardSound.Core.Audio;
using KeyboardSound.Core.Input;
using KeyboardSound.Core.Settings;
using KeyboardSound.Core.SoundPacks;

namespace KeyboardSound.Core.Routing;

/// <summary>
/// Wires the Input layer to the Audio layer. This is the only place that decides *whether* a
/// key press should make sound (key mode / enabled-key check) and *which* category it maps to;
/// the hook itself stays ignorant of settings, and the audio engine stays ignorant of keys.
/// </summary>
public sealed class InputRouter : IDisposable
{
    private readonly IGlobalKeyboardHook _hook;
    private readonly IAudioEngine _audioEngine;
    private readonly SettingsService _settings;

    public InputRouter(IGlobalKeyboardHook hook, IAudioEngine audioEngine, SettingsService settings)
    {
        _hook = hook;
        _audioEngine = audioEngine;
        _settings = settings;
        _hook.KeyEvent += OnKeyEvent;
    }

    private void OnKeyEvent(KeyEvent evt)
    {
        if (evt.Action != KeyAction.Down)
            return; // Only key-down triggers playback (key-up is tracked by the hook for dedup).

        var settings = _settings.Current;
        if (!settings.SoundEnabled)
            return;

        if (evt.Key == LogicalKey.Unknown)
            return;

        if (settings.KeyMode == KeyMode.CustomKeys && !settings.EnabledKeys.Contains(evt.Key))
            return;

        var category = KeyCategoryMap.Resolve(evt.Key);
        _audioEngine.Play(category, settings.SelectedSoundId);
    }

    public void Dispose()
    {
        _hook.KeyEvent -= OnKeyEvent;
    }
}
