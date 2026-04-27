using System.Text;
using System.Text.Json.Nodes;

namespace ClaudeUsageWidgetProvider;

public sealed class JiraUser
{
    public string AccountId { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string? EmailAddress { get; set; }
    public string? AvatarUrl { get; set; }
}

public sealed class JiraProject
{
    public string Key { get; set; } = "";
    public string Name { get; set; } = "";
    public string Display => $"{Name}  ({Key})";
}

public sealed class JiraIssueStat
{
    public string AccountId { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public int TotalIssues { get; set; }
    public int DoneIssues { get; set; }
    public double TotalStoryPoints { get; set; }
    public double DoneStoryPoints { get; set; }
}

public sealed class JiraIssueDetail
{
    public string Key { get; set; } = "";
    public string Summary { get; set; } = "";
    public string StatusName { get; set; } = "";
    public string StatusCategory { get; set; } = ""; // new / indeterminate / done
    public double StoryPoints { get; set; }
    public DateTimeOffset? Updated { get; set; }
}

public sealed class JiraUsageData
{
    public string ProjectKey { get; set; } = "";
    public JiraUser? Me { get; set; }
    /// <summary>Status name → count of MY issues in that status.</summary>
    public Dictionary<string, int> MyByStatus { get; set; } = new();
    /// <summary>"new" / "indeterminate" / "done" category → count of MY issues.</summary>
    public Dictionary<string, int> MyByCategory { get; set; } = new();
    public double MyStoryPoints { get; set; }
    public double MyDoneStoryPoints { get; set; }
    /// <summary>My active (not Done) issues — for popup task list.</summary>
    public List<JiraIssueDetail> MyActiveIssues { get; set; } = [];
    /// <summary>All tracked developers' stats. Sorted by DoneStoryPoints desc (then DoneIssues desc).</summary>
    public List<JiraIssueStat> DeveloperRanking { get; set; } = [];
    public int MyRank { get; set; } // 1-based position in ranking, 0 if not ranked
}

internal sealed class JiraApiClient : IDisposable
{
    private static readonly HttpClient Http = new()
    {
        Timeout = TimeSpan.FromSeconds(15)
    };

    private static readonly TimeSpan FetchInterval = TimeSpan.FromMinutes(5);
    internal const string AccountKey = "jira";

    private DateTimeOffset _lastFetchTime = DateTimeOffset.MinValue;
    private JiraUsageData? _cachedUsage;
    private string? _storyPointsFieldId; // discovered once, cached

    public string? LastError { get; private set; }

    public async Task<JiraUsageData?> GetUsageAsync(bool forceRefresh = false)
    {
        var settings = SettingsStore.Instance;
        if (!HasCreds(settings))
        {
            LastError = "JIRA credentials not set";
            return null;
        }

        if (!forceRefresh && _cachedUsage != null &&
            DateTimeOffset.UtcNow - _lastFetchTime < FetchInterval)
            return _cachedUsage;

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
            LastError = "JIRA request timed out";
            return _cachedUsage;
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            return _cachedUsage;
        }
    }

    private static bool HasCreds(SettingsStore s) =>
        !string.IsNullOrWhiteSpace(s.JiraUrl) &&
        !string.IsNullOrWhiteSpace(s.JiraEmail) &&
        !string.IsNullOrWhiteSpace(s.JiraApiToken) &&
        !string.IsNullOrWhiteSpace(s.JiraProjectKey);

