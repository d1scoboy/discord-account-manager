using DiscoBox.Core.Logging;
using DiscoBox.Core.Models;
using DiscoBox.Core.Net;

namespace DiscoBox.Core.Proxy;

public class ResolvedProxy
{
    public ProxyEndpoint Endpoint { get; init; } = null!;

    public string? PoolLineId { get; init; }

    public string Description => Endpoint.MaskedUrl;
}

public class ProxyResolver
{
    public const int MaxRetakes = 3;

    private readonly ProxyPool pool;
    private readonly FileLogger logger;

    public ProxyResolver(ProxyPool pool, FileLogger logger)
    {
        this.pool = pool;
        this.logger = logger;
    }

    public ResolvedProxy? Resolve(Account account)
    {
        if (account.ProxyMode == ProxyMode.Manual)
        {
            if (string.IsNullOrWhiteSpace(account.ProxyUrl))
            {
                logger.Warn("proxy", $"account {account.Username}: manual mode without url, falling back to pool");
                return TakeFromPool();
            }

            try
            {
                return new ResolvedProxy { Endpoint = ProxyEndpoint.Parse(account.ProxyUrl) };
            }
            catch (FormatException exception)
            {
                logger.Error("proxy", $"account {account.Username}: bad manual proxy url", exception);
                return null;
            }
        }

        return TakeFromPool();
    }

    public ResolvedProxy? Retake(ResolvedProxy failed)
    {
        if (failed.PoolLineId is not null)
        {
            pool.MarkDead(failed.PoolLineId);
            logger.Warn("proxy", $"pool line {failed.PoolLineId} marked dead after failed task");
        }

        return TakeFromPool();
    }

    public void Release(ResolvedProxy? resolved)
    {
        if (resolved?.PoolLineId is not null)
        {
            pool.Release(resolved.PoolLineId);
        }
    }

    private ResolvedProxy? TakeFromPool()
    {
        var line = pool.TakeFree();
        if (line is null)
        {
            logger.Warn("proxy", "no free proxy lines left in pool");
            return null;
        }

        try
        {
            return new ResolvedProxy
            {
                Endpoint = ProxyEndpoint.Parse(line.Url),
                PoolLineId = line.Id,
            };
        }
        catch (FormatException exception)
        {
            logger.Error("proxy", $"pool line {line.Id} has bad url", exception);
            pool.MarkDead(line.Id);
            return null;
        }
    }
}
