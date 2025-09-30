using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Library;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Wox.Plugin;

namespace Community.PowerToys.Run.Plugin.RunLLM
{
    public class Main : IPlugin, IContextMenu, IDisposable, ISettingProvider, IDelayedExecutionPlugin
    {
        public static string PluginID => "0167343682284415AF592A37253E75AA";
        public string Name => "RunLLM";
        public string Description => "RunLLM Description";

        private static readonly System.Net.Http.HttpClient client = new System.Net.Http.HttpClient();
        private PluginInitContext Context { get; set; }

        private string IconPath { get; set; }

        private bool Disposed { get; set; }

        // Settings
        private string Url, APIKey, DfModel;
        private bool UseAPIKeyEndpoint;
        //        private string SystemPrompt = @"You are a highly capable and efficient AI assistant running locally on a secure system. Your purpose is to provide accurate, concise, and helpful responses to user queries while prioritizing privacy and local resource constraints. Follow these guidelines:
        //1. **Accuracy and Relevance**: Provide fact-based, precise answers. If uncertain or lacking data, admit limitations and suggest alternatives (e.g., 'I don't have enough information to answer fully, but you could check [specific resource]'.).
        //2. **Clarity and Tone**: Use clear, friendly, and professional language. Adapt tone based on user preference (e.g., formal, casual, or technical).
        //3. **Resource Efficiency**: Optimize responses for low computational overhead, avoiding unnecessary verbosity or complex processing unless explicitly requested.
        //4. **Privacy**: Do not store, share, or transmit any user data outside the local system. Treat all inputs as confidential.
        //5. **Context Awareness**: Leverage any provided user context or previous interactions stored locally to personalize responses, but only if explicitly allowed by the user.
        //6. **Task Scope**: Handle a wide range of tasks, including answering questions, solving problems, generating text, or performing calculations, as requested. If a task is outside your capabilities, clearly state so.
        //7. **Safety and Ethics**: Avoid generating harmful, biased, or offensive content. Flag any problematic requests and respond neutrally or decline appropriately.
        //Current date and time: [currentTime]. Respond to all queries with these principles in mind, and ask for clarification if the user's request is ambiguous.";
        private string SystemPrompt = "";
        public IEnumerable<PluginAdditionalOption> AdditionalOptions => new List<PluginAdditionalOption>()
        {
            new PluginAdditionalOption()
            {
                Key = "LLMUrl",
                DisplayLabel = "LLM URL",
                DisplayDescription = "Enter the URL of your LLM model. Ex. http://localhost:11434",
                PluginOptionType = PluginAdditionalOption.AdditionalOptionType.Textbox,
                TextValue = "http://localhost:11434",
            },
            new PluginAdditionalOption()
            {
                Key = "DfModel",
                DisplayLabel = "Default model",
                DisplayDescription = "Your default model. Ex. qwen3",
                PluginOptionType = PluginAdditionalOption.AdditionalOptionType.Textbox,
                TextValue = "qwen3",
            },
            new PluginAdditionalOption()
            {
                Key = "APIKey",
                DisplayLabel = "Use API Key endpoint",
                DisplayDescription = "Check to use API key and enter your key below",
                PluginOptionType = PluginAdditionalOption.AdditionalOptionType.CheckboxAndTextbox,
                TextValue = "YOUR_API_KEY"
            },
            new PluginAdditionalOption()
            {
                Key = "SystemPrompt",
                DisplayLabel = "System prompt",
                DisplayDescription = "Enter the system prompt",
                PluginOptionType = PluginAdditionalOption.AdditionalOptionType.MultilineTextbox,
                TextValue = SystemPrompt
            }
        };

        public void UpdateSettings(PowerLauncherPluginSettings settings)
        {
            if (settings?.AdditionalOptions != null)
            {
                Url = settings.AdditionalOptions.FirstOrDefault(x => x.Key == "LLMUrl")?.TextValue ?? "http://localhost:11434";
                DfModel = settings.AdditionalOptions.FirstOrDefault(x => x.Key == "DfModel")?.TextValue ?? "qwen/qwen3-4b";

                var apiOption = settings.AdditionalOptions.FirstOrDefault(x => x.Key == "APIKey");
                UseAPIKeyEndpoint = apiOption != null && apiOption.Value is bool b2 && b2;
                APIKey = apiOption?.TextValue ?? "";

                SystemPrompt = settings.AdditionalOptions.FirstOrDefault(x => x.Key == "SystemPrompt")?.TextValue ?? SystemPrompt;
            }
        }


        public Control CreateSettingPanel()
        {
            throw new NotImplementedException();
        }

        private async Task<List<string>> FetchModelsFromEndpointAsync()
        {
            try
            {
                string apiUrl;
                apiUrl = $"{Url}/v1/models";

                var response = await client.GetStringAsync(apiUrl);
                var jsonDocument = JsonDocument.Parse(response);

                List<string> modelNames = new List<string>();

                // OpenAI-style: { "data": [ { "id": "gpt-3.5-turbo" }, ... ] }
                var models = jsonDocument.RootElement.GetProperty("data").EnumerateArray();
                foreach (var model in models)
                {
                    var modelName = model.GetProperty("id").GetString();
                    if (!string.IsNullOrEmpty(modelName))
                        modelNames.Add(modelName);
                }

                return modelNames;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error fetching models from API: {ex.Message}");
                return new List<string>();
            }
        }

