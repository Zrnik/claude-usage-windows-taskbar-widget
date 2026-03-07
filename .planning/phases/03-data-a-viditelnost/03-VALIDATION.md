---
phase: 3
slug: data-a-viditelnost
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-03-07
---

# Phase 3 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | Žádný automatický test framework — manuální verifikace |
| **Config file** | none |
| **Quick run command** | `dotnet build ClaudeUsageWidget/ClaudeUsageWidget.csproj` |
| **Full suite command** | `dotnet build ClaudeUsageWidget/ClaudeUsageWidget.csproj` |
| **Estimated runtime** | ~10 seconds |

---

## Sampling Rate

- **After every task commit:** Run `dotnet build ClaudeUsageWidget/ClaudeUsageWidget.csproj`
- **After every plan wave:** Run `dotnet build ClaudeUsageWidget/ClaudeUsageWidget.csproj`
- **Before `/gsd:verify-work`:** Build green + manuální verifikace všech 6 requirements
- **Max feedback latency:** 10 seconds (build)

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|-----------|-------------------|-------------|--------|
| 3-01-01 | 01 | 1 | DATA-04 | manual | `dotnet build ...` (build check) | ✅ | ⬜ pending |
| 3-01-02 | 01 | 1 | DATA-03 | manual | `dotnet build ...` (build check) | ✅ | ⬜ pending |
| 3-01-03 | 01 | 1 | DATA-01, DATA-02 | manual | `dotnet build ...` (build check) | ✅ | ⬜ pending |
| 3-02-01 | 02 | 1 | VIS-01 | manual | `dotnet build ...` (build check) | ✅ | ⬜ pending |
| 3-02-02 | 02 | 1 | VIS-02 | manual | `dotnet build ...` (build check) | ✅ | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

Žádné — build infrastruktura existuje. Všechny testy jsou manuální povahou (vyžadují živé API a specifické systémové podmínky).

*Existing infrastructure covers all phase requirements.*

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Rate limit headers čteny správně | DATA-01 | Vyžaduje live API call | Spustit widget, sledovat v logu nebo UI progress bary |
| OAuth token funguje s inference call | DATA-02 | Vyžaduje platné credentials | Spustit widget s credentials, zkontrolovat data |
| Refresh každou minutu | DATA-03 | Vizuální verifikace timingu | Sledovat widget 2 min, ověřit aktualizaci hodnot |
| Error stav = maroon + "Error" text | DATA-04 | Vizuální verifikace barvy | Odpojit síť, zkontrolovat barvu a text |
| Auto-hide taskbar → widget zmizí/vrátí | VIS-01 | Vyžaduje auto-hide taskbar nastavení | Zapnout auto-hide taskbar v Settings, ověřit chování widgetu |
| Fullscreen → widget zmizí na stejném monitoru | VIS-02 | Vyžaduje fullscreen aplikaci | Spustit fullscreen app (např. YouTube v prohlížeči), ověřit zmizení widgetu |

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references
- [ ] No watch-mode flags
- [ ] Feedback latency < 15s
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** pending
