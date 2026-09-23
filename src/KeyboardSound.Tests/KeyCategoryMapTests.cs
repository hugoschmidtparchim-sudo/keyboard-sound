using KeyboardSound.Core.Input;
using KeyboardSound.Core.SoundPacks;

namespace KeyboardSound.Tests;

public class KeyCategoryMapTests
{
    [Theory]
    [InlineData(LogicalKey.A, SoundCategory.Normal)]
    [InlineData(LogicalKey.Z, SoundCategory.Normal)]
    [InlineData(LogicalKey.D5, SoundCategory.Normal)]
    [InlineData(LogicalKey.NumPad3, SoundCategory.Normal)]
    [InlineData(LogicalKey.Space, SoundCategory.Space)]
    [InlineData(LogicalKey.Enter, SoundCategory.Enter)]
    [InlineData(LogicalKey.NumPadEnter, SoundCategory.Enter)]
    [InlineData(LogicalKey.Backspace, SoundCategory.Backspace)]
    [InlineData(LogicalKey.ShiftLeft, SoundCategory.Shift)]
    [InlineData(LogicalKey.ShiftRight, SoundCategory.Shift)]
    [InlineData(LogicalKey.CtrlLeft, SoundCategory.Ctrl)]
    [InlineData(LogicalKey.AltRight, SoundCategory.Alt)]
    [InlineData(LogicalKey.Tab, SoundCategory.Tab)]
    [InlineData(LogicalKey.F5, SoundCategory.Other)]
    [InlineData(LogicalKey.ArrowUp, SoundCategory.Other)]
    [InlineData(LogicalKey.WindowsLeft, SoundCategory.Other)]
    public void Resolve_MapsExpectedCategory(LogicalKey key, SoundCategory expected)
    {
        Assert.Equal(expected, KeyCategoryMap.Resolve(key));
    }
}
