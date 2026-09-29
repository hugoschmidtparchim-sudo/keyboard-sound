using KeyboardSound.Core.Input;
using KeyboardSound.Core.Stats;

namespace KeyboardSound.Tests;

public class UsageStatsTrackerTests : IDisposable
{
    private readonly string _tempFile = Path.Combine(Path.GetTempPath(), $"ks-stats-{Guid.NewGuid():N}.json");

    [Fact]
    public void RecordKeyPress_IncrementsTotalAndPerKeyCounts()
    {
        var tracker = new UsageStatsTracker(_tempFile);

        tracker.RecordKeyPress(LogicalKey.A);
        tracker.RecordKeyPress(LogicalKey.A);
        tracker.RecordKeyPress(LogicalKey.Space);

        Assert.Equal(3, tracker.Current.TotalKeyPresses);
        Assert.Equal(2, tracker.Current.KeyPressCountByKey[LogicalKey.A.ToString()]);
        Assert.Equal(1, tracker.Current.KeyPressCountByKey[LogicalKey.Space.ToString()]);
    }

    [Fact]
    public void MostUsedKey_ReturnsHighestCount()
    {
        var tracker = new UsageStatsTracker(_tempFile);

        tracker.RecordKeyPress(LogicalKey.A);
        tracker.RecordKeyPress(LogicalKey.Space);
        tracker.RecordKeyPress(LogicalKey.Space);

        Assert.Equal(LogicalKey.Space.ToString(), tracker.MostUsedKey);
    }

    [Fact]
    public void MostUsedKey_NullWhenNothingRecordedYet()
    {
        var tracker = new UsageStatsTracker(_tempFile);
        Assert.Null(tracker.MostUsedKey);
    }

    [Fact]
    public void FirstStart_UnlocksOnceOnFirstConstruction()
    {
        var unlocked = new List<string>();
        var tracker = new UsageStatsTracker(_tempFile);
        tracker.AchievementUnlocked += unlocked.Add;

        Assert.Contains("FIRST_START", tracker.Current.UnlockedAchievements);
    }

    [Fact]
    public void RecordSoundPlayed_UnlocksFirstSoundOnlyOnce()
    {
        var unlockCount = 0;
        var tracker = new UsageStatsTracker(_tempFile);
        tracker.AchievementUnlocked += name => { if (name == "FIRST_SOUND") unlockCount++; };

        tracker.RecordSoundPlayed();
        tracker.RecordSoundPlayed();
        tracker.RecordSoundPlayed();

        Assert.Equal(1, unlockCount);
        Assert.Contains("FIRST_SOUND", tracker.Current.UnlockedAchievements);
    }

    [Fact]
    public void RecordKeyPress_UnlocksKeypressMilestones()
    {
        var tracker = new UsageStatsTracker(_tempFile);
        for (var i = 0; i < 10_000; i++)
            tracker.RecordKeyPress(LogicalKey.A);

        Assert.Contains("10K_KEYPRESSES", tracker.Current.UnlockedAchievements);
        Assert.DoesNotContain("100K_KEYPRESSES", tracker.Current.UnlockedAchievements);
    }

    [Fact]
    public void Reset_ClearsCountsButKeepsAchievementsAndFirstLaunch()
    {
        var tracker = new UsageStatsTracker(_tempFile);
        tracker.RecordKeyPress(LogicalKey.A);
        tracker.RecordSoundPlayed();
        var firstLaunch = tracker.Current.FirstLaunchAt;

        tracker.Reset();

        Assert.Equal(0, tracker.Current.TotalKeyPresses);
        Assert.Empty(tracker.Current.KeyPressCountByKey);
        Assert.Contains("FIRST_SOUND", tracker.Current.UnlockedAchievements);
        Assert.Equal(firstLaunch, tracker.Current.FirstLaunchAt);
    }

    [Fact]
    public void Stats_PersistAcrossReload()
    {
        var tracker = new UsageStatsTracker(_tempFile);
        tracker.RecordKeyPress(LogicalKey.A);
        tracker.EndSession();

        var reloaded = new UsageStatsTracker(_tempFile);
        Assert.Equal(1, reloaded.Current.TotalKeyPresses);
    }

    public void Dispose()
    {
        if (File.Exists(_tempFile)) File.Delete(_tempFile);
    }
}
