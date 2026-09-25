using System.IO.Compression;

namespace DiscoBox.Core.Chrome;

public class ProfileStore
{
    private static readonly string[] PruneDirs =
    {
        Path.Combine("Default", "Cache"),
        Path.Combine("Default", "Code Cache"),
        Path.Combine("Default", "GPUCache"),
        Path.Combine("Default", "DawnWebGPUCache"),
        Path.Combine("Default", "DawnGraphiteCache"),
        Path.Combine("Default", "ShaderCache"),
        Path.Combine("Default", "GrShaderCache"),
        Path.Combine("Default", "GraphiteDawnCache"),
        Path.Combine("Default", "Service Worker", "CacheStorage"),
        Path.Combine("Default", "Service Worker", "ScriptCache"),
        Path.Combine("Default", "Shared Dictionary"),
        Path.Combine("Default", "Safe Browsing"),
        Path.Combine("Default", "Segmentation Platform"),
        "component_crx_cache",
        "extensions_crx_cache",
        "Crashpad",
    };

    private readonly string profilesDir;

    public ProfileStore(string dataDir)
    {
        profilesDir = Storage.DataPaths.Profiles(dataDir);
        Directory.CreateDirectory(profilesDir);
    }

    public bool HasProfile(string accountId) => File.Exists(ZipPath(accountId));

    private string ZipPath(string accountId) => Path.Combine(profilesDir, $"{Sanitize(accountId)}.zip");

    private static string Sanitize(string accountId) =>
        string.Concat(accountId.Split(Path.GetInvalidFileNameChars()));

    public void UnzipTo(string accountId, string targetDir)
    {
        DeleteDirectoryRetry(targetDir);
        Directory.CreateDirectory(targetDir);
        var zip = ZipPath(accountId);
        if (File.Exists(zip))
        {
            ZipFile.ExtractToDirectory(zip, targetDir);
        }
    }

    public void ZipFrom(string accountId, string sourceDir)
    {
        // tried incremental repack via ZipArchive updates, slower and left stale entries, full create wins
        Prune(sourceDir);
        var zip = ZipPath(accountId);
        if (File.Exists(zip))
        {
            File.Delete(zip);
        }

        ZipFile.CreateFromDirectory(sourceDir, zip, CompressionLevel.Optimal, false);
        DeleteDirectoryRetry(sourceDir);
    }

    public static void Prune(string profileDir)
    {
        foreach (var relative in PruneDirs)
        {
            var path = Path.Combine(profileDir, relative);
            if (Directory.Exists(path))
            {
                try
                {
                    Directory.Delete(path, recursive: true);
                }
                catch (IOException)
                {
                }
                catch (UnauthorizedAccessException)
                {
                }
            }
        }

        foreach (var baseDir in new[] { profileDir, Path.Combine(profileDir, "Default") })
        {
            if (!Directory.Exists(baseDir))
            {
                continue;
            }

            foreach (var path in Directory.EnumerateFileSystemEntries(baseDir, "optimization_guide_*",
                         SearchOption.TopDirectoryOnly))
            {
                try
                {
                    if (Directory.Exists(path))
                    {
                        Directory.Delete(path, recursive: true);
                    }
                    else
                    {
                        File.Delete(path);
                    }
                }
                catch (IOException)
                {
                }
            }
        }
    }

    public static void DeleteDirectoryRetry(string path)
    {
        for (var attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                if (Directory.Exists(path))
                {
                    Directory.Delete(path, recursive: true);
                }

                return;
            }
            catch (IOException)
            {
                Thread.Sleep(300);
            }
            catch (UnauthorizedAccessException)
            {
                Thread.Sleep(300);
            }
        }
    }
}
