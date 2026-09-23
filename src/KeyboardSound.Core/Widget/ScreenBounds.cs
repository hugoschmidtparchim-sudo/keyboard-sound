namespace KeyboardSound.Core.Widget;

/// <summary>Plain rectangle describing a monitor's working area, decoupled from any UI
/// framework's screen APIs so this module stays testable without WPF/WinForms.</summary>
public readonly record struct ScreenBounds(double X, double Y, double Width, double Height);
