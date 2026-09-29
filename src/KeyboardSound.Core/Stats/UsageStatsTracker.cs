using KeyboardSound.Core.Diagnostics;
using KeyboardSound.Core.Input;
using KeyboardSound.Core.Persistence;

namespace KeyboardSound.Core.Stats;

/// <summary>
/// Tracks local-only usage counters and fires internal "achievement-readiness" events (see
/// <see cref="UsageStats.UnlockedAchievements"/>) - no Steamworks call exists yet, this only
/// prepares the internal event names so that integration can subscribe later without any of
/// this needing to change. Everything here is an in-memory increment; disk writes are batched
/// the same way <see cref="Log"/> batches its writes, so the keypress hot path never does file
/// I/O on every key - see <see cref="RecordKeyPress"/>.
/// </summary>
public sealed class UsageStatsTracker
{
    private const int FlushEveryNPresses = 50;

    private readonly JsonFileStore<UsageStats> _store;
    private readonly DateTime _sessionStart = DateTime.UtcNow;
    private int _pressesSinceFlush;

    public UsageStats Current { get; }

    /// <summary>Fires once per achievement name, the moment it is first unlocked.</summary>
    public event Action<string>? AchievementUnlocked;

    public UsageStatsTracker(string filePath)
    {
        _store = new JsonFileStore<UsageStats>(filePath);
        Current = _store.Load(() => new UsageStats());

        if (Current.FirstLaunchAt is null)
        {
            Current.FirstLaunchAt = DateTime.UtcNow;
            Unlock("FIRST_START");
        }
    }

    /// <summary>Cheap in-memory increment only - safe to call on every keypress. Flushes to disk
    /// at most once every <see cref="FlushEveryNPresses"/> presses, never synchronously per key.</summary>
    public void RecordKeyPress(LogicalKey key)
    {
        Current.TotalKeyPresses++;
        var name = key.ToString();
        Current.KeyPressCountByKey[name] = Current.KeyPressCountByKey.GetValueOrDefault(name) + 1;

        if (Current.TotalKeyPresses == 10_000) Unlock("10K_KEYPRESSES");
        if (Current.TotalKeyPresses == 100_000) Unlock("100K_KEYPRESSES");

        if (++_pressesSinceFlush >= FlushEveryNPresses)
        {
            _pressesSinceFlush = 0;
            Flush();
        }
    }

    public void RecordSoundPlayed() => Unlock("FIRST_SOUND");

    public void RecordSoundpackImported() => Unlock("FIRST_IMPORTED_SOUNDPACK");

    /// <summary>Null if no key has been pressed yet this install.</summary>
    public string? MostUsedKey =>
        Current.KeyPressCountByKey.Count == 0
            ? null
            : Current.KeyPressCountByKey.OrderByDescending(kv => kv.Value).First().Key;

    /// <summary>Total tracked usage time including all previous sessions plus how long the
    /// current one has run so far.</summary>
    public TimeSpan TotalUsageTime =>
        TimeSpan.FromSeconds(Current.TotalSessionSeconds) + (DateTime.UtcNow - _sessionStart);

    public void Flush() => _store.Save(Current);

    /// <summary>Folds the current session's elapsed time into the persisted total and flushes -
    /// called once at app shutdown.</summary>
    public void EndSession()
    {
        Current.TotalSessionSeconds += (long)(DateTime.UtcNow - _sessionStart).TotalSeconds;
        Flush();
    }

    /// <summary>Clears usage counters only. Deliberately does not clear
    /// <see cref="UsageStats.FirstLaunchAt"/> or already-unlocked achievements - "reset
    /// statistics" means clear usage counts, not un-earn milestones already reached.</summary>
    public void Reset()
    {
        Current.TotalKeyPresses = 0;
        Current.KeyPressCountByKey.Clear();
        Current.TotalSessionSeconds = 0;
        Flush();
    }

    private void Unlock(string achievementName)
    {
        if (Current.UnlockedAchievements.Add(achievementName))
        {
            Log.Info($"Achievement-readiness event unlocked: {achievementName}");
            AchievementUnlocked?.Invoke(achievementName);
            Flush();
        }
    }
}
