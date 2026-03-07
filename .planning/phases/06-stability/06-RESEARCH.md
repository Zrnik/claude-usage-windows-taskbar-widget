# Phase 6: Stability - Research

**Researched:** 2026-03-07
**Domain:** C# WPF single-instance enforcement, token expiry recovery, account deduplication, progress bar rendering
**Confidence:** HIGH — vychazi z prime analyzy zdrojoveho kodu

---

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|-----------------|
| STAB-01 | Widget povoluje pouze jednu instanci — nove spusteni zabije predchozi a nastartuje se samo | Mutex pattern s GC-pinning, kill old PID |
| STAB-02 | Po 401 widget precte credentials z disku — pokud se lisi, pouzije nove okamzite; pokud jsou stejne, prejde do error stavu | ClaudeApiClient._noReload flag treba obejit pri 401 recovery, AccountPanel.ShowErrorState() uz existuje |
| STAB-03 | Accounts se deduplikuji: Claude = hash org ID z JWT, Codex = hash account_id; credentials bez zjistitelneho klice jsou tise preskoceny | CredentialStore.LoadAllAccounts() pouziva token prefix jako klic — nespravne; treba JWT decode pro org ID |
| STAB-04 | Text v progress baru se nezasekava na lomitku | SpinnerFrames s "\" je problem, StopSpinner() race condition identifikovana |
</phase_requirements>

---

## Summary

Phase 6 resi 4 stability bugy v existujicim C# WPF widgetu (~2500 LOC). Vsechny 4 bugy jsou presne lokalizovany v kodu — zadny z oprav nevyzaduje novou architekturu.

**Plan 06-01** (STAB-01): Program.cs nema zadny Mutex. Pridani je jednoduche — Mutex jako field na App tride (ne lokalni promenna — GC pitfall v Release builds). Nova instance zavola `Process.Kill()` na predchozi a pak se spusti normalne. Pouzit `Local\ClaudeUsageWidget` prefix.

**Plan 06-02** (STAB-02 + STAB-03 + STAB-04): Tri bugy sdileji jeden plan. Token expiry recovery v `ClaudeApiClient` je neuplna — pri 401 se pokusi o refresh, ale pro per-account instance (`_noReload = true`) nemuze precist nove credentials z disku. Deduplication v `CredentialStore.LoadAllAccounts()` pouziva token prefix misto org ID z JWT. Progress bar text se zasekava na `\` protoze `StopSpinner()` zastavuje timer ale neresetuje text.

**Primarni doporuceni:** Implementovat v poradi 06-01 → 06-02; oba plany jsou nezavisle.

---

## Standard Stack

### Core (jiz existuje v projektu)
| Komponenta | Verze | Pouziti |
|------------|-------|---------|
| .NET | 8.0 | Target framework |
| WPF | net8.0-windows | UI framework |
| System.Threading.Mutex | built-in | Single-instance enforcement |
| System.Diagnostics.Process | built-in | Kill predchozi instance |
| System.Text.Json | built-in | JWT payload decode pro org ID |

### Zadne nove NuGet balicky nejsou potreba

Vsechny potrebne API jsou v .NET 8 BCL.

---

## Architecture Patterns

### STAB-01: Single Instance Enforcement

**Aktualni stav (Program.cs):**
- Zadny Mutex, zadna instance check
- `App.OnStartup()` se spusti vzdycky bez kontroly
- Predchozi instance zustane bezet, nova se prida

**Pattern pro opravu — Mutex jako field na App:**

```
App trida:
  - field: Mutex _mutex (ne lokalni promenna — GC pitfall)
  - Main(): zkus vytvorit Mutex s nazvem "Local\ClaudeUsageWidget"
    - pokud uz existuje (createdNew == false):
      - najdi predchozi proces (Process.GetProcessesByName())
      - zabij ho (process.Kill())
      - pockat max 2s az skonci
    - drz Mutex po celou dobu behu aplikace
    - uvolni v App.OnExit() nebo Dispose
