# Phase 10: Per-Key Chart Windows + Extra Usage - Context

**Gathered:** 2026-03-11
**Status:** Ready for planning

<domain>
## Phase Boundary

Každý rate limit klíč zobrazuje graf s vlastním časovým oknem dle defaultů. Podpora pro extra usage: (1) utilization > 100% v grafech i progress barech, (2) nový progress bar zobrazující spotřebu peněz (USD spend vs limit). Research jak získat extra usage spend data z API je součástí této fáze. Data collection a persistence patří do Phase 7, Settings UI do Phase 11.

</domain>

<decisions>
## Implementation Decisions

### Per-key časové okno
- Substring match na API label: obsahuje "5h" → 2 dny, "7d" → 14 dní, "session"/"100h" → 14 dní, "review" → 7 dní
- Fallback pro neznámé klíče: 14 dní (max z UsageHistoryStore)
- Zobrazit všechny unikátní klíče z API včetně model-specifických variant (unified-7d_sonnet, unified-5h_sonnet atd.)

### Extra usage vizualizace — utilization >100%
- Y osa grafu: normálně 0-100%, dynamicky se rozšíří jen když data překročí 100%
- Nový barevný práh na 100%: tmavě červená/fialová — jasně odlišit "blíží se limitu" (červená 90-100%) vs "překročil limit" (>100%)
- Tenká horizontální čára na 100% hranici v grafu — vidět kde je limit při dynamické Y ose
- Barva >100% konzistentní napříč celým UI (graf, progress bar, tooltip)

### Extra usage vizualizace — spend bar
- Nový samostatný progress bar v taskbaru: zobrazuje USD spotřebu vs limit (např. $5.40 / $20.00)
- Viditelnost konfigurovatelná v Settings (Phase 11 přidá UI, Phase 10 připraví infrastrukturu)
- Research: zjistit jak získat extra usage spend data z API — zatím neznámé (OAuth token, rate limit headery to nevracejí?)
- Pokud API neposkytuje spend data: zvážit manuální tracking (počítat cenu z inference callů) nebo uživatelem zadaný limit v settings

### Chart Y scaling
- Y osa končí na 100% pokud všechna data ≤ 100%
- Rozšíří se na max hodnotu jen při extra usage (>100%)

### Progress bar >100%
- Bar na 100% šířky (plný), ale novou barvou (konzistentní s grafem >100%)
- Text zobrazí reálnou hodnotu (např. "137%")

### Claude's Discretion
- Přesná barva pro >100% (tmavě červená nebo fialová — hlavně jasně odlišitelná od červené)
- Y axis rounding/padding při dynamickém škálování
- Interní datová struktura pro key-to-window mapping
- Způsob získání extra usage spend dat (závisí na výsledku research)

</decisions>

<specifics>
## Specific Ideas

- Barevné prahy konzistentní s Phase 8: zelená (#4CAF50) <75%, oranžová (#FF9800) 75-90%, červená (#F44336) ≥90%, nová barva ≥100%
- Substring match pokryje i budoucí varianty (např. "unified-5h_opus" automaticky matchne "5h" → 2d)
- 100% čára v grafu jen při dynamické Y ose (ne když max ≤ 100%)
- Extra usage bar: inspirace Anthropic Settings > Usage stránkou — spend bar s USD hodnotou

</specifics>

<code_context>
## Existing Code Insights

### Reusable Assets
- `HistoryChart.xaml.cs` — time-anchored rendering z Phase 9 (ProcessGaps, BuildColorSegments, TimestampToX). Y výpočet nutno upravit pro dynamický max.
- `HistoryChart.SetData(IReadOnlyList<HistoryRecord>, string)` — stávající API zůstane, přidá se time window filtrování
- `AccountPanel.SetBarColor()` — barva dle utilization, přidat práh ≥100
- `PopupWindow.GetBarBrush()` — stejná logika, přidat >100% barvu
- `ExtractRateLimits()` v ClaudeApiClient — dynamicky parsuje všechny `anthropic-ratelimit-*` headery, už teď vrací model-specifické klíče
- `AccountPanel` + `CreateBarEntry()` — reuse pro extra usage bar (stejný vizuální styl)

### Established Patterns
- WPF Canvas + Polyline multicolor segmenty — zachovat, přidat 4. barvu pro >100%
- SegmentColor enum (Green, Orange, Red) — rozšířit o novou barvu
- Barvy definované inline (`Color.FromRgb(...)`) — konzistentně přidat novou
- `EnsureBarCount()` dynamicky přidává/odebírá bary — použít i pro extra usage bar

### Integration Points
- `HistoryChart.SetData()` — přidat time window filtrování (ořízne data podle per-key okna)
- `HistoryChart.RenderChart()` — windowStart/windowEnd se změní z fixních 14d na per-key okno
- `AccountPanel.SetBarColor()` a `PopupWindow.GetBarBrush()` — nový práh ≥100
- `ProgressBar.Maximum` — zůstane 100, ale šířka baru clampnuta na 100%
- `ClaudeApiClient` — potenciální nový endpoint/metoda pro extra usage spend data

</code_context>

<deferred>
## Deferred Ideas

None — diskuze zůstala v rámci scope fáze.

</deferred>

---

*Phase: 10-per-key-chart-windows*
*Context gathered: 2026-03-11*
