using KeyboardSound.Core.SoundPacks;

namespace KeyboardSound.Tests;

public class SampleSelectorTests
{
    [Fact]
    public void SelectSample_ReturnsNull_WhenCategoryAndNormalBothMissing()
    {
        var selector = new SampleSelector<string>();
        var samples = new Dictionary<SoundCategory, IReadOnlyList<string>>();

        var result = selector.SelectSample(SoundCategory.Space, samples);

        Assert.Null(result);
    }

    [Fact]
    public void SelectSample_FallsBackToNormal_WhenCategoryHasNoSamples()
    {
        var selector = new SampleSelector<string>();
        var samples = new Dictionary<SoundCategory, IReadOnlyList<string>>
        {
            [SoundCategory.Normal] = new List<string> { "n1" }
        };

        var result = selector.SelectSample(SoundCategory.Space, samples);

        Assert.Equal("n1", result);
    }

    [Fact]
    public void SelectSample_PrefersDirectCategory_OverNormal()
    {
        var selector = new SampleSelector<string>();
        var samples = new Dictionary<SoundCategory, IReadOnlyList<string>>
        {
            [SoundCategory.Normal] = new List<string> { "n1" },
            [SoundCategory.Space] = new List<string> { "s1" }
        };

        var result = selector.SelectSample(SoundCategory.Space, samples);

        Assert.Equal("s1", result);
    }

    [Fact]
    public void SelectSample_SingleCandidate_AlwaysReturnsIt()
    {
        var selector = new SampleSelector<string>();
        var samples = new Dictionary<SoundCategory, IReadOnlyList<string>>
        {
            [SoundCategory.Normal] = new List<string> { "only" }
        };

        for (var i = 0; i < 5; i++)
            Assert.Equal("only", selector.SelectSample(SoundCategory.Normal, samples));
    }

    [Fact]
    public void SelectSample_MultipleCandidates_NeverRepeatsImmediately()
    {
        var selector = new SampleSelector<string>(new Random(42));
        var samples = new Dictionary<SoundCategory, IReadOnlyList<string>>
        {
            [SoundCategory.Normal] = new List<string> { "a", "b", "c", "d" }
        };

        string? previous = null;
        for (var i = 0; i < 100; i++)
        {
            var chosen = selector.SelectSample(SoundCategory.Normal, samples);
            Assert.NotNull(chosen);
            if (previous is not null)
                Assert.NotEqual(previous, chosen);
            previous = chosen;
        }
    }

    [Fact]
    public void SelectSample_MultipleCandidates_EventuallyUsesAllOfThem()
    {
        var selector = new SampleSelector<string>(new Random(1));
        var samples = new Dictionary<SoundCategory, IReadOnlyList<string>>
        {
            [SoundCategory.Normal] = new List<string> { "a", "b", "c" }
        };

        var seen = new HashSet<string>();
        for (var i = 0; i < 50; i++)
            seen.Add(selector.SelectSample(SoundCategory.Normal, samples)!);

        Assert.Equal(3, seen.Count);
    }
}
