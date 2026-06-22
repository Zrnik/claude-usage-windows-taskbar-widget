namespace ClaudeUsageWidgetProvider;

internal static class XdgPaths
{
    private const string AppId = "claude-usage-widget";

    public static string ConfigDir => Path.Combine(
        Environment.GetEnvironmentVariable("XDG_CONFIG_HOME") ??
        Path.Combine(HomeDir, ".config"),
        AppId);

    public static string DataDir => Path.Combine(
        Environment.GetEnvironmentVariable("XDG_DATA_HOME") ??
        Path.Combine(HomeDir, ".local", "share"),
        AppId);

    public static string StateDir => Path.Combine(
        Environment.GetEnvironmentVariable("XDG_STATE_HOME") ??
        Path.Combine(HomeDir, ".local", "state"),
        AppId);

    public static string SettingsPath => Path.Combine(ConfigDir, "settings.json");
    public static string HistoryDir => Path.Combine(DataDir, "history");
    public static string RuntimeDir => Path.Combine(StateDir, "runtime");

    private static string HomeDir
    {
        get
        {
            var home = Environment.GetEnvironmentVariable("HOME");
            if (!string.IsNullOrWhiteSpace(home))
                return home;

            home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (!string.IsNullOrWhiteSpace(home))
                return home;

            var user = Environment.GetEnvironmentVariable("USER");
            if (string.IsNullOrWhiteSpace(user))
                user = Environment.UserName;
            if (!string.IsNullOrWhiteSpace(user))
                return Path.Combine("/home", user);

            throw new InvalidOperationException("Cannot resolve user home directory for widget storage.");
        }
    }
}
