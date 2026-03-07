using System.Diagnostics;
using System.Net.Http.Headers;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json.Nodes;

namespace ClaudeUsageWidgetProvider;

internal sealed class UpdateInfo
{
    public string Version { get; init; } = "";
    public string DownloadUrl { get; init; } = "";
}

internal static class Updater
{
    private const string Repo = "Zrnik/claude-usage-windows-taskbar-widget";

    private static readonly HttpClient Http = new();
    private static Task<UpdateInfo?>? _checkTask;

    static Updater()
    {
        Http.DefaultRequestHeaders.UserAgent.Add(
            new ProductInfoHeaderValue("ClaudeUsageWidget", CurrentVersion));
    }

    public static string CurrentVersion
    {
        get
        {
            var v = Assembly.GetExecutingAssembly().GetName().Version;
            return v == null ? "0.0.0" : $"{v.Major}.{v.Minor}.{v.Build}";
        }
    }

    // Deduplicates — multiple callers share the same Task
    public static Task<UpdateInfo?> CheckAsync() =>
        _checkTask ??= DoCheckAsync();

    private static async Task<UpdateInfo?> DoCheckAsync()
    {
        try
        {
            var json = await Http.GetStringAsync(
                $"https://api.github.com/repos/{Repo}/releases/latest");
            var node = JsonNode.Parse(json);
            if (node == null) return null;

            var tag = node["tag_name"]?.GetValue<string>() ?? "";
            var latestVersion = tag.TrimStart('v');

            var arch = RuntimeInformation.ProcessArchitecture == Architecture.Arm64 ? "arm64" : "x64";
            var assetName = $"ClaudeUsageWidget-win-{arch}.exe";

            var assets = node["assets"]?.AsArray();
            if (assets == null) return null;

            foreach (var asset in assets)
            {
                if (asset?["name"]?.GetValue<string>() == assetName)
                {
                    var url = asset["browser_download_url"]?.GetValue<string>();
                    if (url != null)
                        return new UpdateInfo { Version = latestVersion, DownloadUrl = url };
                }
            }
        }
        catch { }
        return null;
    }

    public static void LaunchUpdaterTerminal(UpdateInfo update)
    {
        var scriptPath = Path.Combine(Path.GetTempPath(), "ClaudeUsageWidget_update.ps1");
        var currentExe = Environment.ProcessPath!;
        var currentPid = Environment.ProcessId;
        var tempExe = Path.Combine(Path.GetTempPath(), "ClaudeUsageWidget_update.exe");

        // PS script downloads, kills the app, replaces exe, starts new version
        var script =
            $"Write-Host 'Downloading Claude Usage Widget...' -ForegroundColor Cyan\r\n" +
            $"curl.exe -L --progress-bar -o '{tempExe}' '{update.DownloadUrl}'\r\n" +
            $"Write-Host 'Stopping app...' -ForegroundColor Yellow\r\n" +
            $"Stop-Process -Id {currentPid} -Force -ErrorAction SilentlyContinue\r\n" +
            $"Start-Sleep -Milliseconds 500\r\n" +
            $"Write-Host 'Installing update...' -ForegroundColor Yellow\r\n" +
            $"Copy-Item '{tempExe}' '{currentExe}' -Force\r\n" +
            $"Remove-Item '{tempExe}' -Force\r\n" +
            $"Write-Host 'Launching new version...' -ForegroundColor Green\r\n" +
            $"Start-Process '{currentExe}'\r\n" +
            $"Start-Sleep -Seconds 2\r\n" +
            $"Remove-Item '{scriptPath}' -Force\r\n";

        File.WriteAllText(scriptPath, script);

        Process.Start(new ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = $"/c start \"ClaudeUsageWidget Update\" powershell.exe -ExecutionPolicy Bypass -File \"{scriptPath}\"",
            UseShellExecute = false,
            CreateNoWindow = true
        });

    }
}
