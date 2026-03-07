# Claude Usage Widget — Taskbar

## What This Is

C# WPF aplikace zobrazující rate limit usage pro Claude a Codex přímo v taskbaru Windows. Každý unikátní účet (org ID) má vlastní sadu flat progress barů (5h + 7d) s ikonou služby, zobrazené vedle sebe. Skrývá se při auto-hide taskbaru a fullscreen aplikacích.

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

### Active

_(prázdné — definováno v dalším milestone)_

### Out of Scope

- Windows Widgets panel integrace — nahrazeno borderless window přístupem
- Kliknutím otevřít detail popup — hover tooltip dostatečný
- Model-specifické limity (Sonnet vs. unified) — unified limity z API headers jsou správné
- MSIX distribuce — exe je jednodušší a dostatečné
- Cross-platform — Windows only

## Context

**Shipped v0.1.10** — funkční WPF widget s multi-account podporou, ~2500 LOC C#/XAML.

Tech stack: C# (.NET), WPF, Win32 P/Invoke (SHAppBarMessage, FindWindow, GetWindowRect).

Credentials: čte `~/.claude/.credentials.json` a `~/.codex/auth.json` z disku při každém API callu (Windows + WSL cesty). OAuth token jako `x-api-key` na `api.anthropic.com/v1/messages`. Codex používá stejný endpoint s jiným tokenem.

Multi-account: LoadAllAccounts() v CredentialStore.cs agreguje všechny accounts, deduplikuje dle org ID (HashSet), vrací List<AccountInfo>. Každý AccountInfo má ServiceType (Claude/Codex) a credentials. Per-account ClaudeApiClient(AccountInfo) s `_noReload` flag.

UI: AccountPanel UserControl — ikona (20x20 PNG) + 2 progress bary. MainWindow = horizontální StackPanel, Width = N×170px nastaven před Show().

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

---
*Last updated: 2026-03-07 after v0.1.10 milestone*
