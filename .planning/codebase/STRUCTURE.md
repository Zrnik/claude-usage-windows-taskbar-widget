# Codebase Structure

**Analysis Date:** 2026-03-05

## Directory Layout

```
claude-usage-widget/
├── ClaudeUsageWidget/                      # Solution root
│   ├── ClaudeUsageWidget.sln              # Visual Studio solution file
│   ├── README.md                           # Project documentation
│   └── ClaudeUsageWidgetProvider/         # Main provider application
│       ├── ClaudeUsageWidgetProvider.csproj
│       ├── Program.cs                      # COM registration entry point
│       ├── WidgetProvider.cs               # Widget lifecycle & updates
│       ├── ClaudeApiClient.cs              # Claude API integration
│       ├── CredentialStore.cs              # Credential loading/persisting
│       ├── FactoryHelper.cs                # COM interop & class factory
│       ├── Package.appxmanifest            # Windows App Package manifest
│       ├── Templates/                      # Adaptive Card JSON templates
│       │   ├── UsageWidgetTemplate.json    # Main usage display
│       │   ├── LoginRequiredTemplate.json  # Auth required state
│       │   └── ErrorTemplate.json          # Error display state
│       ├── Assets/                         # Application icons
│       │   ├── Square150x150Logo.png
│       │   ├── Square44x44Logo.png
│       │   └── StoreLogo.png
│       ├── ProviderAssets/                 # Marketing/provider assets
│       │   ├── ClaudeIcon.png
│       │   └── UsageScreenshot.png
│       ├── bin/                            # Build output (generated)
│       └── obj/                            # Object files (generated)
└── .planning/                              # Analysis documents
    └── codebase/
```

## Directory Purposes

**ClaudeUsageWidget/:**
- Purpose: Solution container for the widget provider project
- Contains: One project (ClaudeUsageWidgetProvider)
- Key files: `ClaudeUsageWidget.sln` (Visual Studio solution)

**ClaudeUsageWidgetProvider/:**
- Purpose: Main Windows widget provider application
- Contains: C# source code, templates, assets, configuration
- Key files: `Program.cs` (startup), `WidgetProvider.cs` (core logic)

**Templates/:**
- Purpose: Adaptive Card UI definitions for different widget states
- Contains: JSON template files with Adaptive Card schema
- Key files: `UsageWidgetTemplate.json` (primary display), fallback templates for auth/errors

**Assets/:**
- Purpose: Windows App Package branding icons
- Contains: PNG images for task bar and app store
- Generated: No, committed to repo

**ProviderAssets/:**
- Purpose: Widget provider showcase images and icons
- Contains: Icon and screenshot for widget catalog display
- Generated: No, committed to repo

**bin/ and obj/:**
- Purpose: Compiler output directories
- Generated: Yes, created during `dotnet build`
- Committed: No, ignored in .gitignore

## Key File Locations

**Entry Points:**
- `ClaudeUsageWidgetProvider/Program.cs`: Application startup, COM registration
- `ClaudeUsageWidgetProvider/WidgetProvider.cs`: Widget lifecycle implementation

**Configuration:**
- `ClaudeUsageWidgetProvider/ClaudeUsageWidgetProvider.csproj`: Build configuration, dependencies, output type
- `ClaudeUsageWidgetProvider/Package.appxmanifest`: Windows App Package identity and capabilities

**Core Logic:**
- `ClaudeUsageWidgetProvider/WidgetProvider.cs`: Widget state management, template rendering, refresh coordination
- `ClaudeUsageWidgetProvider/ClaudeApiClient.cs`: HTTP requests, token refresh, usage API calls
- `ClaudeUsageWidgetProvider/CredentialStore.cs`: Credential file I/O, multi-environment support

**COM Interop:**
- `ClaudeUsageWidgetProvider/FactoryHelper.cs`: IClassFactory implementation, P/Invoke declarations

