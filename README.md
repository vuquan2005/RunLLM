# RunLLM Plugin for PowerToys Run

RunLLM integrates Large Language Models (LLMs) into PowerToys Run, enabling direct AI interaction from the search bar. It supports any service with an OpenAI API-style endpoint (e.g., Ollama, LMStudio).

## ✨ Features

- 💬 **Chat with LLMs**: Send prompts and receive streaming responses.
- 🔄 **Switch Models**: List and switch between available models from the API.
- 🧠 **Thinking Mode**: Toggle reasoning mode with `/think` or `/no_think`.
- 📝 **Custom System Prompt**: Define custom system instructions.
- 📋 **Quick Copy**: Copy full or partial responses.
- ⚙️ **Configurable**: Set API URL, default model, and system prompt.

## 📦 Requirements

- Any OpenAI-style service (e.g., Ollama, LMStudio).

## 🔧 Installation

1. **Download**: Get the release files, including `Community.PowerToys.Run.Plugin.RunLLM.dll`.
2. **Decompress and Copy**: Place the files in:  
   `%LocalAppData%\Microsoft\PowerToys\PowerToys Run\Plugins`

   Example structure:
   ```
   RunLLM/
   ├── Community.PowerToys.Run.Plugin.RunLLM.dll
   └── Images/
       ├── model.png
       ├── run.png
       ├── change.png
       ├── brain.png
       ├── timer.png
       ├── transfer.png
       └── access.png
   ```

3. **Restart PowerToys**: Ensure PowerToys is restarted to load the plugin.

## ⚙️ Configuration

In **PowerToys Settings** → **PowerToys Run** → **RunLLM**:

- 🌐 **LLM URL**: Set the API endpoint (e.g., `http://localhost:11434` for Ollama).
- 🏷️ **Default Model**: Specify the default model (e.g., `qwen/qwen3-4b`, `llama3.1:70b`, `gpt-oss:latest`).

## 🚀 Usage

1. Open PowerToys Run: `Alt + Space`.
2. Type your query:
   ```
   runllm What is the capital of France?
   ```

## 📝 Notes

- ✅ If **Include in global result** is enabled, you can chat without typing `runllm`.
- 🔑 Use the `runllm` keyword to change models or toggle thought mode, regardless of global result settings.
- ⚠️ Ensure your LLM service supports `/v1/models` and `/v1/chat/completions` endpoints.

## 🤝 Contributing

Contributions are welcome! Submit issues or PRs at the [GitHub Repo](https://github.com).

## 📜 License

MIT License – see [LICENSE](https://github.com).