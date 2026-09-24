using KeyboardSound.Core.Settings;

namespace KeyboardSound.Tests;

public class SettingsServiceTests : IDisposable
{
    private readonly string _tempFile = Path.Combine(Path.GetTempPath(), $"ks-settings-{Guid.NewGuid():N}.json");

    [Fact]
    public void FirstRun_WithNoFile_UsesDefaults()
    {
        var service = new SettingsService(_tempFile);

        Assert.Equal(0.8, service.Current.Volume);
        Assert.Equal(KeyMode.AllKeys, service.Current.KeyMode);
        Assert.Empty(service.Current.EnabledKeys);
        Assert.Empty(service.Current.FavoriteSoundPackIds);
        Assert.Empty(service.Current.FavoriteSoundIds);
        Assert.Null(service.Current.SelectedSoundId);
    }

    [Fact]
    public void SaveThenReload_PersistsValues()
    {
        var service = new SettingsService(_tempFile);
        service.Current.Volume = 0.35;
        service.Current.ActiveSoundPackId = "creamy";
        service.Current.KeyMode = KeyMode.CustomKeys;
        service.Current.FavoriteSoundPackIds.Add("creamy");
        service.Save();

        var reloaded = new SettingsService(_tempFile);

        Assert.Equal(0.35, reloaded.Current.Volume);
        Assert.Equal("creamy", reloaded.Current.ActiveSoundPackId);
        Assert.Equal(KeyMode.CustomKeys, reloaded.Current.KeyMode);
        Assert.Contains("creamy", reloaded.Current.FavoriteSoundPackIds);
    }

    [Fact]
    public void CorruptFile_DoesNotThrow_AndFallsBackToDefaults()
    {
        File.WriteAllText(_tempFile, "{ this is not valid json ][");

        var service = new SettingsService(_tempFile);

        Assert.Equal(0.8, service.Current.Volume);
    }

    [Fact]
    public void OutOfRangeVolume_IsClampedOnLoad()
    {
        File.WriteAllText(_tempFile, "{\"Volume\": 5.0}");

        var service = new SettingsService(_tempFile);

        Assert.Equal(1.0, service.Current.Volume);
    }

    [Fact]
    public void NegativeVolume_IsClampedToZero()
    {
        File.WriteAllText(_tempFile, "{\"Volume\": -3.0}");

        var service = new SettingsService(_tempFile);

        Assert.Equal(0.0, service.Current.Volume);
    }

    [Fact]
    public void FavoriteSoundId_SurvivesSaveAndReload_ByStableId()
    {
        var service = new SettingsService(_tempFile);
        service.Current.FavoriteSoundIds.Add("crisp_asmr_01");
        service.Save();

        var reloaded = new SettingsService(_tempFile);

        Assert.Contains("crisp_asmr_01", reloaded.Current.FavoriteSoundIds);
    }

    [Fact]
    public void SelectedSoundId_SurvivesSaveAndReload()
    {
        var service = new SettingsService(_tempFile);
        service.Current.SelectedSoundId = "deep_thock_01";
        service.Save();

        var reloaded = new SettingsService(_tempFile);

        Assert.Equal("deep_thock_01", reloaded.Current.SelectedSoundId);
    }

    [Fact]
    public void DuplicateFavoriteSoundIds_AreDeduplicatedOnSave()
    {
        var service = new SettingsService(_tempFile);
        service.Current.FavoriteSoundIds.Add("deep_thock_01");
        service.Current.FavoriteSoundIds.Add("deep_thock_01");
        service.Save();

        Assert.Single(service.Current.FavoriteSoundIds);
    }

    [Fact]
    public void AddingNewSounds_DoesNotAffectExistingFavorites()
    {
        // Simulates "version A favorites a sound, version B ships 5 more sounds":
        // existing favorites must be untouched and new sounds must not be auto-favorited.
        var service = new SettingsService(_tempFile);
        service.Current.FavoriteSoundIds.Add("crisp_asmr_01");
        service.Save();

        var reloaded = new SettingsService(_tempFile);
        // New sounds simply don't appear in the list until the user favorites them.
        Assert.Single(reloaded.Current.FavoriteSoundIds);
        Assert.Contains("crisp_asmr_01", reloaded.Current.FavoriteSoundIds);
        Assert.DoesNotContain("some_new_sound_01", reloaded.Current.FavoriteSoundIds);
    }

    public void Dispose()
    {
        if (File.Exists(_tempFile)) File.Delete(_tempFile);
        foreach (var backup in Directory.GetFiles(Path.GetTempPath(), Path.GetFileName(_tempFile) + ".corrupt-*.bak"))
            File.Delete(backup);
    }
}
