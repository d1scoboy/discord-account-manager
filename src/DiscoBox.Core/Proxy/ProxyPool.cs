using DiscoBox.Core.Models;
using DiscoBox.Core.Net;
using DiscoBox.Core.Storage;

namespace DiscoBox.Core.Proxy;

public class ProxyPool
{
    private readonly object gate = new();
    private readonly IProxyStore store;
    private List<ProxyLine> lines = new();

    public ProxyPool(IProxyStore store)
    {
        this.store = store;
    }

    public void Load()
    {
        lock (gate)
        {
            lines = store.Load();
            foreach (var line in lines.Where(l => l.Status == ProxyStatus.InUse))
            {
                line.Status = ProxyStatus.Valid;
            }
        }
    }

    public void Persist()
    {
        lock (gate)
        {
            store.Save(lines);
        }
    }

    public List<ProxyLine> Snapshot()
    {
        lock (gate)
        {
            return lines.ToList();
        }
    }

    public ProxyLine Add(string url, string? country = null)
    {
        ProxyEndpoint.Parse(url);
        var line = new ProxyLine { Url = url, Country = country };
        lock (gate)
        {
            lines.Add(line);
            store.Save(lines);
        }

        return line;
    }

    public bool Remove(string id)
    {
        lock (gate)
        {
            var removed = lines.RemoveAll(l => l.Id == id) > 0;
            if (removed)
            {
                store.Save(lines);
            }

            return removed;
        }
    }

    public ProxyLine? TakeFree()
    {
        lock (gate)
        {
            var line = lines.FirstOrDefault(l => l.Status == ProxyStatus.Valid);
            if (line is null)
            {
                return null;
            }

            line.Status = ProxyStatus.InUse;
            return line;
        }
    }

    public void Release(string id)
    {
        lock (gate)
        {
            var line = lines.FirstOrDefault(l => l.Id == id);
            if (line is { Status: ProxyStatus.InUse })
            {
                line.Status = ProxyStatus.Valid;
            }
        }
    }

    public void MarkDead(string id)
    {
        lock (gate)
        {
            var line = lines.FirstOrDefault(l => l.Id == id);
            if (line is not null)
            {
                line.Status = ProxyStatus.Dead;
                store.Save(lines);
            }
        }
    }

    public void RecordCheck(string id, string? exitIp)
    {
        lock (gate)
        {
            var line = lines.FirstOrDefault(l => l.Id == id);
            if (line is not null)
            {
                line.LastCheck = DateTime.UtcNow;
                line.ExitIp = exitIp;
            }
        }
    }
}
