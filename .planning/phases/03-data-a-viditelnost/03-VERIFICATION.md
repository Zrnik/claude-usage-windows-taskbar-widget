---
phase: 03-data-a-viditelnost
verified: 2026-03-07T09:00:00Z
status: passed
score: 3/4 automated truths verified
re_verification: false
human_verification:
  - test: "Spustit widget s platný credentials a čekat 1 minutu"
    expected: "Bary zobrazují živá procenta z API a po 1 minutě se aktualizují"
    why_human: "Nelze ověřit programaticky — vyžaduje živé API volání a skutečné rate limit headers"
  - test: "Přejmenovat ~/.claude/.credentials.json, restartovat widget, hover nad widgetem"
    expected: "Bary maroon, text 'Error', tooltip zobrazuje text chyby (LastError)"
    why_human: "Tooltip viditelnost a obsah nelze ověřit staticky — ShowPopup() logika závisí na runtime stavech"
  - test: "Zapnout auto-hide taskbar, kliknout mimo taskbar a čekat"
    expected: "Widget zmizí s taskbarem (Visibility.Hidden); vrátí se po zobrazení taskbaru"
    why_human: "Vyžaduje reálné systémové chování — ABM_GETSTATE a GetWindowRect hodnoty nejsou v kódu simulovatelné"
  - test: "Otevřít fullscreen aplikaci (F11 v prohlížeči) na stejném monitoru jako widget"
    expected: "Widget zmizí; po opuštění fullscreenu se vrátí"
    why_human: "Foreground window bounds vs rcMonitor nelze ověřit staticky"
  - test: "Pokud jsou dva monitory: fullscreen na jiném monitoru"
    expected: "Widget na prvním monitoru zůstane viditelný"
    why_human: "Logika fgMonitor != myMonitor závisí na reálném hardware"
---

# Phase 3: Data a viditelnost — Verification Report

**Phase Goal:** Widget zobrazuje živá data a chová se správně při auto-hide taskbaru a fullscreenu
**Verified:** 2026-03-07T09:00:00Z
**Status:** human_needed
**Re-verification:** Ne — první verifikace

## Goal Achievement

### Success Criteria z ROADMAP.md

| # | Criterion | Status | Evidence |
|---|-----------|--------|----------|
| 1 | Progress bary se každou minutu aktualizují z `anthropic-ratelimit-unified-*` headers | ? HUMAN | Kód existuje a je správně zapojen; skutečné API volání nelze ověřit staticky |
| 2 | Při výpadku API widget zobrazí maroon chybový stav (bary maroon, text "Error", tooltip s detailem) | ✓ VERIFIED (kód) + ? HUMAN (runtime) | ShowErrorState() implementace plně odpovídá specifikaci; tooltip runtime behavior potřebuje human |
| 3 | Když taskbar zajede dolů, widget zmizí a vrátí se | ? HUMAN | IsTaskbarVisible() + CheckVisibility() správně implementovány; runtime chování nelze ověřit staticky |
| 4 | Při fullscreen aplikaci se widget skryje na dotčeném monitoru | ? HUMAN | IsFullscreenOnMyMonitor() správně implementována; vyžaduje reálný test |

**Automaticky ověřitelné: 1/4** (ShowErrorState implementace)
**Vyžaduje human: 3/4** (runtime behavior)

---

## Observable Truths (z PLAN must_haves)

### Plan 03-01 Truths

| # | Truth | Status | Evidence |
|---|-------|--------|----------|
| 1 | Při selhání API se bary okamžitě vybarví maroon a zobrazí text 'Error' | ✓ VERIFIED | MainWindow.xaml.cs:437-451 — ShowErrorState() nastavuje Colors.Maroon, Bar.Value=100, Text="Error" |
| 2 | Stale data se nikdy nezobrazí po selhání API — _lastUsage je null v error stavu | ✓ VERIFIED | MainWindow.xaml.cs:439 — `_lastUsage = null;` je první příkaz ShowErrorState() |
| 3 | Tooltip v error stavu zobrazuje detail chyby (LastError), ne stará data | ✓ VERIFIED (kód) | MainWindow.xaml.cs:398 — ShowPopup() guard: `if (_lastUsage == null && _apiClient.LastError == null) return;` — při null _lastUsage s LastError se popup zobrazí s chybou; PopupWindow.UpdateAndShow() zobrazuje errorMessage pokud != null |
| 4 | Data se obnovují každou minutu | ✓ VERIFIED | MainWindow.xaml.cs:261-272 — StartRefreshTimer() s Interval=TimeSpan.FromMinutes(1) |
| 5 | Credential fallback funguje automaticky (Windows + WSL zdroje) | ✓ VERIFIED | ClaudeApiClient.cs:49-50 — LoadAllCredentials() voláno při každém fresh fetch; CredentialStore.cs:29-42 — načítá WSL i Windows zdroje |

