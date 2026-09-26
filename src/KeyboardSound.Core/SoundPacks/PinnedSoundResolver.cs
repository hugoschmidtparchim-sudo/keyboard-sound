namespace KeyboardSound.Core.SoundPacks;

/// <summary>
/// Pure decision logic for what "pin this sound" means for a given category - separated from
/// <c>Audio.NAudioEngine</c> so it's testable without any real audio device or decoded sample.
/// </summary>
public static class PinnedSoundResolver
{
    /// <summary>
    /// Resolves which sound id should play for <paramref name="category"/> when
    /// <paramref name="pinned"/> is the user's selected/pinned sound. Always returns an id -
    /// never "give up and randomize" - matching the requirement that selecting a sound fixes
    /// every key's sound, not just its own category's:
    /// <list type="bullet">
    /// <item>if the category matches the pinned sound's own category, its own id;</item>
    /// <item>else if the pinned sound declares a linked sound for this category, that id;</item>
    /// <item>else the pinned sound's own id again (deterministic reuse, no dedicated variant).</item>
    /// </list>
    /// </summary>
    public static string ResolveTargetId(Sound pinned, SoundCategory category)
    {
        if (pinned.Category == category)
            return pinned.Id;

        if (pinned.LinkedSoundIds is not null && pinned.LinkedSoundIds.TryGetValue(category, out var linkedId))
            return linkedId;

        return pinned.Id;
    }
}
