# Phase 3: Data a viditelnost - Research

**Researched:** 2026-03-07
**Domain:** WPF Win32 interop — visibility management (auto-hide taskbar, fullscreen detection), live API data, error states
**Confidence:** HIGH

## Summary

Fáze 3 dokončuje widget napojením živých dat a správou viditelnosti. Kód z Phase 2 má většinu Win32 infrastruktury hotovou — `APPBARDATA`, `MONITORINFO`, `MonitorFromWindow`, `GetMonitorInfo`, `ABM_GETTASKBARPOS` jsou importovány a funkční. Chybí jen logika samotné detekce a visibility timer.

Data vrstva (`ClaudeApiClient`, `CredentialStore`) je také hotová a funkční — `LoadAllCredentials()` a `SetCredentials(List<OAuthCredential>)` jsou implementovány. Stávající `_refreshTimer` (1 min) a `ShowErrorState()` existují v `MainWindow`. Fáze 3 rozšiřuje tyto hotové komponenty o chybějící chování.

Klíčové zjištění: `ShowErrorState()` v aktuálním kódu zobrazuje červenou barvu (`#F44336`) a text "error" — rozhodnutí v CONTEXT.md mění barvu na **maroon** a text na **"Error"** (velké E). Tato metoda musí být upravena.

**Primární doporučení:** Jeden nový `DispatcherTimer` (500ms) na fullscreen + auto-hide detekci, rozšíření `ShowErrorState()` o maroon barvu, napojení credential fallbacku při startu (již hotovo v `Program.cs`).

---

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions

**Error stav (DATA-04)**
- Bary se vybarví **maroon** barvou (tmavě červená)
- Uvnitř baru zobrazen text **"Error"** místo procent
- Tooltip při hover obsahuje detail — co se stalo (expired token, network error, timeout)
- Přechod na error stav: okamžitě při selhání API, bez zobrazování stale dat

**Credential fallback**
- Při startu načíst **všechny dostupné credential zdroje** (Windows + WSL) automaticky
- `CredentialStore` vrátí seznam, `ClaudeApiClient.SetCredentials(List<OAuthCredential>)` je zkouší postupně
- Bez interakce uživatele — rotuje přes dostupné credentials dokud nenajde funkční
- Teprve když selžou všechny → error stav

**Fullscreen detekce (VIS-02)**
- Widget se schová **na monitoru kde běží fullscreen aplikace** — jiné monitory nejsou dotčeny
- Detekce pomocí `SHQueryUserNotificationState` nebo porovnáním HWND foreground okna s rozměry monitoru
- Polling každých **500ms**
- Okamžité hide/show, žádné animace

**Auto-hide taskbar (VIS-01)**
- Taskbar zajede dolů → widget **okamžitě zmizí**
- Taskbar vyjede nahoru → widget se zobrazí **až když taskbar dosáhne finální pozice** (sledovat dynamicky Y souřadnici taskbaru, zobrazit až je na místě)
- Polling každých **500ms** — stejný timer jako fullscreen
- Podpora jen základní — kdo má auto-hide, widget se bude chovat intuitivně ale není to priorita

**Přechody viditelnosti**
- Žádné animace — okamžité `Visibility.Visible` / `Visibility.Hidden`
- Oba triggery (auto-hide + fullscreen) sdílí jeden visibility timer (500ms)
- Logika: widget viditelný jen když `taskbarVisible && !fullscreenOnSameMonitor`

**Data refresh (DATA-01, DATA-02, DATA-03)**
- Stávající `_refreshTimer` z Phase 2 — zachovat, frekvence každou minutu
- Stávající `ClaudeApiClient.cs` — zachovat beze změny
- `_lastUsage` používán pro zobrazení — null znamená error stav

### Claude's Discretion
- Konkrétní metoda detekce fullscreen (SHQueryUserNotificationState vs. HWND comparison)
- Jak přesně detekovat "taskbar je na finální pozici" (RECT comparison nebo ABM_GETTASKBARPOS)

### Deferred Ideas (OUT OF SCOPE)
None — diskuze zůstala v rámci Phase 3 scope.
</user_constraints>

