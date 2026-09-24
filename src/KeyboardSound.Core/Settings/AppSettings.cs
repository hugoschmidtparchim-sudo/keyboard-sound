using KeyboardSound.Core.Input;

namespace KeyboardSound.Core.Settings;

public sealed class WidgetPosition
{
    public double X { get; set; }
    public double Y { get; set; }
}

/// <summary>
/// The full set of user-configurable, persisted application state. Every field has a safe
/// default so a freshly created instance (first run, or recovery from a corrupt config file)
/// is immediately usable without extra null-checks elsewhere.
/// </summary>
public sealed class AppSettings
{
    public int SchemaVersion { get; set; } = 1;

    /// <summary>Empty means "no preference yet" — <c>ApplicationState</c> falls back to
    /// discovery order. The shipped default pack id is set here so a first run (no settings
    /// file yet) starts on the user's own curated pack rather than whichever pack happens to
    /// sort first on disk.</summary>
    public string ActiveSoundPackId { get; set; } = "curated-click";

    /// <summary>0.0 - 1.0</summary>
    public double Volume { get; set; } = 0.8;

    public KeyMode KeyMode { get; set; } = KeyMode.AllKeys;

    /// <summary>Only consulted when <see cref="KeyMode"/> is <see cref="Settings.KeyMode.CustomKeys"/>.</summary>
    public List<LogicalKey> EnabledKeys { get; set; } = new();

    /// <summary>Favorited whole soundpacks, by stable pack id (not display name or list
    /// position).</summary>
    public List<string> FavoriteSoundPackIds { get; set; } = new();

    /// <summary>Favorited individual sounds, by stable <see cref="SoundPacks.Sound.Id"/> - never
    /// by display name, file path, or list position, so favorites survive renames, reordering,
    /// and new sounds being added. Ids that no longer resolve to a loaded sound (removed file,
    /// uninstalled pack) are simply ignored wherever this list is consulted; they are not
    /// eagerly pruned, since the pack they belong to might just not be the active one right now.</summary>
    public List<string> FavoriteSoundIds { get; set; } = new();

    /// <summary>The individual sound the user last explicitly picked ("pin this exact sound"),
    /// by stable id. When set and the id resolves to a sound loaded for the category being
    /// played, playback uses that exact sound instead of the pool's normal rotation - see
    /// <see cref="Audio.IAudioEngine.Play"/>. Null means "no pin, use normal rotation for every
    /// category".</summary>
    public string? SelectedSoundId { get; set; }

    public WidgetPosition? WidgetPosition { get; set; }

    public bool WidgetVisible { get; set; } = true;

    public bool SoundEnabled { get; set; } = true;

    public bool StartWithWindows { get; set; } = false;

    public bool DebugLogging { get; set; } = false;
}
