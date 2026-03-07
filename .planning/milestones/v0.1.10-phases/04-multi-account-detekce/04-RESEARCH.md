# Phase 4: Multi-account detekce - Research

**Researched:** 2026-03-07
**Domain:** C# credential loading — multi-source deduplikace, Codex auth.json, per-account API klienti
**Confidence:** HIGH (Claude stack), MEDIUM (Codex auth formát)

## Summary

Phase 4 rozšiřuje stávající `CredentialStore` a `ClaudeApiClient` o podporu více účtů. Infrastruktura pro čtení z Windows i WSL je kompletně hotová — `LoadAllCredentials()`, `TryReadWslCredential()`, `TryReadCredential()`, `RunWsl()` všechno existuje a funguje. Fáze přidá deduplikaci podle org ID a separátní načítání Codex credentials.

Klíčová otázka pro Codex: přesný formát `auth.json` není veřejně zdokumentovaný. Dokumentace říká jen "contains access tokens". Implementace musí být defenzivní — pokusit se načíst známé pole (`access_token`), při neúspěchu přeskočit tiše. Codex používá OpenAI API (`api.openai.com/v1/...`), ne Anthropic API — rate limit headers jsou odlišné (`x-ratelimit-*`, ne `anthropic-ratelimit-unified-*`).

Org ID pro deduplikaci Clauda: přístupový token je JWT, ale opaque `sk-ant-oat01-...` — není z něj org ID dekódovatelné přímo. Správné řešení je volat API a extrahovat org ID z response, nebo deduplikovat pouze pomocí hash access tokenu. Nejjednodušší přístup: **deduplikovat podle access tokenu** (stejný token = stejný účet).

**Primární doporučení:** Zavést `AccountInfo` record (service, credential, cached usage), rozšířit `CredentialStore` o `LoadCodexCredentials()`, v `Program.cs` sestavit seznam účtů a každý MainWindow dostat vlastní `ClaudeApiClient` instanci vázanou na jeden účet.

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|-----------------|
| MULTI-01 | Widget načte Claude credentials z Windows i WSL, deduplikuje podle org ID | `CredentialStore.LoadAllCredentials()` existuje — přidat deduplikaci; org ID = hash access tokenu (nejjednodušší) nebo volat org API |
| MULTI-02 | Widget načte Codex credentials z `~/.codex/auth.json` (Windows i WSL) | Nová metoda `LoadCodexCredentials()` v `CredentialStore` — analogická k existující Claude logice; formát auth.json = MEDIUM confidence |
| MULTI-03 | Každý unikátní účet generuje vlastní sadu progress barů s vlastními API daty | Každý `MainWindow` dostane vlastní `ClaudeApiClient` instanci; `Program.cs` vytváří okna per-account |
</phase_requirements>

---

## Standard Stack

### Core (vše již v projektu)
| Komponenta | Verze | Účel |
|------------|-------|------|
| `CredentialStore.cs` | stávající | Načítání credentials — rozšířit o Codex |
| `ClaudeApiClient.cs` | stávající | API klient — instancovat per-account |
| `Program.cs` | stávající | Startup — sestavit seznam AccountInfo |
| `MainWindow.xaml.cs` | stávající | UI okno — již dostává `ClaudeApiClient` v konstruktoru |

### Nové koncepty pro Phase 4
| Koncept | Kde | Účel |
|---------|-----|------|
| `AccountInfo` record | nový soubor nebo `CredentialStore.cs` | Zapouzdřuje service type + credential + label |
| `ServiceType` enum | tamtéž | `Claude` / `Codex` — pro budoucí UI Phase 5 |
| `LoadCodexCredentials()` | `CredentialStore.cs` | Načte `~/.codex/auth.json` z Windows i WSL |

### Codex API
| Vlastnost | Hodnota | Confidence |
|-----------|---------|------------|
| Base URL | `https://api.openai.com/v1/` | HIGH |
| Auth header | `Authorization: Bearer {access_token}` | HIGH |
| Rate limit headers | `x-ratelimit-limit-requests`, `x-ratelimit-remaining-requests`, `x-ratelimit-reset-requests` | HIGH |
| Unified utilization header | NEEXISTUJE — OpenAI nepoužívá `anthropic-ratelimit-unified-*` | HIGH |
| auth.json formát | `{"access_token": "..."}` — přesná pole LOW confidence | LOW |

**Instalace:** Žádné nové NuGet packages.

## Architecture Patterns

### Doporučená struktura změn

