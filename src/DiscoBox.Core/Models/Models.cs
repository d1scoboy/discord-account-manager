namespace DiscoBox.Core.Models;

public enum AccountStatus
{
    Unchecked,
    Valid,
    Invalid,
    Locked,
    Limited,
    RateLimited,
    Error,
}

public enum ProxyMode
{
    Random,
    Manual,
}

public enum ProxyStatus
{
    Valid,
    InUse,
    Dead,
}

public enum RunStatus
{
    Pending,
    Running,
    Success,
    Failed,
    Skipped,
}

public enum JobStatus
{
    Queued,
    Running,
    Done,
    Cancelled,
}

public class Account
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    public string Username { get; set; } = "";

    public string UserId { get; set; } = "";

    public string Token { get; set; } = "";

    public string? Email { get; set; }

    public string? Password { get; set; }

    public ProxyMode ProxyMode { get; set; } = ProxyMode.Random;

    public string? ProxyUrl { get; set; }

    public string Group { get; set; } = "default";

    public List<string> Tags { get; set; } = new();

    public string Notes { get; set; } = "";

    public AccountStatus Status { get; set; } = AccountStatus.Unchecked;

    public DateTime? LastChecked { get; set; }

    public string LastTaskName { get; set; } = "";

    public RunStatus? LastTaskResult { get; set; }

    public DateTime? LastTaskTime { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class ProxyLine
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    public string Url { get; set; } = "";

    public string? Country { get; set; }

    public ProxyStatus Status { get; set; } = ProxyStatus.Valid;

    public DateTime? LastCheck { get; set; }

    public string? ExitIp { get; set; }
}

public class JobRun
{
    public string AccountId { get; set; } = "";

    public RunStatus Status { get; set; } = RunStatus.Pending;

    public string Note { get; set; } = "";

    public DateTime? StartedAt { get; set; }

    public DateTime? FinishedAt { get; set; }
}

public class Job
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    public string Name { get; set; } = "";

    public string Selection { get; set; } = "";

    public string Action { get; set; } = "";

    public JobStatus Status { get; set; } = JobStatus.Queued;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public List<JobRun> Runs { get; set; } = new();
}

public class JobEvent
{
    public DateTime Time { get; set; } = DateTime.UtcNow;

    public string Level { get; set; } = "info";

    public string Message { get; set; } = "";
}

public class AppSettings
{
    public string? DataDir { get; set; }

    public string ChromePath { get; set; } = "";

    public string GostPath { get; set; } = "";

    public int Concurrency { get; set; } = 3;

    public string LogLevel { get; set; } = "Info";
}
