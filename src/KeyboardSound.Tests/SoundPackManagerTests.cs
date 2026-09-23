using KeyboardSound.Core.SoundPacks;

namespace KeyboardSound.Tests;

public class SoundPackManagerTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"ks-packs-{Guid.NewGuid():N}");

    public SoundPackManagerTests()
    {
        Directory.CreateDirectory(_root);
    }

    [Fact]
    public void Discover_FindsValidPack_WithMetadataAndSamples()
    {
        var packDir = CreatePack("creamy", writeMetadata: true);
        WriteSample(packDir, "normal", "click1.wav");
        WriteSample(packDir, "normal", "click2.wav");
        WriteSample(packDir, "space", "thock.wav");

        var packs = new SoundPackManager().Discover(_root);

        var pack = Assert.Single(packs);
        Assert.Equal("creamy", pack.Id);
        Assert.Equal("Creamy", pack.Metadata.Name);
        Assert.Equal(2, pack.SamplePaths[SoundCategory.Normal].Count);
        Assert.Single(pack.SamplePaths[SoundCategory.Space]);
    }

    [Fact]
    public void Discover_PackWithoutMetadata_FallsBackToFolderName()
    {
        var packDir = CreatePack("nometa", writeMetadata: false);
        WriteSample(packDir, "normal", "a.wav");

        var packs = new SoundPackManager().Discover(_root);

        var pack = Assert.Single(packs);
        Assert.Equal("nometa", pack.Id);
        Assert.Equal("nometa", pack.Metadata.Name);
    }

    [Fact]
    public void Discover_PackWithCorruptMetadata_StillLoadsSamples()
    {
        var packDir = CreatePack("broken", writeMetadata: false);
        File.WriteAllText(Path.Combine(packDir, "pack.json"), "{ not json ]]");
        WriteSample(packDir, "normal", "a.wav");

        var packs = new SoundPackManager().Discover(_root);

        var pack = Assert.Single(packs);
        Assert.Equal("broken", pack.Id);
    }

    [Fact]
    public void Discover_PackWithNoSamples_IsSkipped()
    {
        CreatePack("empty", writeMetadata: true);

        var packs = new SoundPackManager().Discover(_root);

        Assert.Empty(packs);
    }

    [Fact]
    public void Discover_UnknownCategoryFolder_IsIgnored()
    {
        var packDir = CreatePack("pack", writeMetadata: true);
        WriteSample(packDir, "normal", "a.wav");
        WriteSample(packDir, "notarealcategory", "b.wav");

        var packs = new SoundPackManager().Discover(_root);

        var pack = Assert.Single(packs);
        Assert.Single(pack.SamplePaths); // only "normal" recognized
    }

    [Fact]
    public void Discover_NonexistentRoot_ReturnsEmptyWithoutThrowing()
    {
        var packs = new SoundPackManager().Discover(Path.Combine(_root, "does-not-exist"));

        Assert.Empty(packs);
    }

    private string CreatePack(string id, bool writeMetadata)
    {
        var dir = Path.Combine(_root, id);
        Directory.CreateDirectory(dir);
        if (writeMetadata)
        {
            var name = char.ToUpperInvariant(id[0]) + id[1..];
            File.WriteAllText(Path.Combine(dir, "pack.json"),
                $"{{ \"id\": \"{id}\", \"name\": \"{name}\", \"author\": \"Test\", \"version\": \"1.0.0\" }}");
        }
        return dir;
    }

    private static void WriteSample(string packDir, string category, string fileName)
    {
        var categoryDir = Path.Combine(packDir, category);
        Directory.CreateDirectory(categoryDir);
        // Content doesn't matter for discovery — only decoded when a pack is activated.
        File.WriteAllBytes(Path.Combine(categoryDir, fileName), new byte[] { 0x00 });
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }
}
