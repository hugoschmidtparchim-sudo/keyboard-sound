namespace KeyboardSound.Core.SoundPacks;

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
}
