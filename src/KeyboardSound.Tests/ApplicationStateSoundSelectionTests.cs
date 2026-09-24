using KeyboardSound.Core.AppState;
using KeyboardSound.Core.Audio;
using KeyboardSound.Core.Input;
using KeyboardSound.Core.Settings;
using KeyboardSound.Core.SoundPacks;

namespace KeyboardSound.Tests;

/// <summary>
/// Exercises ApplicationState's individual-sound favorite/select orchestration with fake
/// Audio/Input implementations (no real WASAPI device or OS hook needed), since these are
/// pure coordination + persistence logic.
/// </summary>
public class ApplicationStateSoundSelectionTests : IDisposable
{
    private readonly string _tempFile = Path.Combine(Path.GetTempPath(), $"ks-appstate-{Guid.NewGuid():N}.json");
    private readonly string _packsRoot = Path.Combine(Path.GetTempPath(), $"ks-appstate-packs-{Guid.NewGuid():N}");

    public ApplicationStateSoundSelectionTests()
    {
        Directory.CreateDirectory(_packsRoot);
    }

    private ApplicationState CreateState()
    {
        var packDir = Path.Combine(_packsRoot, "curated");
        var normalDir = Path.Combine(packDir, "normal");
        Directory.CreateDirectory(normalDir);
        File.WriteAllText(Path.Combine(packDir, "pack.json"), """
            {
              "id": "curated",
              "name": "Curated",
              "sounds": [
                { "id": "crisp_asmr_01", "displayName": "Crisp ASMR", "category": "Normal", "file": "normal/a.wav" },
                { "id": "deep_thock_01", "displayName": "Deep Thock", "category": "Normal", "file": "normal/b.wav" }
              ]
            }
            """);
        File.WriteAllBytes(Path.Combine(normalDir, "a.wav"), new byte[] { 0x00 });
        File.WriteAllBytes(Path.Combine(normalDir, "b.wav"), new byte[] { 0x00 });

        var settings = new SettingsService(_tempFile);
        settings.Current.ActiveSoundPackId = "curated";
        return new ApplicationState(settings, new FakeAudioEngine(), new FakeHook(), new[] { _packsRoot });
    }

    [Fact]
    public void GetActivePackSounds_ReturnsCuratedSoundsForActivePack()
    {
        var state = CreateState();
        state.Start();

        var sounds = state.GetActivePackSounds(SoundCategory.Normal);

        Assert.Equal(2, sounds.Count);
        Assert.Contains(sounds, s => s.Id == "crisp_asmr_01" && s.DisplayName == "Crisp ASMR");
    }

    [Fact]
    public void ToggleFavoriteSound_PersistsByStableId()
    {
        var state = CreateState();
        state.Start();

        state.ToggleFavoriteSound("crisp_asmr_01");

        Assert.True(state.IsFavoriteSound("crisp_asmr_01"));
        Assert.False(state.IsFavoriteSound("deep_thock_01"));
    }

    [Fact]
    public void ToggleFavoriteSound_Twice_RemovesFavorite()
    {
        var state = CreateState();
        state.Start();

        state.ToggleFavoriteSound("crisp_asmr_01");
        state.ToggleFavoriteSound("crisp_asmr_01");

        Assert.False(state.IsFavoriteSound("crisp_asmr_01"));
    }

    [Fact]
    public void SelectSound_PersistsAcrossReload()
    {
        var state = CreateState();
        state.Start();
        state.SelectSound("deep_thock_01");
        state.Shutdown();

        var reloadedSettings = new SettingsService(_tempFile);
        Assert.Equal("deep_thock_01", reloadedSettings.Current.SelectedSoundId);
    }

    [Fact]
    public void IsFavoriteSound_UnknownId_ReturnsFalseWithoutThrowing()
    {
        var state = CreateState();
        state.Start();

        Assert.False(state.IsFavoriteSound("some_removed_sound_id"));
    }

    public void Dispose()
    {
        if (File.Exists(_tempFile)) File.Delete(_tempFile);
        if (Directory.Exists(_packsRoot)) Directory.Delete(_packsRoot, recursive: true);
    }

    private sealed class FakeAudioEngine : IAudioEngine
    {
        public double Volume { get; set; }
        public void LoadPack(SoundPackInfo pack) { }
        public void Play(SoundCategory category, string? preferredSoundId = null) { }
        public void Dispose() { }
    }

    private sealed class FakeHook : IGlobalKeyboardHook
    {
        public event Action<KeyEvent>? KeyEvent;
        public void Start() { }
        public void Stop() { }
        public void Dispose() { }
    }
}