```
CredentialStore.cs
├── Přidat: enum ServiceType { Claude, Codex }
├── Přidat: record AccountInfo(ServiceType Service, OAuthCredential Credential)
├── Přidat: LoadCodexCredentials() → List<AccountInfo>
├── Přidat: LoadAllAccounts() → List<AccountInfo>  ← Claude + Codex, deduplikováno
└── Zachovat: LoadAllCredentials(), LoadCredential() — zpětná kompatibilita

Program.cs (OnStartup)
├── Místo: var apiClient = new ClaudeApiClient();
└── Nahradit: var accounts = CredentialStore.LoadAllAccounts();
            foreach (var account in accounts)
                new MainWindow(new ClaudeApiClient(account), ...).Show();
```

### Pattern 1: AccountInfo jako přepravka
**Co:** Jednoduchý record nesoucí vše co MainWindow potřebuje vědět o účtu.
**Kdy:** Vždy — vyhnout se předávání loose parametrů.

```csharp
// Přidat do CredentialStore.cs
internal enum ServiceType { Claude, Codex }

internal sealed record AccountInfo(
    ServiceType Service,
    OAuthCredential Credential,
    string Label  // např. "claude-wsl", "codex-windows" — pro debug
);
```

### Pattern 2: Deduplikace Claude účtů
**Co:** Windows i WSL mohou mít stejné credentials (uživatel přihlášen přes oba). Deduplikovat podle access tokenu.
**Proč access token, ne org ID:** Org ID není v credentials souboru uloženo — bylo by nutné dělat API call. Access token je dostatečný identifikátor — stejný token = identické limity.

```csharp
// V LoadAllAccounts():
var claudeCredentials = LoadAllCredentials(); // stávající metoda
var seen = new HashSet<string>();
foreach (var cred in claudeCredentials)
{
    // První N znaků tokenu jako dedup klíč (tokens jsou jedinečné per-session)
    var key = cred.AccessToken[..Math.Min(32, cred.AccessToken.Length)];
    if (seen.Add(key))
        accounts.Add(new AccountInfo(ServiceType.Claude, cred, $"claude-{LabelFrom(cred.SourcePath)}"));
}
```

### Pattern 3: Codex credential loading
**Co:** Analogický pattern k existujícímu Claude credential loadingu.
**Cesta Windows:** `%USERPROFILE%\.codex\auth.json`
**Cesta WSL:** `~/.codex/auth.json` (přes `wsl -- cat`)

```csharp
private static AccountInfo? TryLoadCodexWindowsCredential()
{
    var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    var path = Path.Combine(userProfile, ".codex", "auth.json");
    if (!File.Exists(path)) return null;
    try
    {
        var json = File.ReadAllText(path);
        var cred = ParseCodexCredentialJson(json, path);
        return cred != null ? new AccountInfo(ServiceType.Codex, cred, "codex-windows") : null;
    }
    catch { return null; }
}

private static AccountInfo? TryLoadCodexWslCredential()
{
    try
    {
        var json = RunWsl("cat ~/.codex/auth.json");
        if (string.IsNullOrWhiteSpace(json)) return null;
        var cred = ParseCodexCredentialJson(json, "wsl:~/.codex/auth.json");
        return cred != null ? new AccountInfo(ServiceType.Codex, cred, "codex-wsl") : null;
    }
    catch { return null; }
}
```

### Pattern 4: Codex JSON parsing (defenzivní)
**Problém:** Přesná pole auth.json nejsou veřejně zdokumentovaná — LOW confidence.
**Strategie:** Zkusit více kandidátních polí (`access_token`, `accessToken`, `token`), vrátit null pokud nic nevyhovuje.

```csharp
private static OAuthCredential? ParseCodexCredentialJson(string json, string sourcePath)
{
    try
    {
        var doc = JsonNode.Parse(json);
        if (doc == null) return null;

        // Zkusit různé možné field names (dokumentace nespecifikuje)
        var token = doc["access_token"]?.GetValue<string>()
                 ?? doc["accessToken"]?.GetValue<string>()
                 ?? doc["token"]?.GetValue<string>();

        if (string.IsNullOrEmpty(token)) return null;

        // Codex tokeny mohou být static API keys nebo OAuth access tokeny
        // RefreshToken nemusí existovat — best-effort
        var refreshToken = doc["refresh_token"]?.GetValue<string>()
                        ?? doc["refreshToken"]?.GetValue<string>()
                        ?? "";

        var expiresAt = doc["expires_at"]?.GetValue<long>()  // epoch ms
                     ?? doc["expiresAt"]?.GetValue<long>()
                     ?? long.MaxValue; // API key = nikdy nevyprší

        return new OAuthCredential
        {
            AccessToken = token,
            RefreshToken = refreshToken,
            ExpiresAt = expiresAt,
            SourcePath = sourcePath
        };
    }
    catch { return null; }
}
```

