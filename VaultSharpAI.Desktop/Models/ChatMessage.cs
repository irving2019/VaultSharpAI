using CommunityToolkit.Mvvm.ComponentModel;

namespace VaultSharpAI.Desktop.Models;

public partial class ChatMessage : ObservableObject
{
    [ObservableProperty]
    private string _sender = string.Empty;

    [ObservableProperty]
    private string _text = string.Empty;

    public bool IsUser => Sender == "Вы";
    public bool IsSystem => Sender == "Система";
    public bool IsAssistant => !IsUser && !IsSystem;
}
