using Windows.Data.Xml.Dom;
using Windows.UI.Notifications;

namespace ClaudeUsageWidgetProvider;

internal sealed class NotificationService
{
    public static readonly NotificationService Instance = new();

    private static readonly double[] Thresholds = [75, 90];

    // key = "{accountKey}:{limitLabel}" → set of thresholds already notified
    private readonly Dictionary<string, HashSet<double>> _notified = new();

    // key = "{accountKey}:{limitLabel}" → last known ResetsAt value
    private readonly Dictionary<string, DateTimeOffset> _lastResetAt = new();

    // key = "{accountKey}:{limitLabel}" → last known utilization value
    private readonly Dictionary<string, double> _lastUtilization = new();

    private NotificationService() { }

    public void CheckAndNotify(string? accountKey, UsageData usage)
    {
        if (string.IsNullOrEmpty(accountKey)) return;
        var settings = SettingsStore.Instance;

        foreach (var limit in usage.Limits)
        {
            var key = $"{accountKey}:{limit.Label}";
            _notified.TryGetValue(key, out var notifiedSet);
            notifiedSet ??= new HashSet<double>();
            _notified[key] = notifiedSet;

            // Detect reset: ResetsAt changed → clear notified + optionally toast
            _lastUtilization.TryGetValue(key, out var prevUtil);
            if (_lastResetAt.TryGetValue(key, out var prevReset) && limit.ResetsAt != prevReset)
            {
                notifiedSet.Clear();
                if (settings.NotificationsEnabled && settings.NotifyOnReset && limit.Utilization < 10 && prevUtil > 0)
                    ShowToast($"Limit reset: {FormatLabel(limit.Label)}", "Usage has been reset.");
            }
            _lastResetAt[key] = limit.ResetsAt;
            _lastUtilization[key] = limit.Utilization;

            // Remove thresholds if utilization dropped below them
            foreach (var threshold in Thresholds)
            {
                if (limit.Utilization < threshold)
                    notifiedSet.Remove(threshold);
            }

            // Notify if utilization crossed a threshold
            if (!settings.NotificationsEnabled) continue;
            foreach (var threshold in Thresholds)
            {
                if (limit.Utilization >= threshold && notifiedSet.Add(threshold))
                {
                    var severity = threshold >= 90 ? "Critical" : "Warning";
                    ShowToast(
                        $"{severity}: {FormatLabel(limit.Label)} at {limit.Utilization:0}%",
                        $"Reset {TimeFormatter.FormatResetTime(limit.ResetsAt)}");
                }
            }
        }
    }

    private static void ShowToast(string title, string body)
    {
        try
        {
            var xml = new XmlDocument();
            xml.LoadXml($"""
                <toast>
                  <visual>
                    <binding template="ToastGeneric">
                      <text>{EscapeXml(title)}</text>
                      <text>{EscapeXml(body)}</text>
                    </binding>
                  </visual>
                </toast>
                """);

            var toast = new ToastNotification(xml);
            ToastNotificationManager.CreateToastNotifier("ClaudeUsageWidget").Show(toast);
        }
        catch { }
    }

    private static string EscapeXml(string s) =>
        s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");

    private static string FormatLabel(string apiLabel)
    {
        var parts = apiLabel.Split('-');
        return parts.Length >= 2 ? parts[^1].ToUpperInvariant() : apiLabel.ToUpperInvariant();
    }
}
