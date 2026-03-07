# Requirements: Claude Usage Widget — Taskbar

**Defined:** 2026-03-07
**Core Value:** Okamžitě viditelné vytížení Claude a Codex limitů přímo v taskbaru — bez klikání, bez otevírání oken.

## v1 Requirements

### Multi-account detekce

- [x] **MULTI-01**: Widget načte Claude credentials z Windows (`%USERPROFILE%\.claude\.credentials.json`) a WSL (`~/.claude/.credentials.json`) a deduplikuje účty podle org ID — stejné org ID = jedna sada barů
- [x] **MULTI-02**: Widget načte Codex credentials z `~/.codex/auth.json` (Windows i WSL)
- [x] **MULTI-03**: Každý unikátní účet (org ID) generuje vlastní sadu progress barů s vlastními API daty

### UI — ikony a layout

- [ ] **UI-07**: Každá sada progress barů zobrazuje ikonu příslušné služby (Claude logo / Codex logo) vlevo od barů
- [ ] **UI-08**: Více sad barů se zobrazuje horizontálně vedle sebe (každý účet = jeden sloupec)
- [ ] **UI-09**: Šířka widgetu se dynamicky přizpůsobuje počtu účtů tak, aby se widget vždy vešel mezi system tray a ikony vlevo v taskbaru

## v2 Requirements

- **CFG-01**: Možnost skrýt/zobrazit konkrétní účet přes context menu
- **CFG-02**: Vlastní pořadí účtů (drag or config)

## Out of Scope

| Feature | Reason |
|---------|--------|
| Windows Widgets panel integrace | Nahrazeno borderless window přístupem |
| Model-specifické limity | Unified limity z API headers jsou správné |
| MSIX distribuce | Exe je jednodušší |
| Cross-platform | Windows only |
| Přihlášení přímo z widgetu | Credentials spravuje Claude CLI / Codex CLI |

## Traceability

| Requirement | Phase | Status |
|-------------|-------|--------|
| MULTI-01 | Phase 4 | Complete |
| MULTI-02 | Phase 4 | Complete |
| MULTI-03 | Phase 4 | Complete |
| UI-07 | Phase 5 | Pending |
| UI-08 | Phase 5 | Pending |
| UI-09 | Phase 5 | Pending |

**Coverage:**
- v1 requirements: 6 total
- Mapped to phases: 6
- Unmapped: 0 ✓

---
*Requirements defined: 2026-03-07*