### Pattern 5: Per-account ClaudeApiClient
**Co:** `ClaudeApiClient` dostane `AccountInfo` v konstruktoru a používá pouze daný credential.
**Proč:** Stávající `ClaudeApiClient` interně rotuje přes credentials (fallback logika) — pro multi-account potřebujeme pevně daný credential per-instance.

```csharp
// Rozšířit konstruktor ClaudeApiClient:
internal ClaudeApiClient(AccountInfo account)
{
    _account = account;
    // Přepsat _credentials na single-item list (bez rotace)
}
```

Alternativa: Přidat metodu `SetSingleCredential(OAuthCredential)` a nevolat `LoadAllCredentials()` v `GetUsageAsync`.

### Codex rate limit API
**Problém:** Codex (OpenAI) nevrací `anthropic-ratelimit-unified-*` headers. OpenAI vrací `x-ratelimit-*` headers, ale ty jsou per-RPM/TPM, ne kumulativní utilization.

**Možnosti:**
1. **Volat OpenAI API a zobrazit zbývající requesty** — `x-ratelimit-remaining-requests` / `x-ratelimit-limit-requests` = procento využití. Dostupné v response headers.
2. **Codex nemá unified utilization** — zobrazit jen "connected" nebo jednoduchý indikátor.

**Doporučení:** Implementovat pro Codex stejný pattern jako pro Claude (API call + headers), ale mapovat `x-ratelimit-remaining-requests` na utilization. Pokud headers chybí, vrátit null a widget tiše přeskočí.

```csharp
// Pro Codex v FetchUsageAsync():
// POST https://api.openai.com/v1/chat/completions
// Authorization: Bearer {access_token}
// Minimal body: {"model":"gpt-4o-mini","messages":[{"role":"user","content":"."}],"max_tokens":1}
//
// Response headers:
//   x-ratelimit-limit-requests: 500
//   x-ratelimit-remaining-requests: 487
//   x-ratelimit-reset-requests: 2025-03-07T00:00:00Z  (ISO 8601, ne unix!)
```

**Upozornění:** Reset time pro OpenAI headers je ISO 8601 string, ne unix timestamp jako u Anthropic.

### Anti-Patterns to Avoid
- **Jeden `ClaudeApiClient` pro všechny účty:** Stávající fallback logika by mísila credentials. Každý účet musí mít vlastní instanci.
- **Deduplikace podle refresh tokenu:** Refresh token se může změnit po refresh — použít access token prefix.
- **Volat Codex API jako Claude API:** Odlišné base URL, odlišné auth header, odlišné rate limit headers.
- **Crashnout při chybějící Codex auth:** Codex credentials jsou optional — vždy `try/catch` s tiché přeskočení.

## Don't Hand-Roll

| Problém | Nebudovat | Použít místo | Proč |
|---------|-----------|--------------|------|
| WSL file reading | vlastní named pipe | existující `RunWsl("cat ...")` | Funguje, testované |
| JSON parsing | custom string split | `JsonNode.Parse()` | Již importováno |
| HTTP client | nový HttpClient per-account | static `HttpClient` (thread-safe) | Stávající pattern |

**Klíčový insight:** `CredentialStore` a `RunWsl()` jsou reusable pro Codex bez změn — pouze nová parse logika.

## Common Pitfalls

### Pitfall 1: Codex expires_at formát
**Co se stane:** Codex může ukládat `expires_at` jako ISO 8601 string (`"2025-03-15T10:30:00Z"`), ne unix epoch. `OAuthCredential.ExpiresAt` je `long` (ms).
**Jak zabránit:** V `ParseCodexCredentialJson` zkusit parsovat jako long, pak jako ISO string:
```csharp
long expiresAt = 0;
var expiresNode = doc["expires_at"] ?? doc["expiresAt"];
if (expiresNode != null)
{
    try { expiresAt = expiresNode.GetValue<long>(); }
    catch
    {
        if (DateTimeOffset.TryParse(expiresNode.GetValue<string>(), out var dto))
            expiresAt = dto.ToUnixTimeMilliseconds();
    }
}
```

