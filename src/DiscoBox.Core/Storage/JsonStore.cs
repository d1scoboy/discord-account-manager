using System.Text.Json;
using System.Text.Json.Serialization;

namespace DiscoBox.Core.Storage;

public static class DataPaths
{
    public static string Root(string? overrideDir = null) =>
        overrideDir ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DiscoBox");

    public static string Accounts(string dataDir) => Path.Combine(dataDir, "accounts.json");

    public static string Proxies(string dataDir) => Path.Combine(dataDir, "proxies.json");

    public static string Jobs(string dataDir) => Path.Combine(dataDir, "jobs.json");

    public static string Settings(string dataDir) => Path.Combine(dataDir, "settings.json");

    public static string Groups(string dataDir) => Path.Combine(dataDir, "groups.json");

    public static string Profiles(string dataDir) => Path.Combine(dataDir, "profiles");

    public static string Logs(string dataDir) => Path.Combine(dataDir, "logs");
}

public static class JsonStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
    };

    public static T Load<T>(string path, T fallback) where T : class
    {
        try
        {
            if (!File.Exists(path))
            {
                return fallback;
            }

            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<T>(json, Options) ?? fallback;
        }
        catch (Exception exception)
        {
            throw new InvalidOperationException($"Failed to read {path}: {exception.Message}", exception);
        }
    }

    public static void Save<T>(string path, T value)
    {
        // ugly but working: full file rewrite on every save, lists stay small enough for this to be fine
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temp = path + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(value, Options));
        File.Move(temp, path, overwrite: true);
    }
}
