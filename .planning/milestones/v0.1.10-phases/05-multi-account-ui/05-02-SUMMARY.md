---
phase: 05-multi-account-ui
plan: "02"
subsystem: ui
tags: [wpf, xaml, usercontrol, multi-account, layout]

requires:
  - phase: 05-01
    provides: PNG assets (claude-logo.png, codex-logo.png) jako WPF Resource pack URI

provides:
  - AccountPanel UserControl (ikona + Bar5h + Bar7d) pro jeden ucet
  - MainWindow s horizontalnim StackPanelem pro N AccountPanel instanci
  - Dynamicka sirka okna: N * 170px
  - ClaudeApiClient.AccountService property pro ServiceType routing

affects:
  - future UI phases using AccountPanel
  - MainWindow layout changes

tech-stack:
  added: []
  patterns:
    - "AccountPanel jako UserControl: jeden sloupec (ikona + 2 bary) — compose-based layout"
    - "MainWindow sirka = N * COL_WIDTH nastavena pred Show() — PositionWindow() funguje bez zmeny"
    - "Tuple list (Client, Panel, LastUsage) pro per-account stav v MainWindow"

key-files:
  created:
    - ClaudeUsageWidget/AccountPanel.xaml
    - ClaudeUsageWidget/AccountPanel.xaml.cs
  modified:
    - ClaudeUsageWidget/MainWindow.xaml
    - ClaudeUsageWidget/MainWindow.xaml.cs
    - ClaudeUsageWidget/Program.cs
    - ClaudeUsageWidget/ClaudeApiClient.cs

key-decisions:
  - "AccountPanel konstruktor internal (ne public) — ServiceType je internal enum, C# vyzaduje konzistentni pristupnost"
  - "Spinner spusten spolecne pro vsechny panely pres StartSpinnerTimer(), zastaven po nacteni"
  - "Popup zobrazuje prvni ucet (_accounts[0]) — PopupWindow nezmenena"

patterns-established:
  - "AccountPanel: ShowLoadingState + AdvanceSpinner + UpdateBars + ShowErrorState + RefreshText"
  - "MainWindow._accounts: List<(Client, Panel, LastUsage)> — immutable tuples s reassignment po update"

requirements-completed: [UI-07, UI-08, UI-09]

duration: 15min
completed: 2026-03-07
---

# Phase 5 Plan 02: AccountPanel UserControl + multi-account layout + dynamicka sirka

**AccountPanel WPF UserControl s ikonou vlevo od 2 progress baru, MainWindow zobrazuje N sloupcu horizontalne s dynamickou sirkou N x 170px**

## Performance

- **Duration:** ~15 min
- **Started:** 2026-03-07T~12:35Z
- **Completed:** 2026-03-07T~12:50Z
- **Tasks:** 2 (+ 1 checkpoint)
- **Files modified:** 6

## Accomplishments
- AccountPanel.xaml: Grid se 3 sloupci (ikona 20px, mezera 4px, bary *) a 3 radky (bar5h, mezera 5px, bar7d)
- AccountPanel.xaml.cs: UpdateBars, ShowLoadingState, AdvanceSpinner, ShowErrorState, RefreshText metody
- MainWindow prepsan: StackPanel Orientation=Horizontal, konstruktor prijima List<ClaudeApiClient>
- Width okna = N * 170 nastaven pred Show() — PositionWindow() bezbezne pouziva Width
- Program.cs: predava cely `clients` list (ne jen clients[0]) do MainWindow
- ClaudeApiClient: pridana AccountService property z _service field

## Task Commits

Kazdy task commitovan atomicky:

1. **Task 05-02-01: AccountPanel UserControl** - `177e756` (feat)
2. **Task 05-02-02: multi-account layout + Program.cs + AccountService** - `dc58fd2` (feat)
3. **Task 05-02-03: vizualni verifikace** - checkpoint (awaiting human verify)

## Files Created/Modified
- `ClaudeUsageWidget/AccountPanel.xaml` - UserControl s Image + ProgressBar x2 + TextBlock x2
- `ClaudeUsageWidget/AccountPanel.xaml.cs` - logika: UpdateBars, spinner, error state
- `ClaudeUsageWidget/MainWindow.xaml` - StackPanel Orientation=Horizontal, bez fixniho Width
- `ClaudeUsageWidget/MainWindow.xaml.cs` - List<ClaudeApiClient>, dynamicka sirka, per-account state
- `ClaudeUsageWidget/Program.cs` - clients (cely list) do MainWindow
- `ClaudeUsageWidget/ClaudeApiClient.cs` - AccountService property

## Decisions Made
- AccountPanel konstruktor `internal` (ne `public`) — ServiceType je `internal enum`, C# CS0051 error
- Popup zustava pro prvni ucet (_accounts[0]) — PopupWindow beze zmeny
- Spinner spusten spolecne pro vsechny AccountPanely pres jeden DispatcherTimer

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] AccountPanel konstruktor internal misto public**
- **Found during:** Task 05-02-01 (AccountPanel UserControl) — odhaleno pri buildu
- **Issue:** Plan specifikoval `public AccountPanel(ServiceType service)` ale `ServiceType` je `internal enum` — C# error CS0051: nekonzistentni pristupnost
- **Fix:** Zmeneno na `internal AccountPanel(ServiceType service)`
- **Files modified:** ClaudeUsageWidget/AccountPanel.xaml.cs
- **Verification:** `dotnet build` projde bez chyb (0 warnings, 0 errors)
- **Committed in:** dc58fd2 (Task 05-02-02 commit)

---

**Total deviations:** 1 auto-fixed (1 bug — pristupnost konstruktoru)
**Impact on plan:** Nutna oprava pro kompilaci, zadny scope creep.

## Issues Encountered
- C# CS0051: Nekonzistentni pristupnost — AccountPanel.AccountPanel(ServiceType) public, ale ServiceType internal. Opraveno zmenou konstruktoru na internal.

## User Setup Required
None — build prochazi, vizualni verifikace ceka na uzivatele (checkpoint 05-02-03).

## Next Phase Readiness
- AccountPanel UserControl plne funkci, ready pro dalsi faze
- MainWindow zobrazuje N AccountPanel instanci horizontalne
- Po vizualni verifikaci checkpoint 05-02-03: Phase 5 kompletni

---
*Phase: 05-multi-account-ui*
*Completed: 2026-03-07*

## Self-Check: PASSED
- AccountPanel.xaml: FOUND
- AccountPanel.xaml.cs: FOUND
- MainWindow.xaml: FOUND (bez Width atributu)
- MainWindow.xaml.cs: FOUND (konstruktor prijima List<ClaudeApiClient>)
- Program.cs: FOUND (predava clients do MainWindow)
- commit 177e756: FOUND
- commit dc58fd2: FOUND
- dotnet build: 0 chyb, 0 warningů
