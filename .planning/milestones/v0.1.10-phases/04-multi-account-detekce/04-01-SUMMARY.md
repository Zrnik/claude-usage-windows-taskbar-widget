---
phase: 04-multi-account-detekce
plan: 01
subsystem: auth
tags: [credentials, oauth, codex, multi-account, deduplication]

# Dependency graph
requires: []
provides:
  - ServiceType enum (Claude, Codex) v ClaudeUsageWidgetProvider namespace
  - AccountInfo sealed record (Service, Credential, Label)
  - CredentialStore.LoadAllAccounts() vracejici List<AccountInfo> bez duplicit
  - Defenzivni Codex credential loading (Windows + WSL, tichy fail)
affects: [04-02, 04-03]

# Tech tracking
tech-stack:
  added: []
  patterns: [HashSet deduplication by token prefix, multi-field JSON fallback, long.MaxValue for API keys without expiry]

key-files:
  created: []
  modified:
    - ClaudeUsageWidget/CredentialStore.cs

key-decisions:
  - "Deduplikace pres HashSet<string> s klicem 'service:token[..32]' — prevence duplicit pri shodnem tokenu z vice zdroju"
  - "ParseCodexCredentialJson pouziva fallback pole (access_token/accessToken/token) pro kompatibilitu s ruznym formatem Codex auth.json"
  - "ExpiresAt = long.MaxValue pro Codex credentials bez expiry pole — nikdy nevyprsely API klice"

patterns-established:
  - "Defenzivni loading: chybejici soubor, null JSON, a vsechny parse chyby vraci null bez vyjimky"
  - "Label konvence: service-platform (claude-wsl, codex-windows, atd.)"

requirements-completed: [MULTI-01, MULTI-02]

# Metrics
duration: 2min
completed: 2026-03-07
---

# Phase 4 Plan 01: Multi-account credential foundation Summary

**ServiceType/AccountInfo typy + LoadAllAccounts() s HashSet deduplikaci a defenzivnim Codex loading pro Windows i WSL**

## Performance

- **Duration:** ~2 min
- **Started:** 2026-03-07T00:00:19Z
- **Completed:** 2026-03-07T00:02:27Z
- **Tasks:** 1
- **Files modified:** 1

## Accomplishments
- Pridan ServiceType enum a AccountInfo record jako datove kontrakty pro Plan 02 a 03
- LoadAllAccounts() aggreguje Claude + Codex credentials s deduplikaci pres HashSet
- Codex loading podporuje Windows i WSL s ticho prehozenym failem na chybejici soubor
- ParseCodexCredentialJson zpracovava flexible formaty (snake_case i camelCase pole)

## Task Commits

1. **Task 1: ServiceType, AccountInfo, LoadAllAccounts + Codex loading** - `0b59a7a` (feat)

**Plan metadata:** TBD (docs commit)

## Files Created/Modified
- `ClaudeUsageWidget/CredentialStore.cs` - Pridany ServiceType, AccountInfo, LoadAllAccounts(), LoadCodexCredentials(), TryLoadCodexWindowsCredential(), TryLoadCodexWslCredential(), ParseCodexCredentialJson()

## Decisions Made
- Deduplikace klice "service:token[..32]" — dostatecne unikatni, neni treba cely token
- Codex JSON parsing s multi-field fallbackem — Codex CLI muze generovat ruzne formaty
- long.MaxValue pro expiry bez pole — API klice nemaji cas expirace

## Deviations from Plan

None — plan executed exactly as written.

## Issues Encountered

Build selhal poprvim s MSB3027 (nelze zkopirovat EXE, widget byl spusten). Druhy build s `--output` do temp adresare uspel bez chyb. Kompilace samotna byla vzdy spravna.

## User Setup Required

None — no external service configuration required.

## Next Phase Readiness
- Plan 02 muze konzumovat AccountInfo a LoadAllAccounts() pro per-account API klienty
- Vsechny typy jsou internal v ClaudeUsageWidgetProvider namespace
- Zpetna kompatibilita zachovana — stare LoadCredential(), LoadAllCredentials(), SaveCredential() beze zmeny

---
*Phase: 04-multi-account-detekce*
*Completed: 2026-03-07*