        private async Task<string> QueryAsync(string prompt, string model)
        {
            SystemPrompt.Replace("[currentTime]", DateTime.Now.ToString());
            var body = new
            {
                model = model,
                prompt = SystemPrompt + "\n\n" + prompt,
                temperature = 0.8,
                top_k = 40,
                top_p = 0.95,
                min_p = 0.05,
                repeat_penalty = 1.1,
                max_token = 1024,
            };


            string json = JsonSerializer.Serialize(body);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            // LM Studio local API
            var response = await client.PostAsync($"{Url}/v1/completions", content);
            response.EnsureSuccessStatusCode();

            string responseContent = await response.Content.ReadAsStringAsync();

            using var doc = JsonDocument.Parse(responseContent);
            return doc.RootElement
                      .GetProperty("choices")[0]
                      .GetProperty("text")
                      .GetString();
        }


        public List<Result> Query(Query query)

        {
            List<Result> results = [];
            return results;
        }


        private static bool isChooseModel = false;
        private static bool isWaitingForGetListModel = false;
        private static bool isWaitingForResponse = false;
        private short waited = 0;
        private string responseText = "";
        List<string> modelNames = new List<string>();
        public List<Result> Query(Query query, bool delayedExecution)
        {
            var search = query.Search;


            List<Result> results = new List<Result>();
            if (isWaitingForResponse)
            {
                waited += 1;
                _ = Task.Run(async () =>
                {
                    await Task.Delay(1000);
                    Context.API.ChangeQuery($"{Context.CurrentPluginMetadata.ActionKeyword} " + search, requery: true);
                });
                return [new Result
                {
                    Title = search,
                    SubTitle = $"Waited : {waited}s.",
                    IcoPath = IconPath
                }];
            }
            else if (waited > 0 || responseText != "")
            {
                waited = 0;
                return [new Result
                {
                    Title = $"Response by: {DfModel}:",
                    SubTitle = responseText,
                    IcoPath = IconPath,
                    Action = e =>
                    {
                        Clipboard.SetText(responseText);
                        responseText = "";
                        return true;
                    }
                }];
            }
            if (isChooseModel)
            {
                if (isWaitingForGetListModel == false)
                    modelNames = FetchModelsFromEndpointAsync().GetAwaiter().GetResult();

                isWaitingForGetListModel = true;

                foreach (var modelName in modelNames)
                {
                    results.Add(new Result
                    {
                        Title = modelName,
                        SubTitle = "",
                        IcoPath = IconPath,
                        Action = e =>
                        {
                            DfModel = modelName;
                            isChooseModel = false;
                            isWaitingForGetListModel = false;
                            Context.API.ChangeQuery($"{Context.CurrentPluginMetadata.ActionKeyword} ", requery: true);
                            return false;
                        }
                    });
                }
            }
            else
            {
                return [
                    new Result
                    {
                        Title = search,
                        SubTitle = $"Ask {DfModel}",
                        IcoPath = IconPath,
                        Action = e =>
                        {
                            _ = Task.Run(async () =>
                            {
                                try
                                {
                                    string responseText = await QueryAsync(search, DfModel);
                                    Context.API.ShowMsg("RunLLM", "Response copied to clipboard!", string.Empty);
                                    Context.API.ChangeQuery($"{Context.CurrentPluginMetadata.ActionKeyword} ", requery: true);
                                    isWaitingForResponse = false;
                                }
                                catch (Exception ex)
                                {
                                    isWaitingForResponse = false;
                                    responseText = $"Error: {ex.Message}";
                                }
                            });

                            isWaitingForResponse = true;
                            Context.API.ChangeQuery($"{Context.CurrentPluginMetadata.ActionKeyword} " + search, requery: true);
                            return false;
                        },

                        ContextData = search
                    },
                    new Result
                    {
                        Title = search,
                        SubTitle = "Choose another Model",
                        IcoPath = IconPath,
                        Action = e =>
                        {
                            isChooseModel = true;
                            Context.API.ChangeQuery($"{Context.CurrentPluginMetadata.ActionKeyword} ", requery: true);
                            return false;
                        },
                        ContextData = search
                    }
                    ];
            }
            return results;
        }

        public void Init(PluginInitContext context)
        {
            Context = context ?? throw new ArgumentNullException(nameof(context));
            Context.API.ThemeChanged += OnThemeChanged;
            UpdateIconPath(Context.API.GetCurrentTheme());
        }

        public List<ContextMenuResult> LoadContextMenus(Result selectedResult)
        {
            if (selectedResult.ContextData is string search)
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
                            Clipboard.SetDataObject(search);
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
            if (Disposed || !disposing)
            {
                return;
            }

            if (Context?.API != null)
            {
                Context.API.ThemeChanged -= OnThemeChanged;
            }

            Disposed = true;
        }
        private void UpdateIconPath(Theme theme) => IconPath = theme == Theme.Light || theme == Theme.HighContrastWhite ? "Images/runllm.light.png" : "Images/runllm.dark.png";
        private void OnThemeChanged(Theme currentTheme, Theme newTheme) => UpdateIconPath(newTheme);
    }
}
