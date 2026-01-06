using Microsoft.PowerToys.Settings.UI.Library;
using System.Collections.Generic;
using System.Linq;

namespace Community.PowerToys.Run.Plugin.RunLLM
{
    /// <summary>
    /// Manages plugin settings with thread-safe access.
    /// </summary>
    public class PluginSettings
    {
        private readonly object _lock = new();

        // Settings properties
        private string _url = Constants.DefaultUrl;
        private string _model = Constants.DefaultModel;
        private string _systemPrompt = Constants.DefaultSystemPrompt;
        private string _apiKey = "";
        private bool _useApiKey = false;
        private bool _enableThinking = false;
        private string _reasoningEffort = "medium";
        private string _thinkingModeType = "prompt"; // "prompt" or "request"

        public string Url
        {
            get { lock (_lock) return _url; }
            set { lock (_lock) _url = value; }
        }

        public string Model
        {
            get { lock (_lock) return _model; }
            set { lock (_lock) _model = value; }
        }

        public string SystemPrompt
        {
            get { lock (_lock) return _systemPrompt; }
            set { lock (_lock) _systemPrompt = value; }
        }

        public string ApiKey
        {
            get { lock (_lock) return _apiKey; }
            set { lock (_lock) _apiKey = value; }
        }

        public bool UseApiKey
        {
            get { lock (_lock) return _useApiKey; }
            set { lock (_lock) _useApiKey = value; }
        }

        public bool EnableThinking
        {
            get { lock (_lock) return _enableThinking; }
            set { lock (_lock) _enableThinking = value; }
        }

        public string ReasoningEffort
        {
            get { lock (_lock) return _reasoningEffort; }
            set { lock (_lock) _reasoningEffort = value; }
        }

        /// <summary>
        /// "prompt" = use /think or /no_think in prompt (Qwen3, etc.)
        /// "request" = use enable_thinking in request body (OpenAI, etc.)
        /// </summary>
        public string ThinkingModeType
        {
            get { lock (_lock) return _thinkingModeType; }
            set { lock (_lock) _thinkingModeType = value; }
        }

        /// <summary>
        /// Defines the additional options shown in PowerToys settings.
        /// </summary>
        public IEnumerable<PluginAdditionalOption> AdditionalOptions =>
        [
            new()
            {
                Key = Constants.SettingKeyUrl,
                DisplayLabel = "LLM URL",
                DisplayDescription = "API endpoint URL. Ex: http://localhost:11434",
                PluginOptionType = PluginAdditionalOption.AdditionalOptionType.Textbox,
                TextValue = Constants.DefaultUrl,
            },
            new()
            {
                Key = Constants.SettingKeyModel,
                DisplayLabel = "Default Model",
                DisplayDescription = "Model to use. Ex: qwen/qwen3-4b, llama3.1:8b",
                PluginOptionType = PluginAdditionalOption.AdditionalOptionType.Textbox,
                TextValue = Constants.DefaultModel,
            },
            new()
            {
                Key = Constants.SettingKeyApiKey,
                DisplayLabel = "API Key",
                DisplayDescription = "Enable to use API key authentication",
                PluginOptionType = PluginAdditionalOption.AdditionalOptionType.CheckboxAndTextbox,
                TextValue = "",
            },
            new()
            {
                Key = Constants.SettingKeyEnableThinking,
                DisplayLabel = "Enable Thinking Mode",
                DisplayDescription = "Enable reasoning/thinking for supported models",
                PluginOptionType = PluginAdditionalOption.AdditionalOptionType.Checkbox,
                Value = false,
            },
            new()
            {
                Key = Constants.SettingKeyReasoningEffort,
                DisplayLabel = "Reasoning Effort",
                DisplayDescription = "Level of reasoning depth (none, minimal, low, medium, high)",
                PluginOptionType = PluginAdditionalOption.AdditionalOptionType.Textbox,
                TextValue = "medium",
            },
            new()
            {
                Key = Constants.SettingKeySystemPrompt,
                DisplayLabel = "System Prompt",
                DisplayDescription = "Custom system instructions. Use [currentTime] for timestamp.",
                PluginOptionType = PluginAdditionalOption.AdditionalOptionType.MultilineTextbox,
                TextValue = Constants.DefaultSystemPrompt,
            }
        ];

        /// <summary>
        /// Updates settings from PowerToys settings UI.
        /// </summary>
        public void UpdateFromSettings(PowerLauncherPluginSettings settings)
        {
            if (settings?.AdditionalOptions == null) return;

            lock (_lock)
            {
                _url = GetTextValue(settings, Constants.SettingKeyUrl, Constants.DefaultUrl);
                _model = GetTextValue(settings, Constants.SettingKeyModel, Constants.DefaultModel);
                _systemPrompt = GetTextValue(settings, Constants.SettingKeySystemPrompt, Constants.DefaultSystemPrompt);
                _reasoningEffort = GetTextValue(settings, Constants.SettingKeyReasoningEffort, "medium");

                var apiKeyOption = settings.AdditionalOptions.FirstOrDefault(x => x.Key == Constants.SettingKeyApiKey);
                if (apiKeyOption != null)
                {
                    _useApiKey = apiKeyOption.Value;
                    _apiKey = apiKeyOption.TextValue ?? "";
                }

                var thinkingOption = settings.AdditionalOptions.FirstOrDefault(x => x.Key == Constants.SettingKeyEnableThinking);
                if (thinkingOption != null)
                {
                    _enableThinking = thinkingOption.Value;
                }
            }
        }

        private static string GetTextValue(PowerLauncherPluginSettings settings, string key, string defaultValue)
        {
            return settings.AdditionalOptions.FirstOrDefault(x => x.Key == key)?.TextValue ?? defaultValue;
        }
    }
}
