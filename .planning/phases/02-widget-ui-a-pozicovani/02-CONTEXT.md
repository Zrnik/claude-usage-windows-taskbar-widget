# Phase 2: Widget UI a pozicování - Context

**Gathered:** 2026-03-06
**Status:** Ready for planning

<domain>
## Phase Boundary

Postavit viditelný WPF widget s progress bary a správně ho umístit v taskbaru těsně před system tray oblastí. Zahrnuje i základní API volání (ClaudeApiClient) pro reálná data — error handling a visibility edge cases jsou Phase 3.

</domain>

<decisions>
## Implementation Decisions

### Rozměry okna
- Výška = výška taskbaru (dynamicky zjištěná, obvykle 48px na Windows 11)
- Šířka odpovídá obsahu (2 progress bary + marginy) — Claude rozhodne konkrétní hodnotu

### Progress bar layout a styl
- Marginy: 6px nahoře, 6px dole, 6px mezi bary
- Každý progress bar tedy cca 15px vysoký
- Ostré hrany — žádné border-radius, žádný gradient
- Flat barva: zelená < 75%, oranžová 75–90%, červená ≥ 90%
- Text s procentem zobrazený uvnitř baru (standardní WPF TextBlock přes ProgressBar)

### Data
- Phase 2 rovnou volá ClaudeApiClient — reálná data z API
- Phase 3 přidá error handling, auto-hide, fullscreen support

### Okno v taskbaru a context menu
- `ShowInTaskbar=False` — aplikace nepřidá okno do taskbaru
- Žádný notify icon v system tray
- Pravé tlačítko přímo na widget okno otevře context menu s položkou "Quit"
- Žádný splash screen

### Taskbar orientace
- Podporujeme pouze taskbar dole (nejběžnější případ)
- Při detekci taskbaru jinde než dole: MessageBox "smůla bejku" a aplikace se ukončí

### Tooltip
- Hover nad widgetem zobrazí tooltip s časem do resetu pro oba limity
- Použije existující `FormatResetTime()` z Program.cs

### Claude's Discretion
- Přesná šířka widgetu (auto-size nebo fixed)
- Windows API metoda pro detekci tray šířky (SHAppBarMessage nebo HWND)
- Jak reagovat na změny tray šířky (event-based nebo timer)
- XAML struktura okna (App.xaml nebo jen Program.cs)

</decisions>

<code_context>
## Existing Code Insights

### Reusable Assets
- `ClaudeApiClient.cs`: HTTP client pro Anthropic API — zachovat beze změny, volat přímo z main window
- `CredentialStore.cs` + `OAuthCredential`: Čtení credentials — zachovat beze změny
- `UsageData`: DTO s `FiveHourUtilization`, `SevenDayUtilization`, `FiveHourReset`, `SevenDayReset` — použít přímo pro binding
- `TimeFormatter.FormatResetTime()` v Program.cs: Formátování času do resetu ("in 2h 30m") — použít v tooltipu

### Established Patterns
- WPF framework (zvoleno v Phase 1)
- Namespace `ClaudeUsageWidgetProvider`, target `net8.0-windows10.0.22621.0`
- Aplikace startuje přes `App : Application` v Program.cs

### Integration Points
- Main window volá `ClaudeApiClient.GetUsageAsync()` při startu + každou minutu (Phase 3 přidá polling timer)
- `UsageData` properties se bindují na WPF progress bary + text labels

</code_context>

<specifics>
## Specific Ideas

- Widget musí vizuálně splynout s taskbarem — ostré hrany, žádné stíny, barva pozadí shodná s taskbarem (Win11 dark taskbar: cca #1C1C1C nebo průhledné)
- Messagebox text: "smůla bejku" při taskbaru jinde než dole

</specifics>

<deferred>
## Deferred Ideas

None — diskuze zůstala v rámci Phase 2 scope.

</deferred>

---

*Phase: 02-widget-ui-a-pozicovani*
*Context gathered: 2026-03-06*
