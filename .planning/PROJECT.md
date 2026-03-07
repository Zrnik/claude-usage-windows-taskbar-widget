# Claude Usage Widget — Taskbar

## What This Is

C# WPF aplikace zobrazující rate limit usage pro Claude a Codex přímo v taskbaru Windows. Každý unikátní účet (org ID) má vlastní sadu flat progress barů (5h + 7d) s ikonou služby, zobrazené vedle sebe. Hover tooltip zobrazuje sparkline grafy (14 dní) a reset časy. Skrývá se při auto-hide taskbaru a fullscreen aplikacích.

## Core Value

Okamžitě viditelné vytížení Claude limitů přímo v taskbaru — bez klikání, bez otevírání oken.

## Requirements

### Validated

- ✓ Widget se zobrazuje jako borderless okno v taskbaru (vpravo, před tray oblastí) — v0.1.9
- ✓ Widget se dynamicky repositionuje když se změní šířka system tray oblasti — v0.1.9
- ✓ Dva progress bary nad sebou: 5h session limit + 7d rolling limit — v0.1.9
- ✓ Progress bar mění barvu: zelená (< 75%), oranžová (75–90%), červená (≥ 90%) — v0.1.9
- ✓ V progress baru je zobrazen text s procentem využití — v0.1.9
- ✓ Při hoveru se zobrazí čas do resetu — v0.1.9
- ✓ Data se obnovují každou minutu (API call na anthropic) — v0.1.9
- ✓ ClaudeApiClient.cs (inference call + rate limit headers) znovupoužit — v0.1.9
- ✓ Auto-hide taskbar: widget se skryje/zobrazí společně s taskbarem — v0.1.9
- ✓ Fullscreen aplikace: widget se skryje na dotčeném monitoru — v0.1.9
- ✓ Error stav: maroon bary + "Error" text při výpadku API — v0.1.9
- ✓ Multi-account detekce: Claude (Windows + WSL deduplikováno dle org ID) + Codex — v0.1.10
- ✓ Ikona služby (Claude / Codex) u každé sady progress barů — v0.1.10
- ✓ Více sad barů vedle sebe — jedna sada na unikátní účet — v0.1.10
- ✓ Dynamická šířka widgetu podle počtu účtů (vejde se mezi tray a taskbar ikony) — v0.1.10
- ✓ Widget povoluje pouze jednu běžící instanci — nové spuštění zabije předchozí — v0.1.11
- ✓ Token expiry recovery s disk re-read credentials, JWT-based dedup (org_id/sub) — v0.1.11
- ✓ Widget ukládá utilization hodnoty do AppData při každém API callu (hourly bucket) — v0.1.11
- ✓ Tooltip zobrazuje historický usage graf (14 dnů, 5h + 7d utilization jako dva sparkline grafy) — v0.1.11
- ✓ Tooltip má širší layout (280px): reset čas vlevo, reset datum vpravo — v0.1.11

### Active

- [ ] Možnost přepínat časové okno v tooltipu (24h / 7d / 14d)
- [ ] Annotace v grafu (reset události, error stavy)
- [ ] Notifikace při přiblížení limitu

### Out of Scope

- Windows Widgets panel integrace — nahrazeno borderless window přístupem
- Kliknutím otevřít detail popup — hover tooltip dostatečný
- Model-specifické limity (Sonnet vs. unified) — unified limity z API headers jsou správné
- MSIX distribuce — exe je jednodušší a dostatečné
- Cross-platform — Windows only
- Real-time graf (< 1min refresh) — API rate limit, polling každou minutu dostatečný
- Export history dat — out of core value scope

## Context

**Shipped v0.1.11** — 2311 LOC C#/XAML, 8 fází celkem.

Tech stack: C# (.NET), WPF, Win32 P/Invoke (SHAppBarMessage, FindWindow, GetWindowRect).

Credentials: čte `~/.claude/.credentials.json` a `~/.codex/auth.json` z disku při každém API callu (Windows + WSL cesty). OAuth token jako `x-api-key` na `api.anthropic.com/v1/messages`. Codex používá stejný endpoint s jiným tokenem.

