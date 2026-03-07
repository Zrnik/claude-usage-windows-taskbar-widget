# Phase 3: Data a viditelnost - Context

**Gathered:** 2026-03-07
**Status:** Ready for planning

<domain>
## Phase Boundary

Živá API data s error handling, credential fallback (Windows + WSL), auto-hide taskbar podpora, a fullscreen skrytí widgetu na příslušném monitoru. UI a pozicování jsou hotové z Phase 2 — zde jen napojujeme reálné chování a edge cases viditelnosti.

</domain>

<decisions>
## Implementation Decisions

### Error stav (DATA-04)
- Bary se vybarví **maroon** barvou (tmavě červená)
- Uvnitř baru zobrazen text **"Error"** místo procent
- Tooltip při hover obsahuje detail — co se stalo (expired token, network error, timeout)
- Přechod na error stav: okamžitě při selhání API, bez zobrazování stale dat

### Credential fallback
- Při startu načíst **všechny dostupné credential zdroje** (Windows + WSL) automaticky
- `CredentialStore` vrátí seznam, `ClaudeApiClient.SetCredentials(List<OAuthCredential>)` je zkouší postupně
- Bez interakce uživatele — rotuje přes dostupné credentials dokud nenajde funkční
- Teprve když selžou všechny → error stav

### Fullscreen detekce (VIS-02)
- Widget se schová **na monitoru kde běží fullscreen aplikace** — jiné monitory nejsou dotčeny
- Detekce pomocí `SHQueryUserNotificationState` nebo porovnáním HWND foreground okna s rozměry monitoru
- Polling každých **500ms**
- Okamžité hide/show, žádné animace

### Auto-hide taskbar (VIS-01)
- Taskbar zajede dolů → widget **okamžitě zmizí**
- Taskbar vyjede nahoru → widget se zobrazí **až když taskbar dosáhne finální pozice** (sledovat dynamicky Y souřadnici taskbaru, zobrazit až je na místě)
- Polling každých **500ms** — stejný timer jako fullscreen
- Podpora jen základní — kdo má auto-hide, widget se bude chovat intuitivně ale není to priorita

### Přechody viditelnosti
- Žádné animace — okamžité `Visibility.Visible` / `Visibility.Hidden`
- Oba triggery (auto-hide + fullscreen) sdílí jeden visibility timer (500ms)
- Logika: widget viditelný jen když `taskbarVisible && !fullscreenOnSameMonitor`

### Data refresh (DATA-01, DATA-02, DATA-03)
- Stávající `_refreshTimer` z Phase 2 — zachovat, frekvence každou minutu
- Stávající `ClaudeApiClient.cs` — zachovat beze změny
- `_lastUsage` používán pro zobrazení — null znamená error stav

### Claude's Discretion
- Konkrétní metoda detekce fullscreen (SHQueryUserNotificationState vs. HWND comparison)
- Jak přesně detekovat "taskbar je na finální pozici" (RECT comparison nebo ABM_GETTASKBARPOS)

</decisions>

<code_context>
## Existing Code Insights

### Reusable Assets
- `ClaudeApiClient.cs`: `SetCredentials(List<OAuthCredential>)` — již podporuje více credentials, jen načíst oba zdroje
- `CredentialStore.cs`: Čte credentials z Windows i WSL cest — vrací `List<OAuthCredential>`, připraveno pro fallback
- `_refreshTimer` v `MainWindow.xaml.cs`: Existující 1minutový timer pro API refresh — zachovat
- `_lastUsage` v `MainWindow.xaml.cs`: Nullable `UsageData?` — null = žádná data = error stav
- `ABM_GETTASKBARPOS` / `SHAppBarMessage` v `MainWindow.xaml.cs`: Win32 import pro pozici taskbaru — reuse pro auto-hide detekci
- `MonitorFromWindow` + `GetMonitorInfo`: Existující imports pro multi-monitor — reuse pro fullscreen detekci

### Established Patterns
- WPF `DispatcherTimer` pro periodické akce (tray watch, refresh timer)
- Win32 P/Invoke pro Windows API (RECT, MONITORINFO, APPBARDATA struktury hotové)
- `Visibility.Visible` / `Visibility.Hidden` pro hide/show

### Integration Points
- `UpdateBars(UsageData)` v `MainWindow` — rozšířit o error stav (maroon + "Error" text)
- Nový `DispatcherTimer` (500ms) pro fullscreen + auto-hide check — spustit v `Loaded` event handleru
- `CredentialStore.GetCredentials()` při startu aplikace — načíst všechny zdroje, předat jako seznam

</code_context>

<specifics>
## Specific Ideas

- Bug report z diskuze: expired token zobrazoval stale data bez vizuálního signálu — opravit tím, že null `_lastUsage` + error stav → maroon bary, ne stará data
- Bug report z diskuze: YouTube fullscreen byl viditelný přes widget — VIS-02 to opraví
- Widget musí sledovat na kterém monitoru sám je, ne jen primární monitor

</specifics>

<deferred>
## Deferred Ideas

None — diskuze zůstala v rámci Phase 3 scope.

</deferred>

---

*Phase: 03-data-a-viditelnost*
*Context gathered: 2026-03-07*
