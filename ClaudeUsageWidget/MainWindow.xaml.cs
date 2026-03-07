using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;

namespace ClaudeUsageWidgetProvider;

public partial class MainWindow : Window
{
    // Win32 P/Invoke

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_NOACTIVATE = 0x08000000;
    private const int WS_EX_TOOLWINDOW = 0x00000080;

    [DllImport("shell32.dll")]
    private static extern uint SHAppBarMessage(uint dwMessage, ref APPBARDATA pData);

    [DllImport("user32.dll")]
    private static extern IntPtr FindWindowEx(IntPtr hwndParent, IntPtr hwndChildAfter,
        string lpszClass, string? lpszWindow);

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool EnumChildWindows(IntPtr hwndParent, EnumChildProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

    [DllImport("user32.dll")]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

    [DllImport("shcore.dll")]
    private static extern int GetDpiForMonitor(IntPtr hMonitor, int dpiType, out uint dpiX, out uint dpiY);

    private delegate bool EnumChildProc(IntPtr hwnd, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT { public int Left, Top, Right, Bottom; }

    [StructLayout(LayoutKind.Sequential)]
    private struct APPBARDATA
    {
        public uint cbSize;
        public IntPtr hWnd;
        public uint uCallbackMessage;
        public uint uEdge;
        public RECT rc;
        public int lParam;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MONITORINFO
    {
        public uint cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    private const uint ABM_GETTASKBARPOS = 0x5u;
    private const uint ABM_GETSTATE = 0x4u;
    private const int ABS_AUTOHIDE = 0x1;
    private const uint ABE_BOTTOM = 3u;
    private const uint MONITOR_DEFAULTTONEAREST = 2;
    private const int MDT_EFFECTIVE_DPI = 0;
    private const uint SwpNoZOrder = 0x0004;

    // State

    private int _lastTrayWidth;
    private DispatcherTimer? _trayWatchTimer;
    private DispatcherTimer? _spinnerTimer;
    private DispatcherTimer? _refreshTimer;
    private DispatcherTimer? _textTimer;
    private DispatcherTimer? _visibilityTimer;
    private int _spinnerFrame;
    private static readonly string[] SpinnerFrames = ["|", "/", "—", "\\"];
    private readonly ClaudeApiClient _apiClient;
    private readonly IntPtr _taskbarHwnd;
    private readonly bool _isPrimary;
    private PopupWindow? _popup;
    private UsageData? _lastUsage;
    private TopMostEnforcer? _topMostEnforcer;
    private UpdateInfo? _latestRelease;

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint flags);

    private const uint SwpNoMove = 0x0002;
    private const uint SwpNoSize = 0x0001;
    private const uint SwpNoActivate = 0x0010;
    private const int WmMouseActivate = 0x0021;
    private const int MaNoActivate = 3;

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        var hwnd = new WindowInteropHelper(this).Handle;
        var exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
        SetWindowLong(hwnd, GWL_EXSTYLE, exStyle | WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW);
        HwndSource.FromHwnd(hwnd).AddHook(WndProc);

        _topMostEnforcer = new TopMostEnforcer(hwnd);

        Closed += (_, _) =>
        {
            _topMostEnforcer?.Dispose();
            _popup?.Close();
            _refreshTimer?.Stop();
            _textTimer?.Stop();
            _visibilityTimer?.Stop();
        };
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WmMouseActivate)
        {
            handled = true;
            return new IntPtr(MaNoActivate);
        }
        return IntPtr.Zero;
    }

    internal MainWindow(ClaudeApiClient apiClient, IntPtr taskbarHwnd, bool isPrimary)
    {
        _apiClient = apiClient;
        _taskbarHwnd = taskbarHwnd;
        _isPrimary = isPrimary;
        InitializeComponent();
        MouseEnter += (_, _) => ShowPopup();
        MouseLeave += (_, _) => HidePopup();

        Loaded += async (_, _) =>
        {
#if DEBUG
            RemoveStartupIfThisExe();
#endif
            SetTaskbarHeight();
            PositionWindow();
            StartTrayWatchTimer();
            ShowLoadingState();

            var usage = await _apiClient.GetUsageAsync();
            StopSpinner();
            if (usage != null)
                UpdateBars(usage);
            else
                ShowErrorState();

            StartRefreshTimer();
            StartTextTimer();
            StartVisibilityTimer();
            _ = LoadLatestReleaseAsync();
        };
    }

    private double GetMonitorScale()
    {
        var monitor = MonitorFromWindow(_taskbarHwnd, MONITOR_DEFAULTTONEAREST);
        GetDpiForMonitor(monitor, MDT_EFFECTIVE_DPI, out uint dpiX, out _);
        return dpiX / 96.0;
    }

    private void SetTaskbarHeight()
    {
        if (!GetWindowRect(_taskbarHwnd, out RECT rect)) return;
        int heightPx = rect.Bottom - rect.Top;
        if (heightPx <= 0) return;
        Height = heightPx / GetMonitorScale();
    }

    private (bool isBottom, RECT taskbarRect) GetTaskbarInfo()
    {
        if (_isPrimary)
        {
            var data = new APPBARDATA { cbSize = (uint)Marshal.SizeOf<APPBARDATA>() };
            SHAppBarMessage(ABM_GETTASKBARPOS, ref data);
            return (data.uEdge == ABE_BOTTOM, data.rc);
        }

        GetWindowRect(_taskbarHwnd, out RECT rect);
        var monitor = MonitorFromWindow(_taskbarHwnd, MONITOR_DEFAULTTONEAREST);
        var mi = new MONITORINFO { cbSize = (uint)Marshal.SizeOf<MONITORINFO>() };
        GetMonitorInfo(monitor, ref mi);
        bool isBottom = rect.Top > (mi.rcMonitor.Top + mi.rcMonitor.Bottom) / 2;
        return (isBottom, rect);
    }

    private int GetTrayWidth()
    {
        if (_isPrimary)
        {
            var hwndTray = FindWindowEx(_taskbarHwnd, IntPtr.Zero, "TrayNotifyWnd", null);
            if (hwndTray != IntPtr.Zero && GetWindowRect(hwndTray, out RECT trayRect))
                return trayRect.Right - trayRect.Left;
            return 200;
        }
        return GetSecondaryTrayWidth();
    }

    private int GetSecondaryTrayWidth()
    {
        if (!GetWindowRect(_taskbarHwnd, out RECT taskbarRect)) return 150;
        int minLeft = taskbarRect.Right;
        EnumChildWindows(_taskbarHwnd, (child, _) =>
        {
            if (!IsWindowVisible(child)) return true;
            if (!GetWindowRect(child, out RECT cr)) return true;
            int childWidth = cr.Right - cr.Left;
            int taskbarWidth = taskbarRect.Right - taskbarRect.Left;
            if (cr.Left > (taskbarRect.Left + taskbarRect.Right) / 2 && childWidth < taskbarWidth / 2)
                if (cr.Left < minLeft) minLeft = cr.Left;
            return true;
        }, IntPtr.Zero);
        int trayWidth = taskbarRect.Right - minLeft;
        return trayWidth is > 20 and < 600 ? trayWidth : 120;
    }

    private void PositionWindow()
    {
        var (isBottom, taskbarRect) = GetTaskbarInfo();
        if (!isBottom) return; // skip non-bottom taskbars

        double scale = GetMonitorScale();
        int trayWidthPx = GetTrayWidth();
        _lastTrayWidth = trayWidthPx;

        int widgetWidthPx = (int)(Width * scale);
        int posXPx = taskbarRect.Right - trayWidthPx - widgetWidthPx;
        int posYPx = taskbarRect.Top;

        var myHwnd = new WindowInteropHelper(this).Handle;
        SetWindowPos(myHwnd, IntPtr.Zero, posXPx, posYPx, 0, 0, SwpNoSize | SwpNoActivate | SwpNoZOrder);

        // Update WPF coords for popup positioning
        Left = posXPx / scale;
        Top = posYPx / scale;
    }

    private void StartRefreshTimer()
    {
        _refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(1) };
        _refreshTimer.Tick += async (_, _) =>
        {
            var usage = await _apiClient.GetUsageAsync();
            if (usage != null)
                UpdateBars(usage);
            else
                ShowErrorState(); // vždy při selhání — bez podmínky; locked decision z CONTEXT.md
        };
        _refreshTimer.Start();
    }

