# Requirements: Claude Usage Widget

**Defined:** 2026-03-07
**Core Value:** Okamžitě viditelné vytížení Claude limitů přímo v taskbaru — bez klikání, bez otevírání oken.

## v0.1.11 Requirements

### Stability

- [x] **STAB-01**: Widget povoluje pouze jednu instanci — nové spuštění zabije předchozí a nastartuje se samo
- [x] **STAB-02**: Po 401 widget přečte credentials z disku — pokud se liší od aktuálních, použije nové okamžitě; pokud jsou stejné, přejde do error stavu. Žádné zaseknutí.
- [x] **STAB-03**: Accounts se deduplikují: Claude = hash org ID z JWT, Codex = hash `account_id`; credentials bez zjistitelného klíče jsou tiše přeskočeny
- [x] **STAB-04**: Text v progress baru se nezasekává na lomítku

### History Persistence

- [x] **HIST-01**: Widget ukládá utilization hodnoty do AppData JSON při každém API callu (hourly bucket — upsert)
- [x] **HIST-02**: History soubor pojmenován klíčem účtu (dle STAB-03)
- [x] **HIST-03**: JSON se zapisuje atomicky (tmp soubor + File.Move)
- [x] **HIST-04**: Automaticky se ořezává na 14 dní (~336 záznamů na účet)

### Tooltip & Chart

- [ ] **TOOL-01**: PopupWindow je širší (170→280px), reset čas vlevo, reset datum vpravo
- [ ] **TOOL-02**: Tooltip zobrazuje dva sparkline grafy (5h + 7d utilization) pod reset řádkem
- [ ] **TOOL-03**: Grafy ukazují 14denní rolling okno; data downsamplována pro plynulý render

## Budoucí Requirements

### Rozšíření grafu

- **CHART-01**: Možnost přepínat časové okno v tooltipu (24h / 7d / 14d)
- **CHART-02**: Annotace v grafu (reset události, error stavy)

## Out of Scope

| Feature | Reason |
|---------|--------|
| Klik → detail okno | Hover tooltip dostatečný, klik přidává complexity |
| Real-time graf (< 1min refresh) | API rate limit, polling každou minutu je dostatečné |
| Export history dat | Out of core value scope |
| Notifikace při přiblížení limitu | Možné v budoucím milestonu |

## Traceability

| Requirement | Phase | Status |
|-------------|-------|--------|
| STAB-01 | Phase 6 | Complete |
| STAB-02 | Phase 6 | Complete |
| STAB-03 | Phase 6 | Complete |
| STAB-04 | Phase 6 | Complete |
| HIST-01 | Phase 7 | Complete |
| HIST-02 | Phase 7 | Complete |
| HIST-03 | Phase 7 | Complete |
| HIST-04 | Phase 7 | Complete |
| TOOL-01 | Phase 8 | Pending |
| TOOL-02 | Phase 8 | Pending |
| TOOL-03 | Phase 8 | Pending |

**Coverage:**
- v0.1.11 requirements: 11 total
- Mapped to phases: 11
- Unmapped: 0 ✓

---
*Requirements defined: 2026-03-07*
*Last updated: 2026-03-07 after initial definition*