### Pitfall 2: Codex API key vs OAuth token
**Co se stane:** Uživatel může mít v `auth.json` statický API key (začíná `sk-...`), ne OAuth token. Statický key nikdy nevyprší — `IsExpired` by vrátilo true pokud `ExpiresAt = 0`.
**Jak zabránit:** Pokud `ExpiresAt = 0` nebo není přítomný, nastavit na `long.MaxValue` (nikdy nevyprší). Refresh token flow přeskočit pokud `RefreshToken` je prázdný.

### Pitfall 3: Duplicitní Codex účet (Windows + WSL stejný token)
**Co se stane:** Pokud uživatel má stejný `auth.json` přístupný přes Windows i WSL (sdílený home), načte se dvakrát.
**Jak zabránit:** Stejná deduplikace jako pro Claude — hash prefix access tokenu v `HashSet<string>`.

### Pitfall 4: `MainWindow` dostane null apiClient
**Co se stane:** Pokud `LoadAllAccounts()` vrátí prázdný seznam, `OnStartup` nevytvoří žádné okno. App se spustí bez UI.
**Jak zabránit:** Zachovat fallback — pokud žádné účty, vytvořit jedno okno s `ClaudeApiClient` bez credentials (zobrazí error stav).

### Pitfall 5: ClaudeApiClient.GetUsageAsync přepisuje credentials
**Stávající kód:**
```csharp
if (_credentialIndex == 0)
    _credentials = CredentialStore.LoadAllCredentials(); // přepíše per-account credential!
```
**Co se stane:** Per-account `ClaudeApiClient` by při každém refresh přepsal credential z disku.
**Jak zabránit:** Per-account klient NESMÍ volat `LoadAllCredentials()`. Přidat flag nebo jiný konstruktor.

### Pitfall 6: OpenAI reset time je ISO 8601, ne unix
**Co se stane:** `ParseUnixTimestamp()` selže na `"2025-03-07T00:00:00Z"` — vrátí `DateTimeOffset.UtcNow`.
**Jak zabránit:** Pro Codex použít `DateTimeOffset.TryParse()` jako fallback.

## Code Examples

### LoadAllAccounts — kompletní flow
```csharp
// V CredentialStore.cs
public static List<AccountInfo> LoadAllAccounts()
{
    var accounts = new List<AccountInfo>();
    var seen = new HashSet<string>();

    // 1. Claude credentials (Windows + WSL), deduplikovat
    foreach (var cred in LoadAllCredentials())
    {
        var key = "claude:" + cred.AccessToken[..Math.Min(32, cred.AccessToken.Length)];
        if (!seen.Add(key)) continue;
        accounts.Add(new AccountInfo(ServiceType.Claude, cred,
            cred.SourcePath.StartsWith("wsl:") ? "claude-wsl" : "claude-windows"));
    }

    // 2. Codex credentials (Windows + WSL), deduplikovat
    foreach (var info in LoadCodexCredentials())
    {
        var key = "codex:" + info.Credential.AccessToken[..Math.Min(32, info.Credential.AccessToken.Length)];
        if (!seen.Add(key)) continue;
        accounts.Add(info);
    }

    return accounts;
}

private static List<AccountInfo> LoadCodexCredentials()
{
    var result = new List<AccountInfo>();
    var winCred = TryLoadCodexWindowsCredential();
    if (winCred != null) result.Add(winCred);
    var wslCred = TryLoadCodexWslCredential();
    if (wslCred != null) result.Add(wslCred);
    return result;
}
```

### Program.cs — per-account MainWindow
```csharp
// V OnStartup:
var accounts = CredentialStore.LoadAllAccounts();

// Fallback: žádné accounts = jeden prázdný klient (zobrazí error)
if (accounts.Count == 0)
    accounts.Add(new AccountInfo(ServiceType.Claude,
        new OAuthCredential(), "no-credentials"));

foreach (var account in accounts)
{
    var client = new ClaudeApiClient(account); // per-account instance
    // Vytvořit MainWindow pro každý taskbar (existující logika)
    var primaryHwnd = FindWindow("Shell_TrayWnd", null);
    var w = new MainWindow(client, primaryHwnd, isPrimary: true);
    if (account == accounts[0]) MainWindow = w;
    w.Show();
    // TODO Phase 5: multi-account windows se přidají horizontálně
}
```

### ClaudeApiClient — per-account konstruktor
```csharp
// Přidat konstruktor:
internal ClaudeApiClient(AccountInfo account)
{
    _account = account;
    _credentials = [account.Credential]; // single credential, žádná rotace
    _credentialIndex = 0;
    _noReload = true; // přidat field: zabraňuje přepsání v GetUsageAsync
}

// Upravit GetUsageAsync:
if (_credentialIndex == 0 && !_noReload)
    _credentials = CredentialStore.LoadAllCredentials();
```

