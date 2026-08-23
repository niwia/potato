namespace Potato.Core.Services;

public record GameFixResult
{
    public bool HasEos { get; init; }
    public List<string> EosFiles { get; init; } = new();
    public List<string> Executables { get; init; } = new();
    public string Summary { get; init; } = string.Empty;
}

public class GameFixerService
{
    public static GameFixResult AnalyzeGameDirectory(string gameDir)
    {
        if (!Directory.Exists(gameDir))
        {
            return new GameFixResult { Summary = "Game directory does not exist." };
        }

        var eosFiles = new List<string>();
        var executables = new List<string>();

        try
        {
            var files = Directory.GetFiles(gameDir, "*.*", SearchOption.AllDirectories);
            foreach (var file in files)
            {
                var name = Path.GetFileName(file);
                if (name.StartsWith("EOSSDK", StringComparison.OrdinalIgnoreCase) ||
                    name.Contains("EpicOnlineServices", StringComparison.OrdinalIgnoreCase))
                {
                    eosFiles.Add(file);
                }

                if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ||
                    name.EndsWith(".x86_64", StringComparison.OrdinalIgnoreCase))
                {
                    executables.Add(file);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error scanning game directory: {ex.Message}");
        }

        return new GameFixResult
        {
            HasEos = eosFiles.Count > 0,
            EosFiles = eosFiles,
            Executables = executables,
            Summary = $"Found {executables.Count} executables, {eosFiles.Count} EOS SDK files."
        };
    }
}
