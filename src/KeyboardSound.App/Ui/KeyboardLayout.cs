using KeyboardSound.Core.Input;

namespace KeyboardSound.App.Ui;

/// <summary>One key's visual definition for <see cref="KeyboardEditorWindow"/>: which physical
/// row it's in, its label (best-effort German QWERTZ labeling - the underlying
/// <see cref="Key"/> mapping itself is what matters functionally and comes straight from the
/// already-correct <see cref="Win32KeyMap"/>), and its width relative to a standard 1u key.</summary>
public sealed record KeyDef(LogicalKey Key, string Label, double Width = 1.0);

/// <summary>
/// Data-driven German QWERTZ layout (plus a compact navigation cluster, arrows, and numpad) for
/// the visual Keyboard Editor. Purely a UI concern - every <see cref="LogicalKey"/> referenced
/// here already exists and is already correctly produced by the real global hook
/// (<see cref="Win32KeyMap"/>), so nothing about input recognition changes; this only decides
/// how keys are laid out and labeled on screen.
/// </summary>
public static class KeyboardLayout
{
    public static readonly IReadOnlyList<IReadOnlyList<KeyDef>> MainRows = new List<List<KeyDef>>
    {
        new()
        {
            new(LogicalKey.Escape, "Esc"),
            new(LogicalKey.F1, "F1"), new(LogicalKey.F2, "F2"), new(LogicalKey.F3, "F3"), new(LogicalKey.F4, "F4"),
            new(LogicalKey.F5, "F5"), new(LogicalKey.F6, "F6"), new(LogicalKey.F7, "F7"), new(LogicalKey.F8, "F8"),
            new(LogicalKey.F9, "F9"), new(LogicalKey.F10, "F10"), new(LogicalKey.F11, "F11"), new(LogicalKey.F12, "F12"),
        },
        new()
        {
            new(LogicalKey.OemTilde, "^"), new(LogicalKey.D1, "1"), new(LogicalKey.D2, "2"), new(LogicalKey.D3, "3"),
            new(LogicalKey.D4, "4"), new(LogicalKey.D5, "5"), new(LogicalKey.D6, "6"), new(LogicalKey.D7, "7"),
            new(LogicalKey.D8, "8"), new(LogicalKey.D9, "9"), new(LogicalKey.D0, "0"), new(LogicalKey.OemMinus, "ß"),
            new(LogicalKey.OemPlus, "´"), new(LogicalKey.Backspace, "Backspace", 2.0),
        },
        new()
        {
            new(LogicalKey.Tab, "Tab", 1.5),
            new(LogicalKey.Q, "Q"), new(LogicalKey.W, "W"), new(LogicalKey.E, "E"), new(LogicalKey.R, "R"),
            new(LogicalKey.T, "T"), new(LogicalKey.Z, "Z"), new(LogicalKey.U, "U"), new(LogicalKey.I, "I"),
            new(LogicalKey.O, "O"), new(LogicalKey.P, "P"), new(LogicalKey.OemOpenBrackets, "Ü"),
            new(LogicalKey.OemCloseBrackets, "+"),
        },
        new()
        {
            new(LogicalKey.CapsLock, "Caps", 1.75),
            new(LogicalKey.A, "A"), new(LogicalKey.S, "S"), new(LogicalKey.D, "D"), new(LogicalKey.F, "F"),
            new(LogicalKey.G, "G"), new(LogicalKey.H, "H"), new(LogicalKey.J, "J"), new(LogicalKey.K, "K"),
            new(LogicalKey.L, "L"), new(LogicalKey.OemSemicolon, "Ö"), new(LogicalKey.OemQuotes, "Ä"),
            new(LogicalKey.OemPipe, "#"), new(LogicalKey.Enter, "Enter", 1.75),
        },
        new()
        {
            new(LogicalKey.ShiftLeft, "Shift", 1.25),
            new(LogicalKey.OemBackslash, "<"),
            new(LogicalKey.Y, "Y"), new(LogicalKey.X, "X"), new(LogicalKey.C, "C"), new(LogicalKey.V, "V"),
            new(LogicalKey.B, "B"), new(LogicalKey.N, "N"), new(LogicalKey.M, "M"),
            new(LogicalKey.OemComma, ","), new(LogicalKey.OemPeriod, "."), new(LogicalKey.OemQuestion, "-"),
            new(LogicalKey.ShiftRight, "Shift", 2.75),
        },
        new()
        {
            new(LogicalKey.CtrlLeft, "Ctrl", 1.25),
            new(LogicalKey.WindowsLeft, "Win", 1.0),
            new(LogicalKey.AltLeft, "Alt", 1.0),
            new(LogicalKey.Space, "Space", 6.25),
            new(LogicalKey.AltRight, "AltGr", 1.0),
            new(LogicalKey.WindowsRight, "Win", 1.0),
            new(LogicalKey.CtrlRight, "Ctrl", 1.25),
        },
    };

    public static readonly IReadOnlyList<IReadOnlyList<KeyDef>> NavRows = new List<List<KeyDef>>
    {
        new() { new(LogicalKey.PrintScreen, "Print"), new(LogicalKey.ScrollLock, "Scroll"), new(LogicalKey.Pause, "Pause") },
        new() { new(LogicalKey.Insert, "Ins"), new(LogicalKey.Home, "Home"), new(LogicalKey.PageUp, "PgUp") },
        new() { new(LogicalKey.Delete, "Del"), new(LogicalKey.End, "End"), new(LogicalKey.PageDown, "PgDn") },
    };

    public static readonly IReadOnlyList<IReadOnlyList<KeyDef?>> ArrowRows = new List<List<KeyDef?>>
    {
        new() { null, new(LogicalKey.ArrowUp, "↑"), null },
        new() { new(LogicalKey.ArrowLeft, "←"), new(LogicalKey.ArrowDown, "↓"), new(LogicalKey.ArrowRight, "→") },
    };

    public static readonly IReadOnlyList<IReadOnlyList<KeyDef>> NumPadRows = new List<List<KeyDef>>
    {
        new() { new(LogicalKey.NumLock, "Num"), new(LogicalKey.NumPadDivide, "/"), new(LogicalKey.NumPadMultiply, "*"), new(LogicalKey.NumPadSubtract, "-") },
        new() { new(LogicalKey.NumPad7, "7"), new(LogicalKey.NumPad8, "8"), new(LogicalKey.NumPad9, "9"), new(LogicalKey.NumPadAdd, "+") },
        new() { new(LogicalKey.NumPad4, "4"), new(LogicalKey.NumPad5, "5"), new(LogicalKey.NumPad6, "6") },
        new() { new(LogicalKey.NumPad1, "1"), new(LogicalKey.NumPad2, "2"), new(LogicalKey.NumPad3, "3"), new(LogicalKey.NumPadEnter, "Enter") },
        new() { new(LogicalKey.NumPad0, "0", 2.0), new(LogicalKey.NumPadDecimal, ".") },
    };
}
