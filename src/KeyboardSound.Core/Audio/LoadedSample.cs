using KeyboardSound.Core.SoundPacks;

namespace KeyboardSound.Core.Audio;

/// <summary>Pairs a sample's stable metadata with its decoded audio, so playback can both pick
/// a sound and know which stable id it just played (for "pin this exact sound" and future
/// last-played tracking) without re-deriving anything from a file path.</summary>
public readonly record struct LoadedSample(Sound Meta, CachedSound Audio);
