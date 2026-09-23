namespace KeyboardSound.Core.Input;

/// <summary>
/// Maps Win32 virtual-key codes to the platform-independent <see cref="LogicalKey"/>.
/// This is the only place in the codebase that needs to know VK constants.
/// </summary>
public static class Win32KeyMap
{
    private static readonly Dictionary<int, LogicalKey> Map = BuildMap();

    public static LogicalKey ToLogicalKey(int virtualKeyCode) =>
        Map.TryGetValue(virtualKeyCode, out var key) ? key : LogicalKey.Other;

    private static Dictionary<int, LogicalKey> BuildMap()
    {
        var map = new Dictionary<int, LogicalKey>();

        // A-Z (0x41-0x5A), 0-9 (0x30-0x39) follow ASCII values on Win32.
        for (var c = 'A'; c <= 'Z'; c++)
            map[c] = Enum.Parse<LogicalKey>(c.ToString());
        for (var d = 0; d <= 9; d++)
            map[0x30 + d] = Enum.Parse<LogicalKey>("D" + d);

        for (var f = 1; f <= 12; f++)
            map[0x70 + (f - 1)] = Enum.Parse<LogicalKey>("F" + f); // VK_F1 = 0x70

        map[0x20] = LogicalKey.Space;
        map[0x0D] = LogicalKey.Enter;
        map[0x08] = LogicalKey.Backspace;
        map[0x09] = LogicalKey.Tab;
        map[0x1B] = LogicalKey.Escape;

        map[0xA0] = LogicalKey.ShiftLeft;   // VK_LSHIFT
        map[0xA1] = LogicalKey.ShiftRight;  // VK_RSHIFT
        map[0x10] = LogicalKey.ShiftLeft;   // VK_SHIFT (generic, LL hook usually gives L/R variant)
        map[0xA2] = LogicalKey.CtrlLeft;    // VK_LCONTROL
        map[0xA3] = LogicalKey.CtrlRight;   // VK_RCONTROL
        map[0x11] = LogicalKey.CtrlLeft;    // VK_CONTROL
        map[0xA4] = LogicalKey.AltLeft;     // VK_LMENU
        map[0xA5] = LogicalKey.AltRight;    // VK_RMENU
        map[0x12] = LogicalKey.AltLeft;     // VK_MENU
        map[0x5B] = LogicalKey.WindowsLeft; // VK_LWIN
        map[0x5C] = LogicalKey.WindowsRight;// VK_RWIN

        map[0x14] = LogicalKey.CapsLock;

        map[0x26] = LogicalKey.ArrowUp;
        map[0x28] = LogicalKey.ArrowDown;
        map[0x25] = LogicalKey.ArrowLeft;
        map[0x27] = LogicalKey.ArrowRight;

        map[0x2D] = LogicalKey.Insert;
        map[0x2E] = LogicalKey.Delete;
        map[0x24] = LogicalKey.Home;
        map[0x23] = LogicalKey.End;
        map[0x21] = LogicalKey.PageUp;
        map[0x22] = LogicalKey.PageDown;

        for (var n = 0; n <= 9; n++)
            map[0x60 + n] = Enum.Parse<LogicalKey>("NumPad" + n); // VK_NUMPAD0 = 0x60
        map[0x6B] = LogicalKey.NumPadAdd;
        map[0x6D] = LogicalKey.NumPadSubtract;
        map[0x6A] = LogicalKey.NumPadMultiply;
        map[0x6F] = LogicalKey.NumPadDivide;
        map[0x6E] = LogicalKey.NumPadDecimal;
        map[0x90] = LogicalKey.NumLock;

        map[0xBC] = LogicalKey.OemComma;
        map[0xBE] = LogicalKey.OemPeriod;
        map[0xBD] = LogicalKey.OemMinus;
        map[0xBB] = LogicalKey.OemPlus;
        map[0xBA] = LogicalKey.OemSemicolon;
        map[0xBF] = LogicalKey.OemQuestion;
        map[0xC0] = LogicalKey.OemTilde;
        map[0xDB] = LogicalKey.OemOpenBrackets;
        map[0xDD] = LogicalKey.OemCloseBrackets;
        map[0xDC] = LogicalKey.OemPipe;
        map[0xDE] = LogicalKey.OemQuotes;
        map[0xE2] = LogicalKey.OemBackslash;

        map[0x2C] = LogicalKey.PrintScreen;
        map[0x91] = LogicalKey.ScrollLock;
        map[0x13] = LogicalKey.Pause;

        return map;
    }

    /// <summary>
    /// NumPad Enter shares VK_RETURN (0x0D) with the main Enter key; the LL hook distinguishes
    /// it via the extended-key flag in lParam. Callers that can observe that flag should use
    /// this to refine the mapping.
    /// </summary>
    public static LogicalKey ResolveEnter(bool isExtendedKey) =>
        isExtendedKey ? LogicalKey.NumPadEnter : LogicalKey.Enter;
}
