# Architecture

**Analysis Date:** 2026-03-05

## Pattern Overview

**Overall:** COM-based Windows Widget Provider with asynchronous API client and credential management

**Key Characteristics:**
- Windows App SDK Widget Provider implementing `IWidgetProvider` interface
- COM class factory registration for widget provider instantiation
- Multi-layered separation: UI templates (Adaptive Cards), business logic, API client, credential storage
- Fire-and-forget async pattern for background operations
- Periodic refresh with 5-minute intervals and client-side caching

## Layers

**Widget Provider Layer:**
- Purpose: Manages widget lifecycle (create, delete, activate, deactivate) and coordinates updates
- Location: `ClaudeUsageWidgetProvider/WidgetProvider.cs`
- Contains: Widget state management, template rendering, update dispatching
- Depends on: ClaudeApiClient, CredentialStore, Adaptive Card templates
- Used by: Windows App SDK runtime via COM interface

**API Client Layer:**
- Purpose: Handles OAuth token refresh, organization lookup, and usage data retrieval
- Location: `ClaudeUsageWidgetProvider/ClaudeApiClient.cs`
- Contains: HTTP communication, JSON parsing, caching logic, token expiration handling
- Depends on: CredentialStore (for token persistence), external Claude/Anthropic APIs
- Used by: WidgetProvider for fetching usage data

**Credential Management Layer:**
- Purpose: Loads and persists OAuth credentials across multiple environments
- Location: `ClaudeUsageWidgetProvider/CredentialStore.cs`
- Contains: Credential file I/O, multi-path resolution (Windows native, WSL distros)
- Depends on: File system, environment queries
- Used by: WidgetProvider and ClaudeApiClient

**COM Interop Layer:**
- Purpose: Bridges managed C# code to COM infrastructure required by Windows widgets
- Location: `ClaudeUsageWidgetProvider/FactoryHelper.cs`
- Contains: IClassFactory implementation, COM registration/revocation, P/Invoke declarations
- Depends on: Windows SDK (ole32.dll)
- Used by: Program.Main for service registration

**Presentation Layer:**
- Purpose: Defines widget UI as Adaptive Card JSON templates
- Location: `ClaudeUsageWidgetProvider/Templates/`
- Contains: UsageWidgetTemplate.json (primary), LoginRequiredTemplate.json, ErrorTemplate.json
- Depends on: Data binding from WidgetProvider
- Used by: WidgetProvider for rendering

## Data Flow

**Widget Creation and Initial Update:**

1. Windows App SDK calls `CreateWidget()` with widget context
2. WidgetProvider stores widget in `_widgets` dictionary
3. Fire-and-forget spawns `UpdateWidgetAsync()` task
4. UpdateWidgetAsync checks credential status via CredentialStore
5. If no credential: push LoginRequiredTemplate, return
6. If credential expired: call `RefreshTokenAsync()` to get new access token
7. Fetch organization ID from `/api/organizations` endpoint
8. Fetch usage data from `/api/organizations/{orgId}/usage` endpoint
9. Transform usage into template data (percentages, bar widths, reset times)
10. Push update to widget via `WidgetManager.UpdateWidget()`

**Periodic Refresh:**

1. Timer fires every 5 minutes
2. Calls `RefreshAndUpdateAll()`
3. Iterates active widget IDs in snapshot
4. Calls `UpdateWidgetAsync()` for each without forceRefresh
5. GetUsageAsync checks cache: if < 60s old, return cached data
6. Otherwise repeats fetch sequence

**User Action (Manual Refresh):**

1. User taps "Refresh" button in widget
2. `OnActionInvoked()` called with verb="refresh"
3. Calls `UpdateWidgetAsync()` with forceRefresh=true
4. Skips cache, fetches fresh data immediately

**Error Handling and Fallback:**

- 401 Unauthorized: Attempts single token refresh, fails over to stale cache
- Other HTTP errors: Returns last cached usage data
- Token expiration: Proactively detected, refresh attempted before request
- No credential: Shows LoginRequiredTemplate immediately