## State of the Art

| Oblast | Starý přístup | Nový přístup | Dopad |
|--------|--------------|--------------|-------|
| Credential loading | Jeden credential, fallback rotace | Per-account, fixed credential | Každý účet = izolovaný klient |
| Codex podpora | Žádná | `~/.codex/auth.json` loading | Nový service type |
| Deduplikace | Žádná | Hash prefix access tokenu | Eliminuje duplicitní bary |

## Open Questions

1. **Přesný formát `~/.codex/auth.json`**
   - Co víme: Obsahuje access token. Dokumentace zmiňuje field "access tokens" obecně.
   - Co není jasné: Přesné field jméno (`access_token` vs `accessToken` vs jiné). Zda existuje `refresh_token`.
   - Doporučení: Implementovat defenzivně — zkusit více kandidátů. Při prvním spuštění zalogovat do crash logu jaká pole byla nalezena.

2. **Codex rate limit — co zobrazit?**
   - Co víme: OpenAI headers jsou `x-ratelimit-remaining-requests` / `x-ratelimit-limit-requests` — dávají utilization.
   - Co není jasné: Zda Codex CLI (ChatGPT Pro) subscription má smysluplné limity v těchto headerech, nebo vrací -1/0.
   - Doporučení: Implementovat API call + header parsing. Pokud headers chybí nebo jsou -1, vrátit null — widget tiše přeskočí Codex nebo zobrazí N/A.

3. **Pořadí accounts pro budoucí UI (Phase 5)**
   - Co víme: Phase 5 zobrazí horizontální řadu — pořadí záleží.
   - Co není jasné: Má být Claude vždy první? Nebo pořadí načtení?
   - Doporučení: Pro Phase 4 — Claude first (stávající logika), Codex second. Phase 5 může dodat sorting.

## Validation Architecture

### Test Framework
| Property | Value |
|----------|-------|
| Framework | Žádný automatický test framework (stejné jako Phase 3) |
| Config file | none |
| Quick run command | `dotnet build ClaudeUsageWidget/ClaudeUsageWidget.csproj` |
| Full suite command | `dotnet build ClaudeUsageWidget/ClaudeUsageWidget.csproj` |

### Phase Requirements → Test Map
| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| MULTI-01 | Claude credentials z Windows+WSL načteny, duplicity odstraněny | manual-only | Vyžaduje credentials na disku | ❌ manuální |
| MULTI-02 | Codex `auth.json` načten z Windows i WSL | manual-only | Vyžaduje Codex credentials | ❌ manuální |
| MULTI-03 | Každý účet má vlastní API data (ne sdílená) | manual-only | Vizuální verifikace | ❌ manuální |

### Sampling Rate
- **Per task commit:** `dotnet build ClaudeUsageWidget/ClaudeUsageWidget.csproj`
- **Per wave merge:** `dotnet build ClaudeUsageWidget/ClaudeUsageWidget.csproj`
- **Phase gate:** Build green + manuální test s reálnými credentials

### Wave 0 Gaps
Žádné — build infrastruktura existuje.

## Sources

### Primary (HIGH confidence)
- Analýza `CredentialStore.cs` — stávající `LoadAllCredentials()`, `RunWsl()`, `TryReadWslCredential()` patterns
- Analýza `ClaudeApiClient.cs` — credential handling, `_credentials` list, `_credentialIndex` logika
- Analýza `Program.cs` — `OnStartup`, `MainWindow` konstruktor s `ClaudeApiClient`
- Analýza `MainWindow.xaml.cs` — existující single-account flow

### Secondary (MEDIUM confidence)
- OpenAI Codex dokumentace — `~/.codex/auth.json` lokace, "contains access tokens"
- OpenAI rate limit headers — `x-ratelimit-*` headers existence, ISO 8601 reset format
- Source: https://developers.openai.com/codex/auth/

### Tertiary (LOW confidence)
- Codex `auth.json` přesná field names — nebylo možné ověřit z oficiální dokumentace
- Source: WebSearch výsledky + inference z obecné dokumentace

## Metadata

**Confidence breakdown:**
- Standard stack (Claude část): HIGH — přímá analýza existujícího kódu
- Architecture patterns: HIGH — přímé rozšíření stávajících patterns
- Codex auth formát: LOW — přesná JSON field names nedokumentována
- Codex rate limits: MEDIUM — standard OpenAI headers dokumentovány, ale Codex-specifické chování neověřeno

**Research date:** 2026-03-07
**Valid until:** Stabilní (dokud Codex nezměnění auth.json formát)
