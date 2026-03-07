---
status: complete
phase: 02-widget-ui-a-pozicovani
source: [02-01-SUMMARY.md, 02-02-SUMMARY.md, 02-03-SUMMARY.md]
started: 2026-03-07T00:00:00Z
updated: 2026-03-07T00:01:00Z
---

## Current Test

[testing complete]

## Tests

### 1. Cold Start Smoke Test
expected: Ukonči běžící instanci widgetu. Spusť aplikaci znovu. Aplikace nastartuje bez chyb/dialogů, widget se zobrazí.
result: pass

### 2. Widget na taskbaru — borderless tmavé okno
expected: Widget je viditelný na taskbaru jako tmavé okno (#1C1C1C). Nemá záhlaví, rámeček ani tlačítka. Není v taskbaru (ShowInTaskbar=False). Je vždy nahoře (topmost).
result: pass

### 3. Dva progress bary — 5h a 7d
expected: Widget zobrazuje dva horizontální progress bary. Horní = 5h limit, dolní = 7d limit. Bary jsou ploché (bez border-radius). Barva: zelená <75%, oranžová 75-90%, červená >=90% využití.
result: pass

### 4. Pozice widgetu — vlevo od system tray
expected: Widget je umístěn těsně vlevo od system tray (hodiny/notifikační oblast). Výška odpovídá výšce taskbaru. Widget není přes jiné ikony ani mimo obrazovku.
result: pass

### 5. Tray watch timer — přizpůsobení šířce tray
expected: Otevři/zavři nějakou tray aplikaci nebo změň nastavení tray ikonek. Widget se přesune, aby zůstal těsně vlevo od aktuálního okraje tray oblasti (do ~2 sekund).
result: pass

### 6. Tooltip s časem do resetu
expected: Najeď myší na widget. Zobrazí se tooltip s časem do resetu pro 5h limit i 7d limit (např. "5h reset: za 2h 15min\n7d reset: za 3d 4h").
result: pass

### 7. Context menu Quit
expected: Klikni pravým tlačítkem na widget. Zobrazí se context menu s položkou "Quit". Kliknutí na Quit ukončí aplikaci.
result: pass

### 8. Reálná data z API
expected: Progress bary zobrazují skutečné hodnoty z Claude API (ne placeholder 50%/80%). Hodnoty odpovídají aktuálnímu využití tvojí Claude session.
result: pass

## Summary

total: 8
passed: 8
issues: 0
pending: 0
skipped: 0

## Gaps

[none yet]
