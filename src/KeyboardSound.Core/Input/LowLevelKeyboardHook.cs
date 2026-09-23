using System.Runtime.InteropServices;
using KeyboardSound.Core.Diagnostics;

namespace KeyboardSound.Core.Input;

/// <summary>
/// Windows low-level keyboard hook (WH_KEYBOARD_LL). Captures key presses regardless of which
/// application has focus, without polling. The hook callback must stay fast (the OS enforces a
/// timeout and will silently unhook a slow callback), so this class does no I/O and only raises
/// an in-process event for the InputRouter to handle.
/// </summary>
public sealed class LowLevelKeyboardHook : IGlobalKeyboardHook
{
    private const int WH_KEYBOARD_LL = 13;
    private const int WM_KEYDOWN = 0x0100;
    private const int WM_KEYUP = 0x0101;
    private const int WM_SYSKEYDOWN = 0x0104;
    private const int WM_SYSKEYUP = 0x0105;
    private const int LLKHF_EXTENDED = 0x01;

    private delegate nint LowLevelKeyboardProc(int nCode, nint wParam, nint lParam);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern nint SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, nint hMod, uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(nint hhk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern nint CallNextHookEx(nint hhk, int nCode, nint wParam, nint lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern nint GetModuleHandle(string? lpModuleName);

    [StructLayout(LayoutKind.Sequential)]
    private struct KBDLLHOOKSTRUCT
    {
        public int vkCode;
        public int scanCode;
        public int flags;
        public int time;
        public nint dwExtraInfo;
    }

    // Keep the delegate alive for the lifetime of the hook; otherwise the GC can collect it
    // while the OS still holds a native pointer to it, crashing the process.
    private readonly LowLevelKeyboardProc _proc;
    private nint _hookHandle;

    // Tracks keys currently held down so OS auto-repeat key-down messages don't retrigger
    // sound playback for a key the user never released.
    private readonly HashSet<int> _pressedVirtualKeys = new();

    public event Action<KeyEvent>? KeyEvent;

    public LowLevelKeyboardHook()
    {
        _proc = HookCallback;
    }

    public void Start()
    {
        if (_hookHandle != 0) return;

        using var curProcess = System.Diagnostics.Process.GetCurrentProcess();
        using var curModule = curProcess.MainModule!;
        _hookHandle = SetWindowsHookEx(WH_KEYBOARD_LL, _proc, GetModuleHandle(curModule.ModuleName), 0);

        if (_hookHandle == 0)
        {
            var error = Marshal.GetLastWin32Error();
            Log.Error($"Failed to install keyboard hook (Win32 error {error})");
        }
        else
        {
            Log.Info("Global keyboard hook installed.");
        }
    }

    public void Stop()
    {
        if (_hookHandle == 0) return;
        UnhookWindowsHookEx(_hookHandle);
        _hookHandle = 0;
        _pressedVirtualKeys.Clear();
        Log.Info("Global keyboard hook removed.");
    }

    private nint HookCallback(int nCode, nint wParam, nint lParam)
    {
        if (nCode >= 0)
        {
            var data = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);
            var msg = (int)wParam;

            if (msg is WM_KEYDOWN or WM_SYSKEYDOWN)
            {
                HandleKeyDown(data);
            }
            else if (msg is WM_KEYUP or WM_SYSKEYUP)
            {
                HandleKeyUp(data);
            }
        }

        return CallNextHookEx(_hookHandle, nCode, wParam, lParam);
    }

    private void HandleKeyDown(KBDLLHOOKSTRUCT data)
    {
        // Windows re-sends WM_KEYDOWN repeatedly while a key is held (auto-repeat).
        // Only the first transition from "up" to "down" should trigger a sound.
        if (!_pressedVirtualKeys.Add(data.vkCode))
            return;

        var logical = ResolveKey(data);
        KeyEvent?.Invoke(new KeyEvent(logical, KeyAction.Down, data.vkCode));
    }

    private void HandleKeyUp(KBDLLHOOKSTRUCT data)
    {
        _pressedVirtualKeys.Remove(data.vkCode);
        var logical = ResolveKey(data);
        KeyEvent?.Invoke(new KeyEvent(logical, KeyAction.Up, data.vkCode));
    }

    private static LogicalKey ResolveKey(KBDLLHOOKSTRUCT data)
    {
        if (data.vkCode == 0x0D) // VK_RETURN, ambiguous between Enter and NumPad Enter
        {
            var isExtended = (data.flags & LLKHF_EXTENDED) != 0;
            return Win32KeyMap.ResolveEnter(isExtended);
        }

        return Win32KeyMap.ToLogicalKey(data.vkCode);
    }

    public void Dispose() => Stop();
}
