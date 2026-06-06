namespace ClaudeUsageWidgetProvider;

internal static class XdgPaths
{
    private const string AppId = "claude-usage-widget";

    public static string ConfigDir => Path.Combine(
        Environment.GetEnvironmentVariable("XDG_CONFIG_HOME") ??
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config"),
        AppId);

    public static string DataDir => Path.Combine(
        Environment.GetEnvironmentVariable("XDG_DATA_HOME") ??
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "share"),
        AppId);

    public static string StateDir => Path.Combine(
        Environment.GetEnvironmentVariable("XDG_STATE_HOME") ??
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "state"),
        AppId);

    public static string SettingsPath => Path.Combine(ConfigDir, "settings.json");
    public static string HistoryDir => Path.Combine(DataDir, "history");
    public static string RuntimeDir => Path.Combine(StateDir, "runtime");
}
