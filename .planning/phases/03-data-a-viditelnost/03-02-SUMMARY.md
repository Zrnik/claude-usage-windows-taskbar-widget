---
phase: 03-data-a-viditelnost
plan: 02
subsystem: ui
tags: [wpf, csharp, win32, pinvoke, visibility, taskbar, fullscreen]

# Dependency graph
requires:
  - phase: 03-data-a-viditelnost
    provides: MainWindow.xaml.cs s Win32 importy (SHAppBarMessage, GetWindowRect, MonitorFromWindow, GetMonitorInfo, APPBARDATA, MONITORINFO, RECT)
provides:
  - DispatcherTimer _visibilityTimer (500ms) spuštěný v Loaded, zastaven v Closed
  - StartVisibilityTimer() volající CheckVisibility() každých 500ms
  - CheckVisibility() nastavující Visibility.Visible nebo Hidden
  - IsTaskbarVisible() rozlišující primární (ABM_GETSTATE) a sekundární (heuristika rcMonitor) taskbar
  - IsFullscreenOnMyMonitor() detekující fullscreen na stejném monitoru jako taskbar
affects: [03-data-a-viditelnost]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Auto-hide detekce: ABM_GETSTATE → ABS_AUTOHIDE flag → GetWindowRect porovnání s ABM_GETTASKBARPOS"
    - "Fullscreen detekce: foreground window bounds vs rcMonitor (fyzické rozměry, ne rcWork)"

key-files:
  created: []
  modified:
    - ClaudeUsageWidget/MainWindow.xaml.cs

key-decisions:
  - "Stub CheckVisibility() v Task 1 pro green build — replaced plnou implementací v Task 2"
  - "IsTaskbarVisible sekundární taskbar: tolerance 2px na Top < rcMonitor.Bottom jako heuristika pro auto-hide"
  - "IsFullscreenOnMyMonitor porovnává s rcMonitor (ne rcWork) — taskbar nesmí ovlivnit detekci"

patterns-established:
  - "Visibility timer pattern: 500ms DispatcherTimer → synchronní Win32 volání → nastavit Visibility property"

requirements-completed: [VIS-01, VIS-02]

# Metrics
duration: 5min
completed: 2026-03-07
---

# Phase 3 Plan 02: Visibility Timer Summary

**500ms DispatcherTimer s auto-hide taskbar detekcí (ABM_GETSTATE) a fullscreen detekcí (foreground window vs rcMonitor) pro skrývání widgetu**

## Performance

- **Duration:** 5 min
- **Started:** 2026-03-07T08:00:00Z
- **Completed:** 2026-03-07T08:05:00Z
- **Tasks:** 2
- **Files modified:** 1

## Accomplishments
- _visibilityTimer (500ms) spuštěn v Loaded, zastaven v Closed
- IsTaskbarVisible() detekuje auto-hide stav primárního taskbaru přes ABM_GETSTATE + GetWindowRect
- IsFullscreenOnMyMonitor() ignoruje fullscreen na jiném monitoru, porovnává s rcMonitor
- CheckVisibility() kombinuje obě podmínky: viditelný jen pokud taskbarVisible && !fullscreen

## Task Commits

Každý task byl commitován atomicky:

1. **Task 1: Přidat P/Invoke importy a StartVisibilityTimer()** - `d2596be` (feat)
2. **Task 2: Implementovat CheckVisibility(), IsTaskbarVisible(), IsFullscreenOnMyMonitor()** - `52ebf9f` (feat)

**Plan metadata:** (viz finální commit)

## Files Created/Modified
- `ClaudeUsageWidget/MainWindow.xaml.cs` - Přidány importy GetForegroundWindow, konstanty ABM_GETSTATE/ABS_AUTOHIDE, pole _visibilityTimer, metody StartVisibilityTimer/CheckVisibility/IsTaskbarVisible/IsFullscreenOnMyMonitor

## Decisions Made
- Task 1 obsahuje stub `CheckVisibility() { }` pro green build — bez něj by Timer.Tick lambda nešla zkompilovat před Task 2
- Sekundární taskbar: tolerance 2px (`taskbarRect.Top < mi.rcMonitor.Bottom - 2`) pro rozlišení skrytého stavu
- Fullscreen porovnání: `rcMonitor` (fyzické rozměry obrazovky), nikoli `rcWork` (pracovní plocha bez taskbaru)

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] Stub CheckVisibility() pro green build po Task 1**
- **Found during:** Task 1 (build verification)
- **Issue:** StartVisibilityTimer() referencuje CheckVisibility() — bez stubové implementace build selže s CS0103
- **Fix:** Přidán prázdný stub `private void CheckVisibility() { }` v Task 1, nahrazen plnou implementací v Task 2
- **Files modified:** ClaudeUsageWidget/MainWindow.xaml.cs
- **Verification:** Build green po Task 1 i Task 2
- **Committed in:** d2596be (Task 1 commit)

---

**Total deviations:** 1 auto-fixed (1 blocking)
**Impact on plan:** Nutný pro splnění "build green" done kritéria v Task 1. Bez scope creep.

## Issues Encountered
- Build po Task 1 selhal na CS0103 (CheckVisibility neexistuje) — vyřešeno přidáním stubu dle Rule 3.

## Next Phase Readiness
- Visibility timer plně funkční, widget se skryje při auto-hide taskbaru a fullscreen aplikaci
- Zbývají požadavky VIS-03 (test) pokud existují v dalším plánu
- Blocker z STATE.md (ClaudeApiClient nebyl testován) zůstává

---
*Phase: 03-data-a-viditelnost*
*Completed: 2026-03-07*
