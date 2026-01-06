using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Wox.Plugin;

namespace Community.PowerToys.Run.Plugin.RunLLM
{
    /// <summary>
    /// State machine for handling PowerToys Run queries.
    /// </summary>
    public class QueryHandler : IDisposable
    {
        public enum QueryState
        {
            Idle,
            WaitingResponse,
            StreamResponse,
            ShowResponse,
            ChoosingModel,
            GetListOfModels,
            ChangingThinkingMode,
            ChangingEndpoint,
        }

        private readonly object _stateLock = new();
        private readonly PluginSettings _settings;
        private readonly LLMClient _llmClient;
        private PluginInitContext _context = null!;

        private QueryState _currentState = QueryState.Idle;
        private string _responseText = "";
        private List<string> _modelNames = [];
        private DateTime _lastFetchModels = DateTime.MinValue;
        private int _waited = 0;
        private CancellationTokenSource? _cts;
        private bool _disposed;

        public QueryState CurrentState
        {
            get { lock (_stateLock) return _currentState; }
            private set { lock (_stateLock) _currentState = value; }
        }

        public QueryHandler(PluginSettings settings, LLMClient llmClient)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _llmClient = llmClient ?? throw new ArgumentNullException(nameof(llmClient));
        }

        public void Initialize(PluginInitContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public List<Result> HandleQuery(Query query)
        {
            var search = query.Search;
            var rawQuery = query.RawQuery;

            return CurrentState switch
            {
                QueryState.Idle => HandleIdleState(search, rawQuery),
                QueryState.ChangingThinkingMode => HandleChangingThinkingMode(rawQuery),
                QueryState.ChangingEndpoint => HandleChangingEndpoint(rawQuery),
                QueryState.GetListOfModels => HandleGetListOfModels(rawQuery),
                QueryState.ChoosingModel => HandleChoosingModelState(rawQuery),
                QueryState.WaitingResponse => HandleWaitingResponseState(rawQuery),
                QueryState.StreamResponse => HandleStreamResponseState(rawQuery),
                QueryState.ShowResponse => HandleShowResponseState(rawQuery),
                _ => HandleUnknownState(rawQuery),
            };
        }

        private List<Result> HandleIdleState(string search, string rawQuery)
        {
            var results = new List<Result>
            {
                new()
                {
                    Title = string.IsNullOrEmpty(search) ? "Type your question..." : search,
                    SubTitle = $"Ask {_settings.Model}" + (_settings.EnableThinking ? " (thinking enabled)" : ""),
                    IcoPath = Constants.IconRun,
                    Action = _ =>
                    {
                        if (string.IsNullOrWhiteSpace(search)) return false;
                        StartChat(search, rawQuery);
                        return false;
                    }
                }
            };

            // Show menu options only when using action keyword
            if (rawQuery.StartsWith(_context.CurrentPluginMetadata.ActionKeyword))
            {
                results.AddRange([
                    new Result
                    {
                        Title = "Change Model",
                        SubTitle = $"Current: {_settings.Model}",
                        IcoPath = Constants.IconChange,
                        Action = _ =>
                        {
                            CurrentState = QueryState.GetListOfModels;
                            _context.API.ChangeQuery(rawQuery, requery: true);
                            return false;
                        }
                    },
                    new Result
                    {
                        Title = "Thinking Mode",
                        SubTitle = _settings.EnableThinking 
                            ? $"Enabled ({_settings.ReasoningEffort})" 
                            : "Disabled",
                        IcoPath = Constants.IconBrain,
                        Action = _ =>
                        {
                            CurrentState = QueryState.ChangingThinkingMode;
                            _context.API.ChangeQuery(rawQuery, requery: true);
                            return false;
                        }
                    },
                    new Result
                    {
                        Title = "Change Endpoint",
                        SubTitle = $"Current: {_settings.Url}",
                        IcoPath = Constants.IconModel,
                        Action = _ =>
                        {
                            CurrentState = QueryState.ChangingEndpoint;
                            _context.API.ChangeQuery(rawQuery, requery: true);
                            return false;
                        }
                    }
                ]);
            }

            return results;
        }

        private void StartChat(string search, string rawQuery)
        {
            _cts?.Cancel();
            _cts = new CancellationTokenSource();
            var token = _cts.Token;
            _responseText = "";
            _waited = 0;

            _ = Task.Run(async () =>
            {
                try
                {
                    await foreach (var chunk in _llmClient.ChatCompletionStreamAsync(search, _settings.Model, token))
                    {
                        _responseText += chunk;
                        CurrentState = QueryState.StreamResponse;
                        _context.API.ChangeQuery(rawQuery, requery: true);
                    }
                    CurrentState = QueryState.ShowResponse;
                }
                catch (OperationCanceledException)
                {
                    _waited = 0;
                    CurrentState = QueryState.Idle;
                }
                catch (Exception ex)
                {
                    _context.API.ShowNotification("RunLLM Error", ex.Message);
                    CurrentState = QueryState.Idle;
                }

                _context.API.ChangeQuery(rawQuery, requery: true);
            }, token);

            CurrentState = QueryState.WaitingResponse;
            _context.API.ChangeQuery(rawQuery, requery: true);
        }

        private List<Result> HandleGetListOfModels(string rawQuery)
        {
            if ((DateTime.Now - _lastFetchModels).TotalMinutes < Constants.ModelsCacheMinutes && _modelNames.Count > 0)
            {
                CurrentState = QueryState.ChoosingModel;
                _context.API.ChangeQuery(rawQuery, requery: true);
                return [];
            }

            _lastFetchModels = DateTime.Now;
            _modelNames.Clear();

            _ = Task.Run(async () =>
            {
                try
                {
                    _modelNames = await _llmClient.GetModelsAsync();
                }
                catch (Exception ex)
                {
                    _context.API.ShowNotification("RunLLM Error", $"Failed to fetch models: {ex.Message}");
                    CurrentState = QueryState.Idle;
                    _context.API.ChangeQuery(rawQuery, requery: true);
                    return;
                }

                CurrentState = QueryState.ChoosingModel;
                _context.API.ChangeQuery(rawQuery, requery: true);
            });

            return [new Result
            {
                Title = "Loading models...",
                SubTitle = "Please wait",
                IcoPath = Constants.IconTimer
            }];
        }

        private List<Result> HandleChoosingModelState(string rawQuery)
        {
            var results = new List<Result>
            {
                new()
                {
                    Title = "← Back",
                    SubTitle = "Return to main menu",
                    IcoPath = Constants.IconChange,
                    Action = _ =>
                    {
                        CurrentState = QueryState.Idle;
                        _context.API.ChangeQuery(rawQuery, requery: true);
                        return false;
                    }
                }
            };

            foreach (var modelName in _modelNames)
            {
                results.Add(new Result
                {
                    Title = modelName,
                    SubTitle = modelName == _settings.Model ? "✓ Current" : "",
                    IcoPath = Constants.IconModel,
                    Action = _ =>
                    {
                        _settings.Model = modelName;
                        CurrentState = QueryState.Idle;
                        _context.API.ChangeQuery(rawQuery, requery: true);
                        return false;
                    }
                });
            }

            return results;
        }

        private List<Result> HandleChangingThinkingMode(string rawQuery)
        {
            var modeTypeDisplay = _settings.ThinkingModeType == "prompt" 
                ? "/think, /no_think (Qwen3)" 
                : "enable_thinking (OpenAI)";

            return
            [
                new Result
                {
                    Title = "← Back",
                    SubTitle = "Return to main menu",
                    IcoPath = Constants.IconChange,
                    Action = _ =>
                    {
                        CurrentState = QueryState.Idle;
                        _context.API.ChangeQuery(rawQuery, requery: true);
                        return false;
                    }
                },
                new Result
                {
                    Title = _settings.EnableThinking ? "✓ Thinking: ON" : "Thinking: OFF → Enable",
                    SubTitle = $"Toggle thinking mode ({modeTypeDisplay})",
                    IcoPath = Constants.IconBrain,
                    Action = _ =>
                    {
                        _settings.EnableThinking = !_settings.EnableThinking;
                        _context.API.ChangeQuery(rawQuery, requery: true);
                        return false;
                    }
                },
                new Result
                {
                    Title = _settings.ThinkingModeType == "prompt" ? "✓ Mode: Prompt (/think)" : "Mode: Prompt (/think)",
                    SubTitle = "For Qwen3, Ollama models - appends /think to prompt",
                    IcoPath = Constants.IconBrain,
                    Action = _ =>
                    {
                        _settings.ThinkingModeType = "prompt";
                        _context.API.ChangeQuery(rawQuery, requery: true);
                        return false;
                    }
                },
                new Result
                {
                    Title = _settings.ThinkingModeType == "request" ? "✓ Mode: Request (API)" : "Mode: Request (API)",
                    SubTitle = "For OpenAI reasoning models - uses enable_thinking in body",
                    IcoPath = Constants.IconBrain,
                    Action = _ =>
                    {
                        _settings.ThinkingModeType = "request";
                        _context.API.ChangeQuery(rawQuery, requery: true);
                        return false;
                    }
                },
                .. CreateReasoningEffortOptions(rawQuery)
            ];
        }

        private List<Result> CreateReasoningEffortOptions(string rawQuery)
        {
            // Only show reasoning effort options for request-based mode
            if (_settings.ThinkingModeType != "request")
            {
                return [];
            }

            var results = new List<Result>();
            foreach (var level in Constants.ReasoningEffortLevels)
            {
                results.Add(new Result
                {
                    Title = level == _settings.ReasoningEffort ? $"✓ Effort: {level}" : $"Effort: {level}",
                    SubTitle = $"Set reasoning effort to {level}",
                    IcoPath = Constants.IconBrain,
                    Action = _ =>
                    {
                        _settings.ReasoningEffort = level;
                        CurrentState = QueryState.Idle;
                        _context.API.ChangeQuery(rawQuery, requery: true);
                        return false;
                    }
                });
            }
            return results;
        }

        private List<Result> HandleChangingEndpoint(string rawQuery)
        {
            var search = rawQuery.Contains(' ') 
                ? rawQuery[(rawQuery.IndexOf(' ') + 1)..].Trim() 
                : "";

            var results = new List<Result>
            {
                new()
                {
                    Title = "← Back",
                    SubTitle = "Return to main menu",
                    IcoPath = Constants.IconChange,
                    Action = _ =>
                    {
                        CurrentState = QueryState.Idle;
                        _context.API.ChangeQuery(_context.CurrentPluginMetadata.ActionKeyword + " ", requery: true);
                        return false;
                    }
                }
            };

            if (!string.IsNullOrEmpty(search))
            {
                results.Add(new Result
                {
                    Title = $"Validate & set: {search}",
                    SubTitle = "Press Enter to validate and apply",
                    IcoPath = Constants.IconModel,
                    Action = _ =>
                    {
                        ValidateAndSetEndpoint(search);
                        return false;
                    }
                });
            }
            else
            {
                results.Add(new Result
                {
                    Title = $"Current: {_settings.Url}",
                    SubTitle = "Type new URL after the keyword",
                    IcoPath = Constants.IconModel
                });
            }

            // Common endpoints
            var commonEndpoints = new[]
            {
                "http://localhost:11434",
                "http://localhost:1234",
                "https://api.openai.com",
                "https://openrouter.ai/api"
            };

            foreach (var endpoint in commonEndpoints)
            {
                if (endpoint != _settings.Url)
                {
                    results.Add(new Result
                    {
                        Title = endpoint,
                        SubTitle = "Quick select (validates before applying)",
                        IcoPath = Constants.IconModel,
                        Action = _ =>
                        {
                            ValidateAndSetEndpoint(endpoint);
                            return false;
                        }
                    });
                }
            }

            return results;
        }

        private void ValidateAndSetEndpoint(string endpoint)
        {
            _context.API.ShowNotification("RunLLM", $"Validating {endpoint}...");

            _ = Task.Run(async () =>
            {
                var (success, message) = await _llmClient.ValidateEndpointAsync(endpoint);

                if (success)
                {
                    _settings.Url = endpoint;
                    _context.API.ShowNotification("RunLLM ✓", $"Endpoint set to: {endpoint}");
                }
                else
                {
                    _context.API.ShowNotification("RunLLM ✗", $"Validation failed: {message}");
                }

                CurrentState = QueryState.Idle;
                _context.API.ChangeQuery(_context.CurrentPluginMetadata.ActionKeyword + " ", requery: true);
            });
        }

        private List<Result> HandleWaitingResponseState(string rawQuery)
        {
            _waited++;
            _ = Task.Run(async () =>
            {
                await Task.Delay(1000);
                if (CurrentState == QueryState.WaitingResponse)
                {
                    _context.API.ChangeQuery(rawQuery, requery: true);
                }
            });

            return
            [
                new Result
                {
                    Title = $"⏳ Waiting for response... ({_waited}s)",
                    SubTitle = "Press Enter to cancel",
                    IcoPath = Constants.IconTimer,
                    Action = _ =>
                    {
                        _cts?.Cancel();
                        _waited = 0;
                        CurrentState = QueryState.Idle;
                        _context.API.ChangeQuery(rawQuery, requery: true);
                        return true;
                    }
                }
            ];
        }

        private List<Result> HandleStreamResponseState(string rawQuery)
        {
            return
            [
                new Result
                {
                    Title = $"🔄 {_settings.Model}: streaming...",
                    SubTitle = _responseText.Length > 200 
                        ? _responseText[..200] + "..." 
                        : _responseText,
                    IcoPath = Constants.IconTransfer,
                    Action = _ =>
                    {
                        _cts?.Cancel();
                        Clipboard.SetText(_responseText);
                        CurrentState = QueryState.Idle;
                        _context.API.ChangeQuery(rawQuery, requery: true);
                        return true;
                    }
                }
            ];
        }

        private List<Result> HandleShowResponseState(string rawQuery)
        {
            _waited = 0;
            return
            [
                new Result
                {
                    Title = $"✅ Response from {_settings.Model}",
                    SubTitle = _responseText.Length > 200 
                        ? _responseText[..200] + "..." 
                        : _responseText,
                    IcoPath = Constants.IconAccess,
                    ContextData = _responseText,
                    Action = _ =>
                    {
                        Clipboard.SetText(_responseText);
                        _context.API.ShowMsg($"Response by: {_settings.Model}", _responseText, "", false);
                        CurrentState = QueryState.Idle;
                        return true;
                    }
                }
            ];
        }

        private List<Result> HandleUnknownState(string rawQuery)
        {
            return
            [
                new Result
                {
                    Title = "Unknown state",
                    SubTitle = "Click to reset",
                    IcoPath = Constants.IconModel,
                    Action = _ =>
                    {
                        CurrentState = QueryState.Idle;
                        _context.API.ChangeQuery(rawQuery, requery: true);
                        return false;
                    }
                }
            ];
        }

        public void CancelCurrentOperation()
        {
            _cts?.Cancel();
            CurrentState = QueryState.Idle;
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _cts?.Cancel();
                _cts?.Dispose();
                _disposed = true;
            }
            GC.SuppressFinalize(this);
        }
    }
}
