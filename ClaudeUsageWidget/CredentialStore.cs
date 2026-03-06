using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace ClaudeUsageWidgetProvider;

internal sealed class OAuthCredential
{
    public string AccessToken { get; set; } = "";
    public string RefreshToken { get; set; } = "";
    public long ExpiresAt { get; set; } // Unix epoch milliseconds
    public string SourcePath { get; set; } = ""; // "wsl:~/.claude/.credentials.json" or Windows path

    public bool IsExpired =>
        DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() >= ExpiresAt - 60_000; // 60s buffer
}

internal static class CredentialStore
{
    private const string WslCredPath = "~/.claude/.credentials.json";
    private const string WslSourceMarker = "wsl:" + WslCredPath;

    public static OAuthCredential? LoadCredential()
    {
        var all = LoadAllCredentials();
        return all.Count > 0 ? all[0] : null;
    }

    public static List<OAuthCredential> LoadAllCredentials()
    {
        var result = new List<OAuthCredential>();

        var wslCred = TryReadWslCredential();
        if (wslCred != null) result.Add(wslCred);

        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var nativePath = Path.Combine(userProfile, ".claude", ".credentials.json");
        var nativeCred = TryReadCredential(nativePath);
        if (nativeCred != null) result.Add(nativeCred);

        return result;
    }

    public static void SaveCredential(OAuthCredential credential)
    {
        if (string.IsNullOrEmpty(credential.SourcePath)) return;

        if (credential.SourcePath == WslSourceMarker)
        {
            SaveWslCredential(credential);
            return;
        }

        try
        {
            JsonNode? root;
            if (File.Exists(credential.SourcePath))
            {
                var existing = File.ReadAllText(credential.SourcePath);
                root = JsonNode.Parse(existing) ?? new JsonObject();
            }
            else
            {
                root = new JsonObject();
            }

            root["claudeAiOauth"] = new JsonObject
            {
                ["accessToken"] = credential.AccessToken,
                ["refreshToken"] = credential.RefreshToken,
                ["expiresAt"] = credential.ExpiresAt
            };

            var options = new JsonSerializerOptions { WriteIndented = true };
            File.WriteAllText(credential.SourcePath, root.ToJsonString(options));
        }
        catch
        {
            // Best-effort write-back
        }
    }

    private static OAuthCredential? TryReadWslCredential()
    {
        try
        {
            var json = RunWsl($"cat {WslCredPath}");
            if (string.IsNullOrWhiteSpace(json)) return null;

            return ParseCredentialJson(json, WslSourceMarker);
        }
        catch
        {
            return null;
        }
    }

    private static void SaveWslCredential(OAuthCredential credential)
    {
        try
        {
            // Read existing JSON from WSL
            var existingJson = RunWsl($"cat {WslCredPath}");
            JsonNode root = string.IsNullOrWhiteSpace(existingJson)
                ? new JsonObject()
                : JsonNode.Parse(existingJson) ?? new JsonObject();

            root["claudeAiOauth"] = new JsonObject
            {
                ["accessToken"] = credential.AccessToken,
                ["refreshToken"] = credential.RefreshToken,
                ["expiresAt"] = credential.ExpiresAt
            };

            var options = new JsonSerializerOptions { WriteIndented = true };
            var newJson = root.ToJsonString(options);

            // Write via wsl.exe stdin
            if (!File.Exists(WslExe)) return;
            var psi = new ProcessStartInfo
            {
                FileName = WslExe,
                Arguments = $"-- bash -c 'cat > {WslCredPath}'",
                RedirectStandardInput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var proc = Process.Start(psi);
            if (proc == null) return;
            proc.StandardInput.Write(newJson);
            proc.StandardInput.Close();
            proc.WaitForExit(3000);
        }
        catch
        {
            // Best-effort write-back
        }
    }

    private static readonly string WslExe =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "wsl.exe");

    private static string? RunWsl(string command)
    {
        if (!File.Exists(WslExe)) return null;
        var psi = new ProcessStartInfo
        {
            FileName = WslExe,
            Arguments = $"-- {command}",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        using var proc = Process.Start(psi);
        if (proc == null) return null;
        var output = proc.StandardOutput.ReadToEnd();
        proc.WaitForExit(3000);
        return proc.ExitCode == 0 ? output : null;
    }

    private static OAuthCredential? TryReadCredential(string path)
    {
        try
        {
            if (!File.Exists(path)) return null;
            var json = File.ReadAllText(path);
            return ParseCredentialJson(json, path);
        }
        catch
        {
            return null;
        }
    }

    private static OAuthCredential? ParseCredentialJson(string json, string sourcePath)
    {
        try
        {
            var doc = JsonNode.Parse(json);
            var oauth = doc?["claudeAiOauth"];
            if (oauth == null) return null;

            var accessToken = oauth["accessToken"]?.GetValue<string>();
            var refreshToken = oauth["refreshToken"]?.GetValue<string>();
            var expiresAt = oauth["expiresAt"]?.GetValue<long>() ?? 0;

            if (string.IsNullOrEmpty(accessToken) || string.IsNullOrEmpty(refreshToken))
                return null;

            return new OAuthCredential
            {
                AccessToken = accessToken,
                RefreshToken = refreshToken,
                ExpiresAt = expiresAt,
                SourcePath = sourcePath
            };
        }
        catch
        {
            return null;
        }
    }
}
