namespace KeyboardSound.Core.SoundPacks;

/// <summary>
/// Result of discovering a soundpack on disk: metadata plus every sample it contains, each as a
/// <see cref="Sound"/> with a stable id. Deliberately holds file paths, not decoded audio -
/// discovery must stay cheap even with many installed packs. Decoding happens only for the pack
/// the user activates (see the Audio layer).
/// </summary>
public sealed class SoundPackInfo
{
    public required string Id { get; init; }
    public required string RootPath { get; init; }
    public required SoundPackMetadata Metadata { get; init; }
    public required IReadOnlyList<Sound> Sounds { get; init; }

    public int TotalSampleCount => Sounds.Count;

    private Dictionary<SoundCategory, IReadOnlyList<Sound>>? _byCategory;

    /// <summary>Sounds grouped by category, computed once and cached - discovery/activation
    /// happen at startup or on an explicit pack switch, never per keypress.</summary>
    public IReadOnlyDictionary<SoundCategory, IReadOnlyList<Sound>> SoundsByCategory =>
        _byCategory ??= Sounds
            .GroupBy(s => s.Category)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<Sound>)g.ToList());
}
