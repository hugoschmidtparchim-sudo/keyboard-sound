using System.Text.Json;
using KeyboardSound.Core.Diagnostics;

namespace KeyboardSound.Core.SoundPacks;

/// <summary>
/// Discovers soundpacks on disk. A soundpack is a folder containing a "pack.json" and
/// subfolders named after <see cref="SoundCategory"/> values (case-insensitive), each holding
/// one or more ".wav" or ".ogg" samples. A malformed or incomplete pack is skipped with a
/// warning rather than failing discovery for every other pack — one broken folder must never
/// take the whole soundpack list down.
///
/// Every discovered sample becomes a <see cref="Sound"/> with a stable id: if pack.json declares
/// curated metadata for it (matched by relative file path), that id/display name is used as-is
/// - stable even if the file is later renamed. Otherwise an id is auto-derived from
/// (packId, category, file name), which is stable across reordering and new/removed sounds but
/// not across a file rename.
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

        var sounds = DiscoverSounds(packDir, metadata);
        if (sounds.Count == 0)
        {
            Log.Warn($"Soundpack '{folderId}' contains no usable samples. Skipping.");
            return null;
        }

        return new SoundPackInfo
        {
            Id = metadata.Id,
            RootPath = packDir,
            Metadata = metadata,
            Sounds = sounds
        };
    }

    private static List<Sound> DiscoverSounds(string packDir, SoundPackMetadata metadata)
    {
        // Curated entries are matched by relative path so they survive everything except an
        // actual file rename (see Sound.Id remarks).
        var curatedByRelativePath = new Dictionary<string, SoundMetadata>(StringComparer.OrdinalIgnoreCase);
        foreach (var curated in metadata.Sounds ?? Enumerable.Empty<SoundMetadata>())
        {
            if (!string.IsNullOrWhiteSpace(curated.File))
                curatedByRelativePath[NormalizeRelativePath(curated.File)] = curated;
        }
        var matchedCuratedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var result = new List<Sound>();

        foreach (var categoryDir in Directory.EnumerateDirectories(packDir))
        {
            var folderName = Path.GetFileName(categoryDir);
            if (!Enum.TryParse<SoundCategory>(folderName, ignoreCase: true, out var folderCategory))
                continue;

            var files = Directory.EnumerateFiles(categoryDir)
                .Where(f => SupportedExtensions.Contains(Path.GetExtension(f), StringComparer.OrdinalIgnoreCase))
                .OrderBy(f => f, StringComparer.OrdinalIgnoreCase);

            foreach (var filePath in files)
            {
                var relativePath = NormalizeRelativePath(Path.GetRelativePath(packDir, filePath));

                if (curatedByRelativePath.TryGetValue(relativePath, out var curated))
                {
                    matchedCuratedPaths.Add(relativePath);
                    var category = Enum.TryParse<SoundCategory>(curated.Category, ignoreCase: true, out var c)
                        ? c
                        : folderCategory;
                    var id = string.IsNullOrWhiteSpace(curated.Id) ? AutoId(metadata.Id, category, filePath) : curated.Id;
                    var name = string.IsNullOrWhiteSpace(curated.DisplayName) ? AutoDisplayName(filePath) : curated.DisplayName;
                    result.Add(new Sound
                    {
                        Id = id,
                        DisplayName = name,
                        Category = category,
                        FilePath = filePath,
                        PackId = metadata.Id,
                        LinkedSoundIds = ParseLinkedSounds(curated.LinkedSounds, metadata.Id, id)
                    });
                }
                else
                {
                    result.Add(new Sound
                    {
                        Id = AutoId(metadata.Id, folderCategory, filePath),
                        DisplayName = AutoDisplayName(filePath),
                        Category = folderCategory,
                        FilePath = filePath,
                        PackId = metadata.Id
                    });
                }
            }
        }

        foreach (var unmatched in curatedByRelativePath.Keys.Except(matchedCuratedPaths, StringComparer.OrdinalIgnoreCase))
            Log.Warn($"Soundpack '{metadata.Id}': pack.json references sound file '{unmatched}' which was not found on disk. Ignoring that entry.");

        return result;
    }

    private static IReadOnlyDictionary<SoundCategory, string>? ParseLinkedSounds(
        Dictionary<string, string>? raw, string packId, string ownerSoundId)
    {
        if (raw is null || raw.Count == 0) return null;

        var result = new Dictionary<SoundCategory, string>();
        foreach (var (categoryName, targetId) in raw)
        {
            if (!Enum.TryParse<SoundCategory>(categoryName, ignoreCase: true, out var category))
            {
                Log.Warn($"Soundpack '{packId}': sound '{ownerSoundId}' has a linkedSounds entry for unknown category '{categoryName}'. Ignoring.");
                continue;
            }
            if (string.IsNullOrWhiteSpace(targetId))
                continue;
            result[category] = targetId;
        }
        return result.Count > 0 ? result : null;
    }

    private static string NormalizeRelativePath(string path) =>
        path.Replace('\\', '/').TrimStart('/');

    private static string AutoId(string packId, SoundCategory category, string filePath)
    {
        var stem = Path.GetFileNameWithoutExtension(filePath);
        var sanitized = new string(stem.Select(ch => char.IsLetterOrDigit(ch) ? char.ToLowerInvariant(ch) : '_').ToArray());
        return $"{packId}_{category.ToString().ToLowerInvariant()}_{sanitized}";
    }

    private static string AutoDisplayName(string filePath)
    {
        var stem = Path.GetFileNameWithoutExtension(filePath);
        var withSpaces = stem.Replace('_', ' ').Replace('-', ' ');
        return System.Globalization.CultureInfo.InvariantCulture.TextInfo.ToTitleCase(withSpaces);
    }
}
