---
phase: 02-widget-ui-a-pozicovani
plan: 02
subsystem: positioning
tags: [wpf, win32, pinvoke, taskbar, tray, positioning]
dependency_graph:
  requires: [02-01]
  provides: [PositionWindow, GetTrayWidth, GetTaskbarInfo]
  affects: [MainWindow.xaml.cs]
tech_stack:
  added: []
  patterns: [Win32 P/Invoke SHAppBarMessage, DispatcherTimer tray watch, DPI scaling WPF]
key_files:
  created: []
  modified:
    - ClaudeUsageWidget/ClaudeUsageWidgetProvider/MainWindow.xaml.cs
decisions:
  - "Loaded event handler pro PositionWindow — PresentationSource.FromVisual vyžaduje okno v DOM"
  - "Fallback 200px pro tray šířku pokud Shell_TrayWnd/TrayNotifyWnd HWND nenalezeno"
metrics:
  duration: 5m
  completed_date: "2026-03-06"
  tasks_completed: 1
  files_changed: 1
---

# Phase 2 Plan 02: Win32 pozicování widgetu před system tray

Win32 P/Invoke pozicování přes SHAppBarMessage a FindWindow pro umístění widgetu těsně vlevo od tray oblasti s automatickým přepočtem při změně.

## Tasks Completed

| # | Task | Commit |
|---|------|--------|
| 1 | Přidat Win32 P/Invoke a detekci pozice taskbaru | d9d2b00 |

## What Was Built

**MainWindow.xaml.cs** — rozšířen o:

- **P/Invoke deklarace:** `SHAppBarMessage`, `FindWindow`, `FindWindowEx`, `GetWindowRect` + structs `RECT`, `APPBARDATA`
- **`GetTaskbarInfo()`** — volá `SHAppBarMessage(ABM_GETTASKBARPOS)`, vrátí `(isBottom, taskbarRect)`
- **`GetTrayWidth()`** — najde `Shell_TrayWnd` → `TrayNotifyWnd`, změří šířku přes `GetWindowRect`, fallback 200px
- **`PositionWindow()`** — zkontroluje `isBottom` (jinak "smůla bejku" + Shutdown), aplikuje DPI scale, nastaví `Left`/`Top`
- **`StartTrayWatchTimer()`** — `DispatcherTimer` 2s interval, při změně `GetTrayWidth()` zavolá `PositionWindow()`
- **`Loaded` event** — volá `SetTaskbarHeight()`, `PositionWindow()`, `StartTrayWatchTimer()` po zobrazení okna

## Deviations from Plan

None — plan executed exactly as written.

## Self-Check: PASSED

- MainWindow.xaml.cs: FOUND (obsahuje PositionWindow, GetTrayWidth, GetTaskbarInfo)
- Commit d9d2b00: FOUND
- dotnet build: 0 errors
