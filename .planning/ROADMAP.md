# Roadmap: Claude Usage Widget

## Milestones

- ✅ **v0.1.9 MVP** — Phases 1-3 (shipped 2026-03-07)
- ✅ **v0.1.10 Multi-account** — Phases 4-5 (shipped 2026-03-07)
- ✅ **v0.1.11 Usage History** — Phases 6-8 (shipped 2026-03-07)

## Phases

<details>
<summary>✅ v0.1.9 MVP (Phases 1-3) — SHIPPED 2026-03-07</summary>

Phases 1-3 delivered the MVP: borderless taskbar window, progress bars, color coding, hover tooltip, auto-hide + fullscreen support, error state, and ClaudeApiClient via inference call + rate limit headers.

</details>

<details>
<summary>✅ v0.1.10 Multi-account (Phases 4-5) — SHIPPED 2026-03-07</summary>

Phases 4-5 delivered multi-account support: AccountInfo record, ServiceType enum, LoadAllAccounts() with org ID deduplication, per-account ClaudeApiClient, AccountPanel UserControl with embedded icons, horizontal StackPanel layout, dynamic width.

</details>

<details>
<summary>✅ v0.1.11 Usage History (Phases 6-8) — SHIPPED 2026-03-07</summary>

Phases 6-8 delivered stability fixes and history charts: single instance enforcement (Mutex), token 401 recovery with JWT-based deduplication, UsageHistoryStore with atomic hourly-bucket upsert to AppData, HistoryChart UserControl (WPF Canvas sparkline, multicolor segments), and PopupWindow 280px redesign with reset Grid and embedded sparkline charts.

</details>

## Progress

| Phase | Milestone | Plans Complete | Status | Completed |
|-------|-----------|----------------|--------|-----------|
| 1. MVP Foundation | v0.1.9 | - | Complete | 2026-03-07 |
| 2. MVP Core | v0.1.9 | - | Complete | 2026-03-07 |
| 3. MVP Polish | v0.1.9 | - | Complete | 2026-03-07 |
| 4. Multi-account Arch | v0.1.10 | - | Complete | 2026-03-07 |
| 5. Multi-account UI | v0.1.10 | - | Complete | 2026-03-07 |
| 6. Stability | v0.1.11 | 2/2 | Complete | 2026-03-07 |
| 7. History Persistence | v0.1.11 | 1/1 | Complete | 2026-03-07 |
| 8. Tooltip & Chart | v0.1.11 | 2/2 | Complete | 2026-03-07 |
