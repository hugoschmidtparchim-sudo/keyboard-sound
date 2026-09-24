using System.Text.Json;
using KeyboardSound.Core.Diagnostics;

namespace KeyboardSound.Core.SoundPacks;

/// <summary>
/// Discovers soundpacks on disk. A soundpack is a folder containing a "pack.json" and
/// subfolders named after <see cref="SoundCategory"/> values (case-insensitive), each holding
/// one or more ".wav" or ".ogg" samples. A malformed or incomplete pack is skipped with a
/// warning rather than failing discovery for every other pack — one broken folder must never
/// take the whole soundpack list down.
/// </summary>
public sealed class SoundPackManager
{
    private const string MetadataFileName = "pack.json";
    private static readonly string[] SupportedExtensions = { ".wav", ".ogg" };

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public IReadOnlyList<SoundPackInfo> Discover(string rootDirectory)
    {
        var results = new List<SoundPackInfo>();

        if (!Directory.Exists(rootDirectory))
        {
            Log.Warn($"Soundpack directory '{rootDirectory}' does not exist.");
            return results;
        }

        foreach (var packDir in Directory.EnumerateDirectories(rootDirectory))
        {
            var pack = TryLoadPack(packDir);
            if (pack is not null)
                results.Add(pack);
        }

        return results;
    }

    /// <summary>
    /// Discovers across several root directories (e.g. built-in packs shipped with the app,
    /// plus a folder for packs the user adds later) and merges the results. If two roots
    /// contain a pack with the same id, the one from the earlier root wins.
    /// </summary>
    public IReadOnlyList<SoundPackInfo> DiscoverAll(IEnumerable<string> rootDirectories)
    {
        var byId = new Dictionary<string, SoundPackInfo>(StringComparer.OrdinalIgnoreCase);
        foreach (var root in rootDirectories)
        {
            foreach (var pack in Discover(root))
            {
                if (!byId.ContainsKey(pack.Id))
                    byId[pack.Id] = pack;
            }
        }
        return byId.Values.ToList();
    }

    private static SoundPackInfo? TryLoadPack(string packDir)
    {
        var metadataPath = Path.Combine(packDir, MetadataFileName);
        var folderId = Path.GetFileName(packDir);

        SoundPackMetadata metadata;
        if (File.Exists(metadataPath))
        {
            try
            {
                var json = File.ReadAllText(metadataPath);
                metadata = JsonSerializer.Deserialize<SoundPackMetadata>(json, JsonOptions)
                           ?? new SoundPackMetadata();
            }
            catch (Exception ex)
            {
                Log.Warn($"Soundpack '{folderId}' has an invalid pack.json ({ex.Message}). Using folder name as fallback metadata.");
                metadata = new SoundPackMetadata();
            }
        }
        else
        {
            Log.Warn($"Soundpack '{folderId}' has no pack.json. Using folder name as fallback metadata.");
            metadata = new SoundPackMetadata();
        }

        if (string.IsNullOrWhiteSpace(metadata.Id))
            metadata.Id = folderId;
        if (string.IsNullOrWhiteSpace(metadata.Name) || metadata.Name == "Unnamed Soundpack")
            metadata.Name = folderId;

        var samplesByCategory = DiscoverSamples(packDir);
        if (samplesByCategory.Count == 0)
        {
            Log.Warn($"Soundpack '{folderId}' contains no usable samples. Skipping.");
            return null;
        }

        return new SoundPackInfo
        {
            Id = metadata.Id,
            RootPath = packDir,
            Metadata = metadata,
            SamplePaths = samplesByCategory
        };
    }

    private static Dictionary<SoundCategory, IReadOnlyList<string>> DiscoverSamples(string packDir)
    {
        var result = new Dictionary<SoundCategory, IReadOnlyList<string>>();

        foreach (var categoryDir in Directory.EnumerateDirectories(packDir))
        {
            var folderName = Path.GetFileName(categoryDir);
            if (!Enum.TryParse<SoundCategory>(folderName, ignoreCase: true, out var category))
                continue;

            var files = Directory.EnumerateFiles(categoryDir)
                .Where(f => SupportedExtensions.Contains(Path.GetExtension(f), StringComparer.OrdinalIgnoreCase))
                .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (files.Count > 0)
                result[category] = files;
        }

        return result;
    }
}