---

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|-----------------|
| DATA-01 | Čte rate limit data z `api.anthropic.com/v1/messages` response headers | Hotovo v `ClaudeApiClient.FetchUsageFromRateLimitHeadersAsync()` — žádná změna |
| DATA-02 | Používá existující `ClaudeApiClient.cs` (OAuth token, inference call) | Hotovo — zachovat beze změny |
| DATA-03 | Data se obnovují každou minutu | `_refreshTimer` v `MainWindow` existuje — zachovat |
| DATA-04 | Při chybě API zobrazí chybový stav | `ShowErrorState()` existuje, ale s červenou — změnit na maroon + "Error" text; změnit logiku na okamžitý přechod bez stale dat |
| VIS-01 | Auto-hide taskbar → widget se skryje/zobrazí společně s ním | Nový 500ms timer; `GetWindowRect(_taskbarHwnd)` pro Y souřadnici; `ABM_GETTASKBARPOS` pro offset |
| VIS-02 | Fullscreen aplikace → widget se skryje na stejném monitoru | Nový 500ms timer (sdílený s VIS-01); fullscreen detekce via HWND vs monitor bounds |
</phase_requirements>

---

## Standard Stack

### Core (vše již v projektu)
| Komponenta | Verze | Účel |
|------------|-------|------|
| WPF `DispatcherTimer` | .NET 8 | Periodické UI akce na UI threadu |
| `user32.dll` P/Invoke | Win32 | Window/monitor operace |
| `shell32.dll` P/Invoke | Win32 | AppBar API (taskbar pozice) |

### Nové P/Invoke importy potřebné pro Phase 3

**Pro fullscreen detekci (discretion — HWND comparison):**
```csharp
[DllImport("user32.dll")]
private static extern IntPtr GetForegroundWindow();

[DllImport("user32.dll")]
private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);
// RECT již importován
```

**Pro auto-hide detekci (ABM_GETSTATE):**
```csharp
// ABM_GETTASKBARPOS (0x5) již importován
// Přidat ABM_GETSTATE (0x4) pro zjištění zda taskbar má auto-hide zapnutý
private const uint ABM_GETSTATE = 0x4u;
private const int ABS_AUTOHIDE = 0x1;
```

`MonitorFromWindow` a `GetMonitorInfo` jsou již importovány — reuse pro fullscreen detekci.

## Architecture Patterns

### Doporučená struktura změn

Veškeré změny jsou v `MainWindow.xaml.cs` — žádné nové soubory.

```
MainWindow.xaml.cs
├── Nové pole: DispatcherTimer? _visibilityTimer
├── Nová metoda: StartVisibilityTimer()        ← spustit v Loaded
├── Nová metoda: CheckVisibility()             ← volaná timerem každých 500ms
├── Nová metoda: IsFullscreenOnMyMonitor()     ← fullscreen detekce
├── Nová metoda: IsTaskbarVisible()            ← auto-hide detekce
├── Upravit: ShowErrorState()                  ← maroon + "Error"
└── Upravit: StartRefreshTimer() Tick handler  ← okamžitý error bez stale dat
```

### Pattern 1: Sdílený visibility timer
**Co:** Jeden `DispatcherTimer` (500ms) kontroluje oba triggery a rozhoduje o viditelnosti.
**Kdy použít:** Vždy — oba triggery sdílí stejnou logiku výsledku.

```csharp
// Source: CONTEXT.md locked decision
private DispatcherTimer? _visibilityTimer;

private void StartVisibilityTimer()
{
    _visibilityTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
    _visibilityTimer.Tick += (_, _) => CheckVisibility();
    _visibilityTimer.Start();
}

private void CheckVisibility()
{
    bool taskbarVisible = IsTaskbarVisible();
    bool fullscreen = IsFullscreenOnMyMonitor();
    Visibility = (taskbarVisible && !fullscreen)
        ? Visibility.Visible
        : Visibility.Hidden;
}
```

### Pattern 2: Fullscreen detekce — HWND comparison (doporučeno)

