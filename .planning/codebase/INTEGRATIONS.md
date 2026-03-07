# External Integrations

**Analysis Date:** 2026-03-05

## APIs & External Services

**Claude AI API:**
- Anthropic Claude API for organization and usage data retrieval
  - SDK/Client: Custom `HttpClient` in `ClaudeApiClient.cs`
  - Auth: OAuth 2.0 Bearer token (stored as `accessToken` in credentials)
  - Endpoints:
    - `https://console.anthropic.com/v1/oauth/token` - Token refresh (POST)
    - `https://claude.ai/api/organizations` - List user organizations (GET)
    - `https://claude.ai/api/organizations/{orgId}/usage` - Get usage statistics (GET)

**OAuth Token Refresh:**
- Service: `https://console.anthropic.com/v1/oauth/token`
- Grant type: `refresh_token`
- Request body: `grant_type=refresh_token&refresh_token={token}`
- Response: JSON with `access_token`, `refresh_token`, `expires_in` (seconds)
- Auto-refresh on expiry with 60-second buffer before actual expiration

## Data Storage

**Credential Storage:**
- Local filesystem only
- Locations (checked in order):
  1. Windows native path: `{UserProfile}/.claude/.credentials.json`
  2. WSL instances: `\\wsl$\{distro}\home\{user}\.claude\.credentials.json`

**Credentials Format:**
```json
{
  "claudeAiOauth": {
    "accessToken": "string",
    "refreshToken": "string",
    "expiresAt": 1234567890000
  }
}
```

**Widget Templates:**
- Local JSON files in `Templates/` directory:
  - `UsageWidgetTemplate.json` - Widget UI template with usage visualization
  - `LoginRequiredTemplate.json` - Template when credentials missing
  - `ErrorTemplate.json` - Template for error display

**File Storage:**
- Not applicable. Widget assets embedded as content files in MSIX package

**Caching:**
- In-memory caching in `ClaudeApiClient`:
  - `_cachedUsage` - Last fetched usage data
  - `_cachedOrgId` - Organization ID
  - `_lastFetchTime` - Timestamp of last fetch
  - Minimum fetch interval: 60 seconds (prevents rapid-fire API calls)

## Authentication & Identity

**Auth Provider:**
- Claude.ai OAuth 2.0 (Anthropic)
  - Implementation: Custom OAuth client in `ClaudeApiClient.cs`
  - Credentials loaded from local files by `CredentialStore.cs`
  - Token expiry: Checked before each API call with 60-second buffer
  - Automatic token refresh: Attempts one refresh on 401 response

**Credential Loading Flow:**
1. Check Windows native path (`{UserProfile}/.claude/.credentials.json`)
2. If not found, scan WSL distributions for credentials
3. Return first valid credential found, or null
4. On token expiry, automatically attempt refresh before API call

**Credential Persistence:**
- Save successful token refreshes back to source file
- JSON format preserved with indentation
- Best-effort write (exceptions silently caught)

## Monitoring & Observability

**Error Tracking:**
- Not configured. Errors logged via exception messages in widget updates

**Logs:**
- Console output via `Console.WriteLine()` when running in console mode (CLAUDE_WIDGET_CONSOLE="1")
- No persistent logging framework
- Widget error states display in UI via `ErrorTemplate.json`

**Metrics:**
- Usage percentages for 5-hour and 7-day windows from Claude API
- Reset times displayed as human-readable countdown

## CI/CD & Deployment

**Hosting:**
- Windows 11 native - Deployed as MSIX package
- No remote hosting or cloud deployment

**CI Pipeline:**
- Not configured. Manual build via Visual Studio or `dotnet build`
- Build platforms: x64, ARM64
- Deployment method: MSIX installation

**MSIX Configuration:**
- Package type: MSIX (Windows App SDK)
- Self-contained runtime enabled
- Code signing disabled in development (`AppxPackageSigningEnabled=false`)
- Publish profile pattern: `win-$(Platform)` (generates x64/ARM64 variants)

## Environment Configuration

**Required env vars:**
- None strictly required for runtime
- Optional: `CLAUDE_WIDGET_CONSOLE=1` for debug mode

**Secrets location:**
- `{UserProfile}/.claude/.credentials.json` (Windows)
- `~/.claude/.credentials.json` (WSL distributions)
- Contains OAuth tokens with 60-minute typical expiry

**Credential Search Pattern:**
- Windows native path checked first (highest priority)
- WSL paths enumerated from `\\wsl$` mount point
- Searches each WSL distribution's home directory

## Webhooks & Callbacks

**Incoming:**
- None. Widget provider is event-driven:
  - `CreateWidget()` - Widget instance created
  - `DeleteWidget()` - Widget instance deleted
  - `OnActionInvoked()` - User clicked widget button (verb: "refresh")
  - `OnWidgetContextChanged()` - Widget context changed
  - `Activate()` / `Deactivate()` - Visibility changes

**Outgoing:**
- API calls only. No webhooks sent.

**Widget Update Push:**
- `WidgetManager.GetDefault().UpdateWidget()` - Pushes updated data/template to widget host
- Called on 5-minute refresh interval or manual refresh action

---

*Integration audit: 2026-03-05*
