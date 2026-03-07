---
phase: 05-multi-account-ui
plan: 01
subsystem: ui
tags: [wpf, xaml, png, embedded-resource, assets]

# Dependency graph
requires: []
provides:
  - PNG ikony Claude (20x20px, oranžová #CC7000) a Codex (20x20px, modrá #0066CC) jako WPF Resource
  - Pack URI reference: pack://application:,,,/Assets/claude-logo.png a codex-logo.png
  - Assets adresář v projektu pro budoucí ikony
affects:
  - 05-multi-account-ui plan 02 (vizuální identifikátory služby v layout)

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "WPF Resource Build Action pro PNG assets — přístupné přes Pack URI bez externích souborů"

key-files:
  created:
    - ClaudeUsageWidget/Assets/claude-logo.png
    - ClaudeUsageWidget/Assets/codex-logo.png
  modified:
    - ClaudeUsageWidget/ClaudeUsageWidget.csproj

key-decisions:
  - "Build Action Resource (ne EmbeddedResource) — zpřístupňuje soubory přes Pack URI v XAML"
  - "PowerShell System.Drawing pro generování PNG — jednoduché, nevyžaduje externí závislosti"
  - "20x20px placeholder ikony s písmenem (C/X) — minimalistické, funkční pro fázi 5"

patterns-established:
  - "Assets/*.png jako WPF Resource — Include pattern v .csproj ItemGroup"

requirements-completed: [UI-07]

# Metrics
duration: 3min
completed: 2026-03-07
---

# Phase 5 Plan 01: Embedded ikony služeb Summary

**WPF Resource PNG ikony 20x20px pro Claude (oranžová) a Codex (modrá) embedded v exe, přístupné přes Pack URI**

## Performance

- **Duration:** 3 min
- **Started:** 2026-03-07T12:28:49Z
- **Completed:** 2026-03-07T12:32:03Z
- **Tasks:** 1
- **Files modified:** 3

## Accomplishments
- Vytvořen Assets/ adresář s claude-logo.png (238 bytes) a codex-logo.png (296 bytes)
- Ikony vygenerovány PowerShell System.Drawing scriptem — oranžový C a modrý X na 20x20px
- ClaudeUsageWidget.csproj doplněn o Resource ItemGroup pro oba soubory
- Build ověřen: 0 errors, 0 warnings

## Task Commits

Každý task byl commitován atomicky:

1. **Task 05-01-01: PNG ikony a Resource v .csproj** - `8b528a4` (feat)

**Plan metadata:** (viz final commit níže)

## Files Created/Modified
- `ClaudeUsageWidget/Assets/claude-logo.png` - Claude service icon 20x20px, oranžový #CC7000 s bílým C
- `ClaudeUsageWidget/Assets/codex-logo.png` - Codex service icon 20x20px, modrý #0066CC s bílým X
- `ClaudeUsageWidget/ClaudeUsageWidget.csproj` - přidán ItemGroup s Resource Include pro oba PNG

## Decisions Made
- Build Action `Resource` místo `EmbeddedResource` — umožňuje Pack URI `pack://application:,,,/Assets/...` v XAML
- PowerShell generátor ikon (System.Drawing) — žádné externí závislosti, funguje přímo na Windows
- Dočasný `create-icons.ps1` skript smazán po úspěšném vygenerování ikon

## Deviations from Plan

None - plan executed exactly as written.

## Issues Encountered

- `dotnet` není dostupný v WSL PATH — použito `powershell.exe -Command "& dotnet build ..."` (standardní postup pro tento projekt)

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

- Assets adresář a Pack URI reference připraveny pro Plan 02
- `pack://application:,,,/Assets/claude-logo.png` a `codex-logo.png` použitelné v XAML jako ImageSource

---
*Phase: 05-multi-account-ui*
*Completed: 2026-03-07*
