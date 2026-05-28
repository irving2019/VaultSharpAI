using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace VaultSharpAI.Console.Services;

public class CompatibleProvider : IAiProvider
{
    private readonly HttpClient _httpClient;
    private string? _model;
    private readonly string _apiKey;

    public CompatibleProvider(string apiKey, string baseUrl)
    {
        _apiKey = apiKey;
        var normalizedUrl = baseUrl.EndsWith("/") ? baseUrl : baseUrl + "/";

        if (!Uri.TryCreate(normalizedUrl, UriKind.Absolute, out Uri? validUri))
            throw new ArgumentException($"Введен некорректный формат ссылки: {normalizedUrl}");

        _httpClient = new HttpClient { BaseAddress = validUri };
        _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");
    }

    public async Task<string> GenerateResponseAsync(string prompt) => await GenerateResponseAsync(prompt, 4096, false, false, null, null);

    public async Task<string> GenerateResponseAsync(
        string prompt,
        int maxTokens,
        bool enableThinking,
        bool enableSearch,
        string? imageBase64 = null,
        string? imageMimeType = null)
    { 
        if (string.IsNullOrEmpty(_model))
            _model = "gpt-3.5-turbo";

        object contentPayload;

        if (!string.IsNullOrEmpty(imageBase64))
        {
            contentPayload = new object[]
            {
                new {type = "text", text = prompt},
                new {type = "image_url", image_url = new {url = $"data:{imageMimeType}; base64,{imageBase64}"}}
            };
        }
        else
            contentPayload = prompt;
        

        var requestBody = new Dictionary<string, object>
        {
            {"model", _model },
            {"messages", new[] {new {role = "user", content = contentPayload}}},
            {"max_tokens", maxTokens }
        };

        if (enableThinking)
            requestBody["include_reasoning"] = true;

        if (enableSearch)
        {
            requestBody["provider"] = new Dictionary<string, object>
            {
                { "web_search", true }
            };
        }

        try
        {
            var response = await _httpClient.PostAsJsonAsync("chat/completions", requestBody);

            var responseText = await response.Content.ReadAsStringAsync();

            response.EnsureSuccessStatusCode();

            using var doc = JsonDocument.Parse(responseText);
            var root = doc.RootElement;

            if (root.TryGetProperty("choices", out var choices) && choices.GetArrayLength() > 0)
            {
                var messageNode = choices[0].GetProperty("message");
                var content = messageNode.GetProperty("content").GetString() ?? "";

                if (enableThinking && messageNode.TryGetProperty("reasoning", out var reasoningNode))
                {
                    string reasoning = reasoningNode.GetString() ?? "";

                    if (!string.IsNullOrWhiteSpace(reasoning))
                        content = $"*🤔 Думаю:*\n_{reasoning}_\n\n*✉️ Ответ:*\n{content}";
                }

                return content;
            }

            if (root.TryGetProperty("error", out var errorObj))
            {
                var errorMsg = errorObj.GetProperty("message").GetString();
                return $"[Ответ сервера API]: {errorMsg}";
            }

            return $"[Нестандартный ответ сервера]: {responseText}";
        }
        catch (HttpRequestException httpEx)
        {
            return $"[Ошибка сети/Авторизации]: {httpEx.Message}";
        }
        catch (Exception ex)
        {
            return $"[Внутренняя ошибка VaultSharpAI]: {ex.Message}";
        }
    }

    public async Task<List<string>> GetAvailableModelsAsync()
    {
        var modelsList = new List<string>();

        try
        {
            var response = await _httpClient.GetAsync("models");

            if (response.IsSuccessStatusCode)
            {
                var responseText = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(responseText);

                if (doc.RootElement.TryGetProperty("data", out var dataArray) && dataArray.ValueKind == JsonValueKind.Array)
                {
                    foreach (var modelItem in dataArray.EnumerateArray())
                    {
                        if(modelItem.TryGetProperty("id", out var idProperty))
                        {
                            string? id = idProperty.GetString();

                            if (!string.IsNullOrEmpty(id))
                                modelsList.Add(id);
                        }
                    }
                }
            }
        }
        catch
        {

        }

        if (modelsList.Count == 0)
            modelsList.Add("gpt-3.5-turbo");

        return modelsList;
    }

    public void SetModel(string modelName) => _model = modelName;
}