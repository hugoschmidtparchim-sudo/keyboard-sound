namespace KeyboardSound.Core.Configuration;

/// <summary>
/// Single source of truth for every filesystem location the app touches. Centralizing this
/// avoids hardcoded paths scattered across modules and makes portable/dev-vs-installed
/// differences a one-place change.
/// </summary>
public static class AppPaths
{
    private const string AppFolderName = "KeyboardSound";

    /// <summary>Roaming so settings follow the user's Windows profile (small file, infrequent writes).</summary>
    public static string SettingsFilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), AppFolderName, "settings.json");

    /// <summary>Local (not roamed) since logs are machine-specific diagnostics, not user data.</summary>
    public static string LogFilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), AppFolderName, "logs", "app.log");

    /// <summary>Soundpacks shipped alongside the app (copied to the output/publish directory).</summary>
    public static string BuiltInSoundPacksDirectory => Path.Combine(AppContext.BaseDirectory, "soundpacks");

    /// <summary>Where soundpacks the user imports later will live, kept separate from the
    /// built-in ones so an app update never touches user-added content.</summary>
    public static string UserSoundPacksDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), AppFolderName, "Soundpacks");
}
