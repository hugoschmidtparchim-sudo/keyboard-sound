namespace KeyboardSound.Core.Stats;

/// <summary>
/// Local-only usage counters, persisted separately from settings.json (see
/// <see cref="Configuration.AppPaths.StatsFilePath"/>). Every field is aggregate counts only -
/// never actual key contents, text, or anything from other programs.
/// </summary>
public sealed class UsageStats
{
    public long TotalKeyPresses { get; set; }

    /// <summary>Per-key press counts, keyed by <see cref="Input.LogicalKey"/> name - used only to
    /// report the single most-used key, never to reconstruct what was typed.</summary>
    public Dictionary<string, long> KeyPressCountByKey { get; set; } = new();

    public long TotalSessionSeconds { get; set; }

    public DateTime? FirstLaunchAt { get; set; }

    /// <summary>Names of internal achievement-readiness events already fired once (e.g.
    /// "FIRST_START", "10K_KEYPRESSES") - see <see cref="UsageStatsTracker"/>. Not wired to
    /// Steamworks yet; this just prevents an event from firing more than once whenever that
    /// integration is added later.</summary>
    public HashSet<string> UnlockedAchievements { get; set; } = new();
}