```

**Proc field, ne lokalni promenna:**
V Release builds muze GC sebrat lokalni Mutex promennou ktera uz neni referencovana — i kdyz Mutex je stale "drzen" systemem. Pak druha instance uspesne ziska Mutex a obidve bezet soucastne. Reseni: `private Mutex? _mutex` jako field na `App` tride.

**Mutex naming:**
- `Local\ClaudeUsageWidget` — Local prefix = per-session (ne cross-session)
- Widget nema cross-session pozadavek (taskbar widget je uzivatelska session)
- `Global\` by vyzadovalo elevated privileges

**Kill predchozi instance:**
```
Process.GetProcessesByName(nazev_bez_exe)
  → filtrat: odlisit od sebe sama (Process.GetCurrentProcess().Id)
  → Kill() + WaitForExit(2000)
```

**Alternativa (named pipe):**
Komplikovanejsi, vyzaduje async komunikaci. Pro nasli widget Kill() je dostatecny a jednodussi.

### STAB-02: Token Expiry Recovery

**Aktualni stav (ClaudeApiClient.cs):**

`_noReload = true` je nastaveno pro vsechny per-account instance (radek 42). Tento flag blokuje `LoadAllCredentials()` pri kazdem volani `GetUsageAsync()`.

Existujici 401 handling (radky 98-126):
1. Zkusi `RefreshTokenAsync()` — funguje pro Claude OAuth
2. Zkusi dalsi credential source (`_credentialIndex++`) — ale pri `_noReload=true` neni co dalsiho
3. Nastavi `LastError = "Invalid credentials"` — zasekne se

**Co chybi:**
Pri 401, po selhani refreshe, se credentials z disku neprecti. Pro per-account instance (`_noReload=true`) je disk cesta bud WSL nebo Windows path ulozena v `_credential.SourcePath`.

**Pozadovane chovani (STAB-02):**
```
401 received:
  1. Zkus RefreshTokenAsync()
  2. Pokud selhalo: precti credentials z disku (CredentialStore.LoadCredentialFromPath(sourcePath))
  3. Porovnej novy AccessToken s aktualnim
     - Lisi se? → pouzi novy token, zkus API znovu
     - Stejne? → ShowErrorState(), prestan se pokouset (error state)
```

**Existujici infrastruktura ktera pomaha:**
- `OAuthCredential.SourcePath` je vzdy ulozena (WSL marker nebo Windows path)
- `CredentialStore.TryReadCredential(path)` existuje (private) — treba zpristupnit nebo duplikovat logiku
- `AccountPanel.ShowErrorState()` existuje a funguje spravne

**Max 1 retry per poll cycle** (z STATE.md decisions): Po dvou pokusech (refresh + re-read) → error stav.

### STAB-03: Account Deduplication Fix

**Aktualni stav (CredentialStore.LoadAllAccounts(), radky 213-238):**

```csharp
// Claude dedup key: prvnich 32 znaku access tokenu
var key = "claude:" + token[..Math.Min(32, token.Length)];
```

**Problem:**
- Claude OAuth token (`sk-ant-oat01-...`) je opaque — prvnich 32 znaku je spolecny prefix pro vsechny tokeny
- Windows a WSL credentials pro stejny ucet budou mit STEJNY token → spravne deduplikovano
- ALE kdyz se token rotuje (logout/login), klic se zmeni → duplikat

**Pozadovane chovani (STAB-03):**
- Claude: dedup dle org ID extrahovaneho z JWT payloadu access tokenu
- Codex: dedup dle `account_id` z JWT payloadu access tokenu
- Credentials bez zjistitelneho klice: tiche preskoceni (ne error stav)

**JWT decode infrastruktura:**
`GetJwtExpiryMs()` v CredentialStore.cs uz ukazuje jak dekodovat JWT payload (radky 324-339). Stejna technika se pouzije pro extrakci `org_id` / `account_id` z payloadu.

**Claude JWT payload struktura (z MEMORY.md):**
Token scope: `user:inference, user:sessions:claude_code`. JWT payload pravdepodobne obsahuje `org_id` nebo `organization_id`. Treba overit ze skutecneho tokenu — kod pro to uz existuje (`GetJwtExpiryMs` pattern).

**Codex JWT:**
`auth.json` obsahuje JWT (ne opaque token). JWT payload obsahuje `account_id` nebo `sub`. ParseCodexCredentialJson() uz cte JSON strukturu.

**Implementace:**
```
GetAccountKey(token, serviceType):
  - Dekoduj JWT payload (Base64url)
  - Claude: vrat "claude:" + org_id; pokud neni org_id v payloadu → return null (preskoc)
  - Codex: vrat "codex:" + account_id; pokud neni → return null (preskoc)
  - Pokud decode selze → return null (preskoc)

