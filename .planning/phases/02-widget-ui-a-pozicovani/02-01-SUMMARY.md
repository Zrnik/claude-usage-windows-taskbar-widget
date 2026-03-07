---
phase: 02-widget-ui-a-pozicovani
plan: 01
subsystem: ui
tags: [wpf, window, progress-bar, widget-ui]
dependency_graph:
  requires: []
  provides: [MainWindow, UpdateBars]
  affects: [Program.cs, ClaudeApiClient.cs]
tech_stack:
  added: []
  patterns: [WPF borderless window, ControlTemplate flat ProgressBar]
key_files:
  created:
    - ClaudeUsageWidget/ClaudeUsageWidgetProvider/MainWindow.xaml
    - ClaudeUsageWidget/ClaudeUsageWidgetProvider/MainWindow.xaml.cs
  modified:
    - ClaudeUsageWidget/ClaudeUsageWidgetProvider/Program.cs
    - ClaudeUsageWidget/ClaudeUsageWidgetProvider/ClaudeApiClient.cs
decisions:
  - "UsageData zmenen na public class — nutne pro public UpdateBars(UsageData) v MainWindow"
metrics:
  duration: 15m
  completed_date: "2026-03-06"
  tasks_completed: 2
  files_changed: 4
---

# Phase 2 Plan 01: WPF MainWindow s progress bary

Borderless WPF okno 48px s dvema flat progress bary (5h, 7d) a barevnym kodovanim dle utilizace.

## Tasks Completed

| # | Task | Commit |
|---|------|--------|
| 1 | Vytvořit MainWindow.xaml s progress bary | aace5fb |
| 2 | Upravit Program.cs — spustit MainWindow | 1dfd6e7 |

## What Was Built

**MainWindow.xaml** — WPF okno s vlastnostmi:
- `WindowStyle="None"`, `ShowInTaskbar="False"`, `Topmost="True"`, `ResizeMode="NoResize"`
- `Background="#1C1C1C"` (splyvá s Win11 dark taskbarem)
- Grid se dvema progress bary (horní = 5h, dolní = 7d), mezera 6px
- Flat ControlTemplate pro ProgressBar bez border-radius

**MainWindow.xaml.cs** — Code-behind:
- `SetTaskbarHeight()` — čte skutečnou výšku taskbaru přes SystemParameters
- `UpdateBars(UsageData)` — public, nastavuje Value, Text a barvu obou barů
- `SetBarColor(Border, double)` — zelená < 75%, oranžová 75-90%, červená >= 90%
- Placeholder data v konstruktoru (50% / 80%) pro vizuální test

**Program.cs** — OnStartup override:
- Načítá credentials přes `CredentialStore.LoadCredential()`
- Zobrazí chybový MessageBox a ukončí se pokud credentials chybí
- Vytváří a zobrazuje MainWindow jako hlavní okno aplikace

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] UsageData internal → public**
- **Found during:** Task 1 (kompilace)
- **Issue:** `UpdateBars(UsageData)` musí být `public` dle plánu, ale `UsageData` byla `internal sealed class` — CS0051 nekonzistentní dostupnost
- **Fix:** Změněna viditelnost `UsageData` z `internal` na `public` v `ClaudeApiClient.cs`
- **Files modified:** ClaudeUsageWidget/ClaudeUsageWidgetProvider/ClaudeApiClient.cs
- **Commit:** aace5fb

## Self-Check: PASSED

- MainWindow.xaml: FOUND
- MainWindow.xaml.cs: FOUND
- Commit aace5fb: FOUND
- Commit 1dfd6e7: FOUND
