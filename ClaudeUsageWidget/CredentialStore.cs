using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace ClaudeUsageWidgetProvider;

internal enum ServiceType { Claude, Codex }

internal sealed record AccountInfo(
    ServiceType Service,
    OAuthCredential Credential,
    string Label
);

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

    public static List<AccountInfo> LoadAllAccounts()
    {
        var result = new List<AccountInfo>();
        var seen = new HashSet<string>();

        foreach (var cred in LoadAllCredentials())
        {
            var token = cred.AccessToken;
            var key = GetAccountKey(token, ServiceType.Claude);
            if (key == null) continue; // tiché přeskočení — žádný org_id v JWT
            if (!seen.Add(key)) continue; // duplikát (např. WSL i Windows = stejný org)

            var label = cred.SourcePath.StartsWith("wsl:") ? "claude-wsl" : "claude-windows";
            result.Add(new AccountInfo(ServiceType.Claude, cred, label));
        }

        foreach (var account in LoadCodexCredentials())
        {
            var token = account.Credential.AccessToken;
            var key = GetAccountKey(token, ServiceType.Codex);
            if (key == null) continue; // tiché přeskočení
            if (!seen.Add(key)) continue; // duplikát (např. WSL i Windows = stejný org)

            result.Add(account);
        }

        return result;
    }

    private static List<AccountInfo> LoadCodexCredentials()
    {
        var result = new List<AccountInfo>();

        var win = TryLoadCodexWindowsCredential();
        if (win != null) result.Add(win);

        var wsl = TryLoadCodexWslCredential();
        if (wsl != null) result.Add(wsl);

        return result;
    }

    private static AccountInfo? TryLoadCodexWindowsCredential()
    {
        var path = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".codex",
            "auth.json");

        if (!File.Exists(path)) return null;

        try
        {
            var json = File.ReadAllText(path);
            var cred = ParseCodexCredentialJson(json, path);
            return cred != null ? new AccountInfo(ServiceType.Codex, cred, "codex-windows") : null;
        }
        catch
        {
            return null;
        }
    }

    private static AccountInfo? TryLoadCodexWslCredential()
    {
        try
        {
            var json = RunWsl("cat ~/.codex/auth.json");
            if (string.IsNullOrWhiteSpace(json)) return null;
            var cred = ParseCodexCredentialJson(json, "wsl:~/.codex/auth.json");
            return cred != null ? new AccountInfo(ServiceType.Codex, cred, "codex-wsl") : null;
        }
        catch
        {
            return null;
        }
    }

    private static OAuthCredential? ParseCodexCredentialJson(string json, string sourcePath)
    {
        try
        {
            var doc = JsonNode.Parse(json);
            if (doc == null) return null;

            // Codex auth.json nests tokens under "tokens" object
            var tokensNode = doc["tokens"];
            var tokenNode = doc["access_token"] ?? doc["accessToken"] ?? doc["token"]
                ?? tokensNode?["access_token"];
            var token = tokenNode?.GetValue<string>();
            if (string.IsNullOrEmpty(token)) return null;

            var refreshNode = doc["refresh_token"] ?? doc["refreshToken"]
                ?? tokensNode?["refresh_token"];
            var refreshToken = refreshNode?.GetValue<string>() ?? "";

            // Try to get expiry from JWT payload (exp field, seconds → ms)
            var expiresAt = GetJwtExpiryMs(token);

            return new OAuthCredential
            {
                AccessToken = token,
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

    private static long GetJwtExpiryMs(string jwt)
    {
        try
        {
            var parts = jwt.Split('.');
            if (parts.Length < 2) return long.MaxValue;
            var payload = parts[1];
            var padded = payload.PadRight((payload.Length + 3) / 4 * 4, '=')
                                .Replace('-', '+').Replace('_', '/');
            var bytes = Convert.FromBase64String(padded);
            var doc = JsonNode.Parse(System.Text.Encoding.UTF8.GetString(bytes));
            var exp = doc?["exp"]?.GetValue<long>() ?? 0;
            return exp > 0 ? exp * 1000L : long.MaxValue;
        }
        catch { return long.MaxValue; }
    }

    private static string? GetJwtClaim(string jwt, string claimName)
    {
        try
        {
            var parts = jwt.Split('.');
            if (parts.Length < 2) return null;
            var payload = parts[1];
            var padded = payload.PadRight((payload.Length + 3) / 4 * 4, '=')
                                .Replace('-', '+').Replace('_', '/');
            var bytes = Convert.FromBase64String(padded);
            var doc = JsonNode.Parse(System.Text.Encoding.UTF8.GetString(bytes));
            return doc?[claimName]?.GetValue<string>();
        }
        catch { return null; }
    }

    private static string? GetAccountKey(string token, ServiceType service)
    {
        if (service == ServiceType.Claude)
        {
            // Pokus o org_id, fallback na sub (standard JWT claim)
            var orgId = GetJwtClaim(token, "org_id")
                     ?? GetJwtClaim(token, "organization_id")
                     ?? GetJwtClaim(token, "sub");
            return orgId != null ? "claude:" + orgId : null;
        }
        else // Codex
        {
            var accountId = GetJwtClaim(token, "account_id")
                         ?? GetJwtClaim(token, "sub");
            return accountId != null ? "codex:" + accountId : null;
        }
    }

    public static OAuthCredential? LoadCredentialFromPath(string sourcePath)
    {
        if (sourcePath == WslSourceMarker)
            return TryReadWslCredential();
        return TryReadCredential(sourcePath);
    }
}
