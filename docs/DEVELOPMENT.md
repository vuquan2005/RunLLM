# RunLLM Plugin Development Guide

## Prerequisites

- [.NET 9 SDK](https://dotnet.microsoft.com/download) or later
- [PowerToys](https://github.com/microsoft/PowerToys) installed
- (Optional) [Ollama](https://ollama.ai) or any OpenAI-compatible LLM server

## Quick Start

```powershell
# Full development workflow (clean → build → deploy → restart PowerToys)
.\scripts\dev.ps1

# Or run steps individually:
.\scripts\build.ps1 -Configuration Release -Platform x64
.\scripts\deploy.ps1
```

## Scripts Reference

| Script | Purpose | Options |
|--------|---------|---------|
| `build.ps1` | Build the plugin | `-Configuration (Debug\|Release)`, `-Platform (x64\|ARM64)` |
| `deploy.ps1` | Deploy to PowerToys | `-Configuration`, `-Platform`, `-NoRestart` |
| `clean.ps1` | Clean build artifacts | `-All` (also removes installed plugin) |
| `dev.ps1` | Full workflow | `-Configuration`, `-Platform`, `-SkipClean` |

## Project Structure

```
RunLLM/
├── scripts/          # Automation scripts
├── src/              # Source code
│   ├── Main.cs       # Plugin entry point
│   ├── Images/       # Plugin icons
│   └── plugin.json   # Plugin metadata
├── docs/             # Documentation
├── RunLLM.sln        # Solution file
└── README.md
```

## Testing

1. Run `.\scripts\dev.ps1`
2. Press `Alt + Space` to open PowerToys Run
3. Type `runllm Hello` and press Enter

## Troubleshooting

- **Plugin not loading**: Check `%LOCALAPPDATA%\Microsoft\PowerToys\PowerToys Run\Plugins\RunLLM`
- **LLM not responding**: Ensure Ollama is running at `http://localhost:11434`
