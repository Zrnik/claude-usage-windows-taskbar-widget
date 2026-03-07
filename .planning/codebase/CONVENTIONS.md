# Coding Conventions

**Analysis Date:** 2026-03-05

## Naming Patterns

**Files:**
- PascalCase for all `.cs` files: `ClaudeApiClient.cs`, `CredentialStore.cs`, `WidgetProvider.cs`
- Files map one-to-one with primary class names
- No file extension prefixes or suffixes

**Classes:**
- PascalCase for class names: `ClaudeApiClient`, `WidgetProvider`, `OAuthCredential`
- `sealed` modifier used for non-abstract classes by default
- `internal` access level for most classes (windows widget provider context)

**Methods:**
- PascalCase for all methods: `GetUsageAsync()`, `RefreshTokenAsync()`, `LoadCredential()`
- Async methods suffixed with `Async`: `GetUsageAsync`, `FetchOrgIdAsync`, `UpdateWidgetAsync`
- Private helper methods prefixed with logical grouping: `FetchOrgIdAsync()`, `FetchUsageAsync()`, `ParseResetTime()`

**Properties:**
- PascalCase for properties: `AccessToken`, `FiveHourUtilization`, `IsActive`
- Auto-properties with backing fields when modification needed
- Expression-bodied properties for computed values: `public bool IsExpired =>`

**Variables:**
- camelCase for local variables: `widgetId`, `orgId`, `credential`, `remaining`
- camelCase for parameters: `forceRefresh`, `widgetContext`, `customState`
- Prefix underscore for private fields: `_cachedOrgId`, `_lastFetchTime`, `_widgets`
- ALL_CAPS_WITH_UNDERSCORES for constants: `WidgetDefinitionId`, `MinFetchInterval`, `RefreshInterval`

**Interfaces:**
- PascalCase with `I` prefix following standard .NET convention: Not extensively used; framework provides `IWidgetProvider`

## Code Style

**Formatting:**
- No explicit linter config found (no `.editorconfig`, `.stylecop.json`, etc.)
- Follows implicit C# conventions with consistent spacing
- 4-space indentation observed throughout
- Opening braces on same line (K&R style): `public void Method() {`
- Closing braces on own line

**Linting:**
- No enforced linter (no configuration for StyleCop, Roslyn analyzers, etc.)
- Nullable enabled in project: `<Nullable>enable</Nullable>`
- Implicit usings enabled: `<ImplicitUsings>enable</ImplicitUsings>`

## Import Organization

**Order:**
1. System namespace imports: `using System.Net;`
2. System.* derived imports: `using System.Text.Json;`, `using System.Net.Http.Headers;`
3. Third-party/framework imports: `using Microsoft.Windows.Widgets.Providers;`, `using WinRT;`

**Path Aliases:**
- Not applicable (no aliases found, single namespace `ClaudeUsageWidgetProvider`)

## Error Handling

**Patterns:**
- Broad catch blocks with graceful degradation: `catch { return null; }` or `catch { return _cachedUsage; }`
- No exception logging or re-throwing observed
- Fallback to cached/stale data on network errors: Returns `_cachedUsage` when `GetUsageAsync` fails
- Swallowing exceptions in fire-and-forget async operations: `catch { /* swallow */ }`
- Status code specific handling: `catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.Unauthorized)`
- Null-coalescing for missing data: `doc["key"]?.GetValue<T>() ?? defaultValue`

## Logging

**Framework:** Console logging only

**Patterns:**
- Minimal logging observed
- Only `Console.WriteLine()` used in `Program.cs` for console mode indication
- No structured logging framework (no Serilog, NLog, etc.)
- Error messages built as strings and serialized to JSON for widget display

## Comments

**When to Comment:**
- Sparse comments in production code
- Inline comments for non-obvious logic: `// Return stale data on refresh failure`, `// Re-fetch org ID with new token`
- Clarifying comments on fields: `// Unix epoch milliseconds`, `// File path for write-back`
- Comments on constants: `// S_OK` for COM return codes, `// 60s buffer` for timeout handling

**JSDoc/TSDoc:**
- No XML documentation (`///`) used
- Not applicable in this Windows widget provider context

## Function Design

**Size:**
- Functions range from 7-50 lines
- Larger functions like `GetUsageAsync()` (45 lines) encapsulate complex control flow
- Helper methods extracted for clarity: `ParseResetTime()`, `BuildUsageData()`, `BuildErrorData()`

**Parameters:**
- Minimal parameters (1-3 typically)
- Optional boolean parameters for conditional behavior: `bool forceRefresh = false`
- Reference/out parameters used only in COM interop: `out IntPtr ppvObject`

**Return Values:**
- Nullable return types for optional results: `Task<UsageData?>`, `Task<string?>`, `Task<bool>`
- Boolean returns indicate success/failure: `RefreshTokenAsync()` returns `bool`
- Async methods return `Task` or `Task<T>`: All I/O operations are async

## Module Design

**Exports:**
- All public methods are on `internal` classes (Windows provider constraint)
- No barrel files or index files
- Single namespace per file: `namespace ClaudeUsageWidgetProvider;`
- Nested classes used for data transfer: `WidgetInfo` nested in `WidgetProvider`

**Barrel Files:**
- Not used (single namespace, focused classes)

---

*Convention analysis: 2026-03-05*
