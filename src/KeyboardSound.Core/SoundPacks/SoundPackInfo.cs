namespace KeyboardSound.Core.SoundPacks;

/// <summary>
/// Result of discovering a soundpack on disk: metadata plus which sample files exist per
/// category. Deliberately holds file paths, not decoded audio — discovery must stay cheap
/// even with many installed packs. Decoding happens only for the pack the user activates
/// (see the Audio layer).
/// </summary>
public sealed class SoundPackInfo
{
    public required string Id { get; init; }
    public required string RootPath { get; init; }
    public required SoundPackMetadata Metadata { get; init; }
    public required IReadOnlyDictionary<SoundCategory, IReadOnlyList<string>> SamplePaths { get; init; }

    public int TotalSampleCount => SamplePaths.Values.Sum(list => list.Count);
}