    private async Task<JiraUsageData?> FetchUsageAsync(SettingsStore settings)
    {
        var me = await FetchMyselfAsync(settings.JiraUrl, settings.JiraEmail, settings.JiraApiToken);
        if (me == null) return null;

        // Discover Story Points custom field id (lazy, cached for client lifetime)
        _storyPointsFieldId ??= await DiscoverStoryPointsFieldAsync(settings);

        var data = new JiraUsageData
        {
            ProjectKey = settings.JiraProjectKey,
            Me = me
        };

        // Fetch all issues in the project (current sprint or all open — let's pull active sprint or fallback to non-Done)
        // JQL: project = X AND (sprint in openSprints() OR resolution = Unresolved)
        // Using simpler JQL that works without agile: project = X
        var jql = $"project = \"{Escape(settings.JiraProjectKey)}\"";
        var issues = await SearchIssuesAsync(settings, jql, _storyPointsFieldId);

        // Aggregate per developer
        var perDev = new Dictionary<string, JiraIssueStat>();
        foreach (var issue in issues)
        {
            if (issue.AssigneeAccountId == null) continue;
            if (!perDev.TryGetValue(issue.AssigneeAccountId, out var stat))
            {
                stat = new JiraIssueStat
                {
                    AccountId = issue.AssigneeAccountId,
                    DisplayName = issue.AssigneeDisplayName ?? "(unknown)"
                };
                perDev[issue.AssigneeAccountId] = stat;
            }
            stat.TotalIssues++;
            stat.TotalStoryPoints += issue.StoryPoints;
            if (issue.StatusCategory == "done")
            {
                stat.DoneIssues++;
                stat.DoneStoryPoints += issue.StoryPoints;
            }
            // Per-me breakdown
            if (issue.AssigneeAccountId == me.AccountId)
            {
                if (!data.MyByStatus.TryAdd(issue.StatusName, 1))
                    data.MyByStatus[issue.StatusName]++;
                if (!data.MyByCategory.TryAdd(issue.StatusCategory, 1))
                    data.MyByCategory[issue.StatusCategory]++;
                data.MyStoryPoints += issue.StoryPoints;
                if (issue.StatusCategory == "done")
                    data.MyDoneStoryPoints += issue.StoryPoints;
                else
                {
                    data.MyActiveIssues.Add(new JiraIssueDetail
                    {
                        Key = issue.Key,
                        Summary = issue.Summary,
                        StatusName = issue.StatusName,
                        StatusCategory = issue.StatusCategory,
                        StoryPoints = issue.StoryPoints,
                        Updated = issue.Updated
                    });
                }
            }
        }
        // Order: In Progress first, then To Do; within each by most recently updated
        data.MyActiveIssues.Sort((a, b) =>
        {
            int rank(string c) => c switch { "indeterminate" => 0, "new" => 1, _ => 2 };
            var rc = rank(a.StatusCategory).CompareTo(rank(b.StatusCategory));
            if (rc != 0) return rc;
            return (b.Updated ?? DateTimeOffset.MinValue).CompareTo(a.Updated ?? DateTimeOffset.MinValue);
        });

        // Filter ranking to selected developers (or include all if none selected)
        var selected = settings.JiraDeveloperAccountIds;
        var ranking = perDev.Values
            .Where(s => selected.Count == 0 || selected.Contains(s.AccountId))
            .OrderByDescending(s => s.DoneStoryPoints)
            .ThenByDescending(s => s.DoneIssues)
            .ToList();
        // Ensure "me" appears in ranking even if not in selected list
        if (ranking.All(r => r.AccountId != me.AccountId) && perDev.TryGetValue(me.AccountId, out var meStat))
        {
            ranking.Add(meStat);
            ranking = ranking.OrderByDescending(s => s.DoneStoryPoints)
                .ThenByDescending(s => s.DoneIssues).ToList();
        }
        data.DeveloperRanking = ranking;
        data.MyRank = ranking.FindIndex(r => r.AccountId == me.AccountId) + 1;

        return data;
    }

    public async Task<JiraUser?> FetchMyselfAsync(string url, string email, string token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"{TrimSlash(url)}/rest/api/3/myself");
        AddAuth(request, email, token);
        var response = await Http.SendAsync(request);
        if (!response.IsSuccessStatusCode) return null;
        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonNode.Parse(json);
        if (doc == null) return null;
        return new JiraUser
        {
            AccountId = doc["accountId"]?.GetValue<string>() ?? "",
            DisplayName = doc["displayName"]?.GetValue<string>() ?? "",
            EmailAddress = doc["emailAddress"]?.GetValue<string>(),
            AvatarUrl = doc["avatarUrls"]?["48x48"]?.GetValue<string>()
        };
    }

