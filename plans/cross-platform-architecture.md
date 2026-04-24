# Cross-platform architektura: sdílený C# core + platformní UI

## Context

Současně máme Windows widget v C#/WPF. Cíl: přidat Linux/KDE Plasma verzi, ale **sdílet co nejvíc kódu** — hlavně API klienty (Claude/Codex/Toggl), credential reader, pace výpočty. Platformní zůstane jen **zobrazovací vrstva** (WPF na Windows, QML plasmoid v KDE) a **storage backend** (Registry vs JSON). Update mechanismus zůstává přes GitHub Actions — buildí se obě platformy najednou.

## Rozhodnutí architektury

### Co sdílíme (C# core library)

`ClaudeUsageWidget.Core` — **`net8.0` třídní knihovna** bez UI dependence:

```
Core/
├── Api/
│   ├── ClaudeApiClient.cs       # bez změn
│   ├── TogglApiClient.cs        # bez změn
│   └── CredentialStore.cs       # WSL reader zůstane (užitečný i na Linuxu)
├── Models/
│   ├── UsageData.cs, RateLimitEntry.cs
│   ├── TogglUsageData.cs, ProjectEarnings.cs, DayEarnings.cs
│   └── TogglProject.cs
├── Logic/
│   ├── PaceCalculator.cs        # extrakce z PopupWindow + AccountPanel (working days, implied rate, required hours)
│   ├── UsagePrediction.cs       # existující
│   └── TimeFormatter.cs         # existující
├── Storage/
│   ├── ISettingsProvider.cs     # abstrakce nad Registry vs JSON
│   ├── IHistoryStore.cs         # abstrakce pro UsageHistory a TogglHistory
│   └── SettingsModel.cs         # POCO s vlastnostmi (TogglApiKey, ShowClaude, …)
└── Updater/
    ├── ReleaseChecker.cs        # GitHub Releases API parser (současný Updater.cs zlogičtit)
    └── PlatformUpdater.cs       # abstract — platformní implementace v hostech
```

**Co se přesune:** `ClaudeApiClient.cs`, `TogglApiClient.cs`, `CredentialStore.cs`, `UsageHistoryStore.cs`, `TogglHistoryStore.cs`, `UsagePrediction.cs`, `TimeFormatter.cs`. Všechny pace výpočty se vytáhnou z `AccountPanel.UpdateTogglBars` a `PopupWindow.BuildTogglPopup` do **`PaceCalculator`** — stejný vzorec použijí oba hosty.

### Co je platformní

| Vrstva | Windows host | Linux host |
|---|---|---|
| **UI rendering** | WPF (XAML) | QML plasmoid (Qt Quick) |
| **Settings UI** | `SettingsWindow.xaml` (WPF) | `ConfigGeneral.qml` + `ConfigToggl.qml` (Plasma config dialog) |
| **Settings storage** | Registry (`HKEY_CURRENT_USER\Software\ClaudeUsageWidget`) | JSON (`~/.config/claude-usage-widget/settings.json`) přes `Plasmoid.configuration` |
| **History storage** | `%ProgramData%\ClaudeUsageWidget\history\*.json` | `~/.local/share/claude-usage-widget/history/*.json` |
| **Notifikace** | Windows Toast (AUMID) | `KNotification` přes DBus |
| **Auto-start** | Registry `Run` key | systemd user unit |
| **Update install** | Updater.cs (download .exe, kill+replace) | dnf/apt repo, AppImage update, nebo `.plasmoid` přes `kpackagetool6` |

### Most: jak QML komunikuje s C# core

QML neumí nativně načíst .NET assembly. Tři varianty IPC:

1. **Lokální HTTP (doporučeno)**
   - C# daemon běží jako systemd user service, exponuje HTTP REST/JSON server na `127.0.0.1:NNNN` (port v ~/.config)
   - QML používá `XMLHttpRequest` (built-in)
   - Endpoints: `GET /usage` (current data), `POST /refresh` (force-fetch), `GET /events` (Server-Sent Events pro live updates)
   - **Plus:** stejný API protokol může v budoucnu používat web UI / mobil
   - **Mínus:** otevřený lokální port (firewall si může stěžovat — bind jen na 127.0.0.1)

