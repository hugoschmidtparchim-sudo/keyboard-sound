using KeyboardSound.Core.Audio;
using KeyboardSound.Core.Diagnostics;
using KeyboardSound.Core.Input;
using KeyboardSound.Core.Routing;
using KeyboardSound.Core.Settings;
using KeyboardSound.Core.SoundPacks;

namespace KeyboardSound.Core.AppState;

/// <summary>
/// Central runtime state and orchestrator. Owns the lifecycle of every other service
/// (settings, soundpacks, audio, input) so <c>App.xaml.cs</c> only has to call
/// <see cref="Start"/> once at startup and <see cref="Shutdown"/> once at exit, in that order,
/// per the app lifecycle rules: load config, prepare audio, start global input, then UI.
/// </summary>
public sealed class ApplicationState : IDisposable
{
    private readonly IReadOnlyList<string> _soundPackRoots;
    private InputRouter? _router;

    public SettingsService Settings { get; }
    public SoundPackManager PackManager { get; }
    public IAudioEngine AudioEngine { get; }
    public IGlobalKeyboardHook Hook { get; }

    public IReadOnlyList<SoundPackInfo> AvailablePacks { get; private set; } = Array.Empty<SoundPackInfo>();
    public SoundPackInfo? ActivePack { get; private set; }

    public event Action<SoundPackInfo?>? ActivePackChanged;

    public ApplicationState(
        SettingsService settings,
        IAudioEngine audioEngine,
        IGlobalKeyboardHook hook,
        IReadOnlyList<string> soundPackRoots)
    {
        Settings = settings;
        AudioEngine = audioEngine;
        Hook = hook;
        PackManager = new SoundPackManager();
        _soundPackRoots = soundPackRoots;
    }

    /// <summary>Startup sequence: discover packs, activate the last-used (or default) one,
    /// apply saved volume, start routing input to audio, then install the global hook.</summary>
    public void Start()
    {
        RefreshPacks();
        ActivateInitialPack();
        AudioEngine.Volume = Settings.Current.Volume;

        _router = new InputRouter(Hook, AudioEngine, Settings);
        Hook.Start();

        Log.Info("Application state started.");
    }

    public void RefreshPacks()
    {
        AvailablePacks = PackManager.DiscoverAll(_soundPackRoots);
    }

    private void ActivateInitialPack()
    {
        var wantedId = Settings.Current.ActiveSoundPackId;
        var pack = AvailablePacks.FirstOrDefault(p => p.Id == wantedId) ?? AvailablePacks.FirstOrDefault();
        if (pack is not null)
            ActivatePack(pack.Id);
        else
            Log.Warn("No soundpacks available to activate.");
    }

    public bool ActivatePack(string packId)
    {
        var pack = AvailablePacks.FirstOrDefault(p => p.Id == packId);
        if (pack is null)
        {
            Log.Warn($"Cannot activate unknown soundpack '{packId}'.");
            return false;
        }

        // Loading decodes the new pack before dropping the old one's reference, so there is
        // no window where the audio engine has zero samples for a category mid-switch.
        AudioEngine.LoadPack(pack);
        ActivePack = pack;
        Settings.Current.ActiveSoundPackId = pack.Id;
        Settings.Save();
        ActivePackChanged?.Invoke(pack);
        return true;
    }

    public void SetVolume(double volume)
    {
        var clamped = Math.Clamp(volume, 0.0, 1.0);
        AudioEngine.Volume = clamped;
        Settings.Current.Volume = clamped;
        Settings.Save();
    }

    public void ToggleFavorite(string packId)
    {
        var favorites = Settings.Current.FavoriteSoundPackIds;
        if (!favorites.Remove(packId))
            favorites.Add(packId);
        Settings.Save();
    }

    public bool IsFavorite(string packId) => Settings.Current.FavoriteSoundPackIds.Contains(packId);

    public void SetKeyMode(KeyMode mode)
    {
        Settings.Current.KeyMode = mode;
        Settings.Save();
    }

    public void SetEnabledKeys(IEnumerable<LogicalKey> keys)
    {
        Settings.Current.EnabledKeys = keys.Distinct().ToList();
        Settings.Save();
    }

    public void SetSoundEnabled(bool enabled)
    {
        Settings.Current.SoundEnabled = enabled;
        Settings.Save();
    }

    /// <summary>Shutdown sequence: stop the hook first (no more input can arrive), tear down
    /// routing and audio, then persist settings — mirrors the reverse of <see cref="Start"/>.</summary>
    public void Shutdown()
    {
        Hook.Stop();
        _router?.Dispose();
        AudioEngine.Dispose();
        Settings.Save();
        Log.Info("Application state shut down cleanly.");
        Log.Flush();
    }

    public void Dispose() => Shutdown();
}
