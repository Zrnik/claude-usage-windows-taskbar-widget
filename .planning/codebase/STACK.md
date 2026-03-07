# Technology Stack

**Analysis Date:** 2026-03-05

## Languages

**Primary:**
- C# 11+ - Desktop application with async/await support, nullable reference types enabled

**Secondary:**
- JSON - Widget templates and configuration data

## Runtime

**Environment:**
- .NET 8.0 with Windows-specific SDK targeting `net8.0-windows10.0.22621.0`

**Package Manager:**
- NuGet - Declared in `.csproj` file format

## Frameworks

**Core:**
- Microsoft.Windows.AppSDK 1.6.250205002 - Windows App SDK for widget provider integration and lifecycle management
- Microsoft.Windows.SDK.BuildTools 10.0.26100.1742 - Build tools for Windows SDK

**Platform-Specific:**
- Windows Widgets Providers API (`Microsoft.Windows.Widgets.Providers`) - IWidgetProvider interface for widget creation/update
- WinRT interop - For COM interop with Windows widget framework via DllImport on ole32.dll

**HTTP Client:**
- Built-in `System.Net.Http.HttpClient` - Static instance with 15-second timeout for API calls

## Key Dependencies

**Critical:**
- Microsoft.WindowsAppSDK 1.6.250205002 - Core platform dependency for Windows widget integration. Without this, the widget provider cannot register or function within Windows Widgets ecosystem.
- Microsoft.Windows.SDK.BuildTools - Enables MSIX packaging and Windows SDK integration for compilation

**Core Libraries:**
- System.Net (HttpClient, HttpRequestMessage, HttpResponseMessage) - HTTP communication with Claude API
- System.Text.Json - JSON serialization/deserialization for both API responses and credential storage
- System.IO - File I/O for credential persistence and template loading

## Configuration

**Environment:**
- CLAUDE_WIDGET_CONSOLE - Optional. When set to "1", runs provider in console debug mode instead of as background service
- Command line args: `--console` flag equivalent to CLAUDE_WIDGET_CONSOLE="1"

**Build:**
- `.csproj` configuration in `ClaudeUsageWidgetProvider/ClaudeUsageWidgetProvider.csproj`
- Multi-platform support: x64 and ARM64 architectures
- MSIX packaging enabled via `EnableMsixTooling` and `WindowsPackageType=MSIX`

## Platform Requirements

**Development:**
- Windows 10.0.22621.0 or later (Windows 11 22H2)
- Visual Studio 2022 (v17.0+) for .NET 8 support
- .NET 8 SDK installed
- x64 or ARM64 processor support

**Production:**
- Windows 11 with Widgets support (build 22621.0+)
- .NET 8 runtime (Windows App SDK provides self-contained runtime via `WindowsAppSDKSelfContained=true`)
- Deployment as MSIX package with signing (currently disabled: `AppxPackageSigningEnabled=false`)

---

*Stack analysis: 2026-03-05*
