namespace KeyboardSound.Core.SoundPacks;

/// <summary>
/// A single playable sample with a stable identity, independent of its display name, file path,
/// or position in any list. <see cref="Id"/> is the only thing favorites, "last selected", or any
/// other persisted reference should ever store — never a name, a file path, or a list index,
/// since all three of those can legitimately change without the underlying sound changing.
/// </summary>
public sealed class Sound
{
    /// <summary>Stable, permanent identifier. Comes from the pack's curated metadata when a
    /// pack.json provides one; otherwise deterministically derived from
    /// (packId, category, file name) so it stays stable across discovery order and reordering,
    /// but note it does depend on the file name — a pack that wants true rename-proof IDs should
    /// declare them explicitly in pack.json.</summary>
    public required string Id { get; init; }

    public required string DisplayName { get; init; }

    public required SoundCategory Category { get; init; }

    public required string FilePath { get; init; }

    public required string PackId { get; init; }

    /// <summary>
    /// Optional fixed mapping to other sounds in the same pack, keyed by category: when this
    /// sound is the pinned/selected identity (<see cref="Settings.AppSettings.SelectedSoundId"/>),
    /// a key whose category appears here plays that specific linked sound instead of the
    /// category's normal random pool - e.g. selecting "Ultra Crisp" makes Escape always play
    /// the Escape sound that belongs to the Ultra Crisp style, never a random Escape sample from
    /// elsewhere in the pack. A category with no entry here (or when this is null) falls back to
    /// reusing this sound itself for that category - still fully deterministic, just without a
    /// dedicated per-category variant. See <see cref="Audio.IAudioEngine.Play"/>.
    /// </summary>
    public IReadOnlyDictionary<SoundCategory, string>? LinkedSoundIds { get; init; }
}
