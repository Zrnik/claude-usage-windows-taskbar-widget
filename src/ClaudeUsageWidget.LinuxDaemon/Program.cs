using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using ClaudeUsageWidgetProvider;

var builder = WebApplication.CreateSlimBuilder(args);
builder.WebHost.UseUrls($"http://127.0.0.1:{DaemonRuntime.ResolvePort()}");

var app = builder.Build();
var runtime = new DaemonRuntime();
await runtime.StartAsync();

app.MapGet("/health", () => Results.Ok(new { ok = true, version = DaemonRuntime.Version }));
app.MapGet("/state", () => Results.Json(runtime.GetState(), DaemonRuntime.JsonOptions));
app.MapGet("/settings", () => Results.Json(SettingsStore.Instance.ToSnapshot(includeSecrets: false), DaemonRuntime.JsonOptions));
app.MapPost("/settings", (SettingsSnapshot settings) =>
{
    var previous = SettingsStore.Instance.ToSnapshot();
    SettingsStore.Instance.Apply(settings);
    runtime.RequestStateChanged();
    runtime.RefreshAfterSettingsChange(previous);
    return Results.Json(SettingsStore.Instance.ToSnapshot(includeSecrets: false), DaemonRuntime.JsonOptions);
});

app.MapPost("/refresh/{service}", async (string service) =>
{
    var ok = await runtime.RefreshAsync(service, force: true);
    return ok ? Results.Json(runtime.GetState(), DaemonRuntime.JsonOptions) : Results.BadRequest(new { error = "Unknown service" });
});
app.MapPost("/update", () =>
{
    LinuxPackageUpdater.Start();
    return Results.Accepted();
});

app.MapGet("/projects/toggl", FetchTogglProjectsAsync);

app.MapGet("/projects/jira", FetchJiraProjectsAsync);

app.MapGet("/users/jira", FetchJiraUsersAsync);

app.MapGet("/runtime/port", () => Results.Text(DaemonRuntime.ResolvePort().ToString()));

await app.RunAsync();

static async Task<IResult> FetchTogglProjectsAsync()
{
    try
    {
        using var client = new TogglApiClient();
        return Results.Json(await client.FetchProjectsAsync(), DaemonRuntime.JsonOptions);
    }
    catch (Exception ex)
    {
        return DaemonRuntime.UpstreamError("Toggl", ex);
    }
}

static async Task<IResult> FetchJiraProjectsAsync()
{
    try
    {
        using var client = new JiraApiClient();
        return Results.Json(await client.FetchProjectsAsync(), DaemonRuntime.JsonOptions);
    }
    catch (Exception ex)
    {
        return DaemonRuntime.UpstreamError("JIRA", ex);
    }
}

static async Task<IResult> FetchJiraUsersAsync()
{
    try
    {
        using var client = new JiraApiClient();
        return Results.Json(await client.FetchAssignableUsersAsync(), DaemonRuntime.JsonOptions);
    }
    catch (Exception ex)
    {
        return DaemonRuntime.UpstreamError("JIRA", ex);
    }
}

internal sealed class DaemonRuntime
{
    public static readonly string Version =
        typeof(DaemonRuntime).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        ?? typeof(DaemonRuntime).Assembly.GetName().Version?.ToString()
        ?? "0.0.0";
    public static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly object _lock = new();
    private readonly List<(ClaudeApiClient Client, UsageData? Usage)> _accounts = new();
    private readonly TogglApiClient _toggl = new();
    private readonly JiraApiClient _jira = new();
    private TogglUsageData? _togglUsage;
    private JiraUsageData? _jiraUsage;
    private DateTimeOffset _startedAt = DateTimeOffset.UtcNow;
    private long _stateVersion;
    private Timer? _claudeTimer;
    private Timer? _togglJiraTimer;

    public static int ResolvePort()
    {
        var env = Environment.GetEnvironmentVariable("CLAUDE_USAGE_WIDGET_PORT");
        if (int.TryParse(env, out var port) && port is > 1024 and < 65535) return port;
        return 43175;
    }

    public static IResult UpstreamError(string service, Exception ex)
    {
        var message = ex switch
        {
            HttpRequestException http when http.StatusCode is System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.Forbidden =>
                $"Invalid {service} credentials",
            HttpRequestException http when (int?)http.StatusCode == 402 && service == "Toggl" =>
                "Toggl API rate limit exceeded (30/h)",
            TaskCanceledException =>
                $"{service} request timed out",
            _ => ex.Message
        };

        return Results.Json(new { error = message }, DaemonRuntime.JsonOptions, statusCode: StatusCodes.Status502BadGateway);
    }