LoadAllAccounts():
  - Pouzij GetAccountKey() misto token prefix
  - Pokud GetAccountKey() vrati null → skip (nepridavej do result)
```

**Fallback behavior (STAB-03 requirement):**
Credentials bez klice jsou "tise preskoceny" — widget se spusti i kdyz jsou neuplne credentials. Nula accounts fallback v `App.OnStartup()` (radek 58-59) uz existuje.

### STAB-04: Progress Bar Text Fix

**Aktualni stav (AccountPanel.xaml.cs):**

```csharp
private static readonly string[] SpinnerFrames = ["|", "/", "—", "\\"];
```

`"\\"` je C# escape pro backslash `\`. Spinner ukazuje `| / — \` rotujici.

**Bug — kde se zasekava:**
`StopSpinner()` (MainWindow.xaml.cs, radky 207-211):
```csharp
private void StopSpinner()
{
    _spinnerTimer?.Stop();
    _spinnerTimer = null;
}
```

Timer se zastavi, ale text v `Text5h` a `Text7d` zustane na poslednim zobrazenim znaku (`/`, `—`, nebo `\`). Pokud `StopSpinner()` bezi kdyz je `_spinnerFrame == 2` (backslash), text zustane `\`.

**Nasledujici volani:**
- `UpdateBars()` prepisuje text spravne (radky 31-41 v AccountPanel.xaml.cs)
- ALE pokud API call vrati null (error), `ShowErrorState()` se zavola — ta text prepisuje na "Error"
- Pro uspesne accounts se `UpdateBars()` zavola → text se spravne aktualizuje

**Race condition:**
`StopSpinner()` je volano v `Loaded` event po `await` vsech tasks (MainWindow.xaml.cs radek 188). Pokud task pro jeden account skonci drive nez ostatni, spinner muze mit ruzne hodnoty `_spinnerFrame` pri zastaveni.

**Fix:**
`StopSpinner()` by melo resetovat text pro vsechny panels. Protoze v dobe zastaveni uz jsou vysledky dostupne (loading loop skoncil), spravny pristup:
- `StopSpinner()` zastavi timer
- Pak se `UpdateBars()` nebo `ShowErrorState()` zavola pro kazdy panel
- Nebo `StopSpinner()` sam resetuje spinner text na prazdny retezec / "..."

**Pozadovana oprava:** `StopSpinner()` zastavi timer a zaroven resetuje `Text5h.Text` a `Text7d.Text` na "" nebo "0%" pro vsechny panels. Konkretni pristup vidi planner.

---

## Don't Hand-Roll

| Problem | Nepsat vlastni | Pouzit | Proc |
|---------|----------------|--------|------|
| Single-instance | Custom IPC/named pipe server | `System.Threading.Mutex` | Jednoduchy, spolehlivy, OS-managed |
| JWT decode | JWT knihovna | `Convert.FromBase64String` + `JsonNode.Parse` | Uz existuje v `GetJwtExpiryMs()` — stejny pattern |
| Process kill | Custom IPC pro graceful shutdown | `Process.Kill()` + `WaitForExit(2000)` | Widget nema graceful shutdown logiku ktere by bylo potreba |

---

## Common Pitfalls

### Pitfall 1: GC sbere Mutex (Release build)
**Co se stane:** Lokalni `Mutex` promenna v `Main()` nebo `OnStartup()` muze byt sbirana GC kdyz neni referencovana i kdyz OS stale drzi handle. V Release buildech GC agresivnejsi.
**Proc:** .NET GC neresi Win32 handles — jen managed reference.
**Jak predejit:** `private Mutex? _mutex` jako field na `App` tride. Uvolnit v `OnExit`.
**Varovne znameni:** Bug se projevi jen v Release build, ne v Debug.

### Pitfall 2: _noReload flag blokuje disk re-read
**Co se stane:** `ClaudeApiClient(AccountInfo)` vzdy nastavuje `_noReload = true`. Pri 401 handling se credentials z disku neprecti protoze flag je `true`.
**Proc:** Flag byl pridan aby per-account instance neprepsal credentials jinych accounts. Spravna motivace, ale blokuje recovery.
**Jak predejit:** Pri 401, po selhani refresh, precist disk explicitne bez ohledu na `_noReload` — ale jen pro `SourcePath` aktualniho credential, ne vsechny.

### Pitfall 3: JWT org_id muze chybet
**Co se stane:** Pokud JWT payload neobsahuje `org_id` (napr. personal account bez org), `GetAccountKey()` vrati null a credentials budou preskoceny.
**Proc:** Toto je pozadovane chovani dle STAB-03 ("tichy preskoc"). ALE fallback v `App.OnStartup()` musi zustat — kdyz vsechny accounts jsou preskoceny, widget nastartuje v error stavu.
**Jak predejit:** Overit ze fallback `accounts.Count == 0` check (Program.cs radek 58) zustane funkci po zmene dedup logiky.

### Pitfall 4: StopSpinner bez text resetu
**Co se stane:** `StopSpinner()` zastavi timer ale neresetuje text. Spinner znak zustane viset.
**Proc:** WPF neresuje "zobraz naposledy nastavenou hodnotu" — TextBlock drzi posledni text.
**Jak predejit:** `StopSpinner()` explicitne resetuje text, nebo se spoleha na to ze `UpdateBars()`/`ShowErrorState()` bude vzdycky zavolano po `StopSpinner()` — coz v soucasnem kodu ANO je, ale pouze kdyz API vrati ne-null vysledek.

### Pitfall 5: Mutex WaitOne s Abandoned
**Co se stane:** Pokud predchozi instance crashla bez uvolneni Mutex, `Mutex.WaitOne(0)` hodi `AbandonedMutexException`.
**Jak predejit:** `try { mutex.WaitOne(0) } catch (AbandonedMutexException) { /* treat as acquired */ }` — Abandoned Mutex znamena ze predchozi instance crashla, muzeme pokracovat.

---

## Code Examples

### Mutex jako field — spravny pattern

```csharp
// Source: Microsoft docs / .NET BCL — HIGH confidence
internal class App : Application
{
    private Mutex? _mutex;  // field, ne lokalni promenna

