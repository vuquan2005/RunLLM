# RunLLM - PowerToys Run Plugin

[![Build](https://github.com/vuquan2005/RunLLM/actions/workflows/build.yml/badge.svg)](https://github.com/vuquan2005/RunLLM/actions/workflows/build.yml)
[![Release](https://img.shields.io/github/v/release/vuquan2005/RunLLM)](https://github.com/vuquan2005/RunLLM/releases)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

> 🤖 Chat with Large Language Models directly from PowerToys Run

## Features

- **Streaming Responses** - Real-time text streaming from LLMs
- **Model Switching** - Switch between models without leaving PowerToys Run
- **Thinking Mode** - Toggle reasoning with `/think` (Qwen3) or `enable_thinking` (OpenAI)
- **Endpoint Switching** - Change API endpoint on-the-fly with validation
- **API Key Support** - Works with OpenAI, OpenRouter, and other authenticated APIs
- **Custom System Prompt** - Define your own instructions

## Requirements

- [PowerToys](https://github.com/microsoft/PowerToys) v0.70.0+
- OpenAI-compatible LLM server:
  - [Ollama](https://ollama.ai) (local, recommended)
  - [LM Studio](https://lmstudio.ai)
  - OpenAI API / OpenRouter

## Installation

### From Release

1. Download latest ZIP from [Releases](https://github.com/vuquan2005/RunLLM/releases)
2. Extract to `%LocalAppData%\Microsoft\PowerToys\PowerToys Run\Plugins\RunLLM`
3. Restart PowerToys

**Folder structure after extraction:**

```
Plugins/
└── RunLLM/
    ├── Community.PowerToys.Run.Plugin.RunLLM.dll
    ├── Community.PowerToys.Run.Plugin.RunLLM.deps.json
    ├── plugin.json
    └── Images/
        └── *.png
```

### From Source

```powershell
git clone https://github.com/vuquan2005/RunLLM.git
cd RunLLM
.\scripts\dev.ps1
```

## Usage

Open PowerToys Run (`Alt + Space`) and type:

| Command | Action |
|---------|--------|
| `runllm <question>` | Ask the LLM |
| `runllm` → Change Model | Switch between available models |
| `runllm` → Thinking Mode | Toggle thinking, select mode type |
| `runllm` → Change Endpoint | Validate and set new API URL |

### Examples

```
runllm What is the capital of France?
runllm Explain async/await in JavaScript
runllm Write a Python function to reverse a string
```

## Configuration

Go to **PowerToys Settings** → **PowerToys Run** → **Plugins** → **RunLLM**:

| Setting | Description | Default |
|---------|-------------|---------|
| LLM URL | API endpoint | `http://localhost:11434` |
| Default Model | Model name | `qwen/qwen3-4b` |
| API Key | Enable + enter key | (disabled) |
| Enable Thinking | Toggle thinking mode | Off |
| System Prompt | Custom instructions | (empty) |

## Development

See [DEVELOPMENT.md](docs/DEVELOPMENT.md) for build instructions.

```powershell
.\scripts\dev.ps1      # Build + Deploy + Restart
.\scripts\build.ps1    # Build only
.\scripts\clean.ps1    # Clean artifacts
```

## Architecture

```
src/
├── Main.cs            # Plugin entry point
├── QueryHandler.cs    # State machine, UI handlers
├── LLMClient.cs       # HTTP client, streaming
├── PluginSettings.cs  # Settings management
└── Constants.cs       # Default values
```

## License

MIT License - see [LICENSE.txt](LICENSE.txt)