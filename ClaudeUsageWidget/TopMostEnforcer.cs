using System.Runtime.InteropServices;

namespace ClaudeUsageWidgetProvider;

/// <summary>
/// Keeps the widget window topmost using WinEvent hooks on a dedicated background thread.
/// Hook callbacks only set a flag and post WM_USER — SetWindowPos runs on the hook thread,
/// never blocking input or the UI thread.
/// </summary>
internal sealed class TopMostEnforcer : IDisposable
{
    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint flags);

    [DllImport("user32.dll")]
    private static extern IntPtr SetWinEventHook(uint eventMin, uint eventMax, IntPtr hmodWinEventProc,
        WinEventDelegate lpfnWinEventProc, uint idProcess, uint idThread, uint dwFlags);

    [DllImport("user32.dll")]
    private static extern bool UnhookWinEvent(IntPtr hWinEventHook);

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("user32.dll")]
    private static extern bool PostThreadMessage(uint idThread, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern int GetMessage(out MSG lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax);

    [DllImport("user32.dll")]
    private static extern bool TranslateMessage(ref MSG lpMsg);

    [DllImport("user32.dll")]
    private static extern IntPtr DispatchMessage(ref MSG lpMsg);

    [StructLayout(LayoutKind.Sequential)]
    private struct MSG
    {
        public IntPtr hwnd;
        public uint message;
        public IntPtr wParam;
        public IntPtr lParam;
        public uint time;
        public int pt_x, pt_y;
    }

    private delegate void WinEventDelegate(IntPtr hook, uint eventType, IntPtr hwnd,
        int idObject, int idChild, uint idEventThread, uint dwmsEventTime);

    private static readonly IntPtr HwndTopmost = new(-1);
    private const uint WM_QUIT = 0x0012;
    private const uint WM_USER = 0x0400;
    private const uint EventSystemForeground = 0x0003;
    private const uint EventObjectReorder = 0x8004;
    private const uint WinEventOutOfContext = 0x0000;
    private const uint SwpNoMove = 0x0002;
    private const uint SwpNoSize = 0x0001;
    private const uint SwpNoActivate = 0x0010;

    private readonly IntPtr _hwnd;
    private readonly uint _processId;
    private volatile uint _threadId;
    private bool _disposed;

    // Must be fields — local variables get GC'd in Release builds before the message loop ends
    private readonly WinEventDelegate _fgDelegate;
    private readonly WinEventDelegate _reorderDelegate;

    internal TopMostEnforcer(IntPtr hwnd)
    {
        _hwnd = hwnd;
        _processId = (uint)System.Diagnostics.Process.GetCurrentProcess().Id;
        _fgDelegate = OnForegroundChanged;
        _reorderDelegate = OnZOrderChanged;
        var thread = new Thread(Run) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
    }

    private void Run()
    {
        _threadId = GetCurrentThreadId();

        var fgHook = SetWinEventHook(EventSystemForeground, EventSystemForeground,
            IntPtr.Zero, _fgDelegate, 0, 0, WinEventOutOfContext);
        var reorderHook = SetWinEventHook(EventObjectReorder, EventObjectReorder,
            IntPtr.Zero, _reorderDelegate, 0, 0, WinEventOutOfContext);

        int ret;
        while ((ret = GetMessage(out var msg, IntPtr.Zero, 0, 0)) != 0)
        {
            if (ret == -1) break;
            if (msg.message == WM_USER)
            {
                SetWindowPos(_hwnd, HwndTopmost, 0, 0, 0, 0, SwpNoMove | SwpNoSize | SwpNoActivate);
                continue;
            }
            TranslateMessage(ref msg);
            DispatchMessage(ref msg);
        }

        if (fgHook != IntPtr.Zero) UnhookWinEvent(fgHook);
        if (reorderHook != IntPtr.Zero) UnhookWinEvent(reorderHook);
    }

    private void RequestTopmost() => PostThreadMessage(_threadId, WM_USER, IntPtr.Zero, IntPtr.Zero);

    private void OnForegroundChanged(IntPtr hook, uint eventType, IntPtr hwnd,
        int idObject, int idChild, uint idEventThread, uint dwmsEventTime) => RequestTopmost();

    private void OnZOrderChanged(IntPtr hook, uint eventType, IntPtr hwnd,
        int idObject, int idChild, uint idEventThread, uint dwmsEventTime)
    {
        if (hwnd == _hwnd) return;
        GetWindowThreadProcessId(hwnd, out uint pid);
        if (pid == _processId) return;
        RequestTopmost();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (_threadId != 0)
            PostThreadMessage(_threadId, WM_QUIT, IntPtr.Zero, IntPtr.Zero);
    }
}
