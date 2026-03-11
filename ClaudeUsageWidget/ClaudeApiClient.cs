using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace ClaudeUsageWidgetProvider;

public sealed class RateLimitEntry
{
    public string Label { get; set; } = "";
    public double Utilization { get; set; }
    public DateTimeOffset ResetsAt { get; set; }
}

public sealed class UsageData
{
    public List<RateLimitEntry> Limits { get; set; } = [];
    public double? SpendUsed { get; set; }
    public double? SpendLimit { get; set; }
}

internal sealed class ClaudeApiClient : IDisposable
{
    private static readonly HttpClient Http = new()
    {
        Timeout = TimeSpan.FromSeconds(15)
    };

    private DateTimeOffset _lastFetchTime = DateTimeOffset.MinValue;
    private UsageData? _cachedUsage;
    private List<OAuthCredential> _credentials = [];
    private int _credentialIndex;
    private bool _noReload;
    private readonly ServiceType _service;

    private OAuthCredential? _credential => _credentialIndex < _credentials.Count ? _credentials[_credentialIndex] : null;
    private readonly string? _accountKey;

    internal ClaudeApiClient() { }

    internal ClaudeApiClient(AccountInfo account)
    {
        _credentials = [account.Credential];
        _credentialIndex = 0;
        _noReload = true;
        _service = account.Service;
        _accountKey = CredentialStore.GetAccountKey(account);
    }

    public ServiceType AccountService => _service;
    public string? AccountKey => _accountKey;

    public string CredentialPath => _credential?.SourcePath ?? "";
    public string? LastError { get; private set; }

    private static readonly TimeSpan MinFetchInterval = TimeSpan.FromSeconds(60);

    private static readonly string[] ProbeModels =
    [
        "claude-sonnet-4-20250514",
        "claude-haiku-4-5-20251001"
    ];

    private static string BuildMinimalRequest(string model) => JsonSerializer.Serialize(new
    {
        model,
        max_tokens = 1,
        messages = new[] { new { role = "user", content = "." } }
    });

    public async Task<UsageData?> GetUsageAsync(bool forceRefresh = false)
    {
        // Reload credentials from disk on each fresh attempt so changes (add/remove file) take effect immediately
        if (_credentialIndex == 0 && !_noReload)
            _credentials = CredentialStore.LoadAllCredentials();

        if (_credential == null)
        {
            LastError = "No credentials found";
            return null;
        }

        if (!forceRefresh && _cachedUsage != null &&
            DateTimeOffset.UtcNow - _lastFetchTime < MinFetchInterval)
        {
            return _cachedUsage;
        }

        if (_credential.IsExpired)
        {
            if (!await RefreshTokenAsync())
            {
                LastError = "Token expired and refresh failed";
                return _cachedUsage;
            }
        }

        try
        {
            var usage = await FetchWithRetryAsync();
            if (usage != null)
            {
                LastError = null;
                _cachedUsage = usage;
                _lastFetchTime = DateTimeOffset.UtcNow;
            }
            return _cachedUsage;
        }
        catch (UnauthorizedAccessException)
        {
            // Try refresh first
            if (await RefreshTokenAsync())
            {
                try
                {
                    var usage = await FetchUsageFromRateLimitHeadersAsync();
                    if (usage != null)
                    {
                        LastError = null;
                        _cachedUsage = usage;
                        _lastFetchTime = DateTimeOffset.UtcNow;
                    }
                    return _cachedUsage;
                }
                catch { }
            }

            // Refresh selhal — přečti credentials z disku (platí i pro _noReload instance)
            var sourcePath = _credential?.SourcePath;
            if (!string.IsNullOrEmpty(sourcePath))
            {
                var freshCred = CredentialStore.LoadCredentialFromPath(sourcePath);
                if (freshCred != null && freshCred.AccessToken != _credential!.AccessToken)
                {
                    // Nový token — použij ho a zkus znova
                    _credentials[_credentialIndex] = freshCred;
                    return await GetUsageAsync(forceRefresh: true);
                }
                // Stejný token → error stav (nezasekáváme se)
            }

            LastError = "Invalid credentials — re-login with Claude CLI";
            return _cachedUsage;
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            return _cachedUsage;
        }
    }

    private static bool IsTransient(Exception ex) =>
        ex is HttpRequestException or TaskCanceledException;

    private async Task<UsageData?> FetchWithRetryAsync()
    {
        const int maxRetries = 2;
        for (int attempt = 0; attempt < maxRetries; attempt++)
        {
            try
            {
                return await FetchUsageFromRateLimitHeadersAsync();
            }
            catch (Exception ex) when (IsTransient(ex) && attempt < maxRetries - 1)
            {
                await Task.Delay(TimeSpan.FromSeconds(5 * (attempt + 1)));
            }
        }
        return await FetchUsageFromRateLimitHeadersAsync(); // last attempt — let it throw
    }

    private async Task<UsageData?> FetchUsageFromRateLimitHeadersAsync()
    {
        if (_service == ServiceType.Codex)
            return await FetchCodexUsageAsync();

        // Call multiple models in parallel to discover all model-specific rate limits
        var tasks = ProbeModels.Select(model => ProbeModelAsync(model)).ToList();
        var responses = await Task.WhenAll(tasks);

        var limits = new Dictionary<string, RateLimitEntry>();
        foreach (var response in responses)
        {
            if (response == null) continue;
            using (response)
                ExtractRateLimits(response, limits);
        }

        if (limits.Count == 0) return null;

        var result = new UsageData { Limits = limits.Values.ToList() };

        // Mock spend data — TODO: replace with real API data once extra usage headers are discovered
        var spendLimit = SettingsStore.Instance.SpendLimit;
        if (spendLimit > 0)
        {
            const double mockSpendUsed = 22.2;
            result.SpendUsed = mockSpendUsed;
            result.SpendLimit = spendLimit;
            result.Limits.Add(new RateLimitEntry
            {
                Label = "spend",
                Utilization = mockSpendUsed / spendLimit * 100,
                ResetsAt = DateTimeOffset.UtcNow.AddDays(30) // mock: monthly reset
            });
        }

        return result;
    }

