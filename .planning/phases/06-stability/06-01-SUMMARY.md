---
phase: 06-stability
plan: "01"
subsystem: infra
tags: [mutex, single-instance, process-management, csharp]

# Dependency graph
requires: []
provides:
  - Single instance enforcement — druhé spuštění widgetu zabije předchozí instanci
  - Mutex jako field na App třídě (GC-safe v Release buildu)
affects: [07-history, 08-ui]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Mutex jako field (ne lokální proměnná) — odolný vůči GC v Release buildu"
    - "Process.GetProcessesByName + Kill pro kill předchozí instance před WaitOne"

key-files:
  created: []
  modified:
    - ClaudeUsageWidget/Program.cs

key-decisions:
  - "Mutex field na App třídě (ne lokální var) — GC sbírá lokální Mutex v Release buildu"
  - "Local\\ClaudeUsageWidget prefix (ne Global\\) — widget je per-session, cross-session netřeba"
  - "Kill + WaitForExit(2000) před WaitOne(3000) — wait na smrt předchozí instance před pokusem o Mutex"

patterns-established:
  - "OnExit override: uvolnit systémové prostředky (Mutex) před base.OnExit"

requirements-completed: [STAB-01]

# Metrics
duration: 8min
completed: 2026-03-07
---

# Phase 6 Plan 01: Single Instance Enforcement Summary

**Mutex field na App třídě zabraňuje duplicitním widgetům — druhé spuštění vždy zabije předchozí instanci a přebere Mutex**

## Performance

- **Duration:** 8 min
- **Started:** 2026-03-07T16:48:13Z
- **Completed:** 2026-03-07T16:56:00Z
- **Tasks:** 1
- **Files modified:** 1

## Accomplishments

- Přidán `private Mutex? _mutex` field na `App` třídu (GC-safe)
- `OnStartup` vytváří Mutex `Local\ClaudeUsageWidget`, při `!createdNew` zabíjí předchozí instanci a čeká na Mutex
- `OnExit` override uvolňuje Mutex a dispose
- Build prošel bez chyb i warnings

## Task Commits

1. **Task 06-01-01: Mutex field + single-instance logika** - `eddb9ba` (feat)

**Plan metadata:** (docs commit následuje)

## Files Created/Modified

- `ClaudeUsageWidget/Program.cs` - Přidán Mutex field, single-instance logika v OnStartup, OnExit override, using System.Diagnostics

## Decisions Made

- Mutex field (ne lokální proměnná): GC může v Release buildu sbírat lokální handle a ochrana pak nefunguje
- `Local\ClaudeUsageWidget` prefix: widget běží v user session, Global\ netřeba
- Kill + WaitForExit(2000) před WaitOne(3000): nejprve kill předchozí instance, pak čekat na Mutex — zabraňuje deadlocku

## Deviations from Plan

None — plan executed exactly as written.

## Issues Encountered

- `dotnet` není v PATH v WSL — potřeba použít `/mnt/c/Program Files/dotnet/dotnet.exe` s Windows-style cestou (`C:\\Users\\...`). Vyřešeno automaticky.

## Next Phase Readiness

- Single instance enforcement hotovo, widget nebude spouštět duplicitní instance
- Připraveno pro Phase 06-02 (401 recovery + backoff)

---
*Phase: 06-stability*
*Completed: 2026-03-07*
