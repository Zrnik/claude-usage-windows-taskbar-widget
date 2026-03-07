---
phase: 04-multi-account-detekce
plan: 03
subsystem: infra
tags: [csharp, wpf, multi-account, credential-loading]

# Dependency graph
requires:
  - phase: 04-multi-account-detekce-04-01
    provides: CredentialStore.LoadAllAccounts(), AccountInfo, ServiceType
  - phase: 04-multi-account-detekce-04-02
    provides: ClaudeApiClient(AccountInfo) per-account konstruktor
provides:
  - Per-account ClaudeApiClient instance list v OnStartup
  - Fallback pro prázdný credentials seznam (no-credentials AccountInfo)
  - Program.cs připraven pro Phase 5 multi-account UI (clients list existuje)
affects:
  - Phase 5 Multi-account UI (clients list ready, jen layout chybí)

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Per-account client factory: LoadAllAccounts() → clients list → per-client MainWindow"
    - "Fallback pattern: prázdný seznam → synthetic no-credentials AccountInfo"

key-files:
  created: []
  modified:
    - ClaudeUsageWidget/Program.cs
    - ClaudeUsageWidget/GlobalUsings.cs

key-decisions:
  - "Phase 5 layout: zatím first-account-wins pro primární i sekundární taskbar, Phase 5 přidá horizontální řadu"
  - "Fallback no-credentials AccountInfo s prázdným OAuthCredential — widget nesmí crashnout při chybějících credentials"

patterns-established:
  - "OnStartup lifecycle: LoadAllAccounts → build clients list → Exit disposes all clients → show windows"

requirements-completed: [MULTI-01, MULTI-02, MULTI-03]

# Metrics
duration: ~10min
completed: 2026-03-07
---

# Phase 4 Plan 03: Per-account OnStartup instantiation Summary

**Program.cs přepsán pro per-account ClaudeApiClient factory pomocí CredentialStore.LoadAllAccounts(), s fallback pro prázdný credentials seznam a disposal všech clientů při Exit**

## Performance

- **Duration:** ~10 min
- **Started:** 2026-03-07
- **Completed:** 2026-03-07
- **Tasks:** 1 auto + 1 checkpoint (human-verify)
- **Files modified:** 2

## Accomplishments
- OnStartup volá LoadAllAccounts() a vytváří per-account ClaudeApiClient instance
- Fallback: prázdný seznam credentials → synthetic `no-credentials` AccountInfo, widget nesmí crashnout
- Exit handler správně disposuje všechny klienty (foreach c in clients)
- Manuální ověření prošlo: widget se zobrazuje, Claude ikona v taskbaru funguje

## Task Commits

Každý task byl commitován atomicky:

1. **Task 1: Per-account OnStartup — LoadAllAccounts + ClaudeApiClient(AccountInfo)** - `8bd9d4d` (feat)
2. **Task 2: Checkpoint: Ověřit funkčnost widgetu** - manuální verifikace, schváleno uživatelem

## Files Created/Modified
- `ClaudeUsageWidget/Program.cs` - OnStartup přepsán: LoadAllAccounts() → clients list → per-account MainWindow
- `ClaudeUsageWidget/GlobalUsings.cs` - přidán System.Linq global using

## Decisions Made
- Phase 5 přidá horizontální layout pro více účtů; zatím widget zobrazuje první účet na všech taskbarech (first-account-wins)
- Synthetic `no-credentials` AccountInfo s `label = "no-credentials"` jako fallback — žádný crash při absenci credentials

## Deviations from Plan

None - plan executed exactly as written.

## Issues Encountered

None.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness
- Phase 4 kompletní: CredentialStore, ClaudeApiClient(AccountInfo), Program.cs per-account instantiation
- Phase 5 může začít: clients list v OnStartup je architektonicky připraven, stačí přidat UI layout pro více sloupců
- Blokátory: žádné

---
*Phase: 04-multi-account-detekce*
*Completed: 2026-03-07*