    public async Task<List<JiraUser>> FetchAssignableUsersAsync(string? projectKey = null)
    {
        var settings = SettingsStore.Instance;
        projectKey ??= settings.JiraProjectKey;
        if (string.IsNullOrWhiteSpace(projectKey) || !HasCreds(settings)) return [];

        var users = new List<JiraUser>();
        // Atlassian requires `query` for assignable/multiProjectSearch — use empty string + project filter
        using var request = new HttpRequestMessage(HttpMethod.Get,
            $"{TrimSlash(settings.JiraUrl)}/rest/api/3/user/assignable/search?project={Uri.EscapeDataString(projectKey)}&maxResults=100");
        AddAuth(request, settings.JiraEmail, settings.JiraApiToken);
        var response = await Http.SendAsync(request);
        if (!response.IsSuccessStatusCode) return users;
        var json = await response.Content.ReadAsStringAsync();
        var arr = JsonNode.Parse(json) as JsonArray;
        if (arr == null) return users;
        foreach (var u in arr)
        {
            if (u == null) continue;
            // Filter out app users and disabled accounts
            var accountType = u["accountType"]?.GetValue<string>();
            var active = u["active"]?.GetValue<bool>() ?? false;
            if (!active || accountType != "atlassian") continue;
            users.Add(new JiraUser
            {
                AccountId = u["accountId"]?.GetValue<string>() ?? "",
                DisplayName = u["displayName"]?.GetValue<string>() ?? "",
                EmailAddress = u["emailAddress"]?.GetValue<string>(),
                AvatarUrl = u["avatarUrls"]?["24x24"]?.GetValue<string>()
            });
        }
        users.Sort((a, b) => string.Compare(a.DisplayName, b.DisplayName, StringComparison.OrdinalIgnoreCase));
        return users;
    }

