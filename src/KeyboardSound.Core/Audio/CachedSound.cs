using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace KeyboardSound.Core.Audio;

/// <summary>
/// A sample fully decoded into memory once at load time, converted to the mixer's canonical
/// format (see <see cref="NAudioEngine.CanonicalFormat"/>). Playing a sound never touches disk —
/// it just wraps this shared float array in a fresh <see cref="CachedSoundSampleProvider"/> and
/// hands it to the mixer, so many overlapping plays of the same sample cost only a small object,
/// not a decode or a copy of the audio data.
/// </summary>
public sealed class CachedSound
{
    public float[] AudioData { get; }
    public WaveFormat WaveFormat { get; }

    private CachedSound(float[] audioData, WaveFormat waveFormat)
    {
        AudioData = audioData;
        WaveFormat = waveFormat;
    }

    public static CachedSound Load(string filePath, WaveFormat canonicalFormat)
    {
        using var reader = new AudioFileReader(filePath);

        ISampleProvider source = reader;
        if (source.WaveFormat.Channels == 1 && canonicalFormat.Channels == 2)
            source = new MonoToStereoSampleProvider(source);
        else if (source.WaveFormat.Channels == 2 && canonicalFormat.Channels == 1)
            source = new StereoToMonoSampleProvider(source);

        if (source.WaveFormat.SampleRate != canonicalFormat.SampleRate)
            source = new WdlResamplingSampleProvider(source, canonicalFormat.SampleRate);

        var buffer = new List<float>((int)(reader.Length / 2));
        var readBuffer = new float[canonicalFormat.SampleRate * canonicalFormat.Channels];
        int samplesRead;
        while ((samplesRead = source.Read(readBuffer, 0, readBuffer.Length)) > 0)
        {
            buffer.AddRange(new ArraySegment<float>(readBuffer, 0, samplesRead));
        }

        return new CachedSound(buffer.ToArray(), canonicalFormat);
    }
}

/// <summary>Lightweight per-playback cursor over a shared <see cref="CachedSound"/>'s data.</summary>
public sealed class CachedSoundSampleProvider : ISampleProvider
{
    private readonly CachedSound _cachedSound;
    private long _position;

    public CachedSoundSampleProvider(CachedSound cachedSound)
    {
        _cachedSound = cachedSound;
    }

    public int Read(float[] buffer, int offset, int count)
    {
        var available = _cachedSound.AudioData.Length - _position;
        var samplesToCopy = Math.Min(available, count);
        Array.Copy(_cachedSound.AudioData, _position, buffer, offset, samplesToCopy);
        _position += samplesToCopy;
        return (int)samplesToCopy;
    }

    public WaveFormat WaveFormat => _cachedSound.WaveFormat;
}
