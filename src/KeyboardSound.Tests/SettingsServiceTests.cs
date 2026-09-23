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

    public void Dispose()
    {
        if (File.Exists(_tempFile)) File.Delete(_tempFile);
        foreach (var backup in Directory.GetFiles(Path.GetTempPath(), Path.GetFileName(_tempFile) + ".corrupt-*.bak"))
            File.Delete(backup);
    }
}