Multi-account: LoadAllAccounts() v CredentialStore.cs agreguje všechny accounts, deduplikuje dle JWT org_id/sub (GetAccountKey()), vrací List<AccountInfo>. Každý AccountInfo má ServiceType (Claude/Codex) a credentials. Per-account ClaudeApiClient(AccountInfo) s `_noReload` flag.

UI: AccountPanel UserControl — ikona (20x20 PNG) + 2 progress bary. MainWindow = horizontální StackPanel, Width = N×170px nastaven před Show(). PopupWindow 280px = reset Grid (countdown/datum) + 2× HistoryChart UserControl.

History: UsageHistoryStore singleton, AppData JSON s hourly-bucket upsert, max 336 záznamů/účet (14d), atomický zápis (tmp + File.Move).

Pozicování: Win32 SHAppBarMessage pro taskbar bounds + FindWindowEx pro tray šířku. DispatcherTimer 2s pro tray watch, 500ms pro visibility check.

## Constraints

- **OS**: Windows 11 — Windows API pro detekci taskbar pozice (AppBar API / `SHAppBarMessage`)
- **Tech stack**: C# (.NET) + WPF — zachovat pro konzistenci
- **Pozice**: Vpravo v taskbaru, dynamicky před tray oblastí — musí sledovat změny tray

## Key Decisions

| Decision | Rationale | Outcome |
|----------|-----------|---------|
| Borderless window nad taskbarem | Windows nemá veřejné API pro vložení custom UI přímo do taskbaru — borderless window je standardní přístup třetích stran | ✓ Funguje, vizuálně splyne s taskbarem |
| WPF místo WinForms | Lepší custom rendering pro flat progress bary (ControlTemplate) | ✓ Správná volba |
| Polling každou minutu | Nízká zátěž API, dostatečná frekvence pro usage tracking | ✓ Funguje |
| Colors.Maroon pro error stav | Dostatečně odlišná od červené (≥90%), jednoznačně signalizuje chybu | ✓ Dobrá volba |
| _lastUsage = null v ShowErrorState() | Eliminuje zobrazení stale dat po selhání API | ✓ Bug odstraněn |
| Credentials z disku při každém callu | Zabraňuje stale token bugu po rotaci credentials | ✓ Robustní |
| IsFullscreenOnMyMonitor porovnává rcMonitor | Fyzické rozměry monitoru, ne pracovní plocha — správné pro fullscreen detekci | ✓ Korektní |
| AccountInfo record + ServiceType enum | Čistý datový kontrakt pro multi-account; ServiceType řídí typ ikony i API endpoint | ✓ Čistá architektura |
| _noReload flag v ClaudeApiClient | Per-account instance nesmí přepisovat credentials z disku — flag blokuje reload | ✓ Bug odstraněn |
| Width = N×170px před Show() | PositionWindow() musí znát Width před voláním — pořadí inicializace | ✓ Správné pořadí |
| PNG ikony generované PowerShell System.Drawing | Jednodušší než XAML geometrie, přenositelné jako embedded Resource | ✓ Funguje |
| Mutex field na App třídě (ne lokální var) | GC sbírá lokální Mutex v Release buildu — field je GC-safe | ✓ Single instance funguje |
| Local\ClaudeUsageWidget Mutex prefix | Widget je per-session, Global\ prefix zbytečný | ✓ Správné |
| GetAccountKey() vrací null pro nevalidní JWT | Credentials bez org_id/sub jsou tiše přeskočeny — bezpečnější než fallback | ✓ Dedup funguje |
| Hourly bucket upsert (ne per-minute) | Max 336 záznamů/účet (14d×24h) — přijatelná paměťová zátěž | ✓ Efektivní |
| Atomic write: tmp + File.Move(overwrite) | Crash při zápisu nezkorumpuje history soubor | ✓ Crash-safe |
| WPF Canvas + Polyline pro sparkline | Built-in WPF, žádný NuGet, multicolor segmenty přes více Polyline objektů | ✓ Funguje |
| PadToLength na 336 bodů zleva | Konzistentní X-škálování bez ohledu na počet skutečných dat | ✓ Správné |
| accountKey optional parametr v UpdateAndShow | Zpětná kompatibilita bez breaking change při přidání history | ✓ Čisté |

---
*Last updated: 2026-03-07 after v0.1.11 milestone*