    private async Task<HttpResponseMessage?> ProbeModelAsync(string model)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.anthropic.com/v1/messages");
        request.Headers.Add("x-api-key", _credential!.AccessToken);
        request.Headers.Add("anthropic-version", "2023-06-01");
        request.Content = new StringContent(BuildMinimalRequest(model), Encoding.UTF8, "application/json");

        var response = await Http.SendAsync(request);

        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            throw new UnauthorizedAccessException("401 Unauthorized — invalid or expired credentials");

        // Both 200 and 429 carry rate limit headers
        if (response.IsSuccessStatusCode ||
            response.StatusCode == (System.Net.HttpStatusCode)429)
            return response;

        response.Dispose();
        return null;
    }

    private static void ExtractRateLimits(HttpResponseMessage response, Dictionary<string, RateLimitEntry> limits)
    {
        const string prefix = "anthropic-ratelimit-";
        const string utilSuffix = "-utilization";

        foreach (var header in response.Headers)
        {
            var name = header.Key.ToLowerInvariant();
            if (!name.StartsWith(prefix) || !name.EndsWith(utilSuffix)) continue;

            var label = name[prefix.Length..^utilSuffix.Length]; // e.g. "unified-5h", "unified-7d_sonnet"
            if (limits.ContainsKey(label)) continue;

            var utilStr = header.Value.FirstOrDefault();
            if (!double.TryParse(utilStr, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var util)) continue;

            var resetStr = GetHeader(response, $"{prefix}{label}-reset");
            limits[label] = new RateLimitEntry
            {
                Label = label,
                Utilization = util * 100,
                ResetsAt = ParseUnixTimestamp(resetStr)
            };
        }
    }

    private async Task<UsageData?> FetchCodexUsageAsync()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get,
            "https://chatgpt.com/backend-api/wham/usage");
        request.Headers.Add("Authorization", $"Bearer {_credential!.AccessToken}");
        request.Headers.Add("User-Agent",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/145.0.0.0 Safari/537.36");
        request.Headers.Add("oai-client-build-number", "5146072");
        request.Headers.Add("oai-language", "en-US");
        request.Headers.Add("Referer", "https://chatgpt.com/codex/settings/usage");
        request.Headers.Add("sec-fetch-mode", "cors");
        request.Headers.Add("sec-fetch-site", "same-origin");

        var response = await Http.SendAsync(request);

        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            throw new UnauthorizedAccessException("401 Unauthorized — invalid or expired credentials");

        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonNode.Parse(json);
        if (doc == null) return null;

        var limits = new List<RateLimitEntry>();

        var sessionWindow = doc["rate_limit"]?["primary_window"];
        if (sessionWindow != null)
        {
            limits.Add(new RateLimitEntry
            {
                Label = "session",
                Utilization = sessionWindow["used_percent"]?.GetValue<double>() ?? 0,
                ResetsAt = DateTimeOffset.FromUnixTimeSeconds(
                    sessionWindow["reset_at"]?.GetValue<long>() ?? 0)
            });
        }

        var reviewWindow = doc["code_review_rate_limit"]?["primary_window"];
        if (reviewWindow != null)
        {
            limits.Add(new RateLimitEntry
            {
                Label = "review",
                Utilization = reviewWindow["used_percent"]?.GetValue<double>() ?? 0,
                ResetsAt = DateTimeOffset.FromUnixTimeSeconds(
                    reviewWindow["reset_at"]?.GetValue<long>() ?? 0)
            });
        }

        if (limits.Count == 0) return null;
        return new UsageData { Limits = limits };
    }

    public async Task<bool> RefreshTokenAsync()
    {
        if (_credential == null || string.IsNullOrEmpty(_credential.RefreshToken))
            return false;

        // Codex refresh not implemented — token valid for ~10 days, user can re-login
        if (_service == ServiceType.Codex) return false;

        try
        {
            var content = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("grant_type", "refresh_token"),
                new KeyValuePair<string, string>("refresh_token", _credential.RefreshToken)
            });

            var response = await Http.PostAsync(
                "https://console.anthropic.com/v1/oauth/token", content);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            var doc = JsonNode.Parse(json);
            if (doc == null) return false;

            var newAccessToken = doc["access_token"]?.GetValue<string>();
            var newRefreshToken = doc["refresh_token"]?.GetValue<string>();
            var expiresIn = doc["expires_in"]?.GetValue<long>() ?? 0;

            if (string.IsNullOrEmpty(newAccessToken)) return false;

            _credential.AccessToken = newAccessToken;
            if (!string.IsNullOrEmpty(newRefreshToken))
                _credential.RefreshToken = newRefreshToken;
            _credential.ExpiresAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + (expiresIn * 1000);

            CredentialStore.SaveCredential(_credential);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string? GetHeader(HttpResponseMessage response, string name)
    {
        return response.Headers.TryGetValues(name, out var values)
            ? values.FirstOrDefault()
            : null;
    }

    private static DateTimeOffset ParseUnixTimestamp(string? value)
    {
        if (string.IsNullOrEmpty(value)) return DateTimeOffset.UtcNow;
        return long.TryParse(value, out var ts)
            ? DateTimeOffset.FromUnixTimeSeconds(ts)
            : DateTimeOffset.UtcNow;
    }

    public void Dispose() { }
}
