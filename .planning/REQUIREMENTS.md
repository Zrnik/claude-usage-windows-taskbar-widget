# Requirements: Claude Usage Widget

**Defined:** 2026-03-11
**Core Value:** Okamžitě viditelné vytížení Claude limitů přímo v taskbaru — bez klikání, bez otevírání oken.

## v0.1.12 Requirements

Requirements for Chart Windows milestone. Each maps to roadmap phases.

### Chart

- [x] **CHART-01**: Graf zobrazuje osu X jako reálný čas s per-key časovým oknem (ne fixní počet bodů)
- [x] **CHART-02**: Každý rate limit klíč má vlastní defaultní časové okno: 5H=2d, 7D=14d, SESSION/100H=14d, REVIEW=7d
- [x] **CHART-03**: Mezery v datech < 2h se lineárně interpolují; mezery ≥ 2h klesnou na 0

### Settings

- [ ] **SETT-01**: Uživatel může v Settings okně změnit časové okno pro každý rate limit klíč
- [ ] **SETT-02**: Nastavení se persistuje do AppData (přežije restart)
- [ ] **SETT-03**: Změna nastavení okamžitě překreslí graf (bez restartu)

### Tech Debt

- [x] **DEBT-01**: ExtractAccountKey() v ClaudeApiClient refaktorován — používá CredentialStore.GetAccountKey() místo duplikované logiky

## Future Requirements

### Deferred

- **ANNO-01**: Annotace v grafu (reset události, error stavy)
- **NOTF-01**: Notifikace při přiblížení limitu

## Out of Scope

| Feature | Reason |
|---------|--------|
| Přepínání časového okna v tooltipu (24h/7d/14d toggle) | Nahrazeno per-key nastavením v Settings |
| Real-time graf (< 1min refresh) | API rate limit, polling každou minutu dostatečný |
| Export history dat | Out of core value scope |

## Traceability

| Requirement | Phase | Status |
|-------------|-------|--------|
| CHART-01 | Phase 9 | Complete |
| CHART-03 | Phase 9 | Complete |
| DEBT-01 | Phase 9 | Complete |
| CHART-02 | Phase 10 | Complete |
| SETT-01 | Phase 11 | Pending |
| SETT-02 | Phase 11 | Pending |
| SETT-03 | Phase 11 | Pending |

**Coverage:**
- v0.1.12 requirements: 7 total
- Mapped to phases: 7
- Unmapped: 0 ✓

---
*Requirements defined: 2026-03-11*
*Last updated: 2026-03-11 after roadmap creation*