2. **DBus**
   - Nativní pro Linux desktop, integruje se s `KNotification` a system services
   - C# má `Tmds.DBus` knihovnu (working, dobře udržovaná)
   - QML má `org.kde.plasma.dbusinterface` přes Qt Quick
   - **Mínus:** víc setup, povinný `--session` bus, krkolomné v Windows pokud bys jednou potřeboval cross-test

3. **File watching + JSON snapshot**
   - C# daemon píše `~/.local/state/claude-usage-widget/state.json` po každém refreshi
   - QML má `FileSystemWatcher` přes `Qt.labs.folderlistmodel` (limitované) nebo C++ plugin s `QFileSystemWatcher`
   - Nejjednodušší implementace, nejhorší latence (refresh na panelu po sebrání eventu)

**Doporučuji HTTP localhost** — flexibilní, debuggovatelné `curl`em, žádný IPC overhead.

## Repo struktura

```
claude-usage-widget/                       # současný repo
├── src/
│   ├── ClaudeUsageWidget.Core/            # NEW — sdílená netstandard2.1 (nebo net8.0) knihovna
│   │   ├── Api/
│   │   ├── Models/
│   │   ├── Logic/
│   │   ├── Storage/
│   │   └── ClaudeUsageWidget.Core.csproj
│   ├── ClaudeUsageWidget.Windows/         # současný projekt přejmenovaný
│   │   ├── MainWindow.xaml(.cs)
│   │   ├── PopupWindow.xaml(.cs)
│   │   ├── AccountPanel.xaml(.cs)
│   │   ├── SettingsWindow.xaml(.cs)
│   │   ├── RegistrySettingsProvider.cs    # implementace ISettingsProvider
│   │   ├── ProgramDataHistoryStore.cs     # implementace IHistoryStore
│   │   └── ClaudeUsageWidget.Windows.csproj  # references Core
│   └── ClaudeUsageWidget.LinuxDaemon/     # NEW — single-file daemon
│       ├── Program.cs                     # ASP.NET Core minimal HTTP server
│       ├── JsonSettingsProvider.cs        # ISettingsProvider impl
│       ├── XdgHistoryStore.cs             # IHistoryStore impl
│       ├── DBusNotifier.cs                # KNotification client
│       └── ClaudeUsageWidget.LinuxDaemon.csproj
├── plasmoid/                              # NEW — KDE plasmoid (čistě QML)
│   ├── metadata.json
│   └── contents/
│       ├── ui/
│       │   ├── main.qml                   # PlasmoidItem, vola HTTP daemon
│       │   ├── CompactRepresentation.qml
│       │   ├── FullRepresentation.qml
│       │   └── ConfigToggl.qml
│       ├── config/
│       └── code/
│           └── api.js                     # XHR helpers
├── packaging/
│   ├── linux/
│   │   ├── systemd/claude-usage-widget.service
│   │   ├── flatpak/org.zrnik.ClaudeUsageWidget.yml
│   │   └── debian/ (control, postinst…)
│   └── windows/
│       └── ... (současné MSIX nebo plain .exe)
├── .github/workflows/
│   ├── build-windows.yml                  # současný
│   └── build-linux.yml                    # NEW
└── README.md
```

## Build & release pipeline (GitHub Actions)

### Workflow per platform (paralelní v jednom runu)

**`.github/workflows/release.yml`** (jeden centrální workflow, fan-out na windows + linux jobs):

```yaml
on:
  push:
    tags: ['v*']

jobs:
  windows:
    runs-on: windows-latest
    steps:
      - checkout
      - dotnet restore + publish ClaudeUsageWidget.Windows -r win-x64 --self-contained
      - upload artefakt: ClaudeUsageWidget.exe

  linux-daemon:
    runs-on: ubuntu-latest
    steps:
      - checkout
      - dotnet publish ClaudeUsageWidget.LinuxDaemon -r linux-x64 --self-contained
      - upload artefakt: claude-usage-daemon

  linux-plasmoid:
    runs-on: ubuntu-latest
    steps:
      - checkout
      - cd plasmoid && zip -r claude-usage-widget.plasmoid .
      - validate: kpackagetool6 --type Plasma/Applet --validate claude-usage-widget.plasmoid
      - upload artefakt: claude-usage-widget.plasmoid

  flatpak:
    runs-on: ubuntu-latest
    needs: [linux-daemon]
    steps:
      - flatpak-builder s manifestem (zabalí daemon + plasmoid + systemd service)

  release:
    runs-on: ubuntu-latest
    needs: [windows, linux-daemon, linux-plasmoid, flatpak]
    steps:
      - download all artefakty
      - gh release create $TAG --notes-file CHANGELOG.md \
          ClaudeUsageWidget.exe \
          claude-usage-daemon-linux-x64 \
          claude-usage-widget.plasmoid \
          claude-usage-widget.flatpak
```