    public async Task StartAsync()
    {
        Directory.CreateDirectory(XdgPaths.RuntimeDir);
        File.WriteAllText(Path.Combine(XdgPaths.RuntimeDir, "port"), ResolvePort().ToString());
        LoadAccounts();
        await RefreshClaudeCodexAsync(force: true);
        await RefreshTogglAsync(force: true);
        await RefreshJiraAsync(force: true);

        _claudeTimer = new Timer(async _ => await SafeRefresh(() => RefreshClaudeCodexAsync(force: false)),
            null, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));
        _togglJiraTimer = new Timer(async _ =>
        {
            await SafeRefresh(() => RefreshTogglAsync(force: false));
            await SafeRefresh(() => RefreshJiraAsync(force: false));
        }, null, TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));
    }

    public WidgetState GetState()
    {
        lock (_lock)
        {
            return new WidgetState
            {
                Version = Version,
                StateVersion = _stateVersion,
                StartedAt = _startedAt,
                Settings = SettingsStore.Instance.ToSnapshot(includeSecrets: false),
                Accounts = _accounts.Select(a => AccountState.From(a.Client, a.Usage)).ToList(),
                Toggl = ServiceState<TogglUsageData>.From("toggl", "Toggl Track", _togglUsage, _toggl.LastError),
                Jira = ServiceState<JiraUsageData>.From("jira", "JIRA", _jiraUsage, _jira.LastError),
                KnownLabels = SettingsStore.Instance.GetKnownLabels().Select(x => new KnownLabel(x.Label, x.Display)).ToList()
            };
        }
    }

    public async Task<bool> RefreshAsync(string service, bool force)
    {
        switch (service.ToLowerInvariant())
        {
            case "claude":
            case "codex":
            case "accounts":
                await RefreshClaudeCodexAsync(force);
                return true;
            case "toggl":
                await RefreshTogglAsync(force);
                return true;
            case "jira":
                await RefreshJiraAsync(force);
                return true;
            case "all":
                await RefreshClaudeCodexAsync(force);
                await RefreshTogglAsync(force);
                await RefreshJiraAsync(force);
                return true;
            default:
                return false;
        }
    }

    public void RequestStateChanged()
    {
        lock (_lock) _stateVersion++;
    }

    public void RefreshAfterSettingsChange(SettingsSnapshot previous)
    {
        var current = SettingsStore.Instance.ToSnapshot();

        if (TogglSettingsChanged(previous, current) && current.ShowToggl)
            _ = SafeRefresh(() => RefreshTogglAsync(force: true));

        if (JiraSettingsChanged(previous, current) && current.ShowJira)
            _ = SafeRefresh(() => RefreshJiraAsync(force: true));
    }

    private void LoadAccounts()
    {
        var accounts = CredentialStore.LoadAllAccounts()
            .Where(a => a.Service is ServiceType.Claude or ServiceType.Codex)
            .Select(a => (new ClaudeApiClient(a), (UsageData?)null))
            .ToList();
        lock (_lock)
        {
            _accounts.Clear();
            _accounts.AddRange(accounts);
            _stateVersion++;
        }
    }

    private async Task RefreshClaudeCodexAsync(bool force)
    {
        if (force) LoadAccounts();
        List<(ClaudeApiClient Client, UsageData? Usage)> snapshot;
        lock (_lock) snapshot = _accounts.ToList();

        for (var i = 0; i < snapshot.Count; i++)
        {
            var (client, previous) = snapshot[i];
            var usage = await client.GetUsageAsync(force);
            if (usage != null)
            {
                SettingsStore.Instance.RegisterKnownLabels(client.AccountService, usage);
                UsageHistoryStore.Instance.Append(client.AccountKey, usage);
                snapshot[i] = (client, usage);
            }
            else
            {
                snapshot[i] = (client, previous);
            }
        }

        lock (_lock)
        {
            _accounts.Clear();
            _accounts.AddRange(snapshot);
            _stateVersion++;
        }
    }

    private async Task RefreshTogglAsync(bool force)
    {
        var usage = await _toggl.GetUsageAsync(force);
        lock (_lock)
        {
            if (usage != null)
            {
                _togglUsage = usage;
                TogglHistoryStore.Instance.Append(usage);
                TogglHistoryStore.Instance.SaveSnapshot(usage);
            }
            _stateVersion++;
        }
    }

    private async Task RefreshJiraAsync(bool force)
    {
        var usage = await _jira.GetUsageAsync(force);
        lock (_lock)
        {
            if (usage != null)
            {
                _jiraUsage = usage;
                JiraHistoryStore.Instance.Append(usage);
            }
            _stateVersion++;
        }
    }

    private static async Task SafeRefresh(Func<Task> refresh)
    {
        try { await refresh(); }
        catch { }
    }

    private static bool TogglSettingsChanged(SettingsSnapshot previous, SettingsSnapshot current) =>
        previous.ShowToggl != current.ShowToggl ||
        previous.TogglApiKey != current.TogglApiKey ||
        previous.TogglMonthlyTargetCzk != current.TogglMonthlyTargetCzk ||
        previous.WorkdayStartHour != current.WorkdayStartHour ||
        previous.WorkdayEndHour != current.WorkdayEndHour ||
        !DictionaryEquals(previous.TogglProjectRates, current.TogglProjectRates);

    private static bool JiraSettingsChanged(SettingsSnapshot previous, SettingsSnapshot current) =>
        previous.ShowJira != current.ShowJira ||
        previous.JiraUrl != current.JiraUrl ||
        previous.JiraEmail != current.JiraEmail ||
        previous.JiraApiToken != current.JiraApiToken ||
        previous.JiraProjectKey != current.JiraProjectKey ||
        !SetEquals(previous.JiraDeveloperAccountIds, current.JiraDeveloperAccountIds);

    private static bool DictionaryEquals<TKey, TValue>(
        IReadOnlyDictionary<TKey, TValue> a,
        IReadOnlyDictionary<TKey, TValue> b)
        where TKey : notnull
    {
        if (a.Count != b.Count) return false;
        var comparer = EqualityComparer<TValue>.Default;
        foreach (var (key, value) in a)
        {
            if (!b.TryGetValue(key, out var other) || !comparer.Equals(value, other))
                return false;
        }
        return true;
    }

    private static bool SetEquals(IReadOnlyCollection<string> a, IReadOnlyCollection<string> b) =>
        a.Count == b.Count && new HashSet<string>(a, StringComparer.Ordinal).SetEquals(b);
}