    [STAThread]
    static void Main(string[] args)
    {
        var app = new App();
        app.Run();
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        _mutex = new Mutex(initiallyOwned: false, "Local\\ClaudeUsageWidget",
                           out bool createdNew);

        if (!createdNew)
        {
            // Zabij predchozi instanci
            foreach (var p in Process.GetProcessesByName(
                         Path.GetFileNameWithoutExtension(Environment.ProcessPath ?? "")))
            {
                if (p.Id == Environment.ProcessId) continue;
                try { p.Kill(); p.WaitForExit(2000); } catch { }
            }
            // Zkus znova ziskat mutex (predchozi uz mrtvý)
            try { _mutex.WaitOne(3000); }
            catch (AbandonedMutexException) { /* ok, muzeme pokracovat */ }
        }

        // ... zbytek OnStartup
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _mutex?.ReleaseMutex();
        _mutex?.Dispose();
        base.OnExit(e);
    }
}
```

### JWT org_id extrakce — stejny pattern jako GetJwtExpiryMs

```csharp
// Vychazi z GetJwtExpiryMs() v CredentialStore.cs — HIGH confidence
private static string? GetJwtClaim(string jwt, string claimName)
{
    try
    {
        var parts = jwt.Split('.');
        if (parts.Length < 2) return null;
        var payload = parts[1];
        var padded = payload.PadRight((payload.Length + 3) / 4 * 4, '=')
                            .Replace('-', '+').Replace('_', '/');
        var bytes = Convert.FromBase64String(padded);
        var doc = JsonNode.Parse(System.Text.Encoding.UTF8.GetString(bytes));
        return doc?[claimName]?.GetValue<string>();
    }
    catch { return null; }
}
```

### StopSpinner s resetem textu

```csharp
// Uprava StopSpinner() v MainWindow.xaml.cs
private void StopSpinner()
{
    _spinnerTimer?.Stop();
    _spinnerTimer = null;
    // Reset spinner textu — UpdateBars/ShowErrorState prepise spravnou hodnotou
    foreach (var (_, panel, _) in _accounts)
        panel.ClearSpinner();
}

