using NAudio.CoreAudioApi;
using NAudio.CoreAudioApi.Interfaces;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using KeyboardSound.Core.Diagnostics;
using KeyboardSound.Core.SoundPacks;

namespace KeyboardSound.Core.Audio;

/// <summary>
/// WASAPI-backed audio engine. A single shared output device + mixer handles all overlapping
/// playback, so typing fast never spins up new devices/threads per keypress — samples are
/// pre-decoded (<see cref="CachedSound"/>) and simply mixed in.
///
/// Follows the system default output device: unplugging headphones, switching to a different
/// speaker/headset, or any other default-device change is picked up via
/// <see cref="IMMNotificationClient"/> and the WASAPI output is rebound to the new device -
/// <see cref="_mixer"/>/<see cref="_volumeProvider"/> and every already-decoded sample stay
/// exactly as they are, only the output endpoint is swapped. Without this, playback would
/// silently keep going to whatever device was default at startup even after the user switches.
/// </summary>
public sealed class NAudioEngine : IAudioEngine
{
    /// <summary>Every sample is resampled/remixed to this format on load so the mixer can
    /// combine arbitrary soundpacks without per-play format conversion.</summary>
    public static readonly WaveFormat CanonicalFormat = WaveFormat.CreateIeeeFloatWaveFormat(44100, 2);

    /// <summary>Hard cap on simultaneously mixed voices. Protects CPU during pathological
    /// rapid input (e.g. a key stuck or a macro) — short samples free up within ~1 typing beat,
    /// so this is rarely if ever hit during real typing.</summary>
    private const int MaxConcurrentVoices = 48;

    private readonly MixingSampleProvider _mixer;
    private readonly VolumeSampleProvider _volumeProvider;
    private readonly SampleSelector<LoadedSample> _sampleSelector = new();

    // Null only when no audio output device is available at all (e.g. everything unplugged) -
    // Play() keeps working in that state, it just has nothing to feed, per the "an unavailable
    // audio device must never crash the app" requirement.
    private readonly object _outputLock = new();
    private WasapiOut? _output;
    private MMDeviceEnumerator? _deviceEnumerator;
    private DeviceChangeNotifier? _deviceNotifier;

    private IReadOnlyDictionary<SoundCategory, IReadOnlyList<LoadedSample>> _loadedSamples =
        new Dictionary<SoundCategory, IReadOnlyList<LoadedSample>>();

    /// <summary>Flat id -> sample lookup across every category, used to resolve a pinned sound
    /// (and its <see cref="Sound.LinkedSoundIds"/> targets) regardless of which category it or
    /// its linked sounds belong to.</summary>
    private IReadOnlyDictionary<string, LoadedSample> _loadedById =
        new Dictionary<string, LoadedSample>();

    private int _activeVoiceCount;
    private readonly object _voiceCountLock = new();
    private bool _disposed;

    public double Volume
    {
        get => _volumeProvider.Volume;
        set => _volumeProvider.Volume = (float)Math.Clamp(value, 0.0, 1.0);
    }

    public NAudioEngine()
    {
        _mixer = new MixingSampleProvider(CanonicalFormat) { ReadFully = true };
        _volumeProvider = new VolumeSampleProvider(_mixer) { Volume = 0.8f };

        _output = CreateOutput();

        // Best-effort: if this fails (rare COM/driver issue), the engine still works with
        // whatever device was default at startup, it just won't follow later device changes.
        try
        {
            _deviceEnumerator = new MMDeviceEnumerator();
            _deviceNotifier = new DeviceChangeNotifier(this);
            _deviceEnumerator.RegisterEndpointNotificationCallback(_deviceNotifier);
        }
        catch (Exception ex)
        {
            Log.Warn($"Could not register for audio device change notifications: {ex.Message}");
        }
    }

    /// <summary>Short, event-synced latency: keeps keypress-to-sound delay low without the
    /// instability that comes from pushing exclusive-mode/very small buffers too hard. Returns
    /// null (never throws) if no output device is currently available - Play() degrades to a
    /// silent no-op rather than the app crashing or failing to start.</summary>
    private WasapiOut? CreateOutput()
    {
        try
        {
            var output = new WasapiOut(AudioClientShareMode.Shared, useEventSync: true, latency: 40);
            output.Init(_volumeProvider);
            output.Play();
            return output;
        }
        catch (Exception ex)
        {
            Log.Error("Failed to initialize an audio output device. Sound will be unavailable until a device becomes available.", ex);
            return null;
        }
    }

    /// <summary>Called (via <see cref="DeviceChangeNotifier"/>, on an arbitrary COM callback
    /// thread) whenever the system default playback device changes. Rebinds output to the new
    /// device; the mixer, volume, and every already-decoded sample are untouched, so no
    /// soundpack reload is needed.</summary>
    private void OnDefaultDeviceChanged()
    {
        if (_disposed) return;
        lock (_outputLock)
        {
            if (_disposed) return;
            var old = _output;
            _output = CreateOutput();
            if (_output is not null)
                Log.Info("Default audio output device changed - playback switched to the new device.");
            try { old?.Stop(); } catch { /* best-effort */ }
            old?.Dispose();
        }
    }

    private sealed class DeviceChangeNotifier : IMMNotificationClient
    {
        private readonly NAudioEngine _owner;
        public DeviceChangeNotifier(NAudioEngine owner) => _owner = owner;