internal static class LinuxPackageUpdater
{
    private const string UpdaterPath = "/usr/bin/ai-usage-widget-update";

    public static void Start()
    {
        if (!File.Exists(UpdaterPath))
            throw new FileNotFoundException("CI updater is not installed.", UpdaterPath);

        var info = new ProcessStartInfo
        {
            FileName = UpdaterPath,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        Process.Start(info);
    }
}

internal sealed record KnownLabel(string Label, string Display);

internal sealed class WidgetState
{
    public string Version { get; set; } = "";
    public long StateVersion { get; set; }
    public DateTimeOffset StartedAt { get; set; }
    public SettingsSnapshot Settings { get; set; } = new();
    public List<AccountState> Accounts { get; set; } = [];
    public ServiceState<TogglUsageData> Toggl { get; set; } = new();
    public ServiceState<JiraUsageData> Jira { get; set; } = new();
    public List<KnownLabel> KnownLabels { get; set; } = [];
}

internal sealed class AccountState
{
    public string Service { get; set; } = "";
    public string AccountKey { get; set; } = "";
    public string CredentialPath { get; set; } = "";
    public UsageData? Usage { get; set; }
    public string? LastError { get; set; }
    public IReadOnlyList<HistoryRecord> History { get; set; } = [];

    public static AccountState From(ClaudeApiClient client, UsageData? usage) => new()
    {
        Service = client.AccountService.ToString().ToLowerInvariant(),
        AccountKey = client.AccountKey ?? "",
        CredentialPath = client.CredentialPath,
        Usage = usage,
        LastError = client.LastError,
        History = client.AccountKey != null ? UsageHistoryStore.Instance.GetHistory(client.AccountKey) : []
    };
}

internal sealed class ServiceState<T>
{
    public string Service { get; set; } = "";
    public string Label { get; set; } = "";
    public T? Usage { get; set; }
    public string? LastError { get; set; }
    public object? History { get; set; }
    public bool HasCachedData => Usage != null;

    public static ServiceState<T> From(string service, string label, T? usage, string? error) => new()
    {
        Service = service,
        Label = label,
        Usage = usage,
        LastError = error,
        History = service == "jira" ? JiraHistoryStore.Instance.GetLastDays(30) : null
    };
}
