using System.Collections.ObjectModel;

namespace DiscoBox.ProfileManager.ViewModels;

public static class MockPool
{
    public static ObservableCollection<ProxyRowViewModel> Lines { get; } = new();

    static MockPool()
    {
        foreach (var line in new[]
        {
            Line("pool-us:10023", "US", "bound", "Bound", "173.69.10.145", "2h ago"),
            Line("pool-us:10044", "US", "dead", "Dead", "", "3h ago"),
            Line("pool-us:10071", "US", "bound", "Bound", "173.69.44.21", "1h ago"),
            Line("pool-us:10102", "US", "free", "Free", "173.69.71.8", "20m ago"),
            Line("pool-gb:10107", "GB", "bound", "Bound", "82.102.11.4", "2h ago"),
            Line("pool-gb:10119", "GB", "free", "Free", "82.102.30.77", "40m ago"),
            Line("pool-de:10311", "DE", "bound", "Bound", "45.13.22.9", "1d ago"),
            Line("http://198.51.100.7:8080", "manual", "free", "Free", "", "never"),
            Line("socks5://203.0.113.9:1080", "manual", "dead", "Dead", "", "2d ago"),
        })
        {
            Lines.Add(line);
        }
    }

    public static ProxyRowViewModel? TakeFree()
    {
        var line = Lines.FirstOrDefault(l => l.Status == "free");
        if (line is not null)
        {
            line.Status = "bound";
            line.StatusText = "Bound";
        }

        return line;
    }

    public static void Release(ProxyRowViewModel? line)
    {
        if (line is null)
        {
            return;
        }

        line.Status = "free";
        line.StatusText = "Free";
    }

    public static ProxyRowViewModel? FindByEndpoint(string endpoint) =>
        Lines.FirstOrDefault(l => l.Endpoint == endpoint);

    private static ProxyRowViewModel Line(string endpoint, string country, string status, string statusText,
        string exitIp, string lastCheck) => new()
        {
            Endpoint = endpoint,
            Country = country,
            Status = status,
            StatusText = statusText,
            ExitIp = exitIp,
            Source = country == "manual" ? "manual" : "pool",
            LastCheck = lastCheck,
        };
}
