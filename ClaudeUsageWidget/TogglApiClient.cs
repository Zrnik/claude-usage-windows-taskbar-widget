using System.Text;
using System.Text.Json.Nodes;

namespace ClaudeUsageWidgetProvider;

public sealed class TogglProject
{
    public long Id { get; set; }
    public string Name { get; set; } = "";
    public long? ClientId { get; set; }
    public string? ClientName { get; set; }
    public bool Active { get; set; } = true;
}

public sealed class ProjectEarnings
{
    public long ProjectId { get; set; }
    public string ProjectName { get; set; } = "";
    public string? ClientName { get; set; }
    public double Hours { get; set; }
    public double RateCzk { get; set; }
    public double Earned { get; set; }
}

public sealed class TogglUsageData
{
    public double HoursWorked { get; set; }
    public double EarnedCzk { get; set; }
    public double TargetCzk { get; set; }
    public DateTimeOffset MonthStart { get; set; }
    public DateTimeOffset MonthResetsAt { get; set; }
    public List<ProjectEarnings> Breakdown { get; set; } = [];
}

internal sealed class TogglApiClient : IDisposable
{
    private static readonly HttpClient Http = new()
    {
        Timeout = TimeSpan.FromSeconds(15)
    };

    private static readonly TimeSpan FetchInterval = TimeSpan.FromMinutes(5);
    private const string BaseUrl = "https://api.track.toggl.com/api/v9";
    internal const string AccountKey = "toggl";

    private DateTimeOffset _lastFetchTime = DateTimeOffset.MinValue;
    private TogglUsageData? _cachedUsage;

    public string? LastError { get; private set; }

    public async Task<TogglUsageData?> GetUsageAsync(bool forceRefresh = false)
    {
        var settings = SettingsStore.Instance;
        if (string.IsNullOrWhiteSpace(settings.TogglApiKey))
        {
            LastError = "Toggl API key not set";
            return null;
        }

        if (!forceRefresh && _cachedUsage != null &&
            DateTimeOffset.UtcNow - _lastFetchTime < FetchInterval)
        {
            return _cachedUsage;
        }

        try
        {
            var usage = await FetchUsageAsync(settings);
            if (usage != null)
            {
                LastError = null;
                _cachedUsage = usage;
                _lastFetchTime = DateTimeOffset.UtcNow;
            }
            return _cachedUsage;
        }
        catch (HttpRequestException ex)
        {
            LastError = ex.Message;
            return _cachedUsage;
        }
        catch (TaskCanceledException)
        {
            LastError = "Toggl request timed out";
            return _cachedUsage;
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            return _cachedUsage;
        }
    }

    private async Task<TogglUsageData?> FetchUsageAsync(SettingsStore settings)
    {
        var now = DateTimeOffset.Now;
        var monthStart = new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, now.Offset);
        var nextMonth = monthStart.AddMonths(1);

        var startIso = monthStart.ToString("yyyy-MM-ddTHH:mm:ssK");
        var endIso = now.ToString("yyyy-MM-ddTHH:mm:ssK");

        using var request = new HttpRequestMessage(HttpMethod.Get,
            $"{BaseUrl}/me/time_entries?start_date={Uri.EscapeDataString(startIso)}&end_date={Uri.EscapeDataString(endIso)}");
        AddAuth(request, settings.TogglApiKey);

