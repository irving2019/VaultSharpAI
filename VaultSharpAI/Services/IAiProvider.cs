using System.Threading.Tasks;
using System.Collections.Generic;

namespace VaultSharpAI.Console.Services;

public interface IAiProvider
{
    Task<string> GenerateResponseAsync(string prompt);
    Task<List<string>> GetAvailableModelsAsync();
}