using DiscoBox.Core.Models;

namespace DiscoBox.Core.Storage;

public interface IAccountStore
{
    List<Account> Load();
    void Save(IReadOnlyCollection<Account> accounts);
}

public interface IProxyStore
{
    List<ProxyLine> Load();
    void Save(IReadOnlyCollection<ProxyLine> lines);
}

public interface IJobStore
{
    List<Job> Load();
    void Save(IReadOnlyCollection<Job> jobs);
    void AppendEvents(string jobId, IReadOnlyCollection<JobEvent> events);
    List<JobEvent> LoadEvents(string jobId);
}

public interface ISettingsStore
{
    AppSettings Load();
    void Save(AppSettings settings);
}

public interface IGroupStore
{
    (List<string> Groups, List<string> Tags) Load();
    void Save(IReadOnlyCollection<string> groups, IReadOnlyCollection<string> tags);
}

public class AccountStore : IAccountStore
{
    private readonly string path;

    public AccountStore(string dataDir) => path = DataPaths.Accounts(dataDir);

    public List<Account> Load() => JsonStore.Load(path, new List<Account>());

    public void Save(IReadOnlyCollection<Account> accounts) => JsonStore.Save(path, accounts);
}

public class ProxyStore : IProxyStore
{
    private readonly string path;

    public ProxyStore(string dataDir) => path = DataPaths.Proxies(dataDir);

    public List<ProxyLine> Load() => JsonStore.Load(path, new List<ProxyLine>());

    public void Save(IReadOnlyCollection<ProxyLine> lines) => JsonStore.Save(path, lines);
}

public class JobStore : IJobStore
{
    private readonly string dataDir;

    public JobStore(string dataDir) => this.dataDir = dataDir;

    public List<Job> Load() => JsonStore.Load(DataPaths.Jobs(dataDir), new List<Job>());

    public void Save(IReadOnlyCollection<Job> jobs) =>
        JsonStore.Save(DataPaths.Jobs(dataDir),
            jobs.OrderByDescending(job => job.CreatedAt).Take(20).ToList());

    public void AppendEvents(string jobId, IReadOnlyCollection<JobEvent> events)
    {
        if (events.Count == 0)
        {
            return;
        }

        Directory.CreateDirectory(DataPaths.Logs(dataDir));
        var path = Path.Combine(DataPaths.Logs(dataDir), $"job-{jobId}.jsonl");
        using (var writer = File.AppendText(path))
        {
            foreach (var evt in events)
            {
                writer.WriteLine(System.Text.Json.JsonSerializer.Serialize(evt));
            }
        }

        var lines = File.ReadAllLines(path);
        if (lines.Length > 20)
        {
            File.WriteAllLines(path, lines[^20..]);
        }
    }

    public List<JobEvent> LoadEvents(string jobId)
    {
        var path = Path.Combine(DataPaths.Logs(dataDir), $"job-{jobId}.jsonl");
        if (!File.Exists(path))
        {
            return new List<JobEvent>();
        }

        return File.ReadAllLines(path)
            .Where(line => line.Length > 0)
            .Select(line => System.Text.Json.JsonSerializer.Deserialize<JobEvent>(line))
            .Where(evt => evt is not null)
            .Cast<JobEvent>()
            .ToList();
    }
}

public class SettingsStore : ISettingsStore
{
    private readonly string path;

    public SettingsStore(string dataDir) => path = DataPaths.Settings(dataDir);

    public AppSettings Load() => JsonStore.Load(path, new AppSettings());

    public void Save(AppSettings settings) => JsonStore.Save(path, settings);
}

public class GroupStore : IGroupStore
{
    private readonly string path;

    public GroupStore(string dataDir) => path = DataPaths.Groups(dataDir);

    public (List<string> Groups, List<string> Tags) Load()
    {
        var data = JsonStore.Load(path, new GroupData());
        return (data.Groups, data.Tags);
    }

    public void Save(IReadOnlyCollection<string> groups, IReadOnlyCollection<string> tags) =>
        JsonStore.Save(path, new GroupData
        {
            Groups = groups.ToList(),
            Tags = tags.ToList(),
        });

    private class GroupData
    {
        public List<string> Groups { get; set; } = new() { "default" };
        public List<string> Tags { get; set; } = new();
    }
}
