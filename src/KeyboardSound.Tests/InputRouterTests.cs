using KeyboardSound.Core.Audio;
using KeyboardSound.Core.Input;
using KeyboardSound.Core.Routing;
using KeyboardSound.Core.Settings;
using KeyboardSound.Core.SoundPacks;

namespace KeyboardSound.Tests;

/// <summary>
/// Exercises InputRouter's key-to-sound priority logic (per-key custom override, else the
/// pinned global sound, else the category pool) without a real hook or audio device.
/// </summary>
public class InputRouterTests : IDisposable
{
    private readonly string _tempFile = Path.Combine(Path.GetTempPath(), $"ks-router-{Guid.NewGuid():N}.json");

    [Fact]
    public void CustomKeySound_TakesPriorityOverGlobalSelectedSound()
    {
        var settings = new SettingsService(_tempFile);
        settings.Current.SelectedSoundId = "global_sound";
        settings.Current.CustomKeySounds["W"] = "custom_w_sound";
        var hook = new FakeHook();
        var engine = new FakeAudioEngine();
        using var router = new InputRouter(hook, engine, settings);

        hook.Raise(new KeyEvent(LogicalKey.W, KeyAction.Down, 0x57));

        Assert.Single(engine.PlayCalls);
        Assert.Equal("custom_w_sound", engine.PlayCalls[0].PreferredSoundId);
    }

    [Fact]
    public void NoCustomKeySound_FallsBackToGlobalSelectedSound()
    {
        var settings = new SettingsService(_tempFile);
        settings.Current.SelectedSoundId = "global_sound";
        var hook = new FakeHook();
        var engine = new FakeAudioEngine();
        using var router = new InputRouter(hook, engine, settings);

        hook.Raise(new KeyEvent(LogicalKey.A, KeyAction.Down, 0x41));

        Assert.Single(engine.PlayCalls);
        Assert.Equal("global_sound", engine.PlayCalls[0].PreferredSoundId);
    }

    [Fact]
    public void CustomKeySound_OnlyAppliesToItsOwnKey()
    {
        var settings = new SettingsService(_tempFile);
        settings.Current.SelectedSoundId = "global_sound";
        settings.Current.CustomKeySounds["W"] = "custom_w_sound";
        var hook = new FakeHook();
        var engine = new FakeAudioEngine();
        using var router = new InputRouter(hook, engine, settings);

        hook.Raise(new KeyEvent(LogicalKey.A, KeyAction.Down, 0x41));

        Assert.Single(engine.PlayCalls);
        Assert.Equal("global_sound", engine.PlayCalls[0].PreferredSoundId);
    }

    [Fact]
    public void CustomKeyMode_StillGatesEligibility_BeforeCustomSoundIsConsidered()
    {
        var settings = new SettingsService(_tempFile);
        settings.Current.KeyMode = KeyMode.CustomKeys;
        settings.Current.EnabledKeys.Add(LogicalKey.A); // W is not enabled
        settings.Current.CustomKeySounds["W"] = "custom_w_sound";
        var hook = new FakeHook();
        var engine = new FakeAudioEngine();
        using var router = new InputRouter(hook, engine, settings);

        hook.Raise(new KeyEvent(LogicalKey.W, KeyAction.Down, 0x57));

        Assert.Empty(engine.PlayCalls); // W has a custom sound but isn't an enabled key
    }

    [Fact]
    public void KeyUp_NeverTriggersPlayback()
    {
        var settings = new SettingsService(_tempFile);
        var hook = new FakeHook();
        var engine = new FakeAudioEngine();
        using var router = new InputRouter(hook, engine, settings);

        hook.Raise(new KeyEvent(LogicalKey.W, KeyAction.Up, 0x57));

        Assert.Empty(engine.PlayCalls);
    }

    public void Dispose()
    {
        if (File.Exists(_tempFile)) File.Delete(_tempFile);
    }

    private sealed class FakeAudioEngine : IAudioEngine
    {
        public double Volume { get; set; }
        public List<(SoundCategory Category, string? PreferredSoundId)> PlayCalls { get; } = new();
        public void LoadPack(SoundPackInfo pack) { }
        public void Play(SoundCategory category, string? preferredSoundId = null) =>
            PlayCalls.Add((category, preferredSoundId));
        public void Dispose() { }
    }

    private sealed class FakeHook : IGlobalKeyboardHook
    {
        public event Action<KeyEvent>? KeyEvent;
        public void Raise(KeyEvent evt) => KeyEvent?.Invoke(evt);
        public void Start() { }
        public void Stop() { }
        public void Dispose() { }
    }
}
