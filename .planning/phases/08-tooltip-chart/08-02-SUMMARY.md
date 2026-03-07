---
phase: 08-tooltip-chart
plan: 02
subsystem: ui
tags: [wpf, popup, tooltip, sparkline, chart, history]

# Dependency graph
requires:
  - phase: 08-01
    provides: HistoryChart UserControl (sparkline) a UsageHistoryStore.GetUtilizationHistory()
  - phase: 07-01
    provides: UsageHistoryStore.Instance, AccountKey property na ClaudeApiClient
provides:
  - PopupWindow 280px s reset Grid layoutem (countdown vlevo, datum vpravo)
  - HistoryChart embed pod každý limit v tooltipa
  - UpdateAndShow s accountKey parametrem pro načítání historických dat
affects: [08-tooltip-chart]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - Grid s dvěma sloupci (Star + Auto) pro reset řádek v code-behind
    - HistoryChart embed do dynamicky sestavovaného StackPanel
    - Optional accountKey parametr s default null pro zpětnou kompatibilitu

key-files:
  created: []
  modified:
    - ClaudeUsageWidget/PopupWindow.xaml
    - ClaudeUsageWidget/PopupWindow.xaml.cs
    - ClaudeUsageWidget/MainWindow.xaml.cs

key-decisions:
  - "accountKey parametr s default null — zpětná kompatibilita bez breaking change"
  - "SetData voláno před UpdateLayout() — HistoryChart si pamatuje _pendingValues a renderuje po SizeChanged"
  - "MaxWidth error/credential TextBlock: 220→260 (280px okno - 2×10px padding)"

patterns-established:
  - "Reset řádek: Grid (Star + Auto) v code-behind, né XAML template"

requirements-completed: [TOOL-01, TOOL-02, TOOL-03]

# Metrics
duration: 5min
completed: 2026-03-07
---

# Phase 08 Plan 02: PopupWindow Redesign Summary

**280px tooltip s reset Grid (countdown/datum na jednom řádku) a HistoryChart sparkline pod každý limit**

## Performance

- **Duration:** ~5 min
- **Started:** 2026-03-07T18:17:27Z
- **Completed:** 2026-03-07T18:21:00Z
- **Tasks:** 2/3 (Task 3 = human-verify checkpoint)
- **Files modified:** 3

## Accomplishments
- PopupWindow rozšířen ze 170px na 280px
- Reset řádek redesignován na Grid — countdown vlevo, resetové datum vpravo
- HistoryChart UserControl embedován pod každý limit v tooltipa
- MainWindow předává client.AccountKey do UpdateAndShow

## Task Commits

Každý task byl commitnut atomicky:

1. **Task 1: PopupWindow šířka 280px a reset Grid** - `ceee540` (feat)
2. **Task 2: HistoryChart embed + MainWindow integrace** - `d808aaa` (feat)
3. **Task 3: Vizuální ověření** - checkpoint:human-verify (awaiting)

## Files Created/Modified
- `/mnt/c/Users/stepa/source/claude-usage-widget/ClaudeUsageWidget/PopupWindow.xaml` - Width 170→280px
- `/mnt/c/Users/stepa/source/claude-usage-widget/ClaudeUsageWidget/PopupWindow.xaml.cs` - resetGrid, HistoryChart embed, accountKey parametr
- `/mnt/c/Users/stepa/source/claude-usage-widget/ClaudeUsageWidget/MainWindow.xaml.cs` - předání client.AccountKey

## Decisions Made
- accountKey jako optional parametr (default null) pro zpětnou kompatibilitu
- SetData voláno před UpdateLayout() — HistoryChart interně renderuje při SizeChanged

## Deviations from Plan

None - plan executed exactly as written.

## Issues Encountered
None.

## User Setup Required
None - no external service configuration required.

## Next Phase Readiness
- Phase 08 implementace kompletní po vizuálním ověření (Task 3 checkpoint)
- Widget ready pro produkci: sparkline grafy, reset Grid, 280px tooltip

## Self-Check: PASSED
- ceee540 commit exists: confirmed
- d808aaa commit exists: confirmed
- PopupWindow.xaml Width="280": confirmed
- PopupWindow.xaml.cs accountKey parameter: confirmed
- MainWindow.xaml.cs client.AccountKey passed: confirmed

---
*Phase: 08-tooltip-chart*
*Completed: 2026-03-07*
