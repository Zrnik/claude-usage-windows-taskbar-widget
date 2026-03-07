---
phase: 01-cleanup
verified: 2026-03-06T09:50:00Z
status: passed
score: 6/6 must-haves verified
gaps: []
human_verification:
  - test: "Spustit ClaudeUsageWidgetProvider.exe na Windows a ověřit že aplikace naběhne bez pádu"
    expected: "Aplikace se spustí (prázdné okno nebo tichý start), bez MSIX chybové hlášky"
    why_human: "WPF App bez MainWindow — runtime chování nelze ověřit staticky (app.Run() bez Window se může okamžitě ukončit)"
---

# Phase 1: Cleanup — Verification Report

**Phase Goal:** Odstranit veškerý MSIX/Widget provider balast a připravit základ pro Phase 2 (UI). Kompilující WPF projekt s ClaudeApiClient.cs a CredentialStore.cs, bez Widget provider kódu.
**Verified:** 2026-03-06T09:50:00Z
**Status:** PASSED
**Re-verification:** Ne — initial verification

## Goal Achievement

### Observable Truths

| # | Truth | Status | Evidence |
|---|-------|--------|----------|
| 1 | `dotnet build` projde bez chyb | VERIFIED | "Vytváření sestavení bylo úspěšně dokončeno. 0 upozornění. Počet chyb: 0" |
| 2 | ClaudeApiClient.cs a CredentialStore.cs existují beze změny | VERIFIED | Oba soubory přítomny, substantive (156 resp. 118 řádků), zachovány přes commits |
| 3 | WidgetProvider.cs, FactoryHelper.cs, ByparrClient.cs neexistují | VERIFIED | Všechny tři soubory smazány + Package.appxmanifest + Templates/ + Assets/ + ProviderAssets/ |
| 4 | V .csproj není reference na Microsoft.WindowsAppSDK ani Microsoft.Windows.SDK.BuildTools | VERIFIED | grep WindowsAppSDK/EnableMsixTooling/WindowsPackageType vrátil prázdný výstup |
| 5 | V .csproj je `<UseWPF>true</UseWPF>` | VERIFIED | Řádek 10: `<UseWPF>true</UseWPF>` přítomen |
| 6 | Výsledný .exe lze spustit přímo bez MSIX instalace | VERIFIED | `bin/Debug/net8.0-windows10.0.22621.0/ClaudeUsageWidgetProvider.exe` existuje, OutputType=Exe, žádný MSIX packaging |

**Score:** 6/6 truths verified

### Required Artifacts

| Artifact | Expected | Status | Details |
|----------|----------|--------|---------|
| `ClaudeUsageWidgetProvider.csproj` | WPF projekt bez MSIX závislostí | VERIFIED | UseWPF=true, OutputType=Exe, žádné PackageReference na AppSDK |
| `Program.cs` | WPF Application startup | VERIFIED | App : Application, [STAThread], app.Run(), TimeFormatter helper |
| `ClaudeApiClient.cs` | Zachovaný API client | VERIFIED | 156 řádků, HttpClient, rate limit headers, UsageData DTO |
| `CredentialStore.cs` | Zachovaný credential store | VERIFIED | 118 řádků, Windows + WSL paths, OAuthCredential |
| `GlobalUsings.cs` | Auto-fix pro chybějící usings | VERIFIED | global using System.IO + System.Net.Http (nezahrnut v PLAN, přidán jako oprava bug) |

### Key Link Verification

| From | To | Via | Status | Details |
|------|----|-----|--------|---------|
| Program.cs | ClaudeApiClient.cs | WPF Application class | INTENTIONALLY NOT WIRED | Záměrné — PLAN explicitně říká "Phase 2 přidá MainWindow." Program.cs je minimální stub. ClaudeApiClient.cs je zachován jako základ, zapojení proběhne v Phase 2. |

**Poznámka k key link:** Absence přímého volání ClaudeApiClient z Program.cs není gap — PLAN i SUMMARY toto výslovně zmiňují jako záměr. Fáze 1 má za cíl pouze zachovat ClaudeApiClient.cs, ne ho zapojit. Zapojení patří do Phase 2.

### Requirements Coverage

| Requirement | Popis | Status | Evidence |
|-------------|-------|--------|----------|
| CLEAN-01 | Odstranit MSIX/AppX packaging konfiguraci z projektu | SATISFIED | .csproj bez EnableMsixTooling, WindowsPackageType, AppSDK PackageReference |
| CLEAN-02 | Odstranit Windows Widget provider kód (WidgetProvider.cs a závislosti) | SATISFIED | WidgetProvider.cs, FactoryHelper.cs, ByparrClient.cs, Package.appxmanifest, Templates/, Assets/, ProviderAssets/ — vše smazáno |
| CLEAN-03 | Zachovat a případně vyčistit ClaudeApiClient.cs jako core logiku | SATISFIED | ClaudeApiClient.cs zachován beze změny (156 řádků, plná implementace) |
| CLEAN-04 | Výsledný projekt je čisté WPF exe bez balastního kódu | SATISFIED | UseWPF=true, OutputType=Exe, pouze 5 souborů v projektu |
| LIFE-02 | Spustitelná jako exe bez MSIX/AppX packaging | SATISFIED | ClaudeUsageWidgetProvider.exe v bin/, žádný MSIX wrapper |

Všech 5 requirements z PLAN frontmatter pokryto a splněno.

**Orphaned requirements check:** REQUIREMENTS.md mapuje CLEAN-01, CLEAN-02, CLEAN-03, CLEAN-04, LIFE-02 do Phase 1 — shoduje se přesně s PLAN frontmatter. Žádné orphaned requirements.

### Anti-Patterns Found

Žádné anti-patterns nalezeny v souborech dotčených touto fází (Program.cs, .csproj, GlobalUsings.cs).

ClaudeApiClient.cs a CredentialStore.cs neobsahují TODO/FIXME/placeholder komentáře — jsou plně implementované.

### Human Verification Required

#### 1. Runtime chování exe

**Test:** Spustit `ClaudeUsageWidgetProvider.exe` přímo na Windows (bez instalace)
**Expected:** Aplikace se spustí bez pádu a bez "requires installation" chybové hlášky; může se okamžitě ukončit (WPF App bez MainWindow toto dělá)
**Why human:** WPF `app.Run()` bez `MainWindow` se může ihned ukončit — runtime chování nelze ověřit staticky. Ověřuje LIFE-02 v praxi.

### Gaps Summary

Žádné gaps. Všechny 6 must-have truths jsou verified, všech 5 requirements splněno, oba commity (7f9d953, 922c950) existují a odpovídají popsaným změnám.

Projekt je připraven pro Phase 2 (Widget UI a pozicování).

---

_Verified: 2026-03-06T09:50:00Z_
_Verifier: Claude (gsd-verifier)_
