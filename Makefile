SHELL = cmd.exe
DOTNET = "C:\Program Files\dotnet\dotnet.exe"

run:
	dev-run.cmd

build:
	$(DOTNET) build ClaudeUsageWidget
