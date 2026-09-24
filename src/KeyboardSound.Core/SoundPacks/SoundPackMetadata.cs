namespace KeyboardSound.Core.SoundPacks;

/// <summary>
/// One entry of a pack.json's optional "sounds" array: curated, stable metadata for a single
/// sample. Providing this is how a pack gets rename-proof sound IDs and human-friendly display
/// names instead of the auto-generated fallback (see <see cref="SoundPackManager"/>).
/// </summary>
public sealed class SoundMetadata
{
    public string Id { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string Category { get; set; } = "";
    public string File { get; set; } = "";
}

/// <summary>
/// Contents of a soundpack's "pack.json". Deserialized directly, so every field needs a
/// harmless default — a pack.json missing optional fields must still load.
/// </summary>
public sealed class SoundPackMetadata
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "Unnamed Soundpack";
    public string Description { get; set; } = "";
    public string Author { get; set; } = "";
    public string Version { get; set; } = "1.0.0";

    /// <summary>Optional curated per-sound metadata. Null/empty means every sample in this pack
    /// gets an auto-generated id/display name from its file location instead.</summary>
    public List<SoundMetadata>? Sounds { get; set; }
}
