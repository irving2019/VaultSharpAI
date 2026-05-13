using System;
using VaultSharpAI.Console.Services;

Console.WriteLine("=== Настройка VaultSharpAI ===");

Console.WriteLine("Введите Base URL (например, https://api.deepseek.com/v1/): ");
string baseUrl = Console.ReadLine() ?? "https://api.openai.com/v1/";

Console.WriteLine("Введите API ключ: ");
string apiKey = Console.ReadLine() ?? "";

Console.WriteLine("Введите название модели (оставьте пустым для автоопределения): ");
string modelInput = Console.ReadLine() ?? "";

var aiProvider = new CompatibleProvider(apiKey, baseUrl);

if (!string.IsNullOrWhiteSpace(modelInput))
    aiProvider.SetModel(modelInput);

Console.WriteLine("\n--- Ядро запущено. Для выхода введите 'exit' ---");

while(true)
{
    Console.ForegroundColor = ConsoleColor.Green;
    Console.WriteLine("\nВы: ");
    Console.ResetColor();

    string? input = Console.ReadLine();

    if (string.IsNullOrWhiteSpace(input) || input.ToLower() == "exit")
        break;

    string response = await aiProvider.GenerateResponseAsync(input);

    Console.ForegroundColor = ConsoleColor.Cyan;
    Console.WriteLine($"\nVaultSharpAI: {response}");
    Console.ResetColor();
}