**Co:** Porovnání rozměrů foreground okna s rozměry monitoru. Žádný extra import.
**Proč preferovat před `SHQueryUserNotificationState`:** `SHQUNS` vrací globální stav, ne per-monitor. HWND comparison umožňuje zjistit na kterém monitoru fullscreen běží.

```csharp
private bool IsFullscreenOnMyMonitor()
{
    var foreground = GetForegroundWindow();
    if (foreground == IntPtr.Zero) return false;

    // Zjisti monitor widgetu (taskbaru)
    var myMonitor = MonitorFromWindow(_taskbarHwnd, MONITOR_DEFAULTTONEAREST);
    var mi = new MONITORINFO { cbSize = (uint)Marshal.SizeOf<MONITORINFO>() };
    if (!GetMonitorInfo(myMonitor, ref mi)) return false;

    // Zjisti monitor foreground okna
    var fgMonitor = MonitorFromWindow(foreground, MONITOR_DEFAULTTONEAREST);
    if (fgMonitor != myMonitor) return false; // fullscreen na jiném monitoru — neskrývat

    // Porovnej rozměry foreground okna s fyzickými rozměry monitoru
    if (!GetWindowRect(foreground, out RECT fgRect)) return false;
    var mon = mi.rcMonitor;
    return fgRect.Left <= mon.Left && fgRect.Top <= mon.Top
        && fgRect.Right >= mon.Right && fgRect.Bottom >= mon.Bottom;
}
```

**Poznámka:** Porovnávat s `rcMonitor` (fyzické rozměry), ne `rcWork` (pracovní plocha bez taskbaru).

### Pattern 3: Auto-hide detekce

**Co:** Dvě podmínky — (1) taskbar má auto-hide zapnutý, (2) taskbar je momentálně "dole" (skrytý).

```csharp
private bool IsTaskbarVisible()
{
    // Krok 1: Má taskbar auto-hide vůbec zapnuté?
    var stateData = new APPBARDATA { cbSize = (uint)Marshal.SizeOf<APPBARDATA>() };
    uint state = SHAppBarMessage(ABM_GETSTATE, ref stateData);
    bool autoHideEnabled = (state & ABS_AUTOHIDE) != 0;

    if (!autoHideEnabled) return true; // auto-hide vypnutý → taskbar vždy viditelný

    // Krok 2: Je taskbar momentálně v zobrazené poloze?
    // Porovnání aktuálního Top taskbaru s pozicí ze ABM_GETTASKBARPOS
    if (!GetWindowRect(_taskbarHwnd, out RECT actualRect)) return true;

    var posData = new APPBARDATA { cbSize = (uint)Marshal.SizeOf<APPBARDATA>() };
    SHAppBarMessage(ABM_GETTASKBARPOS, ref posData);
    RECT expectedRect = posData.rc;

    // Taskbar je skrytý = actualRect.Top > expectedRect.Bottom (taskbar je "pod obrazovkou")
    return actualRect.Top <= expectedRect.Bottom;
}
```

**Alternativa pro sekundární taskbar:** `GetWindowRect(_taskbarHwnd)` přímo — porovnat Top s dolní hranicí monitoru.

### Anti-Patterns to Avoid
- **Nepoužívat `SHQueryUserNotificationState` pro per-monitor detekci:** vrací globální stav, nezohledňuje na kterém monitoru fullscreen je.
- **Nepoužívat `this.Visibility` přímo v Tick handleru bez null-check hwnd:** pokud okno není initialized, crash.
- **Neponechávat stale data při error stavu:** CONTEXT.md explicitně říká okamžitý přechod na error bez starých dat.

## Don't Hand-Roll

| Problém | Nebudovat | Použít místo | Proč |
|---------|-----------|--------------|------|
| Monitor resolution | vlastní DPI výpočty | `GetMonitorInfo` (rcMonitor) | Již importováno |
| Taskbar pozice | vlastní heuristika | `ABM_GETTASKBARPOS` | Již importováno |
| Win32 threading | background thread + marshal | `DispatcherTimer` (UI thread) | Vyhne se cross-thread exception |

