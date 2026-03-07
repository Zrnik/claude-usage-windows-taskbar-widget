# Milestones

## v0.1.11 Usage History (Shipped: 2026-03-07)

**Phases completed:** 3 phases, 5 plans
**Git range:** feat(06-01) → fix: Claude účet se nezobrazoval a spinner zasekával data
**LOC:** 2311 C# total

**Key accomplishments:**
- Single instance enforcement — Mutex field na App třídě zabraňuje duplicitním widgetům, nové spuštění vždy zabíjí předchozí instanci
- Token 401 recovery — disk re-read credentials po vypršení tokenu, JWT-based dedup účtů (org_id/sub), oprava spinner textu
- UsageHistoryStore — hourly-bucket upsert do AppData JSON, atomický zápis (tmp + File.Move), 14d rolling retention (max 336 záznamů/účet)
- HistoryChart UserControl — WPF Canvas sparkline s multicolor segmenty (zelená/oranžová/červená) a fill polygony, PadToLength na 336 bodů
- PopupWindow 280px redesign — reset Grid (countdown vlevo, datum vpravo), HistoryChart embed pod každý limit (5h + 7d)

---

## v0.1.10 Multi-account (Shipped: 2026-03-07)

**Phases completed:** 2 phases, 5 plans
**Git range:** feat(04-01) → feat: Codex usage support + per-panel tooltip
**LOC:** ~2500 C#/XAML total

**Key accomplishments:**
- ServiceType enum + AccountInfo record — datové kontrakty pro multi-account architekturu
- LoadAllAccounts() agreguje Claude (Windows + WSL, deduplikováno dle org ID) + Codex credentials
- Per-account ClaudeApiClient(AccountInfo) s `_noReload` flag — každý účet má vlastní API client
- Embedded PNG ikony (Claude oranžový C, Codex modrý X) jako WPF Resource (20x20px)
- AccountPanel UserControl — ikona + 2 progress bary (5h + 7d) v jednom sloupci
- MainWindow přepsán na horizontální StackPanel, šířka = N×170px, per-panel tooltip s reset časy

---

## v0.1.9 MVP (Shipped: 2026-03-07)

**Phases completed:** 3 phases, 7 plans, 0 tasks

**Key accomplishments:**
- (none recorded)

---

