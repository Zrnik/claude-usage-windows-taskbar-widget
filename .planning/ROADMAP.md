# Roadmap: Claude Usage Widget

## Milestones

- ✅ **v0.1.9 MVP** — Phases 1-3 (shipped 2026-03-07)
- ✅ **v0.1.10 Multi-account** — Phases 4-5 (shipped 2026-03-07)
- ✅ **v0.1.11 Usage History** — Phases 6-8 (shipped 2026-03-07)
- 🔨 **v0.1.12 Chart Windows** — Phases 9-11

## Phases

<details>
<summary>✅ v0.1.9 MVP (Phases 1-3) — SHIPPED 2026-03-07</summary>

Phases 1-3 delivered the MVP: borderless taskbar window, progress bars, color coding, hover tooltip, auto-hide + fullscreen support, error state, and ClaudeApiClient via inference call + rate limit headers.

</details>

<details>
<summary>✅ v0.1.10 Multi-account (Phases 4-5) — SHIPPED 2026-03-07</summary>

Phases 4-5 delivered multi-account support: AccountInfo record, ServiceType enum, LoadAllAccounts() with org ID deduplication, per-account ClaudeApiClient, AccountPanel UserControl with embedded icons, horizontal StackPanel layout, dynamic width.

</details>

<details>
<summary>✅ v0.1.11 Usage History (Phases 6-8) — SHIPPED 2026-03-07</summary>

Phases 6-8 delivered stability fixes and history charts: single instance enforcement (Mutex), token 401 recovery with JWT-based deduplication, UsageHistoryStore with atomic hourly-bucket upsert to AppData, HistoryChart UserControl (WPF Canvas sparkline, multicolor segments), and PopupWindow 280px redesign with reset Grid and embedded sparkline charts.

</details>

### v0.1.12 Chart Windows (Phases 9-11)

---

### Phase 9: Time-Anchored Charts + Tech Debt

**Goal:** Refaktorovat HistoryChart na časově ukotvenou osu X s interpolací mezer a vyčistit tech debt.

**Requirements:** CHART-01, CHART-03, DEBT-01

**Plans:** 2 plans

Plans:
- [ ] 09-01-PLAN.md — ExtractAccountKey tech debt elimination (DEBT-01)
- [ ] 09-02-PLAN.md — Time-anchored chart with gap processing (CHART-01, CHART-03)

**Dependencies:** None

**Success Criteria:**
1. Osa X grafu odpovídá reálnému času — body jsou umístěny podle svého timestampu
2. Při mezeře < 2h v datech se hodnoty lineárně interpolují (žádné zuby)
3. Při mezeře ≥ 2h graf klesne na 0 (vizuálně jasná přerušení)
4. ExtractAccountKey() v ClaudeApiClient odstraněn — volá CredentialStore.GetAccountKey()

---

### Phase 10: Per-Key Chart Windows + Extra Usage

**Goal:** Každý rate limit klíč zobrazuje graf s vlastním časovým oknem dle defaultů. Vizualizace extra usage (>100%) v grafech i progress barech. Research API pro spend data.

**Requirements:** CHART-02

**Plans:** 1/3 plans executed

Plans:
- [ ] 10-01-PLAN.md — Per-key time windows + >100% color system (CHART-02)
- [ ] 10-02-PLAN.md — Dynamic Y axis + 100% reference line
- [ ] 10-03-PLAN.md — Extra usage spend bar research + decision

**Dependencies:** Phase 9

**Success Criteria:**
1. 5H graf zobrazuje posledních 2 dny dat
2. 7D graf zobrazuje posledních 14 dní dat
3. SESSION/100H graf zobrazuje posledních 14 dní dat
4. REVIEW graf zobrazuje posledních 7 dní dat
5. Každý graf renderuje pouze data v rámci svého okna
6. Utilization >100% zobrazena purpurovou barvou konzistentně v grafech i progress barech
7. Y osa grafu se dynamicky rozšíří při hodnotách >100%
8. Tenká čára na 100% hranici v grafu při dynamické Y ose

---

### Phase 11: Settings UI + Persistence

**Goal:** Uživatel může v Settings okně měnit časové okno per klíč s okamžitým překreslením.

**Requirements:** SETT-01, SETT-02, SETT-03

**Dependencies:** Phase 10

**Success Criteria:**
1. Settings okno obsahuje nastavení časového okna pro každý rate limit klíč
2. Defaultní hodnoty odpovídají hardcoded defaults (5H=2d, 7D=14d, SESSION=14d, REVIEW=7d)
3. Nastavení se persistuje do AppData JSON (přežije restart widgetu)
4. Změna hodnoty v Settings okně okamžitě překreslí odpovídající graf

## Progress

| Phase | Milestone | Plans Complete | Status | Completed |
|-------|-----------|----------------|--------|-----------|
| 1. MVP Foundation | v0.1.9 | - | Complete | 2026-03-07 |
| 2. MVP Core | v0.1.9 | - | Complete | 2026-03-07 |
| 3. MVP Polish | v0.1.9 | - | Complete | 2026-03-07 |
| 4. Multi-account Arch | v0.1.10 | - | Complete | 2026-03-07 |
| 5. Multi-account UI | v0.1.10 | - | Complete | 2026-03-07 |
| 6. Stability | v0.1.11 | 2/2 | Complete | 2026-03-07 |
| 7. History Persistence | v0.1.11 | 1/1 | Complete | 2026-03-07 |
| 8. Tooltip & Chart | v0.1.11 | 2/2 | Complete | 2026-03-07 |
| 9. Time-Anchored Charts + Tech Debt | v0.1.12 | 0/2 | Planned | - |
| 10. Per-Key Chart Windows + Extra Usage | 1/3 | In Progress|  | - |
| 11. Settings UI + Persistence | v0.1.12 | 0/0 | Planned | - |
