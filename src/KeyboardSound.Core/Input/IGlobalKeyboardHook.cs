namespace KeyboardSound.Core.Input;

/// <summary>
/// Listens for keyboard activity system-wide, independent of which window has focus.
/// Implementations must raise <see cref="KeyEvent"/> only once per physical press/release
/// (i.e. suppress OS auto-repeat key-down spam) — see <see cref="LowLevelKeyboardHook"/>.
/// </summary>
public interface IGlobalKeyboardHook : IDisposable
{
    event Action<KeyEvent>? KeyEvent;

    /// <summary>
    /// Installs the hook. Must be called from a thread that pumps Win32 messages
    /// (the WPF UI thread's Dispatcher satisfies this).
    /// </summary>
    void Start();

    void Stop();
}