**Presentation:**
- `ClaudeUsageWidgetProvider/Templates/UsageWidgetTemplate.json`: Primary widget UI (Adaptive Card)
- `ClaudeUsageWidgetProvider/Templates/LoginRequiredTemplate.json`: Authentication required state
- `ClaudeUsageWidgetProvider/Templates/ErrorTemplate.json`: Error display fallback

## Naming Conventions

**Files:**
- C# classes: PascalCase (e.g., `WidgetProvider.cs`, `ClaudeApiClient.cs`)
- JSON templates: PascalCaseWithDescriptor (e.g., `UsageWidgetTemplate.json`, `LoginRequiredTemplate.json`)
- Assets: lowercase-with-hyphens or PascalCase (e.g., `Square150x150Logo.png`, `ClaudeIcon.png`)

**Directories:**
- Top-level projects: PascalCase matching namespace (e.g., `ClaudeUsageWidgetProvider`)
- Logical groupings: PascalCase (e.g., `Templates`, `Assets`, `ProviderAssets`)

**C# Namespaces:**
- Single namespace: `ClaudeUsageWidgetProvider` (matches project name)
- No sub-namespaces; all classes in single namespace

**Classes and Types:**
- Public/Internal classes: PascalCase (e.g., `WidgetProvider`, `ClaudeApiClient`, `OAuthCredential`)
- Nested types: PascalCase (e.g., `WidgetInfo` nested in `WidgetProvider`)
- Interfaces: I-prefix PascalCase (e.g., `IWidgetProvider` from SDK, `IClassFactory`)

**Fields and Properties:**
- Private fields: camelCase with `_` prefix (e.g., `_widgets`, `_apiClient`, `_usageTemplate`)
- Public properties: PascalCase (e.g., `AccessToken`, `IsExpired`)
- Constants: UPPER_CASE (e.g., `MinFetchInterval`, `RefreshInterval`)

## Where to Add New Code

**New Feature (e.g., additional widget metrics):**
- Primary code: Extend `WidgetProvider.cs` with new refresh/update logic; add new model class alongside `UsageData`
- API integration: Add methods to `ClaudeApiClient.cs` for new endpoint
- UI: Create new Adaptive Card JSON in `Templates/`
- Template data: Extend `BuildUsageData()` method to include new fields

**New Model/DTO:**
- Location: Add as class in relevant file (`ClaudeApiClient.cs` for API models, `CredentialStore.cs` for credential types)
- Pattern: Sealed class with auto-properties, no logic beyond property storage

**Utility/Helper Class:**
- Location: If < 50 lines and related to one feature, nest it in the relevant file
- Pattern: Internal sealed class with static methods
- Example: `WidgetInfo` is nested in `WidgetProvider`

**Error Handling Improvements:**
- Location: Modify catch blocks in `WidgetProvider.UpdateWidgetAsync()`, `ClaudeApiClient.GetUsageAsync()`, `CredentialStore` methods
- Current pattern: Swallow exceptions, return null or stale data, let caller decide UI fallback
- Enhancement: Consider structured logging framework (not currently present)

## Special Directories

**Templates/:**
- Purpose: Adaptive Card UI markup (JSON)
- Generated: No; manually maintained
- Committed: Yes
- Strategy: Edit in-place; test via `dotnet run --console` locally

**Assets/ and ProviderAssets/:**
- Purpose: Icon/image resources
- Generated: No; source images external
- Committed: Yes
- Strategy: Replace PNG files during asset updates

**bin/ and obj/:**
- Purpose: Compiler output
- Generated: Yes, by `dotnet build`
- Committed: No (in .gitignore)
- Strategy: Clean with `dotnet clean`

## Build Output Structure

**PublishProfile:** Configured in `.csproj` as `win-$(Platform)` for platform-specific build
**RuntimeIdentifiers:** `win-x64;win-arm64` (dual-architecture support)
**MSIX Packaging:** Enabled via `EnableMsixTooling` and `WindowsPackageType=MSIX`
**Output Assembly:** `ClaudeUsageWidgetProvider.exe` (COM-registered executable)