**Klíčový insight:** Vše potřebné je již importováno. Fáze 3 je pouze o přidání logiky nad existující P/Invoke importy.

## Common Pitfalls

### Pitfall 1: Okno widget vs. okno taskbaru pro monitor lookup
**Co se stane:** Kód volá `MonitorFromWindow(widgetHwnd)` místo `MonitorFromWindow(_taskbarHwnd)` — na multi-monitor setup může widget a taskbar být na různých HMONITOR v edge cases.
**Proč se to stane:** Intuitivně chceme monitor widgetu, ale widget sleduje taskbar.
**Jak zabránit:** Vždy používat `_taskbarHwnd` pro monitor lookup — takto je napsaná i stávající `GetMonitorScale()` a `GetTaskbarInfo()`.

### Pitfall 2: DispatcherTimer Tick handler s async
**Co se stane:** `_visibilityTimer.Tick += async (_, _) => ...` — pokud CheckVisibility bude async, unhandled exception v Tick crashne app.
**Jak zabránit:** `CheckVisibility()` nechat synchronní — všechny Win32 volání jsou synchronní P/Invoke.

### Pitfall 3: `ShowErrorState()` zachovává stale data v `_lastUsage`
**Co se stane:** Stávající `ShowErrorState()` nuluje bary ale `_lastUsage` zůstane nastaveno. Tooltip pak stále zobrazuje stará data.
**Jak zabránit:** V `ShowErrorState()` nastavit `_lastUsage = null`. `PopupWindow.UpdateAndShow()` dostává `_lastUsage` — null způsobí zobrazení error detailu místo starých dat.

### Pitfall 4: `StartRefreshTimer` Tick — stale data zůstávají
**Aktuální chování:**
```csharp
if (usage != null)
    UpdateBars(usage);
else if (_lastUsage == null)   // ← problém: zobrazí stale data pokud _lastUsage != null
    ShowErrorState();
```
**Správné chování dle CONTEXT.md:** Okamžitý přechod na error stav bez stale dat:
```csharp
if (usage != null)
    UpdateBars(usage);
else
    ShowErrorState(); // vždy — bez podmínky
```

### Pitfall 5: Maroon barva — `Colors.Maroon` vs hex
**Co se stane:** `Colors.Maroon` v WPF je `#800000` — standardní web maroon. Použít toto.
```csharp
var maroon = new SolidColorBrush(Colors.Maroon); // #800000
```

## Code Examples

### ShowErrorState — upravená verze (maroon + "Error")
```csharp
private void ShowErrorState()
{
    _lastUsage = null; // okamžitý přechod, bez stale dat
    Bar5h.Value = 0;
    Bar7d.Value = 0;

    var maroon = new SolidColorBrush(Colors.Maroon);
    SetBarColor(GetBarIndicator(Bar5h), -1); // -1 = force maroon
    SetBarColor(GetBarIndicator(Bar7d), -1);

    Text5h.Foreground = Brushes.White;
    Text7d.Foreground = Brushes.White;
    Text5h.Text = "Error";
    Text7d.Text = "Error";
}
```

**Alternativa bez změny `SetBarColor` signatury:**
```csharp
private void ShowErrorState()
{
    _lastUsage = null;
    Bar5h.Value = 100; // plný bar aby barva byla viditelná
    Bar7d.Value = 100;
    var maroon = new SolidColorBrush(Colors.Maroon);
    var ind5h = GetBarIndicator(Bar5h);
    var ind7d = GetBarIndicator(Bar7d);
    if (ind5h != null) ind5h.Background = maroon;
    if (ind7d != null) ind7d.Background = maroon;
    Text5h.Foreground = Brushes.White;
    Text7d.Foreground = Brushes.White;
    Text5h.Text = "Error";
    Text7d.Text = "Error";
}
```

### Cleanup visibility timeru při Close
```csharp
Closed += (_, _) =>
{
    _topMostEnforcer?.Dispose();
    _popup?.Close();
    _refreshTimer?.Stop();
    _textTimer?.Stop();
    _visibilityTimer?.Stop(); // přidat
};
```

