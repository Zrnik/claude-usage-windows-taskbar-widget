---
phase: 02-widget-ui-a-pozicovani
plan: 03
subsystem: ui
tags: [wpf, xaml, tooltip, context-menu, api-client]

# Dependency graph
requires:
  - phase: 02-widget-ui-a-pozicovani
    provides: MainWindow s progress bary a Win32 pozicováním
provides:
  - Tooltip s časem do resetu při hoveru nad widgetem
  - Context menu Quit při pravém kliknutí
  - Reálná data z ClaudeApiClient napojená na UI
  - Program.cs předává ClaudeApiClient do MainWindow
affects: [03-refresh-a-tray]

# Tech tracking
tech-stack:
  added: []
  patterns: [constructor dependency injection pro ClaudeApiClient, async Loaded handler pro API volání]

key-files:
  created: []
  modified:
    - ClaudeUsageWidget/ClaudeUsageWidgetProvider/MainWindow.xaml
    - ClaudeUsageWidget/ClaudeUsageWidgetProvider/MainWindow.xaml.cs
    - ClaudeUsageWidget/ClaudeUsageWidgetProvider/Program.cs

key-decisions:
  - "MainWindow konstruktor internal (ne public) kvůli internal ClaudeApiClient parametru — CS0051"
  - "Tooltip přidán na root Grid (ne na Window) pro pokrytí celého widgetu"

patterns-established:
  - "ClaudeApiClient předáván přes konstruktor, ne přes static/singleton"
  - "Loaded async handler pro API volání — čeká na zobrazení okna"

requirements-completed: [UI-06, LIFE-03]

# Metrics
duration: 15min
completed: 2026-03-06
---

# Phase 2 Plan 03: API napojení, Tooltip a Context Menu Summary

**WPF widget napojený na ClaudeApiClient s tooltipem reset-time a context menu Quit — Phase 2 kompletní**

## Performance

- **Duration:** 15 min
- **Started:** 2026-03-06T~
- **Completed:** 2026-03-06T~
- **Tasks:** 2 (+ checkpoint pending)
- **Files modified:** 3

## Accomplishments
- MainWindow přijímá ClaudeApiClient v konstruktoru (internal visibility fix)
- Tooltip na root Grid zobrazuje čas do resetu pro 5h a 7d limity
- Context menu "Quit" přes pravé tlačítko ukončí aplikaci
- Program.cs vytváří ClaudeApiClient, nastavuje credentials, předává do MainWindow
- Dispose voláno při Exit

## Task Commits

1. **Task 1: Tooltip a context menu** - `b75040a` (feat)
2. **Task 2: Napojit ClaudeApiClient v Program.cs** - `684e499` (feat)

## Files Created/Modified
- `ClaudeUsageWidget/ClaudeUsageWidgetProvider/MainWindow.xaml` - přidán Grid.ToolTip a MouseRightButtonUp handler
- `ClaudeUsageWidget/ClaudeUsageWidgetProvider/MainWindow.xaml.cs` - konstruktor s apiClient, UpdateTooltip, OnRightClick, async Loaded
- `ClaudeUsageWidget/ClaudeUsageWidgetProvider/Program.cs` - ClaudeApiClient instance, SetCredential, Dispose, new MainWindow(apiClient)

## Decisions Made
- `MainWindow` konstruktor je `internal` (ne `public`) — ClaudeApiClient je `internal sealed class`, veřejný konstruktor by způsobil CS0051

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Opravena viditelnost konstruktoru MainWindow**
- **Found during:** Task 1 (build po implementaci)
- **Issue:** CS0051 — public konstruktor s internal parametrem ClaudeApiClient
- **Fix:** Konstruktor změněn z `public` na `internal`
- **Files modified:** MainWindow.xaml.cs
- **Verification:** dotnet build 0 errors
- **Committed in:** b75040a

---

**Total deviations:** 1 auto-fixed (1 bug)
**Impact on plan:** Nutná oprava pro kompilaci. Žádný scope creep.

## Issues Encountered
- dotnet není v WSL PATH — použito `/mnt/c/Program Files/dotnet/dotnet.exe` s Windows cestou

## Checkpoint Status

Checkpoint `human-verify` pending — uživatel musí vizuálně ověřit widget.

## Next Phase Readiness
- Phase 2 kód kompletní a kompiluje
- Čeká na vizuální verifikaci (checkpoint)
- Phase 3: auto-refresh timer, tray icon

---
*Phase: 02-widget-ui-a-pozicovani*
*Completed: 2026-03-06*
