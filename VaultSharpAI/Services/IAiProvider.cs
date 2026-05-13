using System.Threading.Tasks;

namespace VaultSharpAI.Console.Services;

public interface IAiProvider
{
    Task<string> GenerateResponseAsync(string prompt);
}