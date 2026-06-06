SHELL = cmd.exe
DOTNET = "C:\Program Files\dotnet\dotnet.exe"

run:
	dev-run.cmd

build:
	$(DOTNET) build ClaudeUsageWidget

linux-daemon:
	dotnet run --project src/ClaudeUsageWidget.LinuxDaemon/ClaudeUsageWidget.LinuxDaemon.csproj

deb:
	./scripts/build-deb.sh
