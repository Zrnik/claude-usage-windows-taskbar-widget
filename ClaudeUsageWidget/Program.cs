using System.Runtime.InteropServices;
using System.Text;
using System.Windows;

namespace ClaudeUsageWidgetProvider;

internal class App : Application
{
    [DllImport("user32.dll")]
    private static extern IntPtr FindWindow(string lpClassName, string? lpWindowName);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

    private delegate bool EnumWindowsProc(IntPtr hwnd, IntPtr lParam);

    [STAThread]
    static void Main(string[] args)
    {
        WriteCrashLog("started");

        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            WriteCrashLog(e.ExceptionObject?.ToString());

        var app = new App();
        app.DispatcherUnhandledException += (_, e) =>
        {
            WriteCrashLog(e.Exception?.ToString());
            e.Handled = true;
        };
        app.Run();
    }

    private static void WriteCrashLog(string? message)
    {
        try
        {
            var exePath = Environment.ProcessPath ?? "";
            var logPath = Path.ChangeExtension(exePath, ".log");
            var entry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]{Environment.NewLine}{message}{Environment.NewLine}{Environment.NewLine}";
            File.AppendAllText(logPath, entry);
        }
        catch { }
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        // Načíst všechny unikátní účty (Claude Windows+WSL deduplikováno, Codex)
        var accounts = CredentialStore.LoadAllAccounts();

        // Fallback: žádné credentials → jeden client v error stavu
        if (accounts.Count == 0)
            accounts.Add(new AccountInfo(ServiceType.Claude, new OAuthCredential(), "no-credentials"));

        var clients = accounts.Select(a => new ClaudeApiClient(a)).ToList();

        Exit += (_, _) =>
        {
            foreach (var c in clients) c.Dispose();
        };

        var primaryHwnd = FindWindow("Shell_TrayWnd", null);

        // Primární okno = první účet
        var primaryWindow = new MainWindow(clients[0], primaryHwnd, isPrimary: true);
        MainWindow = primaryWindow;
        primaryWindow.Show();

        // Pro každý sekundární taskbar: zobrazit první účet
        // (Phase 5 změní layout na horizontální řadu per-taskbar)
        EnumWindows((hwnd, _) =>
        {
            var sb = new StringBuilder(64);
            GetClassName(hwnd, sb, 64);
            if (sb.ToString() == "Shell_SecondaryTrayWnd")
                new MainWindow(clients[0], hwnd, isPrimary: false).Show();
            return true;
        }, IntPtr.Zero);
    }
}

internal static class TimeFormatter
{
    internal static string FormatResetTime(DateTimeOffset resetTime)
    {
        var remaining = resetTime - DateTimeOffset.UtcNow;
        if (remaining <= TimeSpan.Zero) return "now";
        if (remaining.TotalHours >= 1)
            return $"in {(int)remaining.TotalHours}h {remaining.Minutes}m";
        return $"in {remaining.Minutes}m";
    }
}
