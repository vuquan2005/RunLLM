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
    public class Main : IPlugin, IContextMenu, IDisposable, ISettingProvider
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
                DisplayDescription = "Your default model when you restart Powertoys. Ex. qwen/qwen3-4b",
                PluginOptionType = PluginAdditionalOption.AdditionalOptionType.Textbox,
                TextValue = "qwen/qwen3-4b",
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

        // Fetch models from the endpoint
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
        // Request to the LLM API
        private async Task<string> QueryAsync(string prompt, string model)
        {
            string systemPrompt = SystemPrompt.Replace("[currentTime]", DateTime.Now.ToString());

            var body = new
            {
                model = model,
                messages = new object[]
                {
                    new { role = "system", content = systemPrompt },
                    new { role = "user", content = prompt }
                },
            };

            string json = JsonSerializer.Serialize(body);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            // LM Studio local API
            var response = await client.PostAsync($"{Url}/v1/chat/completions", content);
            response.EnsureSuccessStatusCode();

            string responseContent = await response.Content.ReadAsStringAsync();

            using var doc = JsonDocument.Parse(responseContent);
            return doc.RootElement
                      .GetProperty("choices")[0]
                      .GetProperty("message")
                      .GetProperty("content")
                      .GetString();
        }

        // Query
        enum QueryState
        {
            Idle,
            WaitingResponse,
            ShowResponse,
            ChoosingModel,
            GetListOfModels,
            ChangingThinkingMode,
        }

        private QueryState currentState = QueryState.Idle;
        private short waited = 0;
        private string responseText = "";
        private List<string> modelNames = new();
        public List<Result> Query(Query query)
        {
            var search = query.Search;
            var rawquery = query.RawQuery;
            List<Result> results = new List<Result>();

            switch (currentState)
            {
                case QueryState.Idle:
                    results.Add(new Result
                    {
                        Title = search,
                        SubTitle = $"Ask {DfModel}",
                        IcoPath = "Images/run.png",
                        Action = e =>
                        {
                            // Cancel task, will do later
                            _ = Task.Run(async () =>
                            {
                                try
                                {
                                    responseText = await QueryAsync(search, DfModel);
                                    Context.API.ChangeQuery(rawquery, requery: true);
                                    currentState = QueryState.ShowResponse;
                                }
                                catch (Exception ex)
                                {
                                    currentState = QueryState.ShowResponse;
                                    responseText = $"Error: {ex.Message}";
                                }
                            });
                            currentState = QueryState.WaitingResponse;
                            Context.API.ChangeQuery(rawquery, requery: true);
                            return false;
                        }
                    });
                    if (rawquery.StartsWith(Context.CurrentPluginMetadata.ActionKeyword))
                        results.Add(new Result
                        {
                            Title = "Change model LLM",
                            SubTitle = "Choose another Model",
                            IcoPath = "Images/change.png",
                            Action = e =>
                            {
                                currentState = QueryState.GetListOfModels;
                                Context.API.ChangeQuery(rawquery, requery: true);
                                return false;
                            },
                        });
                    if (rawquery.StartsWith(Context.CurrentPluginMetadata.ActionKeyword))
                        results.Add(new Result
                        {
                            Title = "Change thinking mode",
                            SubTitle = "/think, /no_think, enable_thinking, reasoning{ effort, depth, timeout }.",
                            IcoPath = "Images/brain.png",
                            Action = e =>
                            {
                                currentState = QueryState.ChangingThinkingMode;
                                Context.API.ChangeQuery(rawquery, requery: true);
                                return false;
                            },
                        });
                    break;
                case QueryState.ChangingThinkingMode:
                    results.Add(new Result
                    {
                        Title = "/think",
                        SubTitle = "User Input",
                        IcoPath = "Images/brain.png",
                        Action = e =>
                        {
                            SystemPrompt += " /think";
                            currentState = QueryState.Idle;
                            Context.API.ChangeQuery(rawquery, requery: true);
                            return false;
                        },
                    });
                    results.Add(new Result
                    {
                        Title = "/no_think",
                        SubTitle = "User Input",
                        IcoPath = "Images/brain.png",
                        Action = e =>
                        {
                            SystemPrompt += " /no_think";
                            currentState = QueryState.Idle;
                            Context.API.ChangeQuery(rawquery, requery: true);
                            return false;
                        },
                    });
                    results.Add(new Result
                    {
                        Title = "enable_thinking : true",
                        SubTitle = "Request",
                        IcoPath = "Images/brain.png",
                        Action = e =>
                        {
                            currentState = QueryState.Idle;
                            Context.API.ChangeQuery(rawquery, requery: true);
                            return false;
                        },
                    });
                    results.Add(new Result
                    {
                        Title = "enable_thinking: false",
                        SubTitle = "Request",
                        IcoPath = "Images/brain.png",
                        Action = e =>
                        {
                            currentState = QueryState.Idle;
                            Context.API.ChangeQuery(rawquery, requery: true);
                            return false;
                        },
                    });
                    break;
                case QueryState.GetListOfModels:
                    modelNames = FetchModelsFromEndpointAsync().GetAwaiter().GetResult();
                    currentState = QueryState.ChoosingModel;
                    Context.API.ChangeQuery(rawquery, requery: true);
                    break;
                case QueryState.ChoosingModel:
                    foreach (var modelName in modelNames)
                    {
                        results.Add(new Result
                        {
                            Title = modelName,
                            SubTitle = "",
                            IcoPath = "Images/model.png",
                            Action = e =>
                            {
                                DfModel = modelName;
                                currentState = QueryState.Idle;
                                Context.API.ChangeQuery(rawquery, requery: true);
                                return false;
                            }
                        });
                    }
                    break;
                case QueryState.WaitingResponse:
                    waited += 1;
                    _ = Task.Run(async () =>
                    {
                        await Task.Delay(1000);
                        Context.API.ChangeQuery(rawquery, requery: true);
                    });
                    results.Add(new Result
                    {
                        Title = rawquery,
                        SubTitle = $"Waited : {waited}s. Cancel the task by pressing enter.",
                        IcoPath = "Images/timer.png",
                        Action = e =>
                        {
                            // Cancel task, will do later
                            //waited = 0;
                            //responseText = "";
                            return true;
                        }
                    });
                    break;
                case QueryState.ShowResponse:
                    waited = 0;
                    results.Add(new Result
                    {
                        Title = $"Response by: {DfModel}:",
                        SubTitle = responseText,
                        IcoPath = "Images/access.png",
                        Action = e =>
                        {
                            currentState = QueryState.Idle;
                            Clipboard.SetText(responseText);
                            Context.API.ShowMsg($"Response by: {DfModel}", responseText);
                            responseText = "";
                            return true;
                        }
                    });
                    break;
                default:
                    break;
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
        private void UpdateIconPath(Theme theme) => IconPath = theme == Theme.Light || theme == Theme.HighContrastWhite ? "Images/model.png" : "Images/model.png";
        private void OnThemeChanged(Theme currentTheme, Theme newTheme) => UpdateIconPath(newTheme);
    }
}
