using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using VaultSharpAI.Console.Services;
using Tmds.DBus.Protocol;

namespace VaultSharpAI.Desktop.ViewModels;

public class ApiProfile
{
    public string Name { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
}

public partial class ChatMessage : ObservableObject
{
    [ObservableProperty]
    private string _sender = string.Empty;

    [ObservableProperty]
    private string _text = string.Empty;

    public bool IsUser => Sender == "Вы";
}

public partial class MainWindowViewModel : ViewModelBase
{
    [ObservableProperty]
    private string _baseUrl = "";

    [ObservableProperty]
    private string _apiKey = "";

    [ObservableProperty]
    private string _modelName = "";

    [ObservableProperty]
    private string _userInput = "";

    [ObservableProperty]
    private bool _isThinking = false;

    [ObservableProperty]
    private bool _isThinkingEnabled = false;

    [ObservableProperty]
    private bool _isDeepResearchEnabled = false;

    [ObservableProperty]
    private int _maxTokens = 4096;

    [ObservableProperty]
    private string _attachedFileInfo = "Файл не выбран";


    private string? _attachedImageBase64;
    private string? _attachedImageMime;
    private string? _attachedFileTextContext;
    private string? _attachedFileName;

    public ObservableCollection<ChatMessage> Messages { get; } = new();
    public ObservableCollection<string> AvailableModels { get; } = new();
    public ObservableCollection<ApiProfile> SavedProfiles { get; } = new()
    {
        new ApiProfile {Name = "OpenRouter", BaseUrl = "https://openrouter.ai/api/v1"},
        new ApiProfile {Name = "Локальная модель", BaseUrl = "http://localhost:11434/v1", ApiKey = "ollama"}
    };

    [ObservableProperty]
    private ApiProfile? _selectedProfile;

    partial void OnSelectedProfileChanged(ApiProfile? value)
    {
        if (value != null)
        {
            BaseUrl = value.BaseUrl;
            ApiKey = value.ApiKey;
        }
    }

    private void EnsureProfileIsSaved()
    {
        if (string.IsNullOrWhiteSpace(BaseUrl))
            return;

        var existing = SavedProfiles.FirstOrDefault(p => p.BaseUrl.TrimEnd("/") == BaseUrl.TrimEnd('/'));

        if (existing != null)
        {
            existing.ApiKey = ApiKey;
        }
        else
        {
            var newProfile = new ApiProfile
            {
                Name = $"Профиль {SavedProfiles.Count + 1} ({new Uri(BaseUrl).Host})",
                BaseUrl = BaseUrl,
                ApiKey = ApiKey
            };
            SavedProfiles.Add(newProfile);
        }
    }

    [RelayCommand]
    private async Task LoadModelsAsync()
    {
        if (string.IsNullOrWhiteSpace(BaseUrl) || string.IsNullOrEmpty(ApiKey))
            return;

        EnsureProfileIsSaved();
        IsThinking = true;
        AvailableModels.Clear();

        var provider = new CompatibleProvider(ApiKey, BaseUrl);
        List<string> models = await provider.GetAvailableModelsAsync();

        foreach (var model in models)
            AvailableModels.Add(model);

        if (AvailableModels.Count > 0)
            ModelName = AvailableModels[0];

        IsThinking = false;
    }

    [RelayCommand]
    private async Task AttachFileAsync()
    {
        var desktop = Avalonia.Application.Current?.ApplicationLifetime as Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime;

        if (desktop?.MainWindow == null)
            return;

        var topLevel = Avalonia.Controls.TopLevel.GetTopLevel(desktop.MainWindow);

        if (topLevel == null)
            return;

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new Avalonia.Platform.Storage.FilePickerOpenOptions
        {
            Title = "Выберите изображение или текстовый файл",
            AllowMultiple = false

        });

        if (files == null || files.Count == 0)
            return;

        var file = files[0];
        _attachedFileName = file.Name;
        AttachedFileInfo = $"📎 Файл: {file.Name}";

        var extension = Path.GetExtension(file.Name).ToLower();

        try
        {
            if (extension == ".png" || extension == ".jpg" || extension == ".jpeg" || extension == ".webp")
            {
                using var stream = await file.OpenReadAsync();
                using var ms = new MemoryStream();
                await stream.CopyToAsync(ms);

                _attachedImageBase64 = Convert.ToBase64String(ms.ToArray());
                _attachedImageMime = extension == ".png" ? "image/png" : "image/jpeg";
                _attachedFileTextContext = null;
            }
            else
            {
                using var stream = await file.OpenReadAsync();
                using var reader = new StreamReader(stream);

                _attachedFileTextContext = await reader.ReadToEndAsync();
                _attachedImageBase64 = null;
                _attachedImageMime = null;
            }
        }
        catch (Exception ex)
        {
            AttachedFileInfo = $"Ошибка чтения {ex.Message}";
            _attachedFileName = null;
        }
    }

    [RelayCommand]
    private async Task SendMessageAsync()
    {
        if (string.IsNullOrWhiteSpace(UserInput) && string.IsNullOrEmpty(_attachedFileName))
            return;
        if (string.IsNullOrWhiteSpace(ApiKey) || string.IsNullOrWhiteSpace(ModelName))
            return;

        EnsureProfileIsSaved();

        string finalPrompt = UserInput;

        if (!string.IsNullOrEmpty(_attachedFileTextContext))
            finalPrompt = $"[Контекст из прикрепленного файла {_attachedFileName}]:\n{_attachedFileTextContext}\n\n[Промпт пользователя]:\n{UserInput}";

        string displayMsg = string.IsNullOrEmpty(_attachedFileName) ? UserInput : $"[{_attachedFileName}] {UserInput}";
        Messages.Add(new ChatMessage { Sender = "Вы", Text = displayMsg });

        UserInput = string.Empty;
        IsThinking = true;

        var imgBase64 = _attachedImageBase64;
        var imgMime = _attachedImageMime;

        AttachedFileInfo = "Файл не выбран";
        _attachedFileName = null;
        _attachedFileTextContext = null;
        _attachedImageMime = null;
        _attachedImageBase64 = null;

        var activeProvider = new CompatibleProvider(ApiKey, BaseUrl);
        activeProvider.SetModel(ModelName);

        string response = await activeProvider.GenerateResponseAsync(
            finalPrompt,
            MaxTokens,
            IsThinkingEnabled,
            IsDeepResearchEnabled,
            imgBase64,
            imgMime);

        Messages.Add(new ChatMessage { Sender = "Вы", Text = response });
        IsThinking = false;
    }
}