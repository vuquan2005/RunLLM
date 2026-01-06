# RunLLM Plugin for PowerToys Run

[![Build](https://github.com/vuquan2005/RunLLM/actions/workflows/build.yml/badge.svg)](https://github.com/vuquan2005/RunLLM/actions/workflows/build.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

> 🤖 Chat with LLMs directly from PowerToys Run!

RunLLM integrates Large Language Models into PowerToys Run, enabling direct AI interaction from the search bar. It supports any service with an OpenAI API-style endpoint (Ollama, LMStudio, OpenAI, etc.).

![Demo](docs/demo.gif)

## ✨ Features

- 💬 **Stream Chat**: Real-time streaming responses from LLMs
- 🔄 **Model Switching**: Switch between available models on-the-fly
- 🧠 **Thinking Mode**: Toggle reasoning mode with `/think` or `/no_think`
- 📝 **Custom System Prompt**: Define your own system instructions
- 📋 **Quick Copy**: Copy responses to clipboard instantly
- ⚙️ **Fully Configurable**: Set API URL, default model, and more

## 📦 Requirements

- [PowerToys](https://github.com/microsoft/PowerToys) v0.70.0 or later
- Any OpenAI-compatible LLM service:
  - [Ollama](https://ollama.ai) (recommended for local)
  - [LM Studio](https://lmstudio.ai)
  - OpenAI API
  - Any `/v1/chat/completions` compatible endpoint

## 🔧 Installation

### Option 1: Download Release (Recommended)

1. Download the latest release from [Releases](https://github.com/vuquan2005/RunLLM/releases)
2. Extract to: `%LocalAppData%\Microsoft\PowerToys\PowerToys Run\Plugins\RunLLM`
3. Restart PowerToys

### Option 2: Build from Source

```powershell
git clone https://github.com/vuquan2005/RunLLM.git
cd RunLLM
.\scripts\dev.ps1
```

## ⚙️ Configuration

Open **PowerToys Settings** → **PowerToys Run** → **Plugins** → **RunLLM**:

| Setting | Description | Default |
|---------|-------------|---------|
| LLM URL | API endpoint | `http://localhost:11434` |
| Default Model | Model to use | `qwen/qwen3-4b` |
| System Prompt | Custom instructions | (empty) |

## 🚀 Usage

1. Press `Alt + Space` to open PowerToys Run
2. Type your query:

```
runllm What is the capital of France?
```

### Commands

| Command | Description |
|---------|-------------|
| `runllm <query>` | Ask the LLM |
| `runllm` → "Change model" | Switch to a different model |
| `runllm` → "Thinking mode" | Toggle `/think` or `/no_think` |

### Tips

- ✅ Enable **Include in global result** to chat without typing `runllm`
- 📋 Press `Enter` on a response to copy it to clipboard
- 🔄 Model changes persist during the session

## 🛠️ Development

See [docs/DEVELOPMENT.md](docs/DEVELOPMENT.md) for development setup and build instructions.

```powershell
# Quick start
.\scripts\dev.ps1          # Build + Deploy + Restart PowerToys
.\scripts\build.ps1        # Build only
.\scripts\deploy.ps1       # Deploy only
.\scripts\clean.ps1 -All   # Clean everything
```

## 📁 Project Structure

```
RunLLM/
├── scripts/        # Automation scripts
├── src/            # Source code
├── docs/           # Documentation
└── .github/        # GitHub Actions
```

## 🤝 Contributing

Contributions are welcome! Please:

1. Fork the repository
2. Create a feature branch
3. Submit a Pull Request

## 📜 License

MIT License - see [LICENSE.txt](LICENSE.txt)