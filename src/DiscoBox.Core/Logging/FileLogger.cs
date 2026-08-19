using DiscoBox.Core.Storage;

namespace DiscoBox.Core.Logging;

public enum LogLevel
{
    Debug,
    Info,
    Warning,
    Error,
}

public class FileLogger
{
    private readonly object gate = new();
    private readonly string logDir;
    private readonly SecretMasker masker;
    private LogLevel minimum;
    private string currentDay = "";

    public FileLogger(string dataDir, LogLevel minimum, SecretMasker masker)
    {
        logDir = DataPaths.Logs(dataDir);
        this.minimum = minimum;
        this.masker = masker;
    }

    public void SetLevel(LogLevel level) => minimum = level;

    public void Debug(string source, string message) => Write(LogLevel.Debug, source, message);

    public void Info(string source, string message) => Write(LogLevel.Info, source, message);

    public void Warn(string source, string message) => Write(LogLevel.Warning, source, message);

    public void Error(string source, string message) => Write(LogLevel.Error, source, message);

    public void Error(string source, string message, Exception exception) =>
        Write(LogLevel.Error, source, $"{message}: {exception}");

    private void PruneOldLogs()
    {
        try
        {
            var old = Directory.GetFiles(logDir, "app-*.log")
                .OrderByDescending(path => path)
                .Skip(20)
                .ToList();
            foreach (var path in old)
            {
                File.Delete(path);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private void Write(LogLevel level, string source, string message)
    {
        if (level < minimum)
        {
            return;
        }

        var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{level,-7}] [{source}] {masker.Mask(message)}";
        lock (gate)
        {
            try
            {
                Directory.CreateDirectory(logDir);
                var day = DateTime.Now.ToString("yyyyMMdd");
                if (day != currentDay)
                {
                    currentDay = day;
                    PruneOldLogs();
                }

                File.AppendAllText(Path.Combine(logDir, $"app-{day}.log"), line + Environment.NewLine);
            }
            catch (IOException)
            {
                // disk full or file locked by a reader, dropping the line is better than crashing the app
            }
            catch (UnauthorizedAccessException)
            {
                // same story, nothing sensible to do from inside the logger
            }
        }
    }
}
