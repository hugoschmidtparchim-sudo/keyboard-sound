using KeyboardSound.Core.Input;

namespace KeyboardSound.Core.SoundPacks;

/// <summary>Maps a <see cref="LogicalKey"/> to the <see cref="SoundCategory"/> a soundpack
/// would provide a dedicated sample for.</summary>
public static class KeyCategoryMap
{
    public static SoundCategory Resolve(LogicalKey key) => key switch
    {
        LogicalKey.Space => SoundCategory.Space,
        LogicalKey.Enter or LogicalKey.NumPadEnter => SoundCategory.Enter,
        LogicalKey.Backspace => SoundCategory.Backspace,
        LogicalKey.ShiftLeft or LogicalKey.ShiftRight => SoundCategory.Shift,
        LogicalKey.CtrlLeft or LogicalKey.CtrlRight => SoundCategory.Ctrl,
        LogicalKey.AltLeft or LogicalKey.AltRight => SoundCategory.Alt,
        LogicalKey.Tab => SoundCategory.Tab,

        >= LogicalKey.A and <= LogicalKey.Z => SoundCategory.Normal,
        >= LogicalKey.D0 and <= LogicalKey.D9 => SoundCategory.Normal,
        >= LogicalKey.NumPad0 and <= LogicalKey.NumPad9 => SoundCategory.Normal,

        LogicalKey.OemComma or LogicalKey.OemPeriod or LogicalKey.OemMinus or LogicalKey.OemPlus
            or LogicalKey.OemSemicolon or LogicalKey.OemQuestion or LogicalKey.OemTilde
            or LogicalKey.OemOpenBrackets or LogicalKey.OemCloseBrackets or LogicalKey.OemPipe
            or LogicalKey.OemQuotes or LogicalKey.OemBackslash => SoundCategory.Normal,

        LogicalKey.NumPadAdd or LogicalKey.NumPadSubtract or LogicalKey.NumPadMultiply
            or LogicalKey.NumPadDivide or LogicalKey.NumPadDecimal => SoundCategory.Normal,

        _ => SoundCategory.Other
    };
}
