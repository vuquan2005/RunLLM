using Microsoft.PowerToys.Settings.UI.Library;
using System;
using System.Collections.Generic;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows;
using Wox.Plugin;

namespace Community.PowerToys.Run.Plugin.RunLLM
{
    /// <summary>
    /// Main plugin class for RunLLM - Chat with LLMs from PowerToys Run.
    /// </summary>
    public class Main : IPlugin, IContextMenu, IDisposable, ISettingProvider
    {
        public static string PluginID => Constants.PluginID;
        public string Name => Constants.PluginName;
        public string Description => Constants.PluginDescription;

        private PluginInitContext? _context;
        private readonly PluginSettings _settings = new();
        private LLMClient? _llmClient;
        private QueryHandler? _queryHandler;
        private bool _disposed;

        // AdditionalOptions định nghĩa trực tiếp trong Main class
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
                Key = Constants.SettingKeySystemPrompt,
                DisplayLabel = "System Prompt",
                DisplayDescription = "Custom system instructions. Use [currentTime] for timestamp.",
                PluginOptionType = PluginAdditionalOption.AdditionalOptionType.MultilineTextbox,
                TextValue = "",
            }
        ];

        public void Init(PluginInitContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));

            _llmClient = new LLMClient(_settings);
            _queryHandler = new QueryHandler(_settings, _llmClient);
            _queryHandler.Initialize(context);
        }

        public List<Result> Query(Query query)
        {
            return _queryHandler?.HandleQuery(query) ?? [];
        }

        public void UpdateSettings(PowerLauncherPluginSettings settings)
        {
            _settings.UpdateFromSettings(settings);
        }

        public Control CreateSettingPanel()
        {
            // PowerToys handles settings UI through AdditionalOptions
            return null!;
        }

        public List<ContextMenuResult> LoadContextMenus(Result selectedResult)
        {
            if (selectedResult.ContextData is string responseText && !string.IsNullOrEmpty(responseText))
            {
                return
                [
                    new ContextMenuResult
                    {
                        PluginName = Name,
                        Title = "Copy to clipboard (Ctrl+C)",
                        FontFamily = "Segoe MDL2 Assets",
                        Glyph = "\xE8C8",
                        AcceleratorKey = Key.C,
                        AcceleratorModifiers = ModifierKeys.Control,
                        Action = _ =>
                        {
                            Clipboard.SetDataObject(responseText);
                            return true;
                        },
                    }
                ];
            }

            return [];
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed || !disposing) return;

            _queryHandler?.Dispose();
            _llmClient?.Dispose();
            _disposed = true;
        }
    }
}
