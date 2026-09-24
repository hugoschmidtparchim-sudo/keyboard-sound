using KeyboardSound.Core.Persistence;

namespace KeyboardSound.Core.Settings;

/// <summary>
/// Owns the single <see cref="AppSettings"/> instance for the process: loads it at startup
/// (repairing anything out of range), and persists it on demand. Callers mutate the object
/// returned by <see cref="Current"/> and then call <see cref="Save"/> — there is deliberately
/// no diffing/dirty-tracking machinery here, this is a small app with infrequent settings writes.
/// </summary>
public sealed class SettingsService
{
    private readonly JsonFileStore<AppSettings> _store;

    public AppSettings Current { get; private set; }

    public event Action<AppSettings>? Saved;

    public SettingsService(string filePath)
    {
        _store = new JsonFileStore<AppSettings>(filePath);
        Current = _store.Load(() => new AppSettings());
        Repair(Current);
    }

    public void Save()
    {
        Repair(Current);
        _store.Save(Current);
        Saved?.Invoke(Current);
    }

    /// <summary>
    /// Clamps/normalizes values that could otherwise put the app in a broken state
    /// (e.g. a hand-edited or partially-written config file).
    /// </summary>
    private static void Repair(AppSettings settings)
    {
        settings.Volume = Math.Clamp(settings.Volume, 0.0, 1.0);
        settings.EnabledKeys ??= new();
        settings.FavoriteSoundPackIds ??= new();
        settings.FavoriteSoundIds ??= new();
        settings.ActiveSoundPackId ??= "";

        // Dedupe defensively (e.g. a hand-edited config, or a future bug elsewhere) - favoriting
        // the same sound twice must never produce two entries.
        DeduplicateInPlace(settings.FavoriteSoundPackIds);
        DeduplicateInPlace(settings.FavoriteSoundIds);
    }

    private static void DeduplicateInPlace(List<string> ids)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        ids.RemoveAll(id => !seen.Add(id));
    }
}
