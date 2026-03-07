# Roadmap: Claude Usage Widget — Taskbar

## Overview

Tři fáze: nejprve vyčistit existující codebase od MSIX/Widget provider kódu, pak postavit viditelný WPF widget s UI a správným pozicováním v taskbaru, nakonec napojit živá data z API a ošetřit edge cases viditelnosti.

## Phases

- [x] **Phase 1: Cleanup** - Odstranit MSIX/Widget provider kód, připravit čisté WPF exe (completed 2026-03-06)
- [x] **Phase 2: Widget UI a pozicování** - Borderless okno s progress bary, správně umístěné v taskbaru (completed 2026-03-06)
- [x] **Phase 3: Data a viditelnost** - Živá API data, auto-hide a fullscreen podpora (completed 2026-03-07)

## Phase Details

### Phase 1: Cleanup
**Goal**: Projekt je čisté WPF exe bez MSIX balasru a Widget provider kódu
**Depends on**: Nothing (first phase)
**Requirements**: CLEAN-01, CLEAN-02, CLEAN-03, CLEAN-04, LIFE-02
**Success Criteria** (what must be TRUE):
  1. Projekt se kompiluje jako standalone exe příkazem `dotnet build` bez chyb
  2. V project souboru neexistuje reference na MSIX/AppX packaging nebo Windows Widget APIs
  3. `ClaudeApiClient.cs` je zachován a funkční, `WidgetProvider.cs` a Widget provider závislosti jsou odstraněny
  4. Výsledné exe lze spustit přímo bez instalace přes MSIX/AppX
**Plans**: 1 plan

Plans:
- [ ] 01-01-PLAN.md — Přepsat .csproj na WPF, smazat Widget provider soubory, ověřit kompilaci

### Phase 2: Widget UI a pozicování
**Goal**: Uživatel vidí widget s progress bary správně umístěný v taskbaru
**Depends on**: Phase 1
**Requirements**: UI-01, UI-02, UI-03, UI-04, UI-05, UI-06, POS-01, POS-02, POS-03, LIFE-01, LIFE-03
**Success Criteria** (what must be TRUE):
  1. Widget se zobrazí jako borderless okno těsně před system tray oblastí, vizuálně splývá s taskbarem
  2. Jsou viditelné dva progress bary — horní (5h) a dolní (7d) — s procentem uvnitř a barevným kódováním (zelená/oranžová/červená)
  3. Při hoveru nad widgetem se zobrazí tooltip s časem do resetu pro oba limity
  4. Když se změní šířka tray oblasti, widget se automaticky přesune na správnou pozici
  5. Pravé tlačítko na widget otevře kontextové menu s možností Quit; aplikace v taskbaru nepřidá žádné okno
**Plans**: 3 plans

Plans:
- [ ] 02-01-PLAN.md — Vytvořit MainWindow.xaml s borderless oknem a flat progress bary
- [ ] 02-02-PLAN.md — Přidat Win32 pozicování těsně před system tray, tray watch timer
- [ ] 02-03-PLAN.md — Napojit ClaudeApiClient, tooltip, context menu Quit

### Phase 3: Data a viditelnost
**Goal**: Widget zobrazuje živá data a chová se správně při auto-hide taskbaru a fullscreenu
**Depends on**: Phase 2
**Requirements**: DATA-01, DATA-02, DATA-03, DATA-04, VIS-01, VIS-02
**Success Criteria** (what must be TRUE):
  1. Progress bary se každou minutu aktualizují na základě skutečných hodnot z `anthropic-ratelimit-unified-*` headers
  2. Při výpadku API widget zobrazí maroon chybový stav (bary maroon, text "Error", tooltip s detailem)
  3. Když taskbar zajede dolů (auto-hide), widget zmizí společně s ním a vrátí se při zobrazení taskbaru
  4. Při spuštění fullscreen aplikace se widget skryje na dotčeném monitoru a vrátí se po opuštění fullscreenu
**Plans**: 3 plans

Plans:
- [ ] 03-01-PLAN.md — Opravit ShowErrorState() (maroon + "Error") a stale data bug v refresh timer
- [ ] 03-02-PLAN.md — Přidat visibility timer (500ms) pro auto-hide taskbar a fullscreen detekci
- [ ] 03-03-PLAN.md — Manuální verifikace všech Phase 3 requirements

## Progress

**Execution Order:** 1 → 2 → 3

| Phase | Plans Complete | Status | Completed |
|-------|----------------|--------|-----------|
| 1. Cleanup | 1/1 | Complete   | 2026-03-06 |
| 2. Widget UI a pozicování | 3/3 | Complete   | 2026-03-06 |
| 3. Data a viditelnost | 2/3 | Complete    | 2026-03-07 |
