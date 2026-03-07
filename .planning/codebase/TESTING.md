# Testing Patterns

**Analysis Date:** 2026-03-05

## Test Framework

**Runner:**
- Not detected - No test projects found in codebase

**Assertion Library:**
- Not applicable

**Run Commands:**
- No test commands configured

## Test File Organization

**Location:**
- Not applicable - No test files present

**Naming:**
- Not applicable

**Structure:**
- Not applicable

## Test Structure

**Suite Organization:**
- Not applicable - No test framework in use

**Patterns:**
- Not applicable

## Mocking

**Framework:**
- Not detected

**Patterns:**
- Not applicable

**What to Mock:**
- Not applicable

**What NOT to Mock:**
- Not applicable

## Fixtures and Factories

**Test Data:**
- Not applicable

**Location:**
- Not applicable

## Coverage

**Requirements:** Not enforced

**View Coverage:**
- No coverage tools configured

## Test Types

**Unit Tests:**
- Not present

**Integration Tests:**
- Not present

**E2E Tests:**
- Not present

## Current Testing State

**No automated testing framework is present.** The codebase consists of:
- `ClaudeApiClient.cs` (175 lines) - HTTP client with caching, token refresh, org/usage API calls
- `CredentialStore.cs` (118 lines) - OAuth credential loading/saving with WSL support
- `WidgetProvider.cs` (221 lines) - Widget lifecycle management, template loading, update dispatch
- `FactoryHelper.cs` (89 lines) - COM interop factory registration
- `Program.cs` (26 lines) - Entry point and COM registration

**Challenging areas without tests:**
- `ClaudeApiClient.GetUsageAsync()` - Complex retry logic with stale data fallback
- `CredentialStore.LoadCredential()` - Multi-path credential discovery (Windows + WSL)
- `WidgetProvider.UpdateWidgetAsync()` - Credential validation, token refresh, API coordination
- `ClaudeApiClient.RefreshTokenAsync()` - Token refresh logic with JSON parsing
- Exception handling with graceful degradation - All methods swallow exceptions broadly

**Manual testing only:**
- Console mode: `CLAUDE_WIDGET_CONSOLE=1` flag or `--console` argument runs in console instead of background
- Widget creation/deletion through Windows Widgets platform
- Manual credential file creation at `~/.claude/.credentials.json`

---

*Testing analysis: 2026-03-05*
