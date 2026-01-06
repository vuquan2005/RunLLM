using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Community.PowerToys.Run.Plugin.RunLLM
{
    /// <summary>
    /// HTTP client for LLM API interactions.
    /// </summary>
    public class LLMClient : IDisposable
    {
        private readonly HttpClient _client;
        private readonly PluginSettings _settings;
        private bool _disposed;

        public LLMClient(PluginSettings settings)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _client = new HttpClient
            {
                Timeout = TimeSpan.FromMinutes(5)
            };
        }

        /// <summary>
        /// Fetches available models from the API.
        /// </summary>
        public async Task<List<string>> GetModelsAsync(CancellationToken token = default)
        {
            var modelNames = new List<string>();

            var request = CreateRequest(HttpMethod.Get, Constants.ModelsEndpoint);
            var response = await _client.SendAsync(request, token);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync(token);
            using var doc = JsonDocument.Parse(json);

            // OpenAI-style: { "data": [ { "id": "gpt-3.5-turbo" }, ... ] }
            if (doc.RootElement.TryGetProperty("data", out var dataArray))
            {
                foreach (var model in dataArray.EnumerateArray())
                {
                    if (model.TryGetProperty("id", out var idProp))
                    {
                        var name = idProp.GetString();
                        if (!string.IsNullOrEmpty(name))
                        {
                            modelNames.Add(name);
                        }
                    }
                }
            }

            return modelNames;
        }

        /// <summary>
        /// Validates if an endpoint is reachable and returns models API.
        /// </summary>
        public async Task<(bool Success, string Message)> ValidateEndpointAsync(string url, CancellationToken token = default)
        {
            try
            {
                var request = new HttpRequestMessage(HttpMethod.Get, $"{url}{Constants.ModelsEndpoint}");
                
                if (_settings.UseApiKey && !string.IsNullOrEmpty(_settings.ApiKey))
                {
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _settings.ApiKey);
                }

                using var cts = CancellationTokenSource.CreateLinkedTokenSource(token);
                cts.CancelAfter(TimeSpan.FromSeconds(5)); // 5 second timeout for validation

                var response = await _client.SendAsync(request, cts.Token);
                
                if (response.IsSuccessStatusCode)
                {
                    return (true, "Endpoint is valid");
                }
                else
                {
                    return (false, $"HTTP {(int)response.StatusCode}: {response.ReasonPhrase}");
                }
            }
            catch (TaskCanceledException)
            {
                return (false, "Connection timeout (5s)");
            }
            catch (HttpRequestException ex)
            {
                return (false, $"Connection failed: {ex.Message}");
            }
            catch (Exception ex)
            {
                return (false, $"Error: {ex.Message}");
            }
        }

        /// <summary>
        /// Streams chat completion responses.
        /// </summary>
        public async IAsyncEnumerable<string> ChatCompletionStreamAsync(
            string prompt,
            string model,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken token = default)
        {
            // Build system prompt with thinking mode (prompt-based)
            var systemPrompt = _settings.SystemPrompt.Replace("[currentTime]", DateTime.Now.ToString());
            
            // For prompt-based thinking mode (Qwen3, etc.), append /think or /no_think
            if (_settings.ThinkingModeType == "prompt")
            {
                if (_settings.EnableThinking)
                {
                    systemPrompt = systemPrompt.TrimEnd() + " /think";
                }
                else
                {
                    // Only add /no_think if user explicitly wants to disable
                    // Some models default to thinking, so this helps disable it
                }
            }

            var bodyDict = new Dictionary<string, object>
            {
                ["model"] = model,
                ["messages"] = new object[]
                {
                    new { role = "system", content = systemPrompt },
                    new { role = "user", content = prompt }
                },
                ["stream"] = true
            };

            // For request-based thinking mode (OpenAI reasoning models, etc.)
            if (_settings.ThinkingModeType == "request" && _settings.EnableThinking)
            {
                bodyDict["enable_thinking"] = true;
                bodyDict["reasoning_effort"] = _settings.ReasoningEffort;
            }

            var json = JsonSerializer.Serialize(bodyDict);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var request = CreateRequest(HttpMethod.Post, Constants.ChatCompletionsEndpoint);
            request.Content = content;

            using var response = await _client.SendAsync(
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
                    if (jsonStr == "[DONE]") yield break;

                    string? deltaContent = null;
                    try
                    {
                        using var doc = JsonDocument.Parse(jsonStr);
                        var choices = doc.RootElement.GetProperty("choices");
                        if (choices.GetArrayLength() > 0)
                        {
                            var delta = choices[0].GetProperty("delta");
                            if (delta.TryGetProperty("content", out var contentProp))
                            {
                                deltaContent = contentProp.GetString();
                            }
                        }
                    }
                    catch (JsonException)
                    {
                        // Skip malformed JSON chunks
                        continue;
                    }

                    if (!string.IsNullOrEmpty(deltaContent))
                    {
                        yield return deltaContent;
                    }
                }
            }
        }

        private HttpRequestMessage CreateRequest(HttpMethod method, string endpoint)
        {
            var request = new HttpRequestMessage(method, $"{_settings.Url}{endpoint}");

            // Add API key if enabled
            if (_settings.UseApiKey && !string.IsNullOrEmpty(_settings.ApiKey))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _settings.ApiKey);
            }

            return request;
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _client.Dispose();
                _disposed = true;
            }
            GC.SuppressFinalize(this);
        }
    }
}