## Key Abstractions

**OAuthCredential:**
- Purpose: Encapsulates OAuth tokens with expiration tracking
- Examples: `ClaudeUsageWidgetProvider/CredentialStore.cs` (OAuthCredential class)
- Pattern: Value object with computed property `IsExpired` (includes 60s buffer)

**UsageData:**
- Purpose: Represents Claude API usage metrics for two time windows
- Examples: `ClaudeUsageWidgetProvider/ClaudeApiClient.cs` (UsageData class)
- Pattern: POCO with properties for utilization percentages and reset times

**WidgetInfo:**
- Purpose: Tracks state of individual widget instance (ID, definition, active status)
- Examples: `ClaudeUsageWidgetProvider/WidgetProvider.cs` (nested sealed class)
- Pattern: Internal state holder, thread-safe access via `_lock`

**IWidgetProvider Interface:**
- Purpose: Contract required by Windows App SDK for widget providers
- Examples: Implemented by `WidgetProvider` class
- Pattern: Callback interface with lifecycle methods (Create, Delete, Activate, Deactivate, OnActionInvoked, OnWidgetContextChanged)

## Entry Points

**Main (COM Registration):**
- Location: `ClaudeUsageWidgetProvider/Program.cs`
- Triggers: Executable launch (either via App runtime or --console flag for testing)
- Responsibilities: Register WidgetProvider as COM class factory, enter wait loop or console mode

**WidgetProvider Constructor:**
- Location: `ClaudeUsageWidgetProvider/WidgetProvider.cs`
- Triggers: COM factory instantiation when Windows App SDK requests provider
- Responsibilities: Load Adaptive Card templates from disk, initialize refresh timer

**Credential Discovery:**
- Location: `ClaudeUsageWidgetProvider/CredentialStore.cs` (`LoadCredential` method)
- Triggers: On widget creation or update
- Responsibilities: Search Windows profile, then all WSL instances for `.claude/.credentials.json`

## Error Handling

**Strategy:** Graceful degradation with fallback to stale data; user-facing errors shown in widget UI

**Patterns:**

- **Token Expiration:** Proactively refreshed 60 seconds before expiration via `IsExpired` property. If refresh fails, returns last cached usage.
- **API Failures:** Caught as exceptions; stale cache returned to prevent blank widget. Error template shown only if no cache exists.
- **Credential Missing:** Immediately triggers LoginRequiredTemplate; no retry.
- **File I/O:** All credential read/write operations wrapped in try-catch; failures silently logged (best-effort).
- **Network Timeouts:** HttpClient configured with 15-second timeout; falls back to cache on timeout.
- **Fire-and-Forget Swallowing:** Background tasks (UpdateWidgetAsync, RefreshAndUpdateAll) swallow exceptions to prevent crash.

## Cross-Cutting Concerns

**Logging:** None. Errors are silent except for console mode (--console flag shows status messages to stdout). No persistent log file or event tracing.

**Validation:**
- Credential required fields validated on load (AccessToken, RefreshToken non-empty)
- API responses parsed defensively; missing fields treated as null, operations gracefully degrade
- Reset time strings parsed with fallback to current UTC time if malformed

**Authentication:**
- OAuth 2.0 with refresh token flow
- Credentials persisted in `~/.claude/.credentials.json` (shared with Claude CLI)
- Bearer token auth for all API requests
- Token refresh triggered automatically on expiration or 401 response

**Thread Safety:**
- Widget dictionary protected by `_lock` (object-based monitor)
- HttpClient is static (thread-safe by design)
- Timer and refresh operations are fire-and-forget; no coordination beyond lock on widget collection

**Async Pattern:**
- `UpdateWidgetAsync()` spawned as fire-and-forget task from sync callbacks
- `RefreshAndUpdateAll()` iterates widget snapshot under lock, then awaits updates sequentially
- No explicit task coordination or cancellation; tasks allowed to complete or timeout naturally
