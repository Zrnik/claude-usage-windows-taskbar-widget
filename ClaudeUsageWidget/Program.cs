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
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            System.Windows.MessageBox.Show(e.ExceptionObject?.ToString(), "Claude Usage Widget crashed");

        var app = new App();
        app.DispatcherUnhandledException += (_, e) =>
        {
            System.Windows.MessageBox.Show(e.Exception?.ToString(), "Claude Usage Widget crashed");
            e.Handled = true;
        };
        app.Run();
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        var apiClient = new ClaudeApiClient();
        apiClient.SetCredentials(CredentialStore.LoadAllCredentials());

        Exit += (_, _) => apiClient.Dispose();

        var primaryHwnd = FindWindow("Shell_TrayWnd", null);
        var primaryWindow = new MainWindow(apiClient, primaryHwnd, isPrimary: true);
        MainWindow = primaryWindow;
        primaryWindow.Show();

        EnumWindows((hwnd, _) =>
        {
            var sb = new StringBuilder(64);
            GetClassName(hwnd, sb, 64);
            if (sb.ToString() == "Shell_SecondaryTrayWnd")
                new MainWindow(apiClient, hwnd, isPrimary: false).Show();
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
