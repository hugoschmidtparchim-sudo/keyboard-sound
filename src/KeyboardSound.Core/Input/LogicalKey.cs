namespace KeyboardSound.Core.Input;

/// <summary>
/// Platform-independent identifier for a keyboard key. The Win32 virtual-key code is mapped
/// into this enum at the hook boundary (<see cref="Win32KeyMap"/>) so the rest of the app never
/// deals with raw VK codes.
/// </summary>
public enum LogicalKey
{
    Unknown = 0,

    A, B, C, D, E, F, G, H, I, J, K, L, M,
    N, O, P, Q, R, S, T, U, V, W, X, Y, Z,

    D0, D1, D2, D3, D4, D5, D6, D7, D8, D9,

    F1, F2, F3, F4, F5, F6, F7, F8, F9, F10, F11, F12,

    Space, Enter, Backspace, Tab, Escape,

    ShiftLeft, ShiftRight,
    CtrlLeft, CtrlRight,
    AltLeft, AltRight,
    WindowsLeft, WindowsRight,

    CapsLock,

    ArrowUp, ArrowDown, ArrowLeft, ArrowRight,

    Insert, Delete, Home, End, PageUp, PageDown,

    NumPad0, NumPad1, NumPad2, NumPad3, NumPad4,
    NumPad5, NumPad6, NumPad7, NumPad8, NumPad9,
    NumPadAdd, NumPadSubtract, NumPadMultiply, NumPadDivide,
    NumPadDecimal, NumPadEnter, NumLock,

    OemComma, OemPeriod, OemMinus, OemPlus,
    OemSemicolon, OemQuestion, OemTilde,
    OemOpenBrackets, OemCloseBrackets, OemPipe, OemQuotes, OemBackslash,

    PrintScreen, ScrollLock, Pause,

    /// <summary>Any recognized key without a dedicated LogicalKey value falls back here.</summary>
    Other
}
