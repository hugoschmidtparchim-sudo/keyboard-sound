using KeyboardSound.Core.SoundPacks;

namespace KeyboardSound.Tests;

public class PinnedSoundResolverTests
{
    private static Sound MakeSound(string id, SoundCategory category, IReadOnlyDictionary<SoundCategory, string>? links = null) =>
        new()
        {
            Id = id,
            DisplayName = id,
            Category = category,
            FilePath = $"{id}.wav",
            PackId = "test-pack",
            LinkedSoundIds = links
        };

    [Fact]
    public void SameCategoryAsPin_ReturnsPinItself()
    {
        var pinned = MakeSound("ultra_crisp_01", SoundCategory.Normal);

        var result = PinnedSoundResolver.ResolveTargetId(pinned, SoundCategory.Normal);

        Assert.Equal("ultra_crisp_01", result);
    }

    // Note: Escape/CapsLock/Windows/Arrow keys all currently map to SoundCategory.Other via
    // KeyCategoryMap (no dedicated category exists for each of them yet) - these tests use
    // Other as the stand-in for "some special key category" for that reason.

    [Fact]
    public void DifferentCategory_WithLinkedSound_ReturnsLinkedId()
    {
        var links = new Dictionary<SoundCategory, string> { [SoundCategory.Other] = "ultra_crisp_escape_01" };
        var pinned = MakeSound("ultra_crisp_01", SoundCategory.Normal, links);

        var result = PinnedSoundResolver.ResolveTargetId(pinned, SoundCategory.Other);

        Assert.Equal("ultra_crisp_escape_01", result);
    }

    [Fact]
    public void DifferentCategory_NoLinkedSound_ReturnsPinItselfDeterministically()
    {
        // No dedicated Escape/CapsLock/etc. variant exists for this style - must still
        // deterministically reuse the pinned sound rather than falling back to that
        // category's random pool.
        var pinned = MakeSound("ultra_crisp_01", SoundCategory.Normal, links: null);

        var result = PinnedSoundResolver.ResolveTargetId(pinned, SoundCategory.Other);

        Assert.Equal("ultra_crisp_01", result);
    }

    [Fact]
    public void DifferentCategory_LinksExistButNotForThisCategory_ReturnsPinItself()
    {
        var links = new Dictionary<SoundCategory, string> { [SoundCategory.Enter] = "ultra_crisp_enter_01" };
        var pinned = MakeSound("ultra_crisp_01", SoundCategory.Normal, links);

        var result = PinnedSoundResolver.ResolveTargetId(pinned, SoundCategory.Other);

        Assert.Equal("ultra_crisp_01", result);
    }

    [Fact]
    public void Resolution_IsRepeatable_ForSameInputs()
    {
        // Ten "presses" of the same key while the same sound is pinned must all resolve
        // identically - this is the core "no randomness for special keys" guarantee.
        var links = new Dictionary<SoundCategory, string> { [SoundCategory.Other] = "style_a_escape" };
        var pinned = MakeSound("style_a_normal", SoundCategory.Normal, links);

        var results = Enumerable.Range(0, 10)
            .Select(_ => PinnedSoundResolver.ResolveTargetId(pinned, SoundCategory.Other))
            .Distinct()
            .ToList();

        Assert.Single(results);
        Assert.Equal("style_a_escape", results[0]);
    }

    [Fact]
    public void SwitchingPin_ChangesResolvedTarget()
    {
        var linksA = new Dictionary<SoundCategory, string> { [SoundCategory.Other] = "style_a_escape" };
        var linksB = new Dictionary<SoundCategory, string> { [SoundCategory.Other] = "style_b_escape" };
        var pinnedA = MakeSound("style_a_normal", SoundCategory.Normal, linksA);
        var pinnedB = MakeSound("style_b_normal", SoundCategory.Normal, linksB);

        var resultA = PinnedSoundResolver.ResolveTargetId(pinnedA, SoundCategory.Other);
        var resultB = PinnedSoundResolver.ResolveTargetId(pinnedB, SoundCategory.Other);

        Assert.Equal("style_a_escape", resultA);
        Assert.Equal("style_b_escape", resultB);
        Assert.NotEqual(resultA, resultB);
    }
}
