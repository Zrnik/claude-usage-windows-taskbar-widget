---
gsd_state_version: 1.0
milestone: v0.1
milestone_name: milestone
status: planning
stopped_at: Completed 06-stability/06-02-PLAN.md
last_updated: "2026-03-07T16:50:45.238Z"
last_activity: 2026-03-07 — Roadmap created for v0.1.11
progress:
  total_phases: 3
  completed_phases: 1
  total_plans: 2
  completed_plans: 2
---

# Project State

## Project Reference

See: .planning/PROJECT.md (updated 2026-03-07)

**Core value:** Okamžitě viditelné vytížení Claude limitů přímo v taskbaru — bez klikání, bez otevírání oken.
**Current focus:** Phase 6 — Stability

## Current Position

Phase: 6 of 8 (Stability)
Plan: 1 of 2 in current phase (06-01 complete)
Status: In progress
Last activity: 2026-03-07 — Completed 06-01 Single Instance Enforcement

Progress (v0.1.11): [█████████░] 89%

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

### Pending Todos

- Single instance enforcement and better updater (`2026-03-07-single-instance-enforcement-and-better-updater.md`)
- Přejmenovat repozitář na AI Usage Widget (`2026-03-07-prejmenovat-repozitar-na-ai-usage-widget.md`)

### Blockers/Concerns

- History file: HIST-02 říká per-account soubor — rozhodnutí potvrdit v Phase 7

### Known Tech Debt

- Phase 02 nemá VERIFICATION.md — dokumentační problém, implementace funguje

## Session Continuity

Last session: 2026-03-07T16:50:45.210Z
Stopped at: Completed 06-stability/06-02-PLAN.md
Resume file: None
