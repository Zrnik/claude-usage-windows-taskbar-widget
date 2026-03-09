@echo off
setlocal

:: Find production process path before killing it
set "PROD_PATH="
for /f "tokens=2 delims=," %%a in ('wmic process where "name like 'ClaudeUsageWidget-win-%%'" get ExecutablePath /format:csv 2^>nul ^| findstr /i "ClaudeUsageWidget-win-"') do set "PROD_PATH=%%a"

:: Kill production
if defined PROD_PATH (
    echo Killing production: %PROD_PATH%
    taskkill /IM ClaudeUsageWidget-win-x64.exe /F >nul 2>&1
    taskkill /IM ClaudeUsageWidget-win-arm64.exe /F >nul 2>&1
    timeout /t 1 /nobreak >nul
)

:: Run dev build (blocks until closed)
echo Starting dev build...
"C:\Program Files\dotnet\dotnet.exe" run --project ClaudeUsageWidget

:: Restart production
if defined PROD_PATH (
    echo Restarting production: %PROD_PATH%
    start "" "%PROD_PATH%"
)
