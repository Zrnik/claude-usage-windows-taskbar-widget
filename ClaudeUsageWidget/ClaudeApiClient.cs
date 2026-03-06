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
}

internal sealed class ClaudeApiClient : IDisposable
{
    private static readonly HttpClient Http = new()
    {
        Timeout = TimeSpan.FromSeconds(15)
    };

    private DateTimeOffset _lastFetchTime = DateTimeOffset.MinValue;
    private UsageData? _cachedUsage;
    private OAuthCredential? _credential;

    public string CredentialPath => _credential?.SourcePath ?? "";
    public string? LastError { get; private set; }

    private static readonly TimeSpan MinFetchInterval = TimeSpan.FromSeconds(60);

    private static readonly string MinimalRequestBody = JsonSerializer.Serialize(new
    {
        model = "claude-haiku-4-5-20251001",
        max_tokens = 1,
        messages = new[] { new { role = "user", content = "." } }
    });

    public void SetCredential(OAuthCredential credential)
    {
        _credential = credential;
    }

    public async Task<UsageData?> GetUsageAsync(bool forceRefresh = false)
    {
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
            var usage = await FetchUsageFromRateLimitHeadersAsync();
            if (usage != null)
            {
                LastError = null;
                _cachedUsage = usage;
                _lastFetchTime = DateTimeOffset.UtcNow;
            }
            return _cachedUsage;
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            return _cachedUsage;
        }
    }

    private async Task<UsageData?> FetchUsageFromRateLimitHeadersAsync()
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.anthropic.com/v1/messages");
        request.Headers.Add("x-api-key", _credential!.AccessToken);
        request.Headers.Add("anthropic-version", "2023-06-01");
        request.Content = new StringContent(MinimalRequestBody, Encoding.UTF8, "application/json");

        var response = await Http.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var limits = new List<RateLimitEntry>();
        const string prefix = "anthropic-ratelimit-";
        const string utilSuffix = "-utilization";

        foreach (var header in response.Headers)
        {
            var name = header.Key.ToLowerInvariant();
            if (!name.StartsWith(prefix) || !name.EndsWith(utilSuffix)) continue;

            var label = name[prefix.Length..^utilSuffix.Length]; // e.g. "unified-5h"
            var utilStr = header.Value.FirstOrDefault();
            if (!double.TryParse(utilStr, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var util)) continue;

            var resetStr = GetHeader(response, $"{prefix}{label}-reset");
            limits.Add(new RateLimitEntry
            {
                Label = label,
                Utilization = util * 100,
                ResetsAt = ParseUnixTimestamp(resetStr)
            });
        }

        if (limits.Count == 0) return null;
        return new UsageData { Limits = limits };
    }

    public async Task<bool> RefreshTokenAsync()
    {
        if (_credential == null || string.IsNullOrEmpty(_credential.RefreshToken))
            return false;

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
