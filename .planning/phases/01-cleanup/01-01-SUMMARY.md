---
phase: 01-cleanup
plan: 01
subsystem: infra
tags: [wpf, dotnet, csproj, msix, cleanup]

requires: []
provides:
  - WPF projekt bez MSIX zavislosti, kompilujici bez chyb
  - ClaudeApiClient.cs a CredentialStore.cs zachovany jako zaklad pro Phase 2
  - TimeFormatter helper pripraveny pro Phase 2 UI
affects: [02-ui, 03-distribution]

tech-stack:
  added: [WPF (UseWPF=true), GlobalUsings.cs]
  patterns: [minimalni WPF Application startup v Program.cs, global usings pro System.IO + System.Net.Http]

key-files:
  created:
    - ClaudeUsageWidget/ClaudeUsageWidgetProvider/GlobalUsings.cs
  modified:
    - ClaudeUsageWidget/ClaudeUsageWidgetProvider/ClaudeUsageWidgetProvider.csproj
    - ClaudeUsageWidget/ClaudeUsageWidgetProvider/Program.cs
  deleted:
    - ClaudeUsageWidget/ClaudeUsageWidgetProvider/WidgetProvider.cs
    - ClaudeUsageWidget/ClaudeUsageWidgetProvider/FactoryHelper.cs
    - ClaudeUsageWidget/ClaudeUsageWidgetProvider/ByparrClient.cs
    - ClaudeUsageWidget/ClaudeUsageWidgetProvider/Package.appxmanifest
    - ClaudeUsageWidget/ClaudeUsageWidgetProvider/Templates/ (adresar)
    - ClaudeUsageWidget/ClaudeUsageWidgetProvider/Assets/ (adresar)
    - ClaudeUsageWidget/ClaudeUsageWidgetProvider/ProviderAssets/ (adresar)

key-decisions:
  - "GlobalUsings.cs pouzit pro System.IO + System.Net.Http misto upravy ClaudeApiClient.cs/CredentialStore.cs (zachovani souboru beze zmeny)"
  - "TimeFormatter umisten do Program.cs (stejny soubor jako App class) pro jednoduchost"

patterns-established:
  - "WPF startup: App : Application s [STAThread] static void Main"
  - "GlobalUsings.cs pro implicit usings ktere WPF SDK neposkytuje automaticky"

requirements-completed: [CLEAN-01, CLEAN-02, CLEAN-03, CLEAN-04, LIFE-02]

duration: 4min
completed: 2026-03-06
---

# Phase 1 Plan 1: MSIX Widget provider pretvoren na cistu WPF aplikaci

**MSIX/Widget provider projekt vycisten a pretvoren na WPF exe: smazano 7 souboru/adresaru, .csproj zbaven WindowsAppSDK zavislosti, dotnet build prosel bez chyb.**

## Performance

- **Duration:** 4 min
- **Started:** 2026-03-06T08:42:31Z
- **Completed:** 2026-03-06T08:45:58Z
- **Tasks:** 2
- **Files modified:** 3 (modified) + 7 (deleted) + 1 (created)

## Accomplishments

- Smazany vsechny Widget provider soubory (WidgetProvider.cs, FactoryHelper.cs, ByparrClient.cs, Package.appxmanifest, Templates/, Assets/, ProviderAssets/)
- .csproj prevedeny z MSIX/WindowsAppSDK projektu na cistu WPF aplikaci (UseWPF=true, zadne PackageReference na AppSDK)
- Program.cs prevedeny na minimalni WPF Application startup s TimeFormatter helperem
- ClaudeApiClient.cs a CredentialStore.cs zachovany beze zmeny jako zaklad pro Phase 2

## Task Commits

1. **Task 1: Prepsat .csproj a smazat Widget provider soubory** - `7f9d953` (feat)
2. **Task 2: Prepsat Program.cs na WPF startup a overit kompilaci** - `922c950` (feat)

## Files Created/Modified

- `ClaudeUsageWidget/ClaudeUsageWidgetProvider/ClaudeUsageWidgetProvider.csproj` - WPF projekt bez MSIX, s UseWPF=true
- `ClaudeUsageWidget/ClaudeUsageWidgetProvider/Program.cs` - WPF Application startup + TimeFormatter
- `ClaudeUsageWidget/ClaudeUsageWidgetProvider/GlobalUsings.cs` - global using System.IO, System.Net.Http (auto-fix)

## Decisions Made

- Pouzit `GlobalUsings.cs` pro chybejici usings misto modifikace ClaudeApiClient.cs nebo CredentialStore.cs — zachovava tyto soubory beze zmeny dle pozadavku planu.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Chybejici implicit usings po odstraneni WindowsAppSDK**
- **Found during:** Task 2 (kompilace po prepisu .csproj)
- **Issue:** WPF SDK neposkytuje implicit using pro System.Net.Http a System.IO, ktere ClaudeApiClient.cs a CredentialStore.cs potrebuji, ale nemaji v using direktivach (WindowsAppSDK je poskytoval automaticky)
- **Fix:** Pridan GlobalUsings.cs s `global using System.IO;` a `global using System.Net.Http;`
- **Files modified:** ClaudeUsageWidget/ClaudeUsageWidgetProvider/GlobalUsings.cs (created)
- **Verification:** dotnet build: "Build succeeded, 0 errors, 0 warnings"
- **Committed in:** `922c950` (Task 2 commit)

---

**Total deviations:** 1 auto-fixed (Rule 1 - compilation bug)
**Impact on plan:** Auto-fix nutny pro kompilaci. Zadne scope creep — ClaudeApiClient.cs a CredentialStore.cs zachovany beze zmeny.

## Issues Encountered

WPF SDK neposkytuje stejne implicit usings jako WindowsAppSDK. Vyreseno GlobalUsings.cs souborem.

## User Setup Required

Zadne — zadna externi sluzba nevyzaduje konfiguraci.

## Next Phase Readiness

- Projekt kompiluje bez chyb jako cistu WPF exe
- ClaudeApiClient.cs a CredentialStore.cs pripraveny pro pouziti v Phase 2 (UI)
- TimeFormatter helper dostupny v Program.cs pro Phase 2
- ClaudeApiClient.cs zatim netestovan live — nutno overit po pridani UI v Phase 2

---
*Phase: 01-cleanup*
*Completed: 2026-03-06*
