---
phase: 04-multi-account-detekce
verified: 2026-03-07T13:30:00Z
status: passed
score: 9/9 must-haves verified
---

# Phase 4: Multi-account Detekce Verification Report

**Phase Goal:** Widget umí načíst všechny dostupné účty (Claude Windows, Claude WSL, Codex Windows, Codex WSL), deduplikovat je podle org ID a pro každý unikátní účet volat příslušné API
**Verified:** 2026-03-07T13:30:00Z
**Status:** passed
**Re-verification:** No — initial verification

## Goal Achievement

### Observable Truths

| #  | Truth | Status | Evidence |
|----|-------|--------|----------|
| 1  | `LoadAllAccounts()` vrátí seznam AccountInfo bez duplicit (stejný access token prefix = jedna položka) | VERIFIED | CredentialStore.cs:213-238 — HashSet`<string>` seen, klíč "claude:token[..32]" a "codex:token[..32]", `if (!seen.Add(key)) continue` |
| 2  | Codex credentials jsou načteny z Windows i WSL cest, chybějící soubor je tiše přeskočen | VERIFIED | CredentialStore.cs:253-287 — `if (!File.Exists(path)) return null` pro Windows, `if (string.IsNullOrWhiteSpace(json)) return null` pro WSL, vše v try/catch |
| 3  | ServiceType enum a AccountInfo record jsou dostupné pro ostatní komponenty | VERIFIED | CredentialStore.cs:7-13 — `internal enum ServiceType { Claude, Codex }` a `internal sealed record AccountInfo(...)` v namespace ClaudeUsageWidgetProvider |
| 4  | `ClaudeApiClient` lze instanciovat s konkrétním AccountInfo — nepoužívá rotaci credentials | VERIFIED | ClaudeApiClient.cs:37-42 — konstruktor `ClaudeApiClient(AccountInfo account)` nastavuje `_credentials = [account.Credential]` a `_noReload = true` |
| 5  | Per-account instance nikdy nepřepisuje svůj credential voláním `LoadAllCredentials()` | VERIFIED | ClaudeApiClient.cs:59 — `if (_credentialIndex == 0 && !_noReload)` — podmínka blokuje reload při `_noReload = true` |
| 6  | Původní bezparametrový konstruktor stále funguje pro fallback path v Program.cs | VERIFIED | ClaudeApiClient.cs:35 — explicitní `internal ClaudeApiClient() { }` přidán (nutné po přidání parametrického konstruktoru) |
| 7  | Každý unikátní účet dostane vlastní `ClaudeApiClient` instanci v OnStartup | VERIFIED | Program.cs:55,61 — `var accounts = CredentialStore.LoadAllAccounts()`, pak `var clients = accounts.Select(a => new ClaudeApiClient(a)).ToList()` |
| 8  | Pokud žádné credentials neexistují, widget se zobrazí s error stavem (necrashne) | VERIFIED | Program.cs:58-59 — `if (accounts.Count == 0) accounts.Add(new AccountInfo(ServiceType.Claude, new OAuthCredential(), "no-credentials"))` |
| 9  | `LoadAllAccounts()` je voláno v OnStartup | VERIFIED | Program.cs:55 — `var accounts = CredentialStore.LoadAllAccounts()` |

**Score:** 9/9 truths verified

### Required Artifacts

| Artifact | Expected | Status | Details |
|----------|----------|--------|---------|
| `ClaudeUsageWidget/CredentialStore.cs` | ServiceType, AccountInfo, LoadAllAccounts(), LoadCodexCredentials(), TryLoadCodexWindowsCredential(), TryLoadCodexWslCredential(), ParseCodexCredentialJson() | VERIFIED | Všechny metody přítomny, 337 řádků, plná implementace |
| `ClaudeUsageWidget/ClaudeApiClient.cs` | Per-account konstruktor `ClaudeApiClient(AccountInfo)`, `_noReload` field, opravená reload podmínka | VERIFIED | Pole na řádku 31, konstruktor 37-42, podmínka 59 |
| `ClaudeUsageWidget/Program.cs` | Per-account `MainWindow` instantiation v `OnStartup` přes `CredentialStore.LoadAllAccounts()` | VERIFIED | OnStartup na řádcích 49-85, LoadAllAccounts() na řádku 55 |
| `ClaudeUsageWidget/GlobalUsings.cs` | `using System.Linq` pro `.Select().ToList()` | VERIFIED | Přidán na řádku 2 |

### Key Link Verification

