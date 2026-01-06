# Development Guide

## Prerequisites

- [.NET 9 SDK](https://dotnet.microsoft.com/download)
- [PowerToys](https://github.com/microsoft/PowerToys)
- LLM Server (e.g., [Ollama](https://ollama.ai))

## Quick Start

```powershell
# Clone and build
git clone https://github.com/vuquan2005/RunLLM.git
cd RunLLM
.\scripts\dev.ps1
```

This will clean, build, deploy to PowerToys, and restart it.

## Scripts

| Script | Purpose |
|--------|---------|
| `dev.ps1` | Full workflow: clean → build → deploy → restart |
| `build.ps1` | Build only (`-Configuration Release -Platform x64`) |
| `deploy.ps1` | Copy to PowerToys plugins folder |
| `clean.ps1` | Remove build artifacts (`-All` to also remove installed plugin) |

### Examples

```powershell
.\scripts\build.ps1 -Configuration Debug -Platform ARM64
.\scripts\deploy.ps1 -NoRestart
.\scripts\clean.ps1 -All
```

## Project Structure

```
RunLLM/
├── src/                          # Source code
│   ├── Main.cs                   # Plugin entry, IPlugin implementation
│   ├── QueryHandler.cs           # State machine, all UI handlers
│   ├── LLMClient.cs              # HTTP client, streaming, validation
│   ├── PluginSettings.cs         # Settings with thread-safe access
│   ├── Constants.cs              # Default values, keys
│   ├── plugin.json               # Plugin metadata
│   └── Images/                   # Icons
├── scripts/                      # PowerShell automation
├── docs/                         # Documentation
├── .github/workflows/            # CI/CD
│   ├── build.yml                 # Build on push/PR
│   └── release.yml               # Create release on tag
└── RunLLM.sln                    # Solution file
```

## Architecture

### State Machine (QueryHandler)

```
Idle → WaitingResponse → StreamResponse → ShowResponse → Idle
  ↓
GetListOfModels → ChoosingModel → Idle
  ↓
ChangingThinkingMode → Idle
  ↓
ChangingEndpoint → Idle
```

### Key Classes

| Class | Responsibility |
|-------|----------------|
| `Main` | Plugin interface, settings provider |
| `QueryHandler` | State machine, query handling, UI |
| `LLMClient` | HTTP requests, streaming, endpoint validation |
| `PluginSettings` | Thread-safe settings with lock |
| `Constants` | All default values and keys |

## Version Sync

Version is automatically synced from `plugin.json` to the DLL via MSBuild:

```xml
<!-- In .csproj - reads version from plugin.json at build time -->
<Target Name="ReadPluginVersion" BeforeTargets="BeforeBuild">
  ...
</Target>
```

To create a new release:

```powershell
# 1. Update version in plugin.json
# 2. Commit and push
git add . && git commit -m "v2.x.x" && git push origin master

# 3. Create and push tag
git tag v2.x.x && git push origin v2.x.x
```

GitHub Actions will automatically build and create the release.

## Testing

1. Start Ollama: `ollama serve`
2. Pull a model: `ollama pull qwen3:4b`
3. Run dev script: `.\scripts\dev.ps1`
4. Open PowerToys Run: `Alt + Space`
5. Type: `runllm Hello`

### Test Checklist

- [ ] Basic chat works
- [ ] Streaming displays correctly
- [ ] Model switching works
- [ ] Thinking mode toggles
- [ ] Endpoint validation (try invalid URL)
- [ ] Copy to clipboard works

## Troubleshooting

| Issue | Solution |
|-------|----------|
| Plugin not loading | Check folder: `%LocalAppData%\Microsoft\PowerToys\PowerToys Run\Plugins\RunLLM` |
| No response from LLM | Verify Ollama running: `curl http://localhost:11434/v1/models` |
| Settings not showing | Restart PowerToys completely |
| Build fails | Run `dotnet restore` first |

## Contributing

1. Fork the repository
2. Create feature branch: `git checkout -b feature/my-feature`
3. Commit changes: `git commit -m "Add my feature"`
4. Push and create PR