        var response = await Http.SendAsync(request);
        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized ||
            response.StatusCode == System.Net.HttpStatusCode.Forbidden)
        {
            LastError = "Invalid Toggl API key";
            return null;
        }
        if ((int)response.StatusCode == 402)
        {
            LastError = "Toggl API rate limit exceeded (30/h)";
            return null;
        }
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        var arr = JsonNode.Parse(json) as JsonArray;
        if (arr == null)
        {
            LastError = "Unexpected Toggl response";
            return null;
        }

        var projectNames = await LoadProjectNamesAsync(settings.TogglApiKey);

        var perProject = new Dictionary<long, (double Hours, string Name, string? Client)>();
        double totalHours = 0;

        foreach (var entry in arr)
        {
            if (entry == null) continue;
            var duration = entry["duration"]?.GetValue<long>() ?? 0;
            if (duration <= 0) continue; // skip running timers (negative duration) and zero entries
            var projectId = entry["project_id"]?.GetValue<long?>() ?? 0;
            var hours = duration / 3600.0;
            totalHours += hours;

            if (!perProject.TryGetValue(projectId, out var agg))
            {
                projectNames.TryGetValue(projectId, out var meta);
                agg = (0.0, meta.Name ?? (projectId == 0 ? "(no project)" : $"#{projectId}"), meta.ClientName);
            }
            perProject[projectId] = (agg.Hours + hours, agg.Name, agg.Client);
        }

        double totalEarned = 0;
        var breakdown = new List<ProjectEarnings>();
        foreach (var (projectId, agg) in perProject)
        {
            settings.TogglProjectRates.TryGetValue(projectId, out var rate);
            var earned = agg.Hours * rate;
            totalEarned += earned;
            breakdown.Add(new ProjectEarnings
            {
                ProjectId = projectId,
                ProjectName = agg.Name,
                ClientName = agg.Client,
                Hours = agg.Hours,
                RateCzk = rate,
                Earned = earned
            });
        }
        breakdown.Sort((a, b) => b.Earned.CompareTo(a.Earned));

        return new TogglUsageData
        {
            HoursWorked = totalHours,
            EarnedCzk = totalEarned,
            TargetCzk = settings.TogglMonthlyTargetCzk,
            MonthStart = monthStart,
            MonthResetsAt = nextMonth,
            Breakdown = breakdown
        };
    }

    private readonly Dictionary<long, (string Name, string? ClientName)> _projectCache = new();
    private DateTimeOffset _projectCacheLoadedAt = DateTimeOffset.MinValue;

    private async Task<Dictionary<long, (string Name, string? ClientName)>> LoadProjectNamesAsync(string apiKey)
    {
        // Refresh project list once per hour to pick up new projects
        if (_projectCache.Count > 0 && DateTimeOffset.UtcNow - _projectCacheLoadedAt < TimeSpan.FromHours(1))
            return _projectCache;

        try
        {
            var projects = await FetchProjectsAsync(apiKey);
            _projectCache.Clear();
            foreach (var p in projects)
                _projectCache[p.Id] = (p.Name, p.ClientName);
            _projectCacheLoadedAt = DateTimeOffset.UtcNow;
        }
        catch
        {
            // fallback: return whatever we have
        }
        return _projectCache;
    }

    public async Task<List<TogglProject>> FetchProjectsAsync(string? apiKey = null)
    {
        apiKey ??= SettingsStore.Instance.TogglApiKey;
        if (string.IsNullOrWhiteSpace(apiKey))
            return [];

        using var request = new HttpRequestMessage(HttpMethod.Get,
            $"{BaseUrl}/me?with_related_data=true");
        AddAuth(request, apiKey);

        var response = await Http.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonNode.Parse(json);
        if (doc == null) return [];

        var clientsMap = new Dictionary<long, string>();
        if (doc["clients"] is JsonArray clients)
        {
            foreach (var c in clients)
            {
                if (c == null) continue;
                var id = c["id"]?.GetValue<long>() ?? 0;
                var name = c["name"]?.GetValue<string>() ?? "";
                if (id > 0) clientsMap[id] = name;
            }
        }

        var result = new List<TogglProject>();
        if (doc["projects"] is JsonArray projects)
        {
            foreach (var p in projects)
            {
                if (p == null) continue;
                var id = p["id"]?.GetValue<long>() ?? 0;
                if (id <= 0) continue;
                var name = p["name"]?.GetValue<string>() ?? "";
                var clientId = p["client_id"]?.GetValue<long?>();
                var active = p["active"]?.GetValue<bool?>() ?? true;
                result.Add(new TogglProject
                {
                    Id = id,
                    Name = name,
                    ClientId = clientId,
                    ClientName = clientId.HasValue && clientsMap.TryGetValue(clientId.Value, out var cn) ? cn : null,
                    Active = active
                });
            }
        }

        result.Sort((a, b) =>
        {
            var ca = a.ClientName ?? "";
            var cb = b.ClientName ?? "";
            var cmp = string.Compare(ca, cb, StringComparison.OrdinalIgnoreCase);
            return cmp != 0 ? cmp : string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
        });
        return result;
    }

    public static async Task<(bool Success, string? Error)> ValidateKeyAsync(string apiKey)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
            return (false, "Empty API key");
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"{BaseUrl}/me");
            AddAuth(request, apiKey);
            var response = await Http.SendAsync(request);
            if (response.IsSuccessStatusCode)
                return (true, null);
            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized ||
                response.StatusCode == System.Net.HttpStatusCode.Forbidden)
                return (false, "Invalid API key");
            return (false, $"HTTP {(int)response.StatusCode}");
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    private static void AddAuth(HttpRequestMessage request, string apiKey)
    {
        var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{apiKey}:api_token"));
        request.Headers.Add("Authorization", $"Basic {credentials}");
    }

    public void Dispose() { }
}
