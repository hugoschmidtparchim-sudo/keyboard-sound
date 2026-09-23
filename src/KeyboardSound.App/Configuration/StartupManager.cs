using Microsoft.Win32;

namespace KeyboardSound.App.Configuration;

/// <summary>
/// Registers/unregisters the app to launch with Windows via the per-user Run registry key.
/// No installer, no scheduled task, no elevation needed — the simplest mechanism that satisfies
/// "start with Windows" for a per-user utility app.
/// </summary>
public static class StartupManager
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "KeyboardSound";

    public static bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
        return key?.GetValue(ValueName) is string existing &&
               string.Equals(existing.Trim('"'), GetExecutablePath(), StringComparison.OrdinalIgnoreCase);
    }

    public static void SetEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true);
        if (key is null) return;

        if (enabled)
            key.SetValue(ValueName, $"\"{GetExecutablePath()}\"");
        else
            key.DeleteValue(ValueName, throwOnMissingValue: false);
    }

    private static string GetExecutablePath() =>
        Environment.ProcessPath ?? throw new InvalidOperationException("Could not determine the running executable's path.");
}