### Update mechanismus per platform

**Windows:** současný `Updater.cs` zůstává — fetch GitHub releases, download .exe, replace.

**Linux daemon:** stejný GitHub Releases API, daemon si stáhne nový binary, atomicky nahradí, requestuje `systemctl --user restart claude-usage-widget`.

**Linux plasmoid:** dvě cesty:
- **Doporučeno:** distribuce přes KDE Store (https://store.kde.org) — KNewStuff dělá auto-update v System Settings. Pak release plasmoid update jen tam.
- **Alternativa pro early users:** GitHub release má `.plasmoid`, daemon si ho stáhne a zavolá `kpackagetool6 -u file.plasmoid -t Plasma/Applet`. Plasma reload za běhu funguje.

### Verzování — důležité

`metadata.json` plasmoidu, `AssemblyVersion` daemona a `AssemblyVersion` Windows hosta musí mít **stejnou verzi**. Workflow je čte z git tagu (`v0.3.0`) a injektuje. Tag = SemVer.

## Settings — abstrakce

```csharp
// V Core
public interface ISettingsProvider
{
    SettingsModel Load();
    void Save(SettingsModel settings);
    event Action? Changed;
}

public sealed class SettingsModel
{
    public bool ShowClaude { get; set; } = true;
    public bool ShowCodex { get; set; } = true;
    public bool ShowToggl { get; set; } = true;
    public bool NotificationsEnabled { get; set; }
    public string TogglApiKey { get; set; } = "";
    public double TogglMonthlyTargetCzk { get; set; }
    public Dictionary<long, double> TogglProjectRates { get; init; } = new();
    public Dictionary<string, double> ChartWindowHours { get; init; } = new();
}
```

**Windows impl:** `RegistrySettingsProvider` čte/píše `HKCU\Software\ClaudeUsageWidget` (současná logika ze `SettingsStore.cs`).

**Linux impl:** `JsonSettingsProvider` serializuje `SettingsModel` do `~/.config/claude-usage-widget/settings.json`. **Plasmoid Settings UI** modifikuje stejný JSON nebo volá daemon přes `POST /settings`.

**Settings UI samostatně:**
- Windows: WPF `SettingsWindow` (současný)
- KDE: Plasma config dialog (`config/main.xml` + `config/config.qml`) — ale ukládá přes daemon HTTP `POST /settings` (ne přes `Plasmoid.configuration`, protože daemon vlastní zdroj pravdy)

## Migrační plán z současného repa

**Fáze 1 — extrakce Core (1-2 dny):**
1. `mkdir src/ClaudeUsageWidget.Core` + nový .csproj (`net8.0`, žádné WPF deps)
2. Přesunout: API klienty, models, history stores, pace logiku, prediction
3. Vytvořit `ISettingsProvider` interface + `RegistrySettingsProvider` v Windows projektu
4. Aktuální `SettingsStore` rozdělit: data model do Core, registry impl do Windows
5. Windows projekt referuje Core, vše funguje stejně
6. Build OK, smoke test → commit

**Fáze 2 — Linux daemon (2-3 dny):**
1. `mkdir src/ClaudeUsageWidget.LinuxDaemon` — ASP.NET Core minimal API (~50 LOC pro HTTP server)
2. `JsonSettingsProvider` + `XdgHistoryStore` implementace
3. Endpoints: `GET /usage`, `POST /refresh`, `GET /settings`, `POST /settings`, `GET /sse` (server-sent events)
4. Cron loop: `Timer` co 1 min Claude refresh, co 5 min Toggl refresh (logika z `MainWindow.xaml.cs`)
5. systemd user unit: `~/.config/systemd/user/claude-usage-widget.service`
6. Test přes `curl localhost:PORT/usage`

**Fáze 3 — QML plasmoid (2-3 dny):**
1. `metadata.json` + minimální `main.qml` s `PlasmoidItem`
2. `CompactRepresentation` — fetch z daemonu, render bary (port logiky `AccountPanel.UpdateTogglBars` do JS)
3. `FullRepresentation` — popup s pace + breakdown + Canvas graf (port `PopupWindow.BuildTogglPopup`)
4. Config dialog UI v QML (jen Toggl Settings, Show* checkboxy)
5. `kpackagetool6 -i .` lokální install pro test

**Fáze 4 — CI/CD (1 den):**
1. `release.yml` workflow se 4 jobs (windows, linux-daemon, linux-plasmoid, release aggregate)
2. Tag `v0.3.0` → automatický release s 3 artefakty
3. Update Windows klienta aby uměl detekovat verzi i Linux artefaktů (Updater.cs už `gh release latest` dělá)

**Fáze 5 — distribuce KDE Store (volitelné, později):**
1. Upload `.plasmoid` na https://store.kde.org
2. Pull KNewStuff config, KNS Discover automaticky najde updaty

## Edge cases / rizika

- **Daemon crash:** systemd user unit má `Restart=on-failure`, plasmoid při ztrátě connection zobrazí "Daemon offline" hlášku
- **Port collision:** daemon si při startu vybere volný port, zapíše do `~/.config/claude-usage-widget/port.txt`, plasmoid ho čte
- **Multi-user:** každý user má vlastní daemon na vlastním portu (systemd user units, ne system)
- **Permissions:** Toggl API key v JSON souboru s `chmod 600` (per-user). Pro paranoiu přidat KWallet integraci později (DBus volání)
- **Linux Claude credentials:** existuje `~/.claude/.credentials.json`? Pokud ne (instalace přes Flatpak Claude Desktop?), implementovat Linux-specific reader
- **Settings sync mezi Windows a Linux:** out-of-scope — každý OS má vlastní storage
- **Update breaking changes v API:** verzovat HTTP API (`/v1/usage`, `/v1/settings`) — daemon i plasmoid vždy build v páru ze stejného releasu

## Co tím získáš

- **~70 % kódu sdíleného** mezi platformami
- **Single source of truth** pro API logiku — fix v Core opravuje obě platformy
- **GitHub Actions** buildí obě platformy paralelně z jednoho tagu
- **Linux verze běží jako daemon** — funguje i bez plasmoidu (např. časem CLI klient pro tmux statusline)
- **Future-proof:** jednou možno přidat Mac (Avalonia / native Cocoa) bez dalšího refactoru jádra

## Otevřené otázky k odsouhlasení před implementací

1. **netstandard2.1 vs net8.0 pro Core** — net8.0 je modernější, ale ekosystém libek netstandard2.1 širší. Doporučuji **net8.0** (target obou hostů je net8.0)
2. **HTTP vs DBus IPC** — doporučuji HTTP, ale pokud chceš co nejvíc nativní KDE feeling, jde i DBus
3. **Daemon vs in-plasmoid C# (Avalonia)** — daemon je správné rozhodnutí. Avalonia by udělal jeden binary ale **nešel by integrovat do panelu**, byl by to standalone okno. Plasmoid + daemon je jediná cesta k native panel integraci.
4. **Plasmoid distribution** — Flatpak (rozšíření) vs KDE Store vs jen GitHub releases? Doporučuji **GitHub releases first**, KDE Store až po stabilizaci
5. **Settings UI v plasmoidu vs Windows-style separate Settings okno** — Plasma má built-in config dialog framework, použij ho. Settings okno samostatné dělat nemusíme.

## Sources

- [.NET single-file publish for Linux](https://learn.microsoft.com/en-us/dotnet/core/deploying/single-file/overview)
- [ASP.NET Core minimal API](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/minimal-apis/)
- [Tmds.DBus knihovna pro C#](https://github.com/tmds/Tmds.DBus)
- [KDE Plasma Widget Tutorial](https://develop.kde.org/docs/plasma/widget/)
- [systemd user units](https://wiki.archlinux.org/title/Systemd/User)
- [GitHub Actions multi-platform releases](https://docs.github.com/en/actions/automating-builds-and-tests/building-and-testing-with-multiple-jobs)
