using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using VaultSharpAI.Console.Services;
using Tmds.DBus.Protocol;
using VaultSharpAI.Desktop.Models;

namespace VaultSharpAI.Desktop.ViewModels;

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
    [NotifyPropertyChangedFor(nameof(IsFileAttached))]
    [NotifyPropertyChangedFor(nameof(IsFileError))]
    private string _attachedFileInfo = "Файл не выбран";

    public bool IsFileAttached => AttachedFileInfo != "Файл не выбран" && !string.IsNullOrEmpty(AttachedFileInfo) && !AttachedFileInfo.StartsWith("Ошибка");
    public bool IsFileError => AttachedFileInfo.StartsWith("Ошибка");


    private string? _attachedImageBase64;
    private string? _attachedImageMime;
    private string? _attachedFileTextContext;
    private string? _attachedFileName;

    public enum ApiConnectionStatus
    {
        WaitKey,
        Online,
        Offline
    }

    private static readonly string ProfilesFilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "VaultSharpAI",
        "profiles.json");

    public ObservableCollection<ChatMessage> Messages { get; } = new();
    public ObservableCollection<string> AvailableModels { get; } = new();
    public ObservableCollection<ApiProfile> SavedProfiles { get; } = new();

    private readonly List<string> _allAvailableModels = new();

    [ObservableProperty]
    private string _modelSearchText = "";

    [ObservableProperty]
    private bool _isModelDropdownOpen;

    partial void OnIsModelDropdownOpenChanged(bool value)
    {
        if (value)
        {
            ResetModelFilter();
        }
    }

    partial void OnModelSearchTextChanged(string value)
    {
        if (IsModelDropdownOpen && !string.IsNullOrEmpty(value))
        {
            AvailableModels.Clear();
            var filtered = _allAvailableModels
                .Where(m => m.Contains(value, StringComparison.OrdinalIgnoreCase));
            foreach (var model in filtered)
            {
                AvailableModels.Add(model);
            }
        }
    }

    private void ResetModelFilter()
    {
        AvailableModels.Clear();
        foreach (var model in _allAvailableModels)
        {
            AvailableModels.Add(model);
        }
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsStatusWaitKey))]
    [NotifyPropertyChangedFor(nameof(IsStatusOnline))]
    [NotifyPropertyChangedFor(nameof(IsStatusOffline))]
    private ApiConnectionStatus _connectionStatus = ApiConnectionStatus.WaitKey;

    public bool IsStatusWaitKey => ConnectionStatus == ApiConnectionStatus.WaitKey;
    public bool IsStatusOnline => ConnectionStatus == ApiConnectionStatus.Online;
    public bool IsStatusOffline => ConnectionStatus == ApiConnectionStatus.Offline;

    public MainWindowViewModel()
    {
        LoadProfiles();
        UpdateConnectionStatus();
    }

    private void UpdateConnectionStatus()
    {
        if (string.IsNullOrWhiteSpace(ApiKey) || string.IsNullOrWhiteSpace(BaseUrl))
        {
            ConnectionStatus = ApiConnectionStatus.WaitKey;
        }
    }

    partial void OnApiKeyChanged(string value) => UpdateConnectionStatus();
    partial void OnBaseUrlChanged(string value) => UpdateConnectionStatus();

    [RelayCommand]
    private void OpenGithub()
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "https://github.com/irving2019",
                UseShellExecute = true
            });
        }
        catch
        {
            // ignore
        }
    }

    private void LoadProfiles()
    {
        try
        {
            if (File.Exists(ProfilesFilePath))
            {
                var json = File.ReadAllText(ProfilesFilePath);
                var profiles = JsonSerializer.Deserialize<List<ApiProfile>>(json);
                if (profiles != null)
                {
                    foreach (var profile in profiles)
                    {
                        SavedProfiles.Add(profile);
                    }
                }
            }
        }
        catch
        {
            // Ignore loading errors
        }

        if (SavedProfiles.Count == 0)
        {
            SavedProfiles.Add(new ApiProfile { Name = "OpenRouter", BaseUrl = "https://openrouter.ai/api/v1" });
            SavedProfiles.Add(new ApiProfile { Name = "Локальная модель", BaseUrl = "http://localhost:11434/v1", ApiKey = "ollama" });
            SaveProfiles();
        }
    }

    private void SaveProfiles()
    {
        try
        {
            var directory = Path.GetDirectoryName(ProfilesFilePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var json = JsonSerializer.Serialize(SavedProfiles, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(ProfilesFilePath, json);
        }
        catch
        {
            // Ignore saving errors
        }
    }

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
            SaveProfiles();
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
            SaveProfiles();
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

        try
        {
            var provider = new CompatibleProvider(ApiKey, BaseUrl);
            List<string> models = await provider.GetAvailableModelsAsync();

            _allAvailableModels.Clear();
            _allAvailableModels.AddRange(models);
            ResetModelFilter();

            if (AvailableModels.Count > 0)
                ModelName = AvailableModels[0];

            ConnectionStatus = ApiConnectionStatus.Online;
        }
        catch (Exception ex)
        {
            _allAvailableModels.Clear();
            _allAvailableModels.Add("gpt-3.5-turbo");
            ResetModelFilter();

            ModelName = "gpt-3.5-turbo";
            Messages.Add(new ChatMessage 
            { 
                Sender = "Система", 
                Text = $"Ошибка синхронизации моделей: {ex.Message}. Использовано значение по умолчанию (gpt-3.5-turbo)." 
            });
            ConnectionStatus = ApiConnectionStatus.Offline;
        }
        finally
        {
            IsThinking = false;
        }
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

        if (response.StartsWith("[Ошибка") || response.StartsWith("[Нестандартный") || response.StartsWith("[Ответ сервера"))
        {
            ConnectionStatus = ApiConnectionStatus.Offline;
        }
        else
        {
            ConnectionStatus = ApiConnectionStatus.Online;
        }

        Messages.Add(new ChatMessage { Sender = string.IsNullOrEmpty(ModelName) ? "VaultSharpAI" : ModelName, Text = response });
        IsThinking = false;
    }
}