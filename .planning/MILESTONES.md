# Milestones

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