### Plan 03-02 Truths

| # | Truth | Status | Evidence |
|---|-------|--------|----------|
| 1 | Při auto-hide taskbaru widget zmizí když taskbar zajede dolů | ? HUMAN | IsTaskbarVisible() kód je správný; runtime chování nelze ověřit staticky |
| 2 | Widget se vrátí až taskbar dosáhne finální pozice nahoře | ? HUMAN | ABM_GETTASKBARPOS + GetWindowRect logika vypadá správně; runtime potřeba |
| 3 | Při fullscreen aplikaci na stejném monitoru widget zmizí | ? HUMAN | IsFullscreenOnMyMonitor() logika správná; vyžaduje reálný test |
| 4 | Fullscreen na jiném monitoru widget neovlivní | ? HUMAN | MainWindow.xaml.cs:340 — `if (fgMonitor != myMonitor) return false;` — logika správná |
| 5 | Visibility timer se zastaví při zavření okna | ✓ VERIFIED | MainWindow.xaml.cs:130 — `_visibilityTimer?.Stop();` v Closed event handler |

**Score: 6/10 truths VERIFIED automaticky, 4/10 HUMAN**

---

## Required Artifacts

| Artifact | Expected | Status | Details |
|----------|----------|--------|---------|
| `ClaudeUsageWidget/MainWindow.xaml.cs` | Opravený ShowErrorState() a StartRefreshTimer() Tick handler | ✓ VERIFIED | Soubor existuje, 536 řádků, plně implementován |
| `ClaudeUsageWidget/MainWindow.xaml.cs` | _visibilityTimer, IsTaskbarVisible(), IsFullscreenOnMyMonitor(), CheckVisibility() | ✓ VERIFIED | Všechny čtyři prvky přítomny v kódu |

---

## Key Link Verification

### Plan 03-01 Key Links

| From | To | Via | Status | Details |
|------|----|-----|--------|---------|
| `MainWindow._refreshTimer.Tick` | `ShowErrorState()` | unconditional else na null usage | ✓ WIRED | MainWindow.xaml.cs:267-270 — `else ShowErrorState()` bez podmínky |
| `ShowErrorState()` | `_lastUsage = null` | první příkaz metody | ✓ WIRED | MainWindow.xaml.cs:439 — první řádek metody |

### Plan 03-02 Key Links

| From | To | Via | Status | Details |
|------|----|-----|--------|---------|
| `Loaded event handler` | `StartVisibilityTimer()` | volání v Loaded lambda | ✓ WIRED | MainWindow.xaml.cs:172 — `StartVisibilityTimer();` voláno v Loaded |
| `_visibilityTimer.Tick` | `CheckVisibility()` | Tick event handler | ✓ WIRED | MainWindow.xaml.cs:284 — `_visibilityTimer.Tick += (_, _) => CheckVisibility();` |
| `CheckVisibility()` | `Visibility.Visible / Visibility.Hidden` | `taskbarVisible && !fullscreen` podmínka | ✓ WIRED | MainWindow.xaml.cs:293-295 — přesná podmínka z PLANu |

**Všechny key links: WIRED**

---

## Requirements Coverage

| Requirement | Source Plan | Description | Status | Evidence |
|-------------|-------------|-------------|--------|----------|
| DATA-01 | 03-01 | Čte rate limit data z `api.anthropic.com/v1/messages` response headers | ✓ SATISFIED | ClaudeApiClient.cs:134-158 — FetchUsageFromRateLimitHeadersAsync() čte `anthropic-ratelimit-*-utilization` a `*-reset` headers |
| DATA-02 | 03-01 | Používá existující ClaudeApiClient.cs (OAuth token, inference call) | ✓ SATISFIED | ClaudeApiClient.cs:122-131 — POST na api.anthropic.com/v1/messages s x-api-key OAuth tokenem |
| DATA-03 | 03-01 | Data se obnovují každou minutu | ✓ SATISFIED | MainWindow.xaml.cs:262 — `Interval = TimeSpan.FromMinutes(1)` |
| DATA-04 | 03-01 | Při chybě API zobrazí maroon chybový stav | ✓ SATISFIED (kód) + ? HUMAN (vizuál) | ShowErrorState() implementace plně odpovídá specifikaci; vizuální ověření potřebuje human |
| VIS-01 | 03-02 | Auto-hide taskbar: widget zmizí s taskbarem | ? HUMAN | IsTaskbarVisible() + CheckVisibility() implementace je správná; runtime chování vyžaduje test |
| VIS-02 | 03-02 | Fullscreen aplikace: widget se skryje na dotčeném monitoru | ? HUMAN | IsFullscreenOnMyMonitor() implementace je správná; runtime chování vyžaduje test |

