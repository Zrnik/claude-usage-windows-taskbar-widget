# Phase 1: Cleanup - Context

**Gathered:** 2026-03-06
**Status:** Ready for planning

<domain>
## Phase Boundary

Přeměnit existující MSIX Widget provider projekt na čisté WPF exe. Žádné UI ani data logika — jen cleanup a příprava základu pro Phase 2. Výsledek: `dotnet build` funguje, exe spustitelné bez MSIX.

</domain>

<decisions>
## Implementation Decisions

### UI Framework
- **WPF** — zvoleno jako základ pro taskbar widget
- Důvod: přirozená podpora borderless oken, XAML custom rendering pro progress bary, dobrá integrace s Windows API
- MAUI a WinForms zavrženy (MAUI = overkill + omezená Windows API integrace; WinForms = ruční GDI+ rendering)

### Co zachovat
- `ClaudeApiClient.cs` — zachovat celý, je funkční
- `CredentialStore.cs` + `OAuthCredential` — zachovat celý, čistá implementace
- `UsageData` class (v ClaudeApiClient.cs) — zachovat

### Co smazat
- `WidgetProvider.cs` — celý soubor
- `FactoryHelper.cs` — celý soubor
- `ByparrClient.cs` — celý soubor (prázdný stub)
- `Templates/` adresář — JSON šablony pro Windows Widget
- `Assets/` adresář — MSIX loga a ikony
- `ProviderAssets/` adresář — pokud existuje

### Co přepsat
- `.csproj` — odstranit MSIX tooling, WindowsAppSDK, COM packaging; přidat WPF support (`<UseWPF>true</UseWPF>`)
- `Program.cs` — přepsat z COM Widget provider na WPF `Application` startup (minimální, jen spustí WPF app)

### Projekt pojmenování
- Zachovat stávající název `ClaudeUsageWidgetProvider` — není důvod přejmenovávat
- Namespace zůstane `ClaudeUsageWidgetProvider`

### .csproj target
- Zachovat `net8.0-windows10.0.22621.0` — potřebné pro WPF + Windows API
- Odstranit MSIX-specifické properties: `EnableMsixTooling`, `WindowsPackageType`, `AppxPackageSigningEnabled`, `WindowsAppSDKSelfContained`, `PublishProfile`
- Odstranit package references: `Microsoft.WindowsAppSDK`, `Microsoft.Windows.SDK.BuildTools`
- Přidat: `<UseWPF>true</UseWPF>`

### Claude's Discretion
- Zda přidat `App.xaml` / `App.xaml.cs` nebo jen standalone `Program.cs` s WPF Application
- Zda zachovat nebo smazat `.sln` soubor (není kritické)

</decisions>

<code_context>
## Existing Code Insights

### Reusable Assets
- `ClaudeApiClient.cs`: Funkční HTTP client pro Anthropic API — inference call, rate limit headers parsing, 60s caching, token refresh. Zachovat beze změny.
- `CredentialStore.cs`: Čte credentials z Windows i WSL cest. Funkční, zachovat beze změny.
- `OAuthCredential` class: Token model s `IsExpired` logic. Zachovat.
- `UsageData` class: DTO s `FiveHourUtilization`, `SevenDayUtilization`, reset timestamps. Zachovat.
- `FormatResetTime()` v WidgetProvider.cs: Užitečná helper metoda pro formátování ("in 2h 30m") — přesunout/zachovat.

### Established Patterns
- Namespace: `ClaudeUsageWidgetProvider` — zachovat pro konzistenci
- Target framework: `net8.0-windows10.0.22621.0` — zachovat

### Integration Points
- WPF Application startup (nový `Program.cs`) musí načíst credentials a spustit API polling
- `ClaudeApiClient` zůstane jako singleton v main window nebo app class

</code_context>

<specifics>
## Specific Ideas

- `FormatResetTime()` z `WidgetProvider.cs` je dobrá helper funkce — přesunout do utility class nebo přímo do budoucího main window
- Výsledná struktura po cleanup: pouze `ClaudeApiClient.cs`, `CredentialStore.cs`, `Program.cs` (přepsaný), `.csproj` (přepsaný), případně `App.xaml`

</specifics>

<deferred>
## Deferred Ideas

None — diskuze zůstala v rámci cleanup scope.

</deferred>

---

*Phase: 01-cleanup*
*Context gathered: 2026-03-06*
