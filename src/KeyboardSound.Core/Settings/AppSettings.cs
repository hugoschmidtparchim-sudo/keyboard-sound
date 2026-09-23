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

    public string ActiveSoundPackId { get; set; } = "";

    /// <summary>0.0 - 1.0</summary>
    public double Volume { get; set; } = 0.8;

    public KeyMode KeyMode { get; set; } = KeyMode.AllKeys;

    /// <summary>Only consulted when <see cref="KeyMode"/> is <see cref="Settings.KeyMode.CustomKeys"/>.</summary>
    public List<LogicalKey> EnabledKeys { get; set; } = new();

    public List<string> FavoriteSoundPackIds { get; set; } = new();

    public WidgetPosition? WidgetPosition { get; set; }

    public bool WidgetVisible { get; set; } = true;

    public bool SoundEnabled { get; set; } = true;

    public bool StartWithWindows { get; set; } = false;

    public bool DebugLogging { get; set; } = false;
}