    private void StartTextTimer()
    {
        _textTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(15) };
        _textTimer.Tick += (_, _) => RefreshText();
        _textTimer.Start();
    }

    private void StartVisibilityTimer()
    {
        _visibilityTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        _visibilityTimer.Tick += (_, _) => CheckVisibility();
        _visibilityTimer.Start();
    }

    private void CheckVisibility()
    {
        bool taskbarVisible = IsTaskbarVisible();
        bool fullscreen = IsFullscreenOnMyMonitor();
        // Locked decision z CONTEXT.md: widget viditelný jen když taskbarVisible && !fullscreenOnSameMonitor
        Visibility = (taskbarVisible && !fullscreen)
            ? Visibility.Visible
            : Visibility.Hidden;
    }

    private bool IsTaskbarVisible()
    {
        if (_isPrimary)
        {
            // Pro primární taskbar: zjistit zda má auto-hide zapnuté
            var stateData = new APPBARDATA { cbSize = (uint)Marshal.SizeOf<APPBARDATA>() };
            uint state = SHAppBarMessage(ABM_GETSTATE, ref stateData);
            bool autoHideEnabled = (state & ABS_AUTOHIDE) != 0;

            if (!autoHideEnabled) return true; // auto-hide vypnutý → vždy viditelný

            // Auto-hide zapnutý: porovnat aktuální pozici taskbaru s expected pozicí
            if (!GetWindowRect(_taskbarHwnd, out RECT actualRect)) return true;
            var posData = new APPBARDATA { cbSize = (uint)Marshal.SizeOf<APPBARDATA>() };
            SHAppBarMessage(ABM_GETTASKBARPOS, ref posData);
            // Taskbar je skrytý = actualRect.Top je za spodní hranicí expected pozice
            return actualRect.Top <= posData.rc.Bottom;
        }
        else
        {
            // Pro sekundární taskbar: heuristika — porovnat Top s dolní hranicí monitoru
            if (!GetWindowRect(_taskbarHwnd, out RECT taskbarRect)) return true;
            var monitor = MonitorFromWindow(_taskbarHwnd, MONITOR_DEFAULTTONEAREST);
            var mi = new MONITORINFO { cbSize = (uint)Marshal.SizeOf<MONITORINFO>() };
            if (!GetMonitorInfo(monitor, ref mi)) return true;
            // Skrytý = Top taskbaru je blízko dolního okraje monitoru (tolerance 2px)
            return taskbarRect.Top < mi.rcMonitor.Bottom - 2;
        }
    }

    private bool IsFullscreenOnMyMonitor()
    {
        var foreground = GetForegroundWindow();
        if (foreground == IntPtr.Zero) return false;

        // Zjistit monitor taskbaru (widget ho sleduje)
        var myMonitor = MonitorFromWindow(_taskbarHwnd, MONITOR_DEFAULTTONEAREST);
        var mi = new MONITORINFO { cbSize = (uint)Marshal.SizeOf<MONITORINFO>() };
        if (!GetMonitorInfo(myMonitor, ref mi)) return false;

        // Zjistit monitor foreground okna
        var fgMonitor = MonitorFromWindow(foreground, MONITOR_DEFAULTTONEAREST);
        if (fgMonitor != myMonitor) return false; // fullscreen na jiném monitoru — neskrývat

        // Porovnat rozměry foreground okna s fyzickými rozměry monitoru (rcMonitor, ne rcWork)
        if (!GetWindowRect(foreground, out RECT fgRect)) return false;
        var mon = mi.rcMonitor;
        return fgRect.Left <= mon.Left && fgRect.Top <= mon.Top
            && fgRect.Right >= mon.Right && fgRect.Bottom >= mon.Bottom;
    }

    private void RefreshText()
    {
        if (_lastUsage == null) return;
        var first = _lastUsage.Limits.ElementAtOrDefault(0);
        var second = _lastUsage.Limits.ElementAtOrDefault(1);
        if (first != null)
            Text5h.Text = $"{first.Utilization:0}%  {TimeFormatter.FormatResetTime(first.ResetsAt)}";
        if (second != null)
            Text7d.Text = $"{second.Utilization:0}%  {TimeFormatter.FormatResetTime(second.ResetsAt)}";
    }

    private void StartTrayWatchTimer()
    {
        _trayWatchTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        _trayWatchTimer.Tick += (_, _) =>
        {
            if (GetTrayWidth() != _lastTrayWidth)
                PositionWindow();
        };
        _trayWatchTimer.Start();
    }

    private void ShowLoadingState()
    {
        Bar5h.Value = 0;
        Bar7d.Value = 0;
        Text5h.Foreground = Brushes.White;
        Text7d.Foreground = Brushes.White;
        _spinnerFrame = 0;
        Text5h.Text = SpinnerFrames[0];
        Text7d.Text = SpinnerFrames[0];
        _spinnerTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(150) };
        _spinnerTimer.Tick += (_, _) =>
        {
            _spinnerFrame = (_spinnerFrame + 1) % SpinnerFrames.Length;
            Text5h.Text = SpinnerFrames[_spinnerFrame];
            Text7d.Text = SpinnerFrames[_spinnerFrame];
        };
        _spinnerTimer.Start();
    }

    private void StopSpinner()
    {
        _spinnerTimer?.Stop();
        _spinnerTimer = null;
    }

    private void ShowPopup()
    {
        if (_lastUsage == null && _apiClient.LastError == null) return;
        if (_popup == null)
        {
            _popup = new PopupWindow();
            _popup.Owner = this; // owned window always stays above owner, no z-order race
            _popup.MouseLeave += (_, _) => HidePopup();
        }
        _popup.UpdateAndShow(_lastUsage, _apiClient.LastError, _apiClient.CredentialPath, Left, Top);
    }

    private void HidePopup()
    {
        _popup?.Hide();
    }

    public void UpdateBars(UsageData data)
    {
        _lastUsage = data;
        Text5h.Foreground = Brushes.White;
        Text7d.Foreground = Brushes.White;

        var first = data.Limits.ElementAtOrDefault(0);
        var second = data.Limits.ElementAtOrDefault(1);

        if (first != null)
        {
            Bar5h.Value = first.Utilization;
            Text5h.Text = $"{first.Utilization:0}%  {TimeFormatter.FormatResetTime(first.ResetsAt)}";
            SetBarColor(GetBarIndicator(Bar5h), first.Utilization);
        }

        if (second != null)
        {
            Bar7d.Value = second.Utilization;
            Text7d.Text = $"{second.Utilization:0}%  {TimeFormatter.FormatResetTime(second.ResetsAt)}";
            SetBarColor(GetBarIndicator(Bar7d), second.Utilization);
        }
    }

    private void ShowErrorState()
    {
        _lastUsage = null; // okamžitý přechod, bez stale dat — tooltip pak zobrazí LastError
        Bar5h.Value = 100; // Value=100 aby PART_Indicator měl šířku a barva byla viditelná
        Bar7d.Value = 100;
        var maroon = new SolidColorBrush(Colors.Maroon); // #800000 — locked decision z CONTEXT.md
        var ind5h = GetBarIndicator(Bar5h);
        var ind7d = GetBarIndicator(Bar7d);
        if (ind5h != null) ind5h.Background = maroon;
        if (ind7d != null) ind7d.Background = maroon;
        Text5h.Foreground = Brushes.White;
        Text7d.Foreground = Brushes.White;
        Text5h.Text = "Error"; // velké E — locked decision z CONTEXT.md
        Text7d.Text = "Error";
    }

    private async Task LoadLatestReleaseAsync()
    {
        _latestRelease = await Updater.CheckAsync();
    }

    private async Task UpdateAsync()
    {
        var info = _latestRelease ?? await Updater.CheckAsync();
        if (info == null) return;
        Updater.LaunchUpdaterTerminal(info);
    }

    private const string RunRegistryKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string RunRegistryValue = "ClaudeUsageWidget";

    private static bool IsStartupEnabled()
    {
        using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(RunRegistryKey);
        return key?.GetValue(RunRegistryValue) != null;
    }

