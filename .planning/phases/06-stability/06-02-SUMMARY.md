---
phase: 06-stability
plan: "02"
subsystem: api
tags: [jwt, credentials, oauth, wpf, spinner]

# Dependency graph
requires:
  - phase: 06-stability
    provides: ClaudeApiClient, CredentialStore, AccountPanel, MainWindow — existující implementace

provides:
  - Token expiry recovery s disk re-read po 401 + selhání refresh
  - JWT-based dedup účtů (org_id/sub místo token prefixu)
  - ClearSpinner() metoda — reset spinner textu po zastavení

affects:
  - 06-stability
  - Phase 07 (history feature pracuje se stejným CredentialStore)

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Disk re-read po 401: přečti SourcePath z disku, porovnej token, retry nebo error stav"
    - "JWT claim extraction: GetJwtClaim() sdílená metoda, stejný pattern jako GetJwtExpiryMs()"
    - "Dedup dle JWT identity (org_id/sub), ne token textu — WSL + Windows = 1 panel"

key-files:
  created: []
  modified:
    - ClaudeUsageWidget/ClaudeApiClient.cs
    - ClaudeUsageWidget/CredentialStore.cs
    - ClaudeUsageWidget/AccountPanel.xaml.cs
    - ClaudeUsageWidget/MainWindow.xaml.cs

key-decisions:
  - "LoadCredentialFromPath() jako jediná public cesta k disk re-read per path — přes WslSourceMarker i Windows path"
  - "GetAccountKey() vrací null pro credentials bez dekódovatelného JWT — LoadAllAccounts() tiše přeskočí (continue)"
  - "ClearSpinner() nastavuje prázdný string — UpdateBars/ShowErrorState ihned přepíše správnou hodnotou"

patterns-established:
  - "JWT claims: GetJwtClaim(token, claimName) — reuse across GetAccountKey/GetJwtExpiryMs"
  - "401 recovery: disk re-read → porovnej token → retry nebo error (bez smyčky)"

requirements-completed: [STAB-02, STAB-03, STAB-04]

# Metrics
duration: 10min
completed: 2026-03-07
---

# Phase 06 Plan 02: Token Expiry Recovery + Deduplication Fix + Progress Bar Text Fix Summary

**Token 401 recovery s disk re-read, JWT-based dedup účtů (org_id/sub), a reset spinner textu v AccountPanel**

## Performance

- **Duration:** ~10 min
- **Started:** 2026-03-07T07:28:19Z
- **Completed:** 2026-03-07
- **Tasks:** 3
- **Files modified:** 4

## Accomplishments

- STAB-02: Po 401 + selhání refresh widget přečte credentials z disku; nový token → retry; stejný → error stav (bez zaseknutí)
- STAB-03: Dedup Claude i Codex účtů dle JWT org_id/sub — WSL i Windows s identickým účtem = 1 panel; credentials bez JWT tiše přeskočeny
- STAB-04: ClearSpinner() resetuje Text5h/Text7d při StopSpinner() — spinner znak nikdy nezůstane po načtení dat

## Task Commits

Všechny tři tasky sloučeny do jednoho atomického commitu (všechny závislé na sobě — ClaudeApiClient.cs volá LoadCredentialFromPath):

1. **Tasks 06-02-01, 06-02-02, 06-02-03 (STAB-02/03/04)** - `ab4dc7e` (fix)

## Files Created/Modified

- `ClaudeUsageWidget/ClaudeApiClient.cs` - Nahrazen `_credentialIndex++` branch disk re-read logikou
- `ClaudeUsageWidget/CredentialStore.cs` - Přidány GetJwtClaim(), GetAccountKey(), LoadCredentialFromPath(); opravena LoadAllAccounts()
- `ClaudeUsageWidget/AccountPanel.xaml.cs` - Přidána ClearSpinner() metoda
- `ClaudeUsageWidget/MainWindow.xaml.cs` - StopSpinner() volá ClearSpinner() na všechny panely

## Decisions Made

- `LoadCredentialFromPath()` jako jediná public cesta — jednotné rozhraní pro WSL i Windows path
- `GetAccountKey()` vrací null místo fallbacku na token prefix — credentials bez JWT jsou tiše přeskočeny (bezpečnější než nevalidní dedup)
- Commit sloučen (task 01 volá metodu z task 02 — nešlo oddělit bez nefunkčního mezistavu)

## Deviations from Plan

None - plan executed exactly as written.

## Issues Encountered

- dotnet není v PATH WSL — použit `/mnt/c/Program Files/dotnet/dotnet.exe` přímo. Build proběhl úspěšně (0 warnings, 0 errors).

## Next Phase Readiness

- Stability bugy STAB-02/03/04 opraveny, widget stabilní
- Připraveno pro plan 06-03 (pokud existuje) nebo Phase 07 (History)

---
*Phase: 06-stability*
*Completed: 2026-03-07*