## State of the Art

| Oblast | Přístup | Stav |
|--------|---------|------|
| Fullscreen detekce | `SHQueryUserNotificationState` (globální) vs HWND comparison (per-monitor) | HWND comparison je správné pro per-monitor chování |
| Auto-hide detekce | Polling vs WM_APPCOMMAND message | Polling (500ms) je jednodušší a dostatečné pro tento use case |

## Open Questions

1. **`Bar5h.Value = 100` vs `Bar5h.Value = 0` pro error stav**
   - Co víme: `Bar.Value = 0` znamená prázdný bar — barva indikátoru se nemusí zobrazit pokud je `PART_Indicator` prázdný (šířka 0)
   - Co není jasné: Závisí na WPF ProgressBar template zda `Value=0` skryje `PART_Indicator` nebo ne
   - Doporučení: Při implementaci testovat — pokud `Value=0` skryje barvu, použít `Value=100`

2. **Sekundární taskbar auto-hide**
   - Co víme: `ABM_GETSTATE` vrací stav pro primární taskbar
   - Co není jasné: Sekundární taskbar nemá AppBar API — auto-hide stav pro sekundární taskbar nelze spolehlivě zjistit přes ABM
   - Doporučení: Pro sekundární taskbar detekovat auto-hide přes `GetWindowRect` — pokud `taskbarRect.Top >= screenHeight - 2`, taskbar je skrytý (heuristika)

## Validation Architecture

### Test Framework
| Property | Value |
|----------|-------|
| Framework | Žádný automatický test framework nebyl detekován v projektu |
| Config file | none |
| Quick run command | `dotnet build ClaudeUsageWidget/ClaudeUsageWidget.csproj` |
| Full suite command | `dotnet build ClaudeUsageWidget/ClaudeUsageWidget.csproj` |

### Phase Requirements → Test Map
| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| DATA-01 | Rate limit headers čteny správně | manual-only | n/a — vyžaduje live API | ❌ manuální |
| DATA-02 | OAuth token funguje s inference call | manual-only | n/a — vyžaduje credentials | ❌ manuální |
| DATA-03 | Refresh každou minutu | manual-only | vizuální verifikace | ❌ manuální |
| DATA-04 | Error stav = maroon + "Error" text | manual-only | vizuální verifikace | ❌ manuální |
| VIS-01 | Auto-hide taskbar → widget zmizí/vrátí | manual-only | vyžaduje auto-hide taskbar | ❌ manuální |
| VIS-02 | Fullscreen → widget zmizí na stejném monitoru | manual-only | vyžaduje fullscreen app | ❌ manuální |

### Sampling Rate
- **Per task commit:** `dotnet build ClaudeUsageWidget/ClaudeUsageWidget.csproj`
- **Per wave merge:** `dotnet build ClaudeUsageWidget/ClaudeUsageWidget.csproj`
- **Phase gate:** Build green + manuální verifikace všech 6 requirements

### Wave 0 Gaps
Žádné — build infrastruktura existuje. Testy jsou manuální povahou (vyžadují živé API a specifické systémové podmínky).

## Sources

### Primary (HIGH confidence)
- Analýza `MainWindow.xaml.cs` — existující P/Invoke importy a pattern
- Analýza `ClaudeApiClient.cs` — existující data flow
- Analýza `CredentialStore.cs` — existující credential loading
- Analýza `Program.cs` — credential inicializace při startu

### Secondary (MEDIUM confidence)
- Win32 `ABM_GETSTATE` / `ABS_AUTOHIDE` — standardní AppBar API dokumentace
- HWND comparison pro fullscreen — standardní pattern pro per-monitor fullscreen detekci

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH — vše je analýza existujícího kódu
- Architecture: HIGH — patterns jsou přímé rozšíření existujících patterns v kódu
- Pitfalls: HIGH — identifikovány z přímé analýzy kódu (zejména stale data bug)

**Research date:** 2026-03-07
**Valid until:** Stabilní (Win32 API se nemění)
