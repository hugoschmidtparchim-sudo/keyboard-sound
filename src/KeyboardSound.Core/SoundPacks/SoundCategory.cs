namespace KeyboardSound.Core.SoundPacks;

/// <summary>
/// Logical grouping of keys that a soundpack can provide distinct samples for.
/// A pack doesn't need every category — <see cref="SampleSelector"/> falls back to
/// <see cref="Normal"/> when a category has no samples.
/// </summary>
public enum SoundCategory
{
    Normal,
    Space,
    Enter,
    Backspace,
    Shift,
    Ctrl,
    Alt,
    Tab,
    Other
}
