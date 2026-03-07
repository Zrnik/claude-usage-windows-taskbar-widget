# Roadmap: Claude Usage Widget

## Milestones

- ✅ **v0.1.9 MVP** - Phases 1-3 (shipped 2026-03-07)
- ✅ **v0.1.10 Multi-account** - Phases 4-5 (shipped 2026-03-07)
- 🚧 **v0.1.11 Usage History** - Phases 6-8 (in progress)

## Phases

<details>
<summary>✅ v0.1.9 MVP (Phases 1-3) - SHIPPED 2026-03-07</summary>

Phases 1-3 delivered the MVP: borderless taskbar window, progress bars, color coding, hover tooltip, auto-hide + fullscreen support, error state, and ClaudeApiClient via inference call + rate limit headers.

</details>

<details>
<summary>✅ v0.1.10 Multi-account (Phases 4-5) - SHIPPED 2026-03-07</summary>

Phases 4-5 delivered multi-account support: AccountInfo record, ServiceType enum, LoadAllAccounts() with org ID deduplication, per-account ClaudeApiClient, AccountPanel UserControl with embedded icons, horizontal StackPanel layout, dynamic width.

</details>

### 🚧 v0.1.11 Usage History (In Progress)

**Milestone Goal:** Přidat historický usage graf do tooltipu a opravit stabilitu widgetu.

#### Phase 6: Stability
**Goal**: Widget se spustí spolehlivě — vždy jedna instance, bez zaseknutí po expiraci tokenu
**Depends on**: Phase 5
**Requirements**: STAB-01, STAB-02, STAB-03, STAB-04
**Success Criteria** (what must be TRUE):
  1. Spuštění druhé instance widgetu ukončí předchozí instanci a nastartuje novou — žádné duplicity
  2. Po 401 widget přečte credentials z disku — pokud se liší od aktuálních, použije nové okamžitě; pokud jsou stejné, přejde do error stavu. Žádné zaseknutí.
  3. Accounts bez zjistitelného klíče (org ID / account_id) jsou tiše přeskočeny — widget se nastartuje i s neúplnými credentials
  4. Text v progress baru se zobrazuje správně bez zaseknutí na lomítku
**Plans**: TBD

Plans:
- [ ] 06-01: Single instance enforcement (Mutex v Program.cs jako field)
- [ ] 06-02: Token expiry recovery + deduplication fix + progress bar text fix

#### Phase 7: History Persistence
**Goal**: Widget ukládá hourly usage data per účet do AppData — základ pro chart
**Depends on**: Phase 6
**Requirements**: HIST-01, HIST-02, HIST-03, HIST-04
**Success Criteria** (what must be TRUE):
  1. Po každém úspěšném API callu se utilization hodnoty zapíší do `%APPDATA%\ClaudeUsageWidget\` jako JSON
  2. Každý unikátní účet má svůj soubor pojmenovaný dle klíče z STAB-03
  3. JSON se zapisuje atomicky (tmp soubor + File.Move) — crash nezkorumpuje data
  4. History soubor obsahuje maximálně 336 záznamů (14 dní × 24 hodin) — starší záznamy se automaticky ořezávají
**Plans**: 1 plan

Plans:
- [ ] 07-01-PLAN.md — UsageHistoryStore (append, trim, atomic write, in-memory cache) + MainWindow integrace

#### Phase 8: Tooltip & Chart
**Goal**: Tooltip zobrazuje historický usage graf v přepracovaném širším layoutu
**Depends on**: Phase 7
**Requirements**: TOOL-01, TOOL-02, TOOL-03
**Success Criteria** (what must be TRUE):
  1. Hover tooltip je 280px široký; reset čas (countdown) je vlevo, reset datum vpravo na stejném řádku
  2. Pod reset řádkem jsou dva sparkline grafy (5h a 7d utilization) s daty za 14 dní
  3. Grafy se renderují plynule — používají hourly data (max 336 bodů), ne per-minutové záznamy
**Plans**: 2 plans

Plans:
- [ ] 08-01-PLAN.md — HistoryChart UserControl (Canvas + Polyline, multicolor segmenty, fill polygon)
- [ ] 08-02-PLAN.md — PopupWindow redesign (280px, reset Grid, HistoryChart embed) + MainWindow integrace

## Progress

| Phase | Milestone | Plans Complete | Status | Completed |
|-------|-----------|----------------|--------|-----------|
| 1. MVP Foundation | v0.1.9 | - | Complete | 2026-03-07 |
| 2. MVP Core | v0.1.9 | - | Complete | 2026-03-07 |
| 3. MVP Polish | v0.1.9 | - | Complete | 2026-03-07 |
| 4. Multi-account Arch | v0.1.10 | - | Complete | 2026-03-07 |
| 5. Multi-account UI | v0.1.10 | - | Complete | 2026-03-07 |
| 6. Stability | 2/2 | Complete   | 2026-03-07 | - |
| 7. History Persistence | 1/1 | Complete    | 2026-03-07 | - |
| 8. Tooltip & Chart | v0.1.11 | 0/2 | Not started | - |
