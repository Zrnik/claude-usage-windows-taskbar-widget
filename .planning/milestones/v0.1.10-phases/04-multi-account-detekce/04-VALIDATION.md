---
phase: 4
slug: multi-account-detekce
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-03-07
---

# Phase 4 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | none — build verification only |
| **Config file** | none |
| **Quick run command** | `dotnet build ClaudeUsageWidget/ClaudeUsageWidget.csproj` |
| **Full suite command** | `dotnet build ClaudeUsageWidget/ClaudeUsageWidget.csproj` |
| **Estimated runtime** | ~10 seconds |

---

## Sampling Rate

- **After every task commit:** Run `dotnet build ClaudeUsageWidget/ClaudeUsageWidget.csproj`
- **After every plan wave:** Run `dotnet build ClaudeUsageWidget/ClaudeUsageWidget.csproj`
- **Before `/gsd:verify-work`:** Build green + manuální test s reálnými credentials
- **Max feedback latency:** ~10 seconds

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|-----------|-------------------|-------------|--------|
| 4-01-01 | 01 | 1 | MULTI-01 | build | `dotnet build ClaudeUsageWidget/ClaudeUsageWidget.csproj` | ✅ | ⬜ pending |
| 4-01-02 | 01 | 1 | MULTI-01 | manual | Spustit widget, ověřit deduplikaci Claude účtů | ❌ manual | ⬜ pending |
| 4-02-01 | 02 | 1 | MULTI-02 | build | `dotnet build ClaudeUsageWidget/ClaudeUsageWidget.csproj` | ✅ | ⬜ pending |
| 4-02-02 | 02 | 1 | MULTI-02 | manual | Spustit widget s Codex credentials, ověřit zobrazení | ❌ manual | ⬜ pending |
| 4-03-01 | 03 | 2 | MULTI-03 | build | `dotnet build ClaudeUsageWidget/ClaudeUsageWidget.csproj` | ✅ | ⬜ pending |
| 4-03-02 | 03 | 2 | MULTI-03 | manual | Vizuálně ověřit, že každý účet má vlastní API data | ❌ manual | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

*Existing infrastructure covers all phase requirements. Build infrastructure existuje — žádné nové test soubory potřeba.*

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Claude credentials z Windows+WSL načteny, duplicity odstraněny | MULTI-01 | Vyžaduje credentials na disku, vizuální ověření počtu oken | Spustit widget s Windows+WSL Claude credentials; ověřit, že se zobrazí jen jedno okno pro stejný token |
| Codex `auth.json` načten z Windows i WSL | MULTI-02 | Vyžaduje Codex credentials na disku | Spustit widget s `~/.codex/auth.json`; ověřit, že Codex okno se zobrazí |
| Každý účet má vlastní API data | MULTI-03 | Vizuální verifikace | Mít 2 různé účty; ověřit, že každý zobrazuje svá vlastní data |
| Chybějící credentials jsou tiše přeskočeny | MULTI-01, MULTI-02 | Vyžaduje testování bez credentials | Smazat credentials soubory; ověřit, že widget necrashne |

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references
- [ ] No watch-mode flags
- [ ] Feedback latency < 10s
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** pending
