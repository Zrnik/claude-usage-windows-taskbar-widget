# Project Retrospective

*A living document updated after each milestone. Lessons feed forward into future planning.*

## Milestone: v0.1.9 — MVP

**Shipped:** 2026-03-07
**Phases:** 3 | **Plans:** 7 | **Sessions:** ~3

### What Was Built
- WPF borderless widget 48px výšky s dvěma flat progress bary (5h + 7d Claude limity)
- Win32 P/Invoke pozicování těsně vlevo od system tray, dynamické sledování tray šířky
- ClaudeApiClient integrace — OAuth token, inference call, rate limit headers
- Error stav: maroon bary + "Error" text + null ochrana před stale daty
- Visibility timer 500ms pro auto-hide taskbar a fullscreen detekci
- Tooltip s reset-time, context menu Quit

### What Worked
- Postupné fáze (cleanup → UI → data) — každá fáze měla jasný goal bez scope creep
- Win32 P/Invoke přístup fungoval bez problémů pro taskbar positioning
- Credentials načítané při každém API callu — jednoduchá a robustní ochrana před stale token bugem
- GSD workflow dobře vedl k atomickým commitům a jasným SUMMARY.md

### What Was Inefficient
- Phase 02 skončila bez VERIFICATION.md — tech debt zaznamenán v auditu
- Cleanup fáze (Phase 1) mohla být součástí Phase 2 — samostatná fáze pro 1 plán je overhead

### Patterns Established
- `Colors.Maroon` pro error stav — dostatečně odlišná od červené ≥90%
- `_lastUsage = null` jako první příkaz v ShowErrorState() — pattern pro čistý error stav
- Unconditional error v refresh timeru (ne podmíněný) — eliminuje celou třídu stale data bugů
- Stub implementace v Task 1 pro green build, plná implementace v Task 2 — bezpečné postupné budování

### Key Lessons
1. Win32 P/Invoke pro taskbar positioning je přímočarý — SHAppBarMessage + FindWindowEx stačí
2. WPF ControlTemplate pro flat ProgressBar vyžaduje explicitní `Bar.Value=100` v error stavu pro viditelnost PART_Indicator
3. `PresentationSource.FromVisual` vyžaduje okno v DOM — proto Loaded event handler, ne konstruktor

### Cost Observations
- Model mix: ~100% sonnet (žádné opus/haiku)
- Sessions: ~3 krátké sessions
- Notable: Malý projekt, rychlá exekuce — GSD overhead byl relativně velký, ale plán-fáze byl dobře strukturovaný

---

## Milestone: v0.1.10 — Multi-account

**Shipped:** 2026-03-07
**Phases:** 2 (4-5) | **Plans:** 5 | **Sessions:** ~2

### What Was Built
- ServiceType enum + AccountInfo record — čistý datový kontrakt pro multi-account architekturu
- CredentialStore.LoadAllAccounts() — agreguje Claude (Windows + WSL, dedup dle org ID) + Codex credentials
- Per-account ClaudeApiClient(AccountInfo) s `_noReload` flag — každý účet má vlastní, izolovaný API client
- AccountPanel UserControl — ikona (20x20 PNG) + 2 progress bary v jednom sloupci
- Embedded PNG ikony generované PowerShell System.Drawing (oranžový C / modrý X)
- MainWindow horizontální StackPanel, Width = N×170px, per-panel tooltip s reset časy

### What Worked
- Stub-first approach (04-01 jen kontrakty, 04-02 client, 04-03 OnStartup) — bezpečné postupné budování
- Per-account ClaudeApiClient pattern byl čistý — `_noReload` flag elegantně izoloval per-account instanci od credential reload logiky
- PowerShell System.Drawing pro generování PNG ikon byl překvapivě rychlý a jednoduchý
- AccountPanel jako UserControl správná granularita — snadná kompozice v MainWindow

### What Was Inefficient
- Phase 5 měla `[ ]` checkboxy v ROADMAP.md i po dokončení — tracking bug v roadmap (SUMMARY.md správně existovaly)
- Codex API endpoint discovery (v Phase 4 research) — dokumentace je sparse, muselo se testovat

### Patterns Established
- `_noReload` bool flag pattern pro "fixed credential" klienty — blokuje lazy reload pro per-account instance
- `AccountInfo` jako data record (ne class) — hodnota + immutabilita pro credentials
- Width kalkulace před Show() — pořadí inicializace musí respektovat PositionWindow() závislost na Width
- `Build Action = Resource` (ne EmbeddedResource) pro Pack URI přístup v XAML

### Key Lessons
1. C# negeneruje bezparametrový konstruktor pokud existuje jiný konstruktor — explicitní přidání nutné pro zpětnou kompatibilitu
2. WPF Pack URI `pack://application:,,,/Assets/file.png` vyžaduje `Build Action = Resource` (ne EmbeddedResource ani Content)
3. Deduplikace dle `service:token[..32]` prefix key robustnější než jen org ID — funguje i pro tokeny bez org ID (Codex)

### Cost Observations
- Model mix: ~100% sonnet
- Sessions: ~2 sessions (1 den)
- Notable: Kratší milestone než MVP — jasně definované phases s existující architekturou jako základem

---

## Cross-Milestone Trends

### Process Evolution

| Milestone | Sessions | Phases | Key Change |
|-----------|----------|--------|------------|
| v0.1.9 MVP | ~3 | 3 | First milestone — baseline |
| v0.1.10 Multi-account | ~2 | 2 | Multi-account + Codex support |

### Cumulative Quality

| Milestone | UAT Tests | Passed | Issues |
|-----------|-----------|--------|--------|
| v0.1.9 | 8 (Phase 2) | 8/8 | 0 |
| v0.1.10 | manual verify (Phase 4+5) | passed | 0 |
