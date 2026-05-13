using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

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
        {
            throw new ArgumentException($"Введен некорректный формат ссылки: {normalizedUrl}");
        }

        _httpClient = new HttpClient { BaseAddress = validUri };
        _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");
    }

    public async Task<string> GenerateResponseAsync(string prompt)
    {
        if (string.IsNullOrEmpty(_model))
        {
            await DiscoverModelAsync();
        }

        var requestBody = new
        {
            model = _model,
            messages = new[] { new { role = "user", content = prompt } }
        };

        try
        {
            var response = await _httpClient.PostAsJsonAsync("chat/completions", requestBody);

            var responseText = await response.Content.ReadAsStringAsync();

            response.EnsureSuccessStatusCode();

            using var doc = JsonDocument.Parse(responseText);
            var root = doc.RootElement;

            if (root.TryGetProperty("choices", out var choices) && choices.GetArrayLength() > 0)
            {
                var content = choices[0].GetProperty("message").GetProperty("content").GetString();
                return content ?? "";
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

    private async Task DiscoverModelAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync("models");
            if (response.IsSuccessStatusCode)
            {
                var data = await response.Content.ReadFromJsonAsync<JsonElement>();
                _model = data.GetProperty("data")[0].GetProperty("id").GetString();
                System.Console.WriteLine($"\n[VaultSharpAI] Автоопределение модели прошло успешно: {_model}");
                return; 
            }
        }
        catch
        {

        }

        _model = "gpt-3.5-turbo";
        System.Console.WriteLine($"\n[VaultSharpAI] Автоопределение не удалось. Установлена модель по умолчанию: {_model}");
    }

    public void SetModel(string modelName) => _model = modelName;
}