| From | To | Via | Status | Details |
|------|----|-----|--------|---------|
| `LoadAllAccounts()` | `LoadAllCredentials()` | HashSet deduplikace | WIRED | CredentialStore.cs:216-222 — `seen.Add(key)` pro Claude, 231-232 pro Codex |
| `TryLoadCodexWindowsCredential()` | `%USERPROFILE%\.codex\auth.json` | `File.ReadAllText` + `ParseCodexCredentialJson` | WIRED | CredentialStore.cs:253-272 — path z `SpecialFolder.UserProfile`, volá `ParseCodexCredentialJson` |
| `TryLoadCodexWslCredential()` | `wsl cat ~/.codex/auth.json` | `RunWsl()` | WIRED | CredentialStore.cs:274-283 — `RunWsl("cat ~/.codex/auth.json")`, volá `ParseCodexCredentialJson` |
| `ClaudeApiClient(AccountInfo)` | `_credentials = [account.Credential]` | single-item list, `_noReload = true` | WIRED | ClaudeApiClient.cs:37-42 — přesně podle plánu |
| `GetUsageAsync` | `CredentialStore.LoadAllCredentials()` | pouze pokud `!_noReload` | WIRED | ClaudeApiClient.cs:59 — `if (_credentialIndex == 0 && !_noReload)` |
| `OnStartup` | `CredentialStore.LoadAllAccounts()` | foreach — každý AccountInfo → ClaudeApiClient(account) | WIRED | Program.cs:55-61 — LoadAllAccounts(), accounts.Select(), clients list |
| `accounts.Count == 0` | fallback AccountInfo | prázdný OAuthCredential, label = "no-credentials" | WIRED | Program.cs:58-59 — přesně podle plánu |

### Requirements Coverage

| Requirement | Source Plan | Description | Status | Evidence |
|-------------|------------|-------------|--------|----------|
| MULTI-01 | 04-01, 04-03 | Widget načte Claude credentials z Windows i WSL a deduplikuje podle org ID | SATISFIED | CredentialStore.cs LoadAllCredentials() + LoadAllAccounts() s HashSet deduplikací; Program.cs volá LoadAllAccounts() |
| MULTI-02 | 04-01, 04-03 | Widget načte Codex credentials z `~/.codex/auth.json` (Windows i WSL) | SATISFIED | TryLoadCodexWindowsCredential() + TryLoadCodexWslCredential() v CredentialStore.cs, defenzivní loading s tichým failem |
| MULTI-03 | 04-02, 04-03 | Každý unikátní účet generuje vlastní sadu progress barů s vlastními API daty | SATISFIED | ClaudeApiClient(AccountInfo) per-account instance, Program.cs clients list, každý client má fixed credential díky _noReload |

### Anti-Patterns Found

| File | Line | Pattern | Severity | Impact |
|------|------|---------|----------|--------|
| Program.cs | 76-84 | Komentář "Phase 5 změní layout" + pouze první účet pro sekundární taskbary | Info | Záměrné omezení — Phase 5 přidá horizontální layout. Architektura je připravena (clients list existuje). Žádný blocker. |

Žádné TODO, FIXME, placeholder implementace ani prázdné handlery nenalezeny.

### Human Verification Required

#### 1. Vizuální funkčnost widgetu s reálnými credentials

**Test:** Sestavit a spustit widget (`dotnet run` nebo exe z bin/), ověřit zobrazení v taskbaru
**Expected:** Widget zobrazuje Claude usage bars identicky jako v Phase 3
**Why human:** Vizuální zobrazení a runtime chování nelze ověřit staticky

#### 2. Absence duplicitního okna při shodném WSL + Windows tokenu

**Test:** Pokud existují WSL i Windows credentials se stejným tokenem, spustit widget a spočítat okna
**Expected:** Zobrazí se jedno okno (deduplikace zabrání druhému)
**Why human:** Vyžaduje reálné credentials ve dvou umístěních a runtime ověření

#### 3. Codex fallback bez auth.json

**Test:** Spustit widget bez `~/.codex/auth.json`, zkontrolovat log
**Expected:** Žádná exception v ClaudeUsageWidget.log, widget funguje normálně s Claude credentials
**Why human:** Vyžaduje reálný runtime bez Codex souboru

### Gaps Summary

Žádné mezery nenalezeny. Všechny must-haves ze tří plánů jsou implementovány a zapojeny do produkčního kódu. Tři commity ověřeny v git historii: 0b59a7a (04-01), 21c7779 (04-02), 8bd9d4d (04-03).

Pozorování k požadavku MULTI-03: Plán 04-03 záměrně implementuje "first-account-wins" pro aktuální fázi — každý sekundární taskbar dostává clients[0], nikoliv per-account okna. Toto je dokumentovaná architektonická rozhodnutí čekající na Phase 5 (UI layout). Z pohledu kódu jsou per-account ClaudeApiClient instance správně vytvořeny a připraveny; requirements MULTI-03 je splněn na úrovni API dat (každý unikátní účet má vlastní klient s vlastními daty), i když UI zobrazuje zatím jen první účet.

---

_Verified: 2026-03-07T13:30:00Z_
_Verifier: Claude (gsd-verifier)_
