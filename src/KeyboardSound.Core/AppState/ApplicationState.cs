using KeyboardSound.Core.Audio;
using KeyboardSound.Core.Diagnostics;
using KeyboardSound.Core.Input;
using KeyboardSound.Core.Routing;
using KeyboardSound.Core.Settings;
using KeyboardSound.Core.SoundPacks;
using KeyboardSound.Core.Stats;

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
    public UsageStatsTracker Stats { get; }

    public IReadOnlyList<SoundPackInfo> AvailablePacks { get; private set; } = Array.Empty<SoundPackInfo>();
    public SoundPackInfo? ActivePack { get; private set; }

    public event Action<SoundPackInfo?>? ActivePackChanged;

    public ApplicationState(
        SettingsService settings,
        IAudioEngine audioEngine,
        IGlobalKeyboardHook hook,
        IReadOnlyList<string> soundPackRoots,
        UsageStatsTracker stats)
    {
        Settings = settings;
        AudioEngine = audioEngine;
        Hook = hook;
        PackManager = new SoundPackManager();
        _soundPackRoots = soundPackRoots;
        Stats = stats;
    }

    /// <summary>Startup sequence: discover packs, activate the last-used (or default) one,
    /// apply saved volume, start routing input to audio, then install the global hook.</summary>
    public void Start()
    {
        RefreshPacks();
        ActivateInitialPack();
        AudioEngine.Volume = Settings.Current.Volume;

        _router = new InputRouter(Hook, AudioEngine, Settings);
        // A second, independent subscription purely for local usage stats - kept out of
        // InputRouter itself so the hot input->audio path never gains a new dependency; this
        // handler only ever does cheap in-memory increments (see UsageStatsTracker), never file
        // I/O per keystroke.
        Hook.KeyEvent += OnKeyEventForStats;
        Hook.Start();

        Log.Info("Application state started.");
    }

    private void OnKeyEventForStats(KeyEvent evt)
    {
        if (evt.Action != KeyAction.Down) return;
        if (evt.Key == LogicalKey.Unknown) return;

        Stats.RecordKeyPress(evt.Key);
        if (Settings.Current.SoundEnabled)
            Stats.RecordSoundPlayed();
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

    /// <summary>Favorites/unfavorites one individual sound by its stable id (never by name,
    /// file path, or list position - see <see cref="Settings.AppSettings.FavoriteSoundIds"/>).</summary>
    public void ToggleFavoriteSound(string soundId)
    {
        var favorites = Settings.Current.FavoriteSoundIds;
        if (!favorites.Remove(soundId))
            favorites.Add(soundId);
        Settings.Save();
    }

    public bool IsFavoriteSound(string soundId) => Settings.Current.FavoriteSoundIds.Contains(soundId);

    /// <summary>Pins one specific sound as the deterministic choice for its category (instead of
    /// the pool's normal random rotation), and remembers it across restarts. Passing null clears
    /// the pin, returning that category to normal rotation.</summary>
    public void SelectSound(string? soundId)
    {
        Settings.Current.SelectedSoundId = soundId;
        Settings.Save();
    }

    public bool IsSoundSelected(string soundId) => Settings.Current.SelectedSoundId == soundId;

    /// <summary>Plays one sound immediately as an explicit "hear this now" preview, independent
    /// of the global <see cref="Settings.AppSettings.SoundEnabled"/> toggle and not routed
    /// through a keypress. Every sound the Sounds list can preview belongs to
    /// <see cref="SoundCategory.Normal"/>, so that is the only category this ever needs to pass.</summary>
    public void PreviewSound(string soundId) => AudioEngine.Play(SoundCategory.Normal, soundId);

    /// <summary>All sounds of the currently active pack, grouped by category - the data the
    /// individual-sound-selection UI binds to. Empty if no pack is active.</summary>
    public IReadOnlyList<Sound> GetActivePackSounds(SoundCategory category) =>
        ActivePack is not null && ActivePack.SoundsByCategory.TryGetValue(category, out var sounds)
            ? sounds
            : Array.Empty<Sound>();

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

    /// <summary>Assigns one sound to every key in <paramref name="keys"/> (the Keyboard Editor's
    /// multi-select "apply to all selected" action) - a strictly additive override on top of
    /// whatever <see cref="SelectedSoundId"/>/category-pool selection already exists; see
    /// <see cref="Routing.InputRouter"/> for the priority order.</summary>
    public void SetCustomKeySounds(IEnumerable<LogicalKey> keys, string soundId)
    {
        foreach (var key in keys)
            Settings.Current.CustomKeySounds[key.ToString()] = soundId;
        Settings.Save();
    }

    /// <summary>Reverts the given keys to "use global sound" by removing their override, if any.</summary>
    public void ClearCustomKeySounds(IEnumerable<LogicalKey> keys)
    {
        foreach (var key in keys)
            Settings.Current.CustomKeySounds.Remove(key.ToString());
        Settings.Save();
    }

    public string? GetCustomKeySound(LogicalKey key) =>
        Settings.Current.CustomKeySounds.TryGetValue(key.ToString(), out var soundId) ? soundId : null;

    /// <summary>Clears every per-key override at once ("Reset Custom Keys"). Leaves
    /// <see cref="Settings.AppSettings.EnabledKeys"/>, favorites, and every other setting
    /// untouched.</summary>
    public void ResetCustomKeySounds()
    {
        Settings.Current.CustomKeySounds.Clear();
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
        Hook.KeyEvent -= OnKeyEventForStats;
        _router?.Dispose();
        AudioEngine.Dispose();
        Settings.Save();
        Stats.EndSession();
        Log.Info("Application state shut down cleanly.");
        Log.Flush();
    }

    public void Dispose() => Shutdown();
}
