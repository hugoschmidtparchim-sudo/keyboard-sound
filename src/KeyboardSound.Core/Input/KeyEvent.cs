namespace KeyboardSound.Core.Input;

public enum KeyAction
{
    Down,
    Up
}

public readonly record struct KeyEvent(LogicalKey Key, KeyAction Action, int VirtualKeyCode);
