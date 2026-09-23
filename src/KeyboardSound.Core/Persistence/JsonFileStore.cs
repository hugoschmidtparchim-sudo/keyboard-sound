using System.Text.Json;
using KeyboardSound.Core.Diagnostics;

namespace KeyboardSound.Core.Persistence;

/// <summary>
/// Generic, crash-safe JSON persistence for a single settings-like object.
/// Writes go to a temp file and are then swapped in atomically, so a crash or power loss
/// mid-write can never leave a half-written, unparsable file behind. If the existing file on
/// disk is corrupt or unreadable, <see cref="Load"/> backs it up and returns a fresh default
/// instead of throwing — the app must never fail to start because of a bad config file.
/// </summary>
public sealed class JsonFileStore<T> where T : class, new()
{
    private static readonly JsonSerializerOptions Options = CreateOptions();

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.Never
        };
        // Enums as names, not numbers: keeps the file human-readable and resilient to
        // reordering entries in an enum during future development.
        options.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
        return options;
    }

    private readonly string _filePath;

    public JsonFileStore(string filePath)
    {
        _filePath = filePath;
    }

    public T Load(Func<T> createDefault)
    {
        if (!File.Exists(_filePath))
            return createDefault();

        try
        {
            var json = File.ReadAllText(_filePath);
            var value = JsonSerializer.Deserialize<T>(json, Options);
            if (value is not null)
                return value;

            Log.Warn($"Config file '{_filePath}' deserialized to null. Using defaults.");
        }
        catch (Exception ex)
        {
            Log.Error($"Config file '{_filePath}' is corrupt or unreadable. Using defaults.", ex);
            TryBackupCorruptFile();
        }

        return createDefault();
    }

    public void Save(T value)
    {
        try
        {
            var dir = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            var json = JsonSerializer.Serialize(value, Options);
            var tempPath = _filePath + ".tmp";
            File.WriteAllText(tempPath, json);
            File.Move(tempPath, _filePath, overwrite: true);
        }
        catch (Exception ex)
        {
            // Persistence failures must not crash the app; the in-memory state remains
            // authoritative for the rest of the session.
            Log.Error($"Failed to save config file '{_filePath}'.", ex);
        }
    }

    private void TryBackupCorruptFile()
    {
        try
        {
            var backupPath = _filePath + $".corrupt-{DateTime.Now:yyyyMMddHHmmss}.bak";
            File.Copy(_filePath, backupPath, overwrite: true);
        }
        catch
        {
            // Best-effort only.
        }
    }
}