        public void OnDefaultDeviceChanged(DataFlow flow, Role role, string defaultDeviceId)
        {
            // Multimedia role matches what WasapiOut's parameterless device selection uses.
            if (flow == DataFlow.Render && role == Role.Multimedia)
                _owner.OnDefaultDeviceChanged();
        }

        public void OnDeviceAdded(string pwstrDeviceId) { }
        public void OnDeviceRemoved(string deviceId) { }
        public void OnDeviceStateChanged(string deviceId, DeviceState newState) { }
        public void OnPropertyValueChanged(string pwstrDeviceId, PropertyKey key) { }
    }

    public void LoadPack(SoundPackInfo pack)
    {
        var newSamples = new Dictionary<SoundCategory, IReadOnlyList<LoadedSample>>();

        foreach (var (category, sounds) in pack.SoundsByCategory)
        {
            var loaded = new List<LoadedSample>(sounds.Count);
            foreach (var sound in sounds)
            {
                try
                {
                    loaded.Add(new LoadedSample(sound, CachedSound.Load(sound.FilePath, CanonicalFormat)));
                }
                catch (Exception ex)
                {
                    Log.Warn($"Failed to load sample '{sound.FilePath}' in soundpack '{pack.Id}': {ex.Message}");
                }
            }

            if (loaded.Count > 0)
                newSamples[category] = loaded;
        }

        var newById = new Dictionary<string, LoadedSample>();
        foreach (var list in newSamples.Values)
            foreach (var loaded in list)
                newById[loaded.Meta.Id] = loaded;

        // Swap the references atomically; any in-flight Play() calls on the old dictionaries
        // simply finish against the old (still-valid, GC-retained) CachedSound objects.
        _loadedSamples = newSamples;
        _loadedById = newById;
        Log.Info($"Loaded soundpack '{pack.Id}' ({newSamples.Sum(kv => kv.Value.Count)} samples).");
    }

    public void Play(SoundCategory category, string? preferredSoundId = null)
    {
        if (_disposed) return;

        lock (_voiceCountLock)
        {
            if (_activeVoiceCount >= MaxConcurrentVoices)
                return;
        }

        LoadedSample? sample = ResolvePinned(category, preferredSoundId);
        sample ??= _sampleSelector.SelectSample(category, _loadedSamples);
        if (sample is null)
            return;

        var provider = new CachedSoundSampleProvider(sample.Value.Audio);
        var tracked = new VoiceCountingSampleProvider(provider, this);

        lock (_voiceCountLock) _activeVoiceCount++;
        _mixer.AddMixerInput(tracked);
        Log.Debug($"Played {category} sample '{sample.Value.Meta.Id}' ({_activeVoiceCount} active voices).");
    }

    /// <summary>
    /// If the user has pinned a specific sound, resolves what that pin means for this category:
    /// the pin's own sound if the category matches it directly, its explicitly linked sound for
    /// this category if one is declared (<see cref="Sound.LinkedSoundIds"/>), or - when neither
    /// applies - the pin itself again. That last case is deliberate: a selected sound with no
    /// dedicated variant for e.g. Escape still deterministically reuses the selected sound rather
    /// than falling back to that category's random pool, per the "selecting a sound fixes every
    /// key's sound" requirement. Returns null only when nothing is pinned or the pinned id no
    /// longer resolves to a loaded sample (removed/renamed) - callers then fall back to normal
    /// category-pool selection.
    /// </summary>
    private LoadedSample? ResolvePinned(SoundCategory category, string? preferredSoundId)
    {
        if (preferredSoundId is null || !_loadedById.TryGetValue(preferredSoundId, out var pinned))
            return null;

        var targetId = PinnedSoundResolver.ResolveTargetId(pinned.Meta, category);
        // If the resolved target (a linked sound) somehow isn't loaded - e.g. pack.json points
        // at a sample that failed to decode - fall back to the pin itself rather than dropping
        // to random rotation, keeping the "selecting a sound fixes every key" guarantee.
        return _loadedById.TryGetValue(targetId, out var target) ? target : pinned;
    }

    internal void OnVoiceFinished()
    {
        lock (_voiceCountLock) _activeVoiceCount--;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        if (_deviceEnumerator is not null && _deviceNotifier is not null)
        {
            try { _deviceEnumerator.UnregisterEndpointNotificationCallback(_deviceNotifier); }
            catch { /* best-effort */ }
        }
        _deviceEnumerator?.Dispose();

        lock (_outputLock)
        {
            try { _output?.Stop(); }
            catch { /* best-effort shutdown */ }
            _output?.Dispose();
        }
    }

    /// <summary>Wraps a voice so the engine's active-voice counter decrements the moment
    /// the mixer drains it, without the mixer needing to know about voice accounting.</summary>
    private sealed class VoiceCountingSampleProvider : ISampleProvider
    {
        private readonly ISampleProvider _inner;
        private readonly NAudioEngine _owner;
        private bool _finished;

        public VoiceCountingSampleProvider(ISampleProvider inner, NAudioEngine owner)
        {
            _inner = inner;
            _owner = owner;
        }

        public int Read(float[] buffer, int offset, int count)
        {
            var read = _inner.Read(buffer, offset, count);
            // MixingSampleProvider removes an input as soon as a Read() returns fewer samples
            // than requested (not only on an exact 0), so voice accounting must match that
            // exact condition or the counter would leak and eventually block new playback.
            if (read < count && !_finished)
            {
                _finished = true;
                _owner.OnVoiceFinished();
            }
            return read;
        }

        public WaveFormat WaveFormat => _inner.WaveFormat;
    }
}
