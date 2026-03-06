SHELL = cmd.exe
DOTNET = "C:\Program Files\dotnet\dotnet.exe"

run:
	$(DOTNET) run --project ClaudeUsageWidget

build:
	$(DOTNET) build ClaudeUsageWidget
