namespace KeyboardSound.Core.SoundPacks;

/// <summary>
/// Picks which sample to play for a given category out of a soundpack's available samples.
/// Generic over the sample type so this logic is testable without any real audio data
/// (tests use plain strings/ints as <typeparamref name="TSample"/>).
/// </summary>
public sealed class SampleSelector<TSample> where TSample : notnull
{
    private readonly Random _random;

    /// <summary>Remembers the last sample played per category so it can be avoided next time
    /// a category has more than one sample — keeps rapid typing from sounding mechanically
    /// repetitive.</summary>
    private readonly Dictionary<SoundCategory, TSample> _lastPlayed = new();

    public SampleSelector(Random? random = null)
    {
        _random = random ?? Random.Shared;
    }

    /// <summary>
    /// Resolves which category to actually pull a sample from, applying the
    /// "no dedicated samples -> fall back to Normal" rule, and picks a sample within it.
    /// Returns null if the pack has no samples at all for the resolved category or the
    /// Normal fallback.
    /// </summary>
    public TSample? SelectSample(
        SoundCategory requestedCategory,
        IReadOnlyDictionary<SoundCategory, IReadOnlyList<TSample>> samplesByCategory)
    {
        var category = ResolveAvailableCategory(requestedCategory, samplesByCategory);
        if (category is null)
            return default;

        var candidates = samplesByCategory[category.Value];
        return Pick(category.Value, candidates);
    }

    private static SoundCategory? ResolveAvailableCategory(
        SoundCategory requested,
        IReadOnlyDictionary<SoundCategory, IReadOnlyList<TSample>> samplesByCategory)
    {
        if (samplesByCategory.TryGetValue(requested, out var direct) && direct.Count > 0)
            return requested;

        if (requested != SoundCategory.Normal &&
            samplesByCategory.TryGetValue(SoundCategory.Normal, out var fallback) && fallback.Count > 0)
            return SoundCategory.Normal;

        return null;
    }

    private TSample Pick(SoundCategory category, IReadOnlyList<TSample> candidates)
    {
        if (candidates.Count == 1)
        {
            _lastPlayed[category] = candidates[0];
            return candidates[0];
        }

        TSample chosen;
        do
        {
            chosen = candidates[_random.Next(candidates.Count)];
        } while (candidates.Count > 1 &&
                 _lastPlayed.TryGetValue(category, out var last) &&
                 EqualityComparer<TSample>.Default.Equals(chosen, last));

        _lastPlayed[category] = chosen;
        return chosen;
    }
}
