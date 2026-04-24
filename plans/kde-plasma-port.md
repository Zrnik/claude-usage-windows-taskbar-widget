# KDE Plasma port — plán

## Context

Windows widget `ClaudeUsageWidget` je psaný v C#/WPF a integruje se do Windows taskbaru. Potřebujeme ekvivalent pro KDE Plasma 6 se stejnou funkčností (Claude/Codex rate limity + Toggl sekce) a self-update mechanismem. Tenhle plán shrnuje proveditelnost, stack, distribuci a odhad objemu práce.

## Je to nativně možné?

**Ano.** KDE Plasma má první-class widget API: tzv. **plasmoid** (aka applet). Běží přímo v panelu/taskbaru nebo na ploše jako nativní komponenta, nikoliv jako tray ikona nebo okno na přeskáčku jako ve Windows.

## Tech stack (Plasma 6 + KF6 + Qt 6)

| Vrstva | Windows widget | KDE Plasma port |
|---|---|---|
| UI framework | WPF (XAML + C#) | **QML** (Qt Quick 2/3) |
| Jazyk logiky | C# | **JavaScript** v QML (volitelně + C++ plugin) |
| HTTP klient | `System.Net.HttpClient` | `XMLHttpRequest` v QML (built-in), nebo Qt Network + C++ plugin pro větší výkon |
| JSON parsing | `System.Text.Json` | `JSON.parse()` (built-in) |
| Persistence nastavení | Windows Registry | `Plasmoid.configuration` (ukládá do `~/.config/plasma-org.kde.plasma.desktop-appletsrc`) |
| Persistence historie | JSON v `%ProgramData%` | JSON v `~/.local/share/plasma_appletname/` přes `Qt.labs.settings` nebo přímo `XMLHttpRequest`/fs plugin |
| Notifikace | Windows toast | `KNotification` (přes DBus, volitelně C++) |
| Ikona v panelu | `MainWindow.xaml` + P/Invoke pro taskbar | `compactRepresentation` v plasmoidu (auto-škáluje dle panelu) |
| Popup na hover | `PopupWindow` | `fullRepresentation` (plasmoid.expanded) |

**Plasmoid lze psát čistě v QML+JS** — žádný C++ není povinný. C++ plugin se hodí jen pro low-level operace (file I/O za hranicemi data dir, nízkolatenční timery, akcelerace).

## Struktura plasmoidu

```
claude-usage-widget-plasma/
├── metadata.json           # název, kategorie, ID, Icon
├── contents/
│   ├── ui/
│   │   ├── main.qml                 # PlasmoidItem root (entry point v Plasma 6)
│   │   ├── CompactRepresentation.qml # tile v panelu (to co vidíš stále)
│   │   ├── FullRepresentation.qml   # popup po kliknutí / hoveru
│   │   ├── ConfigGeneral.qml        # Settings UI
│   │   ├── ConfigToggl.qml          # Settings UI pro Toggl
│   │   ├── TogglChart.qml           # kumulativní graf (Canvas)
│   │   └── HistoryChart.qml         # Claude/Codex graf
│   ├── config/
│   │   ├── main.xml         # schéma pro Plasmoid.configuration
│   │   └── config.qml       # menu/tabs nastavení
│   └── logic/
│       ├── api-claude.js    # fetch + parse Claude/Codex
│       ├── api-toggl.js     # fetch + parse Toggl
│       └── pace.js          # výpočty (working days, implied rate, atd.)
```

**Plasma 6 API fakta:**
- Root QML musí být `PlasmoidItem` (dříve `Plasmoid.Root`)
- `ui/main.qml` je pevný entry point (nelze přejmenovat)
- `compactRepresentation.Layout.preferredWidth` = šířka tile v horizontálním panelu; thickness panelu si škáluje automaticky
- `fullRepresentation` má fixní max rozměry v system tray (pokud je plasmoid vnořený)

## HTTP fetch v QML — konkrétně

```qml
function fetchClaude() {
    var xhr = new XMLHttpRequest()
    xhr.open("POST", "https://api.anthropic.com/v1/messages")
    xhr.setRequestHeader("Authorization", "Bearer " + token)
    xhr.setRequestHeader("anthropic-beta", "oauth-2025-04-20")
    xhr.onreadystatechange = function() {
        if (xhr.readyState === 4) {
            // xhr.getResponseHeader("anthropic-ratelimit-unified-5h-utilization")
        }
    }
    xhr.send(JSON.stringify({...}))
}
```

`XMLHttpRequest` v QML má přístup ke všem response headers — klíčové pro Claude rate limit data. Toggl funguje stejně s Basic auth.

## Credential storage

**Problém:** ve Windows widget čte credentials z `~/.claude/.credentials.json` a WSL paths. V Linuxu:
- Claude CLI ukládá tam samé: `~/.claude/.credentials.json` → **reuse 1:1** přes `XMLHttpRequest` (čtení lokálního souboru) nebo přes malý C++/Python filelock helper
- Toggl API key: `Plasmoid.configuration.togglApiKey` (v `~/.config/plasma-...appletsrc`)

Alternativa pro citlivější storage: **KWallet** přes DBus (vyžaduje C++ plugin nebo `kwalletd` shell volání).

## Funkční parita — co portovat

| Feature z Windows widget | Plasma port — poznámka |
|---|---|
| Claude 5h + 7d rate limit bary | Přímo portnout fetch + render |
| Codex session + review bar | Detto |
| Toggl tile s progress barem + "Xh/Yh · Kč" | Detto, QML má `ProgressBar` |
| Per-project rates v Settings | `ConfigToggl.qml` — ListView s TextField per projekt |
| Měsíční target CZK | TextField v config |
| Hover popup s pace + graph | `fullRepresentation` se zobrazí po hoveru (Plasma ovládá sama) |
| Kumulativní graf z time entries | QML `Canvas` element — port `RenderCumulativeChart` do JS |
| Historický graf (Claude/Codex) | QML `Canvas` — port `HistoryChart.xaml.cs` logiky |
| Show/Hide sekce checkbox | Plasma: každá sekce = vlastní plasmoid **nebo** jeden plasmoid s toggly v configu |
| Multi-taskbar (monitor) support | **Zdarma v Plasmě** — plasmoid na každém panelu si drží vlastní instanci, sdílení stavu přes `Plasmoid.configuration` nebo DBus |
| Always on top / auto-hide detection | **Nepotřebuje** — plasmoid je součást panelu, řeší si sama Plasma |
| Fullscreen hide logic | **Nepotřebuje** — detto |
| Topmost enforcer | **Nepotřebuje** |

**Odhad redukce kódu: ~30–40 %** — spousta Win32 P/Invoke (MainWindow.xaml.cs) je v Plasmě zbytečná protože plasmoid je **součást panelu**, ne samostatné okno.

## Update mechanismus

**Tři varianty v pořadí od nejjednodušší po nejnativnější:**

### 1. KDE Store + KNewStuff (doporučeno)
- Upload `.plasmoid` package (ZIP s `metadata.json` + `contents/`) na https://store.kde.org
- Uživatel instaluje přes **System Settings → Search "Get New" → Plasma Widgets** (ovládá `kpackagetool6` + KNewStuff)
- Update: System Settings detekuje novou verzi v storu, tlačítko "Update". Plně spravované KDE Frameworky.
- **Bez nutnosti vlastního update kódu** — verzování přes `metadata.json` `Version` field.
- Nevýhoda: moderation review při prvním uploadu (obvykle rychlá).

### 2. Flatpak / Flathub
- Pokud chceš i desktopovou verzi s Settings UI → Flatpak app na Flathubu
- Plasmoid samotný **nejde** distribuovat jako Flatpak (plasmoid musí být v user data path, ne v sandboxu)
- Kombinace: Flatpak app + plasmoid samostatně ze store

### 3. Vlastní self-update (jako ve Windows)
- QML fetch z GitHub releases API → kontrola `metadata.json` version → download `.plasmoid` → `kpackagetool6 -u file.plasmoid --type Plasma/Applet`
- Port stávající logiky `Updater.cs` do JS
- **Nedoporučuji** — duplikuje práci KNewStuff a naráží na permissions (plasmoid se updatuje za běhu = riziko crash Plasma shellu)

**Závěr:** jít cestou 1 (KDE Store). Pro early access můžeš zveřejnit `.plasmoid` soubor i na GitHub releases a zájemci ho nainstalují přes `kpackagetool6 -t Plasma/Applet -i claude-usage-0.3.plasmoid`.

## Šablona — rychlý start

```
# Vytvoří kostru projektu
mkdir -p ~/plasma-port/claude-usage/contents/ui
cd ~/plasma-port/claude-usage

# Minimální metadata.json
cat > metadata.json <<EOF
{
  "KPlugin": {
    "Id": "org.zrnik.claude-usage",
    "Name": "Claude Usage Widget",
    "Description": "Display Claude/Codex usage limits and Toggl earnings in Plasma panel",
    "Version": "0.3.0",
    "Authors": [{"Name": "Štěpán Zrník", "Email": "stepan.zrnik@gmail.com"}],
    "License": "MIT",
    "Category": "System Information",
    "Icon": "applications-development"
  },
  "KPackageStructure": "Plasma/Applet",
  "X-Plasma-API-Minimum-Version": "6.0"
}
EOF

# Testování lokálně bez packagingu:
kpackagetool6 -t Plasma/Applet -i .
# Potom přidat do panelu přes pravý klik → "Add Widgets" → najít "Claude Usage Widget"

# Reload po změnách:
kpackagetool6 -t Plasma/Applet -u .
killall plasmashell && kstart plasmashell
```

## Objemový odhad

Založené na stávajícím C# codebase (~1400 LOC v relevantních souborech):

| Část | LOC C# | Odhad QML+JS | Poznámka |
|---|---|---|---|
| Claude/Codex API klient | ~350 | ~200 | Jednoduchá XHR logika |
| Toggl API klient | ~250 | ~180 | Detto |
| Progress bar tile | ~250 | ~150 | Deklarativnější v QML |
| Popup / chart | ~650 | ~400 | Canvas kreslení + layout |
| Settings UI | ~220 | ~150 | QML má lepší built-ins |
| MainWindow / taskbar P/Invoke | ~400 | **0** | Plasma řeší nativně |
| Credentials reader | ~200 | ~80 | FileIO via XHR |
| History storage | ~150 | ~100 | Qt.labs.settings / FileIO |
| **Celkem** | **~2470** | **~1260** | ~50 % redukce |

**Odhad času:** první funkční verze (Claude 5h bar v panelu) do **1–2 dny**. Plná funkční parita (Claude + Codex + Toggl + grafy + Settings) **~5–8 dní** soustředěné práce. Debugging panel layoutů a QML má svoje specifika, takže +20–30 % rezerva.

## Rizika a nejasnosti

- **Credentials cross-platform**: Windows widget umí číst WSL i native path. Na Linuxu stačí native `~/.claude/.credentials.json`, ale pokud Claude CLI přes Flatpak ukládá jinam, je potřeba fallback
- **Toggl API v QML XHR**: Basic auth přes `xhr.open("GET", url, true, user, password)` — **otestovat** jestli Qt XHR plně implementuje `setRequestHeader("Authorization")` (v některých verzích Qt je CORS/security omezení, na desktopu obvykle OK)
- **Panel resize edge cases**: vertikální vs horizontální panel, různé thickness — `compactRepresentation` musí testovat oba
- **Theming**: barvy bary musí respektovat Plasma color scheme (tmavý/světlý theme) — použít `PlasmaCore.Theme` / `Kirigami.Theme` brushes místo hardcoded hex
- **Auto-start**: plasmoid v panelu **auto-běží**, žádné "startup" řešení (oproti Windows kde je startup menu položka)

## Závěr / doporučení

**Jdi do toho přes KDE Plasma native API**, ne Electron/hybrid. Benefity:
- Žádné Win32 hacky = **50 % méně kódu**
- Nativní škálování s panelem, theming, focus management
- KDE Store zdarma vyřeší distribuci + updaty
- QML umí přesně to co WPF (deklarativní UI + binding + canvas)

**První milník (MVP):** Claude 5h rate limit tile v panelu + minimalistický popup. Zvládneš během víkendu a dostaneš instant feedback jestli Plasma QML vyhovuje. Toggl a grafy přidávat postupně.

## Sources

- [KDE Plasma Widget Tutorial](https://develop.kde.org/docs/plasma/widget/)
- [Widget Properties](https://develop.kde.org/docs/plasma/widget/properties/)
- [Porting Plasmoids to KF6](https://develop.kde.org/docs/plasma/widget/porting_kf6/)
- [Plasma QML API](https://develop.kde.org/docs/plasma/widget/plasma-qml-api/)
- [Zren's Plasma Widget Tutorial](https://zren.github.io/kde/docs/widget/)
- [KNewStuff framework](https://github.com/KDE/knewstuff)
- [KDE Distribute docs](https://develop.kde.org/distribute/)
- [KDE Store](https://store.kde.org/)
