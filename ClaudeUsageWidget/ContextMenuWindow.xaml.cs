using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;

namespace ClaudeUsageWidgetProvider;

public partial class ContextMenuWindow : Window
{
    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint flags);

    private static readonly IntPtr HwndTopmost = new(-1);
    private const uint SWP_NOMOVE = 0x0002;
    private const uint SWP_NOSIZE = 0x0001;
    private const uint SWP_NOACTIVATE = 0x0010;

    private bool _closing;

    public ContextMenuWindow()
    {
        InitializeComponent();
        Deactivated += (_, _) => CloseMenu();
    }

    private void CloseMenu()
    {
        if (_closing) return;
        _closing = true;
        Close();
    }

    public void AddCheckItem(string header, bool isChecked, bool isEnabled, Action<bool> onToggle)
    {
        var row = new Grid { Margin = new Thickness(4, 2, 4, 2) };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(16) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var check = new TextBlock
        {
            Text = isChecked ? "✓" : "",
            Foreground = isEnabled ? Brushes.White : new SolidColorBrush(Color.FromRgb(0x66, 0x66, 0x66)),
            FontSize = 11,
            VerticalAlignment = VerticalAlignment.Center
        };

        var label = new TextBlock
        {
            Text = header,
            Foreground = isEnabled ? Brushes.White : new SolidColorBrush(Color.FromRgb(0x66, 0x66, 0x66)),
            FontSize = 11,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(4, 0, 8, 0)
        };

        Grid.SetColumn(check, 0);
        Grid.SetColumn(label, 1);
        row.Children.Add(check);
        row.Children.Add(label);

        var border = new Border { Child = row, CornerRadius = new CornerRadius(4), Padding = new Thickness(0, 3, 0, 3) };

        if (isEnabled)
        {
            border.Cursor = Cursors.Hand;
            border.MouseEnter += (_, _) => border.Background = new SolidColorBrush(Color.FromRgb(0x33, 0x33, 0x33));
            border.MouseLeave += (_, _) => border.Background = Brushes.Transparent;
            border.MouseLeftButtonUp += (_, _) =>
            {
                var newChecked = check.Text == "";
                check.Text = newChecked ? "✓" : "";
                onToggle(newChecked);
                CloseMenu();
            };
        }

        ItemsPanel.Children.Add(border);
    }

    public void AddSeparator() =>
        ItemsPanel.Children.Add(new Border
        {
            Height = 1,
            Background = new SolidColorBrush(Color.FromRgb(0x33, 0x33, 0x33)),
            Margin = new Thickness(4, 2, 4, 2)
        });

    public void AddItem(string header, Action onClick)
    {
        var label = new TextBlock
        {
            Text = header,
            Foreground = Brushes.White,
            FontSize = 11,
            Margin = new Thickness(20, 0, 8, 0),
            VerticalAlignment = VerticalAlignment.Center
        };

        var border = new Border
        {
            Child = label,
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(0, 3, 0, 3),
            Cursor = Cursors.Hand
        };
        border.MouseEnter += (_, _) => border.Background = new SolidColorBrush(Color.FromRgb(0x33, 0x33, 0x33));
        border.MouseLeave += (_, _) => border.Background = Brushes.Transparent;
        border.MouseLeftButtonUp += (_, _) => { onClick(); CloseMenu(); };

        ItemsPanel.Children.Add(border);
    }

    public void ShowAbove(double x, double y)
    {
        // Measure first so ActualHeight is available
        Left = -10000;
        Top = -10000;
        var helper = new WindowInteropHelper(this);
        helper.EnsureHandle();
        Show();
        UpdateLayout();

        Left = x;
        Top = y - ActualHeight - 4;

        SetWindowPos(helper.Handle, HwndTopmost, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
        Activate();
    }
}