// Nova metoda v AccountPanel.xaml.cs
public void ClearSpinner()
{
    Text5h.Text = "";
    Text7d.Text = "";
}
```

---

## State of the Art

| Stary pristup | Aktualni pristup | Zmena | Dopad |
|---------------|-----------------|-------|-------|
| Token prefix dedup | JWT org_id dedup | Phase 6 | Spravna deduplication |
| Zadny single-instance check | Mutex enforcement | Phase 6 | Zadne duplicity |
| 401 → error state | 401 → re-read disk → error state | Phase 6 | Plynuly recovery |

---

## Open Questions

1. **Jaky JWT claim obsahuje org ID pro Claude OAuth token?**
   - Co vime: Token format je `sk-ant-oat01-...` (opaque), ale je to JWT (ma `.` oddelovace)
   - Co neni jasne: Presny nazev claimu — `org_id`, `organization_id`, `aud`, nebo jiny
   - Doporuceni: Implementovat `GetJwtClaim()` a pridat logging ktery vypise vsechny claims z realneho tokenu pri prvnim spusteni. Alternativa: pouzit claim `sub` (subject = user ID) pro dedup misto org_id — zarucene pritomny v kazdem JWT.
   - POZNAMKA: Pokud token neni JWT (opaque), decode selze → null → preskoc (STAB-03 chceme)

2. **Jaky JWT claim obsahuje account_id pro Codex?**
   - Co vime: Codex token je JWT (ParseCodexCredentialJson dekoduje payload pro `exp`)
   - Co neni jasne: Zda claim je `account_id`, `sub`, nebo neco jineho
   - Doporuceni: Zkusit `sub` jako fallback — je standard claim, zarucene pritomny

3. **Je bezpecne zavolat Process.Kill() na predchozi widget instanci?**
   - Co vime: Widget nema trvalou data store (zatim — Phase 7 prida), takze ztrata dat neni problem
   - Doporuceni: Ano, Kill() je bezpecny pro Phase 6. Phase 7 (atomic write) zaridi ze crash je safe.

---

## Validation Architecture

`nyquist_validation` je `true` v config.json — sekce je povinne.

### Test Framework

| Property | Value |
|----------|-------|
| Framework | Manual testing (zadny test framework v projektu) |
| Config file | none |
| Quick run command | `dotnet build ClaudeUsageWidget.csproj` (compile check) |
| Full suite command | Rucni spusteni widgetu + overeni chování |

Projekt nema automatizovane testy. Phase 6 opravy jsou UI/process-level — vhodne pro manual smoke testy.

### Phase Requirements → Test Map

| Req ID | Chovani | Test Type | Automated Command | Soubor existuje? |
|--------|---------|-----------|-------------------|-----------------|
| STAB-01 | Druha instance zabije predchozi | manual | `dotnet build` (compile only) | N/A — manual |
| STAB-02 | Po 401 re-read credentials z disku | manual | `dotnet build` | N/A — manual |
| STAB-03 | Accounts bez klice preskoceny | manual | `dotnet build` | N/A — manual |
| STAB-04 | Spinner text se nezasekava | manual | `dotnet build` | N/A — manual |

### Klic testovaci scenare

**STAB-01 — Single instance:**
1. Spust widget (Instance A)
2. Spust widget znovu (Instance B)
3. Overeni: Instance A process konci, Instance B bezi, widget zobrazen jednou

**STAB-02 — Token expiry:**
1. Spust widget s platnym tokenem
2. Vymaz/zmen token v credentials souboru (simuluj expiraci)
3. Overeni: Widget prejde do error stavu, nezasekne se
4. Obnov token v credentials souboru
5. Overeni: Widget automaticky pouzije novy token pri pristim poll

**STAB-03 — Account deduplication:**
1. Spust widget s identickymi credentials v WSL i Windows (stejny org)
2. Overeni: Zobrazen jen jeden AccountPanel, ne dva
3. Spust widget s credentials bez platneho JWT (prazdny token)
4. Overeni: Widget nastartuje v no-credentials fallback stavu

**STAB-04 — Progress bar text:**
1. Spust widget
2. Pozoruj spinner behem loading faze
3. Po nacteni: overeni ze text zobrazuje procenta (napr. "45%  in 2h 15m"), ne spinner znak
4. Opakuj nekolikrat pro ruzne loading casy

### Sampling Rate
- Per task commit: `dotnet build ClaudeUsageWidget/ClaudeUsageWidget.csproj` — kompilace musi projit bez chyb
- Per wave merge: Rucni smoke test vsech 4 scenaru vyse
- Phase gate: Vsechny 4 STAB requirements overeny rucne pred `/gsd:verify-work`

### Wave 0 Gaps

Zadny test framework neni instalovan a neni vyzadovan — existujici projekt nema testy a Phase 6 opravy jsou vhodne pro manual testing. Jedina automatizovana kontrola je `dotnet build`.

---

## Sources

### Primary (HIGH confidence)
- Primy zdrojovy kod `ClaudeUsageWidget/Program.cs` — aktualni stav App tridy, zadny Mutex
- Primy zdrojovy kod `ClaudeUsageWidget/CredentialStore.cs` — LoadAllAccounts() dedup logika, GetJwtExpiryMs() pattern
- Primy zdrojovy kod `ClaudeUsageWidget/ClaudeApiClient.cs` — _noReload flag, 401 handling, RefreshTokenAsync()
- Primy zdrojovy kod `ClaudeUsageWidget/AccountPanel.xaml.cs` — SpinnerFrames, StopSpinner chybi text reset
- Primy zdrojovy kod `ClaudeUsageWidget/MainWindow.xaml.cs` — StopSpinner() implementace, loading flow

### Secondary (MEDIUM confidence)
- `.planning/STATE.md` Decisions sekce — Mutex naming, GC pitfall, retry limit rozhodnuti
- `.planning/todos/pending/2026-03-07-single-instance-enforcement-and-better-updater.md` — problem popis

### Tertiary (LOW confidence)
- N/A

---

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH — .NET BCL pouzivane primo
- Architecture: HIGH — zdrojovy kod precteny, problemy presne lokalizovany
- Pitfalls: HIGH — GC pitfall pro Mutex je zdokumentovany .NET gotcha, ostatni z kodu

**Research date:** 2026-03-07
**Valid until:** 2026-04-07 (stabilni technologie, zdroj je zdrojovy kod projektu)

---

## RESEARCH COMPLETE
