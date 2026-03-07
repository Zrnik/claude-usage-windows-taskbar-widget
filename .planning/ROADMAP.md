# Roadmap: Claude Usage Widget — Taskbar

## Milestones

- ✅ **v0.1.9 MVP** — Phases 1-3 (shipped 2026-03-07)
- **v0.1.10 Multi-account** — Phases 4-5 (in progress)

## Phases

<details>
<summary>✅ v0.1.9 MVP (Phases 1-3) — SHIPPED 2026-03-07</summary>

- [x] Phase 1: Cleanup (1/1 plan) — completed 2026-03-06
- [x] Phase 2: Widget UI a pozicování (3/3 plans) — completed 2026-03-06
- [x] Phase 3: Data a viditelnost (3/3 plans) — completed 2026-03-07

</details>

**v0.1.10 Multi-account:**

- [x] **Phase 4: Multi-account detekce** — Widget načítá a deduplikuje účty z Windows + WSL pro Claude i Codex
- [ ] **Phase 5: Multi-account UI** — Widget zobrazuje více sad barů horizontálně s ikonami služeb

## Phase Details

### Phase 4: Multi-account detekce
**Goal**: Widget umí načíst všechny dostupné účty (Claude Windows, Claude WSL, Codex Windows, Codex WSL), deduplikovat je podle org ID a pro každý unikátní účet volat příslušné API
**Depends on**: Phase 3 (data pipeline existuje)
**Requirements**: MULTI-01, MULTI-02, MULTI-03
**Success Criteria** (what must be TRUE):
  1. Widget načte Claude credentials z obou cest (Windows i WSL) a zobrazí jednu sadu barů pro každé unikátní org ID (ne duplicitní)
  2. Widget načte Codex credentials a zobrazí samostatnou sadu barů pro Codex účet
  3. Každá sada barů zobrazuje data z API callu specifického pro daný účet (ne sdílená data)
  4. Pokud credentials pro danou službu neexistují, widget tuto službu tiše přeskočí (nezobrazí error)
**Plans**: 3 plans

Plans:
- [x] 04-01-PLAN.md — ServiceType, AccountInfo, LoadAllAccounts() + Codex credential loading (CredentialStore)
- [x] 04-02-PLAN.md — Per-account konstruktor ClaudeApiClient(AccountInfo) + _noReload fix
- [x] 04-03-PLAN.md — Per-account MainWindow instantiation v Program.cs + manuální verifikace

### Phase 5: Multi-account UI
**Goal**: Widget vizuálně prezentuje více účtů jako horizontální řadu sloupců s ikonou identifikující službu
**Depends on**: Phase 4
**Requirements**: UI-07, UI-08, UI-09
**Success Criteria** (what must be TRUE):
  1. Každá sada progress barů má vlevo od barů ikonu identifikující službu (Claude logo nebo Codex logo)
  2. Více účtů se zobrazuje horizontálně vedle sebe — každý účet je samostatný sloupec
  3. Celková šířka widgetu se mění dynamicky podle počtu zobrazených účtů, aniž by widget překrýval taskbar ikony vlevo nebo tray oblast vpravo
**Plans**: 2 plans

Plans:
- [ ] 05-01-PLAN.md — Embedded PNG ikony pro Claude a Codex jako WPF Resource
- [ ] 05-02-PLAN.md — AccountPanel UserControl + multi-account layout + dynamická šířka + Program.cs fix

## Progress

| Phase | Milestone | Plans Complete | Status | Completed |
|-------|-----------|----------------|--------|-----------|
| 1. Cleanup | v0.1.9 | 1/1 | Complete | 2026-03-06 |
| 2. Widget UI a pozicování | v0.1.9 | 3/3 | Complete | 2026-03-06 |
| 3. Data a viditelnost | v0.1.9 | 3/3 | Complete | 2026-03-07 |
| 4. Multi-account detekce | v0.1.10 | 3/3 | Complete | 2026-03-07 |
| 5. Multi-account UI | 1/2 | In Progress|  | - |