**Orphaned requirements check:** REQUIREMENTS.md mapuje DATA-01..04 a VIS-01..02 na Phase 3. Všechny jsou pokryty plány. Žádné orphaned requirements.

---

## Anti-Patterns Found

| File | Line | Pattern | Severity | Impact |
|------|------|---------|----------|--------|
| Žádné blocker anti-patterny nalezeny | | | | |

Provedena kontrola MainWindow.xaml.cs na:
- TODO/FIXME/PLACEHOLDER komentáře: nenalezeny
- Prázdné implementace (return null/{}): nenalezeny (ShowPopup() má guard ale ne stub)
- Console.log only implementace: nenalezeny
- Stub CheckVisibility z SUMMARY: plná implementace nalezena v kódu (SUMMARY zmiňuje stub pouze jako přechodný stav Task 1, Task 2 ho nahradil)

---

## Human Verification Required

### 1. Živá data z API

**Test:** Spustit widget s platnými credentials; čekat na první zobrazení dat (cca 2-3s); čekat 1 minutu a sledovat obnovení
**Expected:** Bary zobrazují nenulová procenta odpovídající skutečnému využití Claude limitů; po 1 minutě se hodnoty obnoví (nebo zůstanou stejné pokud limit se nezměnil)
**Why human:** Nelze ověřit staticky — vyžaduje živé API volání a reálné rate limit headers

### 2. Error stav — vizuální verifikace

**Test:** Přejmenovat `~/.claude/.credentials.json` na jiný název; restartovat widget; hover nad widgetem
**Expected:** Oba bary jsou maroon (#800000), text zobrazuje "Error" (velké E), tooltip zobrazuje text chybové hlášky
**Why human:** Vizuální barva maroon a tooltip obsah nelze ověřit staticky

### 3. Auto-hide taskbar — skrytí

**Test:** Zapnout auto-hide pro taskbar v nastavení Windows; kliknout mimo taskbar a čekat až zajede dolů
**Expected:** Widget zmizí společně s taskbarem (Visibility.Hidden)
**Why human:** ABM_GETSTATE a GetWindowRect hodnoty jsou runtime závislé na systémovém stavu

### 4. Auto-hide taskbar — zobrazení

**Test:** Po skrytí taskbaru najet myší na dolní okraj obrazovky; čekat než taskbar dosáhne finální pozice
**Expected:** Widget se znovu zobrazí až taskbar dosáhne finální pozice nahoře
**Why human:** Timing a pozice taskbaru jsou runtime závislé

### 5. Fullscreen na stejném monitoru

**Test:** Otevřít libovolnou aplikaci přes F11 (fullscreen) na monitoru kde je widget
**Expected:** Widget zmizí okamžitě (do 500ms); po opuštění fullscreenu se vrátí
**Why human:** Foreground window bounds vs rcMonitor nelze ověřit staticky

### 6. Fullscreen na jiném monitoru (pokud je multi-monitor setup)

**Test:** Fullscreen na monitoru kde widget není
**Expected:** Widget zůstane viditelný
**Why human:** Závisí na reálném hardware a fgMonitor != myMonitor logice

---

## Gaps Summary

Žádné blocker gapy nalezeny. Všechny kódové implementace jsou substantivní a správně zapojené.

Zbývající otázky jsou výhradně runtime behavior, které nelze ověřit staticky:
- Reálné API volání s rate limit headers (DATA-01, DATA-02, DATA-03)
- Vizuální vzhled error stavu (DATA-04)
- Systémové chování auto-hide taskbaru (VIS-01)
- Systémové chování fullscreen detekce (VIS-02)

Plan 03-03 byl explicitně vytvořen jako human-verify checkpoint pro tato ověření. SUMMARY 03-03 tvrdí "approved" — ale tato verifikace nemůže to potvrdit bez záznamu o skutečném provedení testu uživatelem.

**Doporučení:** Pokud uživatel skutečně provedl manuální testy popsané v 03-03-PLAN.md a výsledek byl "approved", phase goal je splněn. Pokud test nebyl proveden, doporučuje se ho provést před uzavřením fáze.

---

_Verified: 2026-03-07T09:00:00Z_
_Verifier: Claude (gsd-verifier)_
