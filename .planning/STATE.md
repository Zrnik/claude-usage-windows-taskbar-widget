---
gsd_state_version: 1.0
milestone: v0.1
milestone_name: milestone
status: executing
stopped_at: "08-02 checkpoint:human-verify — Task 3 awaiting visual verification"
last_updated: "2026-03-07T18:21:44.788Z"
last_activity: 2026-03-07 — Completed 07-01 UsageHistoryStore
progress:
  total_phases: 3
  completed_phases: 3
  total_plans: 5
  completed_plans: 5
---

# Project State

## Project Reference

See: .planning/PROJECT.md (updated 2026-03-07)

**Core value:** Okamžitě viditelné vytížení Claude limitů přímo v taskbaru — bez klikání, bez otevírání oken.
**Current focus:** Phase 7 — History Persistence

## Current Position

Phase: 7 of 8 (History Persistence)
Plan: 1 of 1 in current phase (07-01 complete)
Status: In progress
Last activity: 2026-03-07 — Completed 07-01 UsageHistoryStore

Progress (v0.1.12): [█████████░] 91%

## Accumulated Context

### Decisions

Klíčové pro v0.1.11 (z research):
- Mutex jako field na App třídě (ne lokální proměnná) — GC pitfall v Release builds
- `Local\ClaudeUsageWidget` prefix (ne `Global\`) — widget nemá cross-session požadavek
- Atomic write: `File.WriteAllText(tmp)` + `File.Move(tmp, target, overwrite:true)` — crash safety
- Hourly bucket upsert (ne per-minute) — max 336 záznamů na účet
- Max 1 retry per poll cycle na 401 — pak error stav + 15min backoff
- HistoryChart: WPF Canvas + Polyline (built-in, no NuGet) — přiřadit PointCollection najednou
- [Phase 06-stability]: Mutex field na App třídě (ne lokální var) — GC-safe v Release buildu
- [Phase 06-stability]: Local\ClaudeUsageWidget Mutex prefix — widget je per-session
- [Phase 06-stability]: LoadCredentialFromPath() jako jediná public cesta k disk re-read per path
- [Phase 06-stability]: JWT dedup: GetAccountKey() vrací null pro credentials bez dekódovatelného JWT — tiché přeskočení
- [Phase 06-stability]: ClearSpinner() nastavuje prázdný string — UpdateBars/ShowErrorState přepíše správnou hodnotou
- [Phase 07-history-persistence]: ExtractAccountKey() duplikuje logiku z CredentialStore — private metoda nemůže být sdílena bez refactoru mimo scope
- [Phase 07-history-persistence]: Singleton UsageHistoryStore.Instance — žádné DI, přímý přístup z MainWindow
- [Phase 07-history-persistence]: Atomic write pattern potvrzen: tmp + Move(overwrite:true)
- [Phase 08-tooltip-chart]: PadToLength vždy na 336 bodů (14d × 24h) — konzistentní X-škálování
- [Phase 08-tooltip-chart]: Hraniční bod sdílený mezi segmenty při barevném přechodu — plynulý přechod bez mezery
- [Phase 08-tooltip-chart]: accountKey jako optional parametr s default null — zpětná kompatibilita
- [Phase 08-tooltip-chart]: MaxWidth error/credential TextBlock: 220→260px (odpovídá 280px - 2x10px padding)

### Pending Todos

- Single instance enforcement and better updater (`2026-03-07-single-instance-enforcement-and-better-updater.md`)
- Přejmenovat repozitář na AI Usage Widget (`2026-03-07-prejmenovat-repozitar-na-ai-usage-widget.md`)

### Blockers/Concerns

- (resolved) History file: HIST-02 per-account soubor — potvrzeno implementací v Phase 7

### Known Tech Debt

- Phase 02 nemá VERIFICATION.md — dokumentační problém, implementace funguje

## Session Continuity

Last session: 2026-03-07T18:21:44.767Z
Stopped at: 08-02 checkpoint:human-verify — Task 3 awaiting visual verification
Resume file: None
