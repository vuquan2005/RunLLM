using Microsoft.PowerToys.Settings.UI.Library;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
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
        public string Description => "LLM in Powertoys run";

        private static readonly System.Net.Http.HttpClient client = new System.Net.Http.HttpClient();
        private PluginInitContext Context { get; set; }

        private bool Disposed { get; set; }

        // Settings
        private string Url,/* APIKey,*/ DfModel;
        //private bool UseAPIKeyEndpoint;
        private string SystemPrompt = "";
        public IEnumerable<PluginAdditionalOption> AdditionalOptions =>
        [
            new()
            {
                Key = "LLMUrl",
                DisplayLabel = "LLM URL",
                DisplayDescription = "Enter the URL of your LLM model. Ex. http://localhost:11434",
                PluginOptionType = PluginAdditionalOption.AdditionalOptionType.Textbox,
                TextValue = "http://localhost:11434",
            },
            new()
            {
                Key = "DfModel",
                DisplayLabel = "Default model",
                DisplayDescription = "Your default model when you restart Powertoys. Ex. qwen/qwen3-4b",
                PluginOptionType = PluginAdditionalOption.AdditionalOptionType.Textbox,
                TextValue = "qwen/qwen3-4b",
            },
            //new()
            //{
            //    Key = "APIKey",
            //    DisplayLabel = "Use API Key endpoint. (Comming soon)",
            //    DisplayDescription = "Check to use API key and enter your key below",
            //    PluginOptionType = PluginAdditionalOption.AdditionalOptionType.CheckboxAndTextbox,
            //    TextValue = "YOUR_API_KEY"
            //},
            new()
            {
                Key = "SystemPrompt",
                DisplayLabel = "System prompt",
                DisplayDescription = "Enter the system prompt",
                PluginOptionType = PluginAdditionalOption.AdditionalOptionType.MultilineTextbox,
                TextValue = SystemPrompt
            }
        ];

        public void UpdateSettings(PowerLauncherPluginSettings settings)
        {
            if (settings?.AdditionalOptions != null)
            {
                Url = settings.AdditionalOptions.FirstOrDefault(x => x.Key == "LLMUrl")?.TextValue ?? "http://localhost:11434";
                DfModel = settings.AdditionalOptions.FirstOrDefault(x => x.Key == "DfModel")?.TextValue ?? "qwen/qwen3-4b";

                //var apiOption = settings.AdditionalOptions.FirstOrDefault(x => x.Key == "APIKey");
                //UseAPIKeyEndpoint = apiOption != null && apiOption.Value is bool b2 && b2;
                //APIKey = apiOption?.TextValue ?? "";

                SystemPrompt = settings.AdditionalOptions.FirstOrDefault(x => x.Key == "SystemPrompt")?.TextValue ?? SystemPrompt;
            }
        }

        public Control CreateSettingPanel()
        {
            throw new NotImplementedException();
        }

        // Fetch models from the endpoint
        private async Task<List<string>> GetModelsLLM()
        {
            try
            {
                string apiUrl = $"{Url}/v1/models";

                var response = await client.GetStringAsync(apiUrl);
                var jsonDocument = JsonDocument.Parse(response);

                var modelNames = new List<string>();

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
                Context.API.ShowNotification("Powertoys run RunLLM !Error", $"Error fetching models from API: {ex.Message}");
                return [];
            }
        }
        // Request to the LLM API
        private async Task ChatCompletionStream(string prompt, string model, string rawQuery, CancellationToken token)
        {
            string systemPrompt = SystemPrompt.Replace("[currentTime]", DateTime.Now.ToString());

            var body = new
            {
                model,
                messages = new object[]
                {
                    new { role = "system", content = systemPrompt },
                    new { role = "user", content = prompt }
                },
                stream = true
            };

            string json = JsonSerializer.Serialize(body);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var request = new HttpRequestMessage(HttpMethod.Post, $"{Url}/v1/chat/completions")
            {
                Content = content
            };

            using var response = await client.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                token
            );
            response.EnsureSuccessStatusCode(); 

            using var stream = await response.Content.ReadAsStreamAsync(token);
            using var reader = new StreamReader(stream);

            while (!reader.EndOfStream)
            {
                token.ThrowIfCancellationRequested();

                var line = await reader.ReadLineAsync(token);
                if (string.IsNullOrWhiteSpace(line)) continue;

                if (line.StartsWith("data: "))
                {
                    var jsonStr = line["data: ".Length..];
                    if (jsonStr == "[DONE]") break;

                    try
                    {
                        using var doc = JsonDocument.Parse(jsonStr);
                        var delta = doc.RootElement
                                       .GetProperty("choices")[0]
                                       .GetProperty("delta")
                                       .GetProperty("content")
                                       .GetString();

                        if (!string.IsNullOrEmpty(delta))
                        {
                            responseText += delta;
                            currentState = QueryState.StreamResponse;
                            Context.API.ChangeQuery(rawQuery, requery: true);
                        }
                    }
                    catch { }
                }
            }
        }


        // Query
        enum QueryState
        {
            Idle,
            WaitingResponse,
            StreamResponse,
            ShowResponse,
            ChoosingModel,
            GetListOfModels,
            ChangingThinkingMode,
        }

        private QueryState currentState = QueryState.Idle;
        private short waited = 0;
        private string responseText = "";
        private List<string> modelNames = [];
        private DateTime _lastFetchModels = DateTime.MinValue;

        private CancellationTokenSource? _cts;

        public List<Result> Query(Query query)
        {
            var search = query.Search;
            var rawQuery = query.RawQuery;
            var result = new List<Result>();

            return currentState switch
            {
                QueryState.Idle => HandleIdleState(search, rawQuery),
                QueryState.ChangingThinkingMode => HandleChangingThinkingMode(rawQuery),
                QueryState.GetListOfModels => HandleGetListOfModels(rawQuery),
                QueryState.ChoosingModel => HandleChoosingModelState(rawQuery),
                QueryState.WaitingResponse => HandleWaitingResponseState(rawQuery),
                QueryState.StreamResponse => HandleStreamResponseState(rawQuery),
                QueryState.ShowResponse => HandleShowResponseState(rawQuery),
                _ => [new Result {
                        Title = "Unknown state",
                        SubTitle = "Resetting to idle",
                        IcoPath = "Images/model.png",
                        Action = e =>
                        {
                            currentState = QueryState.Idle;
                            Context.API.ChangeQuery(rawQuery, requery: true);
                            return false;
                        }
                    }],
            };
        }

        private List<Result> HandleIdleState(string search, string rawQuery)
        {
            var results = new List<Result>
            {
                new() {
                    Title = search,
                    SubTitle = $"Ask {DfModel}",
                    IcoPath = "Images/run.png",
                    Action = e =>
                    {
                        _cts?.Cancel();
                        _cts = new CancellationTokenSource();
                        var localToken = _cts.Token;
                        responseText = "";

                        _ = Task.Run(async () =>
                        {
                            try
                            {
                                await ChatCompletionStream(search, DfModel, rawQuery, localToken);
                                currentState = QueryState.ShowResponse;
                            }
                            catch (OperationCanceledException)
                            {
                                waited = 0;
                                currentState = QueryState.Idle;
                            }
                            catch (Exception ex)
                            {
                                Context.API.ShowNotification("Powertoys run RunLLM !Error", $"Error: {ex.Message}");
                                currentState = QueryState.Idle;
                            }

                            Context.API.ChangeQuery(rawQuery, requery: true);
                        }, localToken);

                        currentState = QueryState.WaitingResponse;
                        Context.API.ChangeQuery(rawQuery, requery: true);
                        return false;
                    }
                }
            };

            if (rawQuery.StartsWith(Context.CurrentPluginMetadata.ActionKeyword))
            {
                results.AddRange([
                    new Result
                    {
                        Title = "Change model LLM",
                        SubTitle = "Choose another Model",
                        IcoPath = "Images/change.png",
                        Action = e =>
                        {
                            currentState = QueryState.GetListOfModels;
                            Context.API.ChangeQuery(rawQuery, requery: true);
                            return false;
                        }
                    },
                    new Result
                    {
                        Title = "Change thinking mode",
                        SubTitle = "/think, /no_think, enable_thinking, reasoning{ effort, depth, timeout }.",
                        IcoPath = "Images/brain.png",
                        Action = e =>
                        {
                            currentState = QueryState.ChangingThinkingMode;
                            Context.API.ChangeQuery(rawQuery, requery: true);
                            return false;
                        }
                    }
                ]);
            }
            return results;
        }
        private List<Result> HandleGetListOfModels(string rawQuery)
        {
            var result = new List<Result>();

            if ((DateTime.Now - _lastFetchModels).TotalMinutes < 1 && modelNames.Count > 0)
            {
                currentState = QueryState.ChoosingModel;
                Context.API.ChangeQuery(rawQuery, requery: true);
            }
            else
            {
                _lastFetchModels = DateTime.Now;
                modelNames.Clear();
                _ = Task.Run(async () =>
                {
                    try
                    {
                        modelNames = await GetModelsLLM();
                    }
                    catch (Exception ex)
                    {
                        Context.API.ShowNotification("Powertoys run RunLLM !Error", $"Error: {ex.Message}");
                        currentState = QueryState.Idle;
                    }
                    currentState = QueryState.ChoosingModel;
                    Context.API.ChangeQuery(rawQuery, requery: true);
                });
            }
            return result;
        }
        private List<Result> HandleChoosingModelState(string rawQuery)
        {
            var results = new List<Result>();
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
                        Context.API.ChangeQuery(rawQuery, requery: true);
                        return false;
                    }
                });
            }
            return results;
        }
        private List<Result> HandleChangingThinkingMode(string rawQuery)
        {
            var results = new List<Result>(){new Result
            {
                Title = "/think",
                SubTitle = "User Input",
                IcoPath = "Images/brain.png",
                Action = e =>
                {
                    SystemPrompt += " /think";
                    currentState = QueryState.Idle;
                    Context.API.ChangeQuery(rawQuery, requery: true);
                    return false;
                },
            },
            new() {
                Title = "/no_think",
                SubTitle = "User Input",
                IcoPath = "Images/brain.png",
                Action = e =>
                {
                    SystemPrompt += " /no_think";
                    currentState = QueryState.Idle;
                    Context.API.ChangeQuery(rawQuery, requery: true);
                    return false;
                },
            },
            new() {
                Title = "enable_thinking : true",
                SubTitle = "Request",
                IcoPath = "Images/brain.png",
                Action = e =>
                {
                    currentState = QueryState.Idle;
                    Context.API.ChangeQuery(rawQuery, requery: true);
                    return false;
                },
            },
            new() {
                Title = "enable_thinking: false",
                SubTitle = "Request",
                IcoPath = "Images/brain.png",
                Action = e =>
                {
                    currentState = QueryState.Idle;
                    Context.API.ChangeQuery(rawQuery, requery: true);
                    return false;
                },
            }};
            return results;
        }
        private List<Result> HandleWaitingResponseState(string rawQuery)
        {
            waited += 1;
            _ = Task.Run(async () =>
            {
                await Task.Delay(1000);
                Context.API.ChangeQuery(rawQuery, requery: true);
            });
            var results = new List<Result>
            {
                new() {
                    Title = rawQuery,
                    SubTitle = $"Waited : {waited}s. Cancel the task by pressing enter.",
                    IcoPath = "Images/timer.png",
                    Action = e =>
                    {
                        _cts?.Cancel();
                        waited = 0;
                        currentState = QueryState.Idle;
                        Context.API.ChangeQuery(rawQuery, requery: true);
                        return true;
                    }
                }
            };
            return results;
        }
        private List<Result> HandleStreamResponseState(string rawQuery)
        {
            var results = new List<Result>
            {
                new() {
                    Title = $"\U0001F500 {DfModel}: streaming...",
                    SubTitle = responseText,
                    IcoPath = "Images/transfer.png",
                    Action = e =>
                    {
                        _cts?.Cancel();
                        Clipboard.SetText(responseText);
                        currentState = QueryState.Idle;
                        Context.API.ChangeQuery(rawQuery, requery: true);
                        return true;
                    }
                }
            };
            return results;
        }
        private List<Result> HandleShowResponseState(string rawQuery)
        {
            waited = 0;
            var results = new List<Result>
            {
                new() {
                    Title = $"\u2705 Response by: {DfModel}:",
                    SubTitle = responseText,
                    IcoPath = "Images/access.png",
                    Action = e =>
                    {
                        currentState = QueryState.Idle;
                        Clipboard.SetText(responseText);
                        Context.API.ShowMsg($"Response by: {DfModel}", responseText, "", false);
                        return true;
                    }
                }
            };
            return results;
        }


        public void Init(PluginInitContext context)
        {
            Context = context ?? throw new ArgumentNullException(nameof(context));
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
            Disposed = true;
        }
    }
}
