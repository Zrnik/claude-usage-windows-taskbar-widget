---
phase: 07-history-persistence
plan: 01
subsystem: database
tags: [json, persistence, history, appdata]

# Dependency graph
requires:
  - phase: 06-stability
    provides: ClaudeApiClient s AccountInfo konstruktorem a CredentialStore s GetJwtClaim
provides:
  - UsageHistoryStore singleton pro hourly-bucket upsert do AppData JSON souborů
  - AccountKey property na ClaudeApiClient
  - Automatické ukládání po každém úspěšném API callu
affects: [08-charts, sparklines]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - Singleton store s in-memory cache + lazy load z disku
    - Atomic write: tmp soubor + File.Move(overwrite:true) — crash safety
    - Hourly bucket upsert: timestamp "yyyy-MM-ddTHH:mm:ssZ" zaokrouhlen na celou hodinu
    - Failure-silent persistence — try/catch obali celý Append(), widget nespadne

key-files:
  created:
    - ClaudeUsageWidget/UsageHistoryStore.cs
  modified:
    - ClaudeUsageWidget/ClaudeApiClient.cs
    - ClaudeUsageWidget/MainWindow.xaml.cs

key-decisions:
  - "AccountKey duplikuje logiku z CredentialStore.GetAccountKey() — private metoda nemůže být sdílena bez refactoru"
  - "ExtractAccountKey() v ClaudeApiClient místo exposování CredentialStore.GetAccountKey() jako public/internal"
  - "Hourly bucket (ne per-minute) — max 336 záznamů na účet (14 dní × 24 h)"
  - "Singleton UsageHistoryStore.Instance — žádné DI, přímý přístup z MainWindow"

patterns-established:
  - "Atomic write pattern: WriteAllText(tmp) + Move(tmp, target, overwrite:true) — použít pro další perzistentní soubory"
  - "Failure-silent persistence: celý write wrapper v try/catch — widget nikdy nespadne kvůli disku"

requirements-completed: [HIST-01, HIST-02, HIST-03, HIST-04]

# Metrics
duration: 8min
completed: 2026-03-07
---

# Phase 7 Plan 1: History Persistence Summary

**UsageHistoryStore s hourly-bucket upsert do AppData JSON, AccountKey na ClaudeApiClient, integrace Append() v MainWindow po každém úspěšném API callu**

## Performance

- **Duration:** 8 min
- **Started:** 2026-03-07T17:05:00Z
- **Completed:** 2026-03-07T17:13:00Z
- **Tasks:** 2
- **Files modified:** 3 (1 created, 2 modified)

## Accomplishments
- Nový `UsageHistoryStore.cs` — singleton s Append(), GetHistory(), GetUtilizationHistory()
- Atomic write + failure-silent persistence — widget nekrachne při disk chybě
- `AccountKey` property na ClaudeApiClient s JWT decode pomocí `ExtractAccountKey()`
- Integrace Append() v MainWindow na obou místech kde usage != null (Loaded handler + refresh timer)

## Task Commits

1. **Task 1: UsageHistoryStore.cs + AccountKey property** - `c18f469` (feat)
2. **Task 2: Integrace Append() v MainWindow** - `c587037` (feat)

## Files Created/Modified
- `ClaudeUsageWidget/UsageHistoryStore.cs` - Nová datová vrstva history: Append(), GetHistory(), GetUtilizationHistory(), atomic write do AppData
- `ClaudeUsageWidget/ClaudeApiClient.cs` - Přidána AccountKey property + ExtractAccountKey() private helper
- `ClaudeUsageWidget/MainWindow.xaml.cs` - Přidán _historyStore field, Append() voláno po každém úspěšném GetUsageAsync()

## Decisions Made
- `ExtractAccountKey()` duplikuje logiku z `CredentialStore.GetAccountKey()` — metoda je private a CredentialStore nemá AccountInfo jako parametr, refactor by byl přílišný zásah mimo scope
- Singleton místo DI — konzistentní s ostatními stateless utilitami v projektu

## Deviations from Plan

None - plan executed exactly as written.

## Issues Encountered

None.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness
- UsageHistoryStore připraven pro Phase 8 sparkline grafy
- GetHistory() a GetUtilizationHistory() API jsou k dispozici
- JSON soubory vznikají v `%APPDATA%\ClaudeUsageWidget\history\` po prvním refresh timerticku

## Self-Check: PASSED

- FOUND: ClaudeUsageWidget/UsageHistoryStore.cs
- FOUND: .planning/phases/07-history-persistence/07-01-SUMMARY.md
- FOUND: commit c18f469 (Task 1)
- FOUND: commit c587037 (Task 2)

---
*Phase: 07-history-persistence*
*Completed: 2026-03-07*
