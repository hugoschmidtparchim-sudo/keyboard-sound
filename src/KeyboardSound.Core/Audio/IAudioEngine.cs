using KeyboardSound.Core.SoundPacks;

namespace KeyboardSound.Core.Audio;

/// <summary>
/// Playback abstraction. The Input layer never talks to this directly — it goes through
/// <c>Routing.InputRouter</c> — and nothing outside this interface knows NAudio exists,
/// so the playback backend could be swapped later without touching input or soundpack code.
/// </summary>
public interface IAudioEngine : IDisposable
{
    /// <summary>0.0 - 1.0</summary>
    double Volume { get; set; }

    /// <summary>
    /// Decodes and loads every sample of the given pack into memory, replacing whatever pack
    /// was previously active. Previously loaded samples are released.
    /// </summary>
    void LoadPack(SoundPackInfo pack);

    /// <summary>Plays one sample for the given category (fire-and-forget, overlapping with
    /// any currently playing sounds). If <paramref name="preferredSoundId"/> names a sound
    /// loaded for this category, that exact sound is used deterministically (the "pin a sound"
    /// feature); otherwise a sample is picked from the category's pool (falling back to Normal,
    /// avoiding immediate repeats). No-op if no pack is loaded or the category resolves to no
    /// samples.</summary>
    void Play(SoundCategory category, string? preferredSoundId = null);
}
