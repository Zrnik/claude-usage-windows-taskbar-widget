---
phase: 09-time-anchored-charts-tech-debt
plan: 01
subsystem: api
tags: [refactoring, tech-debt, account-key, credential-store]

# Dependency graph
requires:
  - phase: 07-multi-account
    provides: "AccountInfo record, CredentialStore, ClaudeApiClient account key extraction"
provides:
  - "Single source of truth for account key extraction in CredentialStore.GetAccountKey"
  - "Internal visibility for GetAccountKey enabling cross-class usage"
affects: [multi-account, api-client]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Account key extraction centralized in CredentialStore (not duplicated in consumers)"

key-files:
  created: []
  modified:
    - ClaudeUsageWidget/CredentialStore.cs
    - ClaudeUsageWidget/ClaudeApiClient.cs

key-decisions:
  - "GetAccountKey(AccountInfo) delegates to GetAccountKey(token, service) first, then falls back to opaque token logic"
  - "No try/catch wrapper needed in new overload since underlying methods already handle exceptions"

patterns-established:
  - "Account key extraction: always use CredentialStore.GetAccountKey, never duplicate JWT parsing"

requirements-completed: [DEBT-01]

# Metrics
duration: 2min
completed: 2026-03-11
---

# Phase 9 Plan 1: Account Key Extraction Dedup Summary

**Eliminated ExtractAccountKey duplication by delegating to CredentialStore.GetAccountKey with AccountInfo overload**

## Performance

- **Duration:** 2 min
- **Started:** 2026-03-11T08:08:27Z
- **Completed:** 2026-03-11T08:10:02Z
- **Tasks:** 2
- **Files modified:** 2

## Accomplishments
- Centralized account key extraction logic in CredentialStore (single source of truth)
- Removed 39 lines of duplicated JWT parsing from ClaudeApiClient
- Preserved opaque token fallback (claude:wsl / claude:win) in new overload

## Task Commits

Each task was committed atomically:

1. **Task 1: Make GetAccountKey internal and add AccountInfo overload** - `5999d3c` (refactor)
2. **Task 2: Replace ExtractAccountKey with CredentialStore.GetAccountKey** - `f3b2fb9` (refactor)

## Files Created/Modified
- `ClaudeUsageWidget/CredentialStore.cs` - Changed GetAccountKey visibility to internal, added AccountInfo overload with opaque token fallback
- `ClaudeUsageWidget/ClaudeApiClient.cs` - Constructor now calls CredentialStore.GetAccountKey(account), deleted ExtractAccountKey method

## Decisions Made
- GetAccountKey(AccountInfo) overload delegates to the existing (token, service) overload first, then handles opaque token fallback separately -- cleaner than duplicating JWT parsing
- No try/catch needed in the new overload because the underlying GetAccountKey(token, service) and GetJwtClaim already swallow exceptions

## Deviations from Plan

None - plan executed exactly as written.

## Issues Encountered
None.

## User Setup Required
None - no external service configuration required.

## Next Phase Readiness
- Tech debt resolved, ClaudeApiClient is cleaner
- Ready for Phase 9 Plan 2 (time-anchored charts)

## Self-Check: PASSED

- FOUND: ClaudeUsageWidget/CredentialStore.cs
- FOUND: ClaudeUsageWidget/ClaudeApiClient.cs
- FOUND: 09-01-SUMMARY.md
- FOUND: commit 5999d3c
- FOUND: commit f3b2fb9
- VERIFIED: ExtractAccountKey not found anywhere in ClaudeUsageWidget/
- VERIFIED: CredentialStore.GetAccountKey called in ClaudeApiClient constructor
- VERIFIED: Two internal GetAccountKey overloads in CredentialStore

---
*Phase: 09-time-anchored-charts-tech-debt*
*Completed: 2026-03-11*
