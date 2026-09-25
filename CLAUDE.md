# CLAUDE.md

@AGENTS.md

## Claude Code notes

- Verify changes with `dotnet build FluentAzure.sln -c Release` and `dotnet test FluentAzure.sln -c Release`. Both must pass on net8.0 and net10.0 before committing.
- When a test is meant to prove a bug fix, confirm it fails on the previous code, not just that it passes on the new code.
- Never print full configuration (`GetDebugView()`, dumping `IConfiguration`) while debugging. `FromEnvironment()` loads every environment variable in the machine or container, including tokens.
