# Requirements: Claude Usage Widget — Taskbar

**Defined:** 2026-03-06
**Core Value:** Okamžitě viditelné vytížení Claude limitů přímo v taskbaru — bez klikání, bez otevírání oken.

## v1 Requirements

### Widget UI

- [x] **UI-01**: Borderless okno bez titulbaru nebo rámečku pozicované nad taskbarem
- [x] **UI-02**: Okno má výšku taskbaru a šířku odpovídající obsahu (2 progress bary)
- [x] **UI-03**: 2 progress bary nad sebou — horní = 5h session limit, dolní = 7d rolling limit
- [x] **UI-04**: Progress bar zelený < 75%, oranžový 75–90%, červený ≥ 90%
- [x] **UI-05**: Text s procentem zobrazen uvnitř progress baru
- [x] **UI-06**: Hover nad widgetem zobrazí tooltip s časem do resetu

### Pozicování & Viditelnost

- [x] **POS-01**: Widget se umisťuje co nejdál vpravo na taskbaru, těsně před system tray oblastí
- [x] **POS-02**: Widget sleduje změny šířky tray oblasti a přepočítá pozici dynamicky
- [x] **POS-03**: Widget zůstává always-on-top (pokud není skrytý)
- [x] **VIS-01**: Když je taskbar nastaven jako auto-hide a zajede dolů, widget se skryje společně s ním
- [x] **VIS-02**: Když běží fullscreen aplikace (hra, video), widget se automaticky skryje a vrátí se po opuštění fullscreenu

### Data

- [x] **DATA-01**: Čte rate limit data z `api.anthropic.com/v1/messages` response headers (`anthropic-ratelimit-unified-5h-utilization`, `anthropic-ratelimit-unified-7d-utilization`, reset timestamps)
- [x] **DATA-02**: Používá existující `ClaudeApiClient.cs` (OAuth token z `~/.claude/.credentials.json`, inference call)
- [x] **DATA-03**: Data se obnovují každou minutu
- [x] **DATA-04**: Při chybě API zobrazí poslední known hodnotu nebo šedý/chybový stav

### Životní cyklus

- [x] **LIFE-01**: Aplikace běží na pozadí bez okna v taskbaru a bez splash screenu
- [x] **LIFE-02**: Spustitelná jako exe bez MSIX/AppX packaging
- [x] **LIFE-03**: Pravé tlačítko na widget otevře kontextové menu s možností Quit

### Cleanup projektu

- [x] **CLEAN-01**: Odstranit MSIX/AppX packaging konfiguraci z projektu
- [x] **CLEAN-02**: Odstranit Windows Widget provider kód (`WidgetProvider.cs` a závislosti)
- [x] **CLEAN-03**: Zachovat a případně vyčistit `ClaudeApiClient.cs` jako core logiku
- [x] **CLEAN-04**: Výsledný projekt je čisté WPF exe bez balastního kódu

## v2 Requirements

### Rozšíření UI

- **V2-01**: Konfigurační okno — offset pozice, průhlednost
- **V2-02**: Startup s Windows (registry/Task Scheduler)
- **V2-03**: Model-specifické limity (Sonnet vs unified)

## Out of Scope

| Feature | Reason |
|---------|--------|
| Windows Widgets panel integrace | Nahrazujeme — to je původní přístup |
| Click-to-open detail popup | Hover tooltip stačí pro v1 |
| MSIX distribuce | Exe je jednodušší a dostatečné |
| Cross-platform | Windows only widget |

## Traceability

| Requirement | Phase | Status |
|-------------|-------|--------|
| CLEAN-01 | Phase 1 | Complete |
| CLEAN-02 | Phase 1 | Complete |
| CLEAN-03 | Phase 1 | Complete |
| CLEAN-04 | Phase 1 | Complete |
| UI-01 | Phase 2 | Complete |
| UI-02 | Phase 2 | Complete |
| UI-03 | Phase 2 | Complete |
| UI-04 | Phase 2 | Complete |
| UI-05 | Phase 2 | Complete |
| UI-06 | Phase 2 | Complete |
| POS-01 | Phase 2 | Complete |
| POS-02 | Phase 2 | Complete |
| POS-03 | Phase 2 | Complete |
| DATA-01 | Phase 3 | Complete |
| DATA-02 | Phase 3 | Complete |
| DATA-03 | Phase 3 | Complete |
| DATA-04 | Phase 3 | Complete |
| VIS-01 | Phase 3 | Complete |
| VIS-02 | Phase 3 | Complete |
| LIFE-01 | Phase 2 | Complete |
| LIFE-02 | Phase 1 | Complete |
| LIFE-03 | Phase 2 | Complete |

**Coverage:**
- v1 requirements: 21 total
- Mapped to phases: 21
- Unmapped: 0 ✓

---
*Requirements defined: 2026-03-06*
*Last updated: 2026-03-06 after initial definition*