    public async Task<List<JiraProject>> FetchProjectsAsync(string? url = null, string? email = null, string? token = null)
    {
        var settings = SettingsStore.Instance;
        url ??= settings.JiraUrl;
        email ??= settings.JiraEmail;
        token ??= settings.JiraApiToken;
        if (string.IsNullOrWhiteSpace(url) || string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(token))
            return [];

        var result = new List<JiraProject>();
        int startAt = 0;
        const int pageSize = 50;
        while (true)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get,
                $"{TrimSlash(url)}/rest/api/3/project/search?startAt={startAt}&maxResults={pageSize}");
            AddAuth(request, email, token);
            var response = await Http.SendAsync(request);
            if (!response.IsSuccessStatusCode) break;
            var json = await response.Content.ReadAsStringAsync();
            var doc = JsonNode.Parse(json);
            if (doc?["values"] is not JsonArray arr) break;
            foreach (var p in arr)
            {
                if (p == null) continue;
                var key = p["key"]?.GetValue<string>();
                var name = p["name"]?.GetValue<string>();
                if (!string.IsNullOrEmpty(key))
                    result.Add(new JiraProject { Key = key, Name = name ?? key });
            }
            int total = doc["total"]?.GetValue<int>() ?? result.Count;
            startAt += pageSize;
            if (startAt >= total || arr.Count == 0) break;
        }
        result.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
        return result;
    }

    public static async Task<(bool Success, string? Error)> ValidateCredsAsync(string url, string email, string token)
    {
        if (string.IsNullOrWhiteSpace(url) || string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(token))
            return (false, "Missing URL, email, or token");
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"{TrimSlash(url)}/rest/api/3/myself");
            AddAuth(request, email, token);
            var response = await Http.SendAsync(request);
            if (response.IsSuccessStatusCode) return (true, null);
            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized ||
                response.StatusCode == System.Net.HttpStatusCode.Forbidden)
                return (false, "Invalid credentials");
            return (false, $"HTTP {(int)response.StatusCode}");
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    private async Task<string?> DiscoverStoryPointsFieldAsync(SettingsStore settings)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get,
                $"{TrimSlash(settings.JiraUrl)}/rest/api/3/field");
            AddAuth(request, settings.JiraEmail, settings.JiraApiToken);
            var response = await Http.SendAsync(request);
            if (!response.IsSuccessStatusCode) return null;
            var json = await response.Content.ReadAsStringAsync();
            var arr = JsonNode.Parse(json) as JsonArray;
            if (arr == null) return null;
            foreach (var f in arr)
            {
                var name = f?["name"]?.GetValue<string>();
                if (string.Equals(name, "Story Points", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(name, "Story point estimate", StringComparison.OrdinalIgnoreCase))
                    return f?["id"]?.GetValue<string>();
            }
            return null;
        }
        catch { return null; }
    }

    private sealed record JiraIssue(
        string Key,
        string Summary,
        string StatusName,
        string StatusCategory,   // "new" / "indeterminate" / "done"
        string? AssigneeAccountId,
        string? AssigneeDisplayName,
        double StoryPoints,
        DateTimeOffset? Updated
    );

    private async Task<List<JiraIssue>> SearchIssuesAsync(SettingsStore settings, string jql, string? storyPointsField)
    {
        // The legacy /rest/api/3/search endpoint was removed by Atlassian (returns 410 Gone).
        // Use the new /rest/api/3/search/jql endpoint which is cursor-paged via nextPageToken.
        var result = new List<JiraIssue>();
        string? nextPageToken = null;
        const int pageSize = 100;
        var fieldList = "summary,status,assignee,updated" + (storyPointsField != null ? $",{storyPointsField}" : "");
        while (true)
        {
            var url = $"{TrimSlash(settings.JiraUrl)}/rest/api/3/search/jql?jql={Uri.EscapeDataString(jql)}" +
                      $"&fields={Uri.EscapeDataString(fieldList)}&maxResults={pageSize}";
            if (nextPageToken != null)
                url += $"&nextPageToken={Uri.EscapeDataString(nextPageToken)}";
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            AddAuth(request, settings.JiraEmail, settings.JiraApiToken);
            var response = await Http.SendAsync(request);
            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized ||
                response.StatusCode == System.Net.HttpStatusCode.Forbidden)
            {
                LastError = "Invalid JIRA credentials";
                return result;
            }
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync();
            var doc = JsonNode.Parse(json);
            if (doc == null) break;
            var issuesArr = doc["issues"] as JsonArray;
            if (issuesArr == null) break;
            foreach (var issue in issuesArr)
            {
                if (issue == null) continue;
                var key = issue["key"]?.GetValue<string>() ?? "";
                var fields = issue["fields"];
                var summary = fields?["summary"]?.GetValue<string>() ?? "";
                var statusName = fields?["status"]?["name"]?.GetValue<string>() ?? "Unknown";
                var statusCategory = fields?["status"]?["statusCategory"]?["key"]?.GetValue<string>() ?? "new";
                var assignee = fields?["assignee"];
                string? accountId = assignee?["accountId"]?.GetValue<string>();
                string? displayName = assignee?["displayName"]?.GetValue<string>();
                double sp = 0;
                if (storyPointsField != null && fields?[storyPointsField] != null)
                {
                    try { sp = fields[storyPointsField]!.GetValue<double>(); }
                    catch { sp = 0; }
                }
                DateTimeOffset? updated = null;
                var updStr = fields?["updated"]?.GetValue<string>();
                if (updStr != null && DateTimeOffset.TryParse(updStr, null,
                        System.Globalization.DateTimeStyles.RoundtripKind, out var updTs))
                    updated = updTs;
                result.Add(new JiraIssue(key, summary, statusName, statusCategory, accountId, displayName, sp, updated));
            }
            bool isLast = doc["isLast"]?.GetValue<bool>() ?? true;
            nextPageToken = doc["nextPageToken"]?.GetValue<string>();
            if (isLast || string.IsNullOrEmpty(nextPageToken) || issuesArr.Count == 0) break;
        }
        return result;
    }

    private static void AddAuth(HttpRequestMessage request, string email, string token)
    {
        var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{email}:{token}"));
        request.Headers.Add("Authorization", $"Basic {credentials}");
        request.Headers.Add("Accept", "application/json");
    }

    private static string TrimSlash(string url) => url.TrimEnd('/');
    private static string Escape(string s) => s.Replace("\"", "\\\"");

    public void Dispose() { }
}