#if DEBUG
    private static void RemoveStartupIfThisExe()
    {
        using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(RunRegistryKey, writable: true);
        if (key == null) return;
        var value = key.GetValue(RunRegistryValue) as string;
        var thisExe = $"\"{Environment.ProcessPath}\"";
        if (string.Equals(value, thisExe, StringComparison.OrdinalIgnoreCase))
            key.DeleteValue(RunRegistryValue, throwOnMissingValue: false);
    }
#endif

    private static void SetStartup(bool enable)
    {
        using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(RunRegistryKey, writable: true);
        if (key == null) return;
        if (enable)
            key.SetValue(RunRegistryValue, $"\"{Environment.ProcessPath}\"");
        else
            key.DeleteValue(RunRegistryValue, throwOnMissingValue: false);
    }

    private void OnRightClick(object sender, MouseButtonEventArgs e)
    {
#if DEBUG
        const bool isExe = false;
#else
        const bool isExe = true;
#endif

        HidePopup();

        var menu = new ContextMenuWindow { Owner = this };
        menu.AddCheckItem("Run at startup", IsStartupEnabled(), isExe, SetStartup);
        menu.AddSeparator();
        var updateLabel = _latestRelease != null ? $"Update to v{_latestRelease.Version}" : "Update to latest";
        menu.AddItem(updateLabel, () => _ = UpdateAsync());
        menu.AddSeparator();
        menu.AddItem("Quit", () => Application.Current.Shutdown());

        // Zobrazit na místě tooltipu — nad widgetem vlevo od kurzoru
        menu.ShowAbove(Left, Top);
    }

    private static void SetBarColor(Border? indicator, double utilization)
    {
        if (indicator == null) return;

        var color = utilization < 75
            ? "#4CAF50"
            : utilization < 90
                ? "#FF9800"
                : "#F44336";

        indicator.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
    }

    private static Border? GetBarIndicator(ProgressBar bar)
    {
        bar.ApplyTemplate();
        return bar.Template.FindName("PART_Indicator", bar) as Border;
    }
}
