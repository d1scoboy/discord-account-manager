using System.Collections.ObjectModel;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DiscoBox.Core.Models;
using DiscoBox.ProfileManager.Services;

namespace DiscoBox.ProfileManager.ViewModels;

public partial class JobRunViewModel : ObservableObject
{
    [ObservableProperty]
    private string account = "";

    [ObservableProperty]
    private string status = "pending";

    [ObservableProperty]
    private string statusText = "Pending";

    [ObservableProperty]
    private string note = "";
}

public partial class TraceItemViewModel : ObservableObject
{
    [ObservableProperty]
    private string time = "";

    [ObservableProperty]
    private string message = "";
}

public partial class JobViewModel : ObservableObject
{
    public Job Model { get; }

    public CancellationTokenSource Cancellation { get; } = new();

    [ObservableProperty]
    private string name = "";

    [ObservableProperty]
    private string status = "queued";

    [ObservableProperty]
    private string statusText = "Queued";

    [ObservableProperty]
    private string createdAt = "";

    [ObservableProperty]
    private string countersText = "";

    public ObservableCollection<JobRunViewModel> Runs { get; } = new();

    public ObservableCollection<TraceItemViewModel> Trace { get; } = new();

    public JobViewModel(Job model)
    {
        Model = model;
        SyncHeader();
    }

    public void SyncHeader()
    {
        Name = Model.Name;
        Status = Model.Status.ToString().ToLowerInvariant();
        StatusText = Model.Status.ToString();
        CreatedAt = Model.CreatedAt.ToLocalTime().ToString("HH:mm:ss");
        Recount();
    }

    public void Recount()
    {
        var success = Model.Runs.Count(r => r.Status == RunStatus.Success);
        var failed = Model.Runs.Count(r => r.Status == RunStatus.Failed);
        var skipped = Model.Runs.Count(r => r.Status == RunStatus.Skipped);
        var left = Model.Runs.Count(r => r.Status is RunStatus.Pending or RunStatus.Running);
        CountersText = $"{success} ok / {failed} failed / {skipped} skipped / {left} left";
    }

    public void SyncRuns(IReadOnlyDictionary<string, string> accountNames)
    {
        Runs.Clear();
        foreach (var run in Model.Runs)
        {
            Runs.Add(new JobRunViewModel
            {
                Account = accountNames.GetValueOrDefault(run.AccountId, run.AccountId[..8]),
                Status = run.Status.ToString().ToLowerInvariant(),
                StatusText = run.Status.ToString(),
                Note = run.Note,
            });
        }
    }

    public void SyncTrace(IEnumerable<JobEvent> events)
    {
        Trace.Clear();
        foreach (var evt in events.Reverse())
        {
            Trace.Add(new TraceItemViewModel
            {
                Time = evt.Time.ToLocalTime().ToString("HH:mm:ss"),
                Message = evt.Message,
            });
        }
    }
}

public partial class TasksPageViewModel : ObservableObject
{
    private readonly DispatcherTimer refreshTimer;

    public ObservableCollection<JobViewModel> Jobs { get; } = new();

    public IReadOnlyList<string> Selections { get; } = BuildSelections();

    public IReadOnlyList<string> Actions { get; } = new[] { "check_status", "verify_proxy" };

    [ObservableProperty]
    private string selection = "All accounts";

    [ObservableProperty]
    private string action = "check_status";

    [ObservableProperty]
    private string concurrency = "3";

    [ObservableProperty]
    private JobViewModel? selectedJob;

    [ObservableProperty]
    private string footerMessage = "Ready";

    public string SummaryText =>
        $"{Jobs.Count} jobs • {Jobs.Count(j => j.Model.Status == JobStatus.Running)} running";

    public TasksPageViewModel()
    {
        // HACK: polling timer for the trace because engine workers write events from background threads
        foreach (var job in AppServices.Engine.Jobs)
        {
            Jobs.Add(new JobViewModel(job));
        }

        Concurrency = AppServices.Settings.Concurrency.ToString();
        refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(800) };
        refreshTimer.Tick += (_, _) => RefreshSelected();
        refreshTimer.Start();
    }

    private static List<string> BuildSelections()
    {
        var list = new List<string> { "All accounts" };
        list.AddRange(AppServices.Groups.Groups.Select(g => $"Group: {g}"));
        list.AddRange(AppServices.Groups.Tags.Select(t => $"Tag: {t}"));
        return list;
    }

    partial void OnSelectedJobChanged(JobViewModel? value) => RefreshSelected();

    public void RefreshSelected()
    {
        if (SelectedJob is null)
        {
            return;
        }

        var names = AppServices.Accounts.ToDictionary(a => a.Id, a => a.Username);
        SelectedJob.SyncHeader();
        SelectedJob.SyncRuns(names);
        SelectedJob.SyncTrace(AppServices.JobStore.LoadEvents(SelectedJob.Model.Id));
        OnPropertyChanged(nameof(SummaryText));
    }

    [RelayCommand]
    private async Task CreateJobAsync()
    {
        var models = ResolveSelection();
        if (models.Count == 0)
        {
            FooterMessage = "Selection is empty: nothing to run";
            return;
        }

        var job = AppServices.Engine.CreateJob($"{Action} • {models.Count} acc", Action, Selection, models);
        var vm = new JobViewModel(job);
        Jobs.Insert(0, vm);
        SelectedJob = vm;
        OnPropertyChanged(nameof(SummaryText));
        FooterMessage = $"Job created: {job.Name} ({models.Count} runs)";

        await Task.Run(() => AppServices.Engine.RunAsync(job, models, ParseConcurrency(), vm.Cancellation.Token));
        RefreshSelected();
        FooterMessage = $"Job {job.Status}: {vm.CountersText}";
    }

    [RelayCommand]
    private async Task RetryFailedAsync()
    {
        if (SelectedJob is null)
        {
            FooterMessage = "Select a job first";
            return;
        }

        var vm = SelectedJob;
        var retry = vm.Model.Runs.Count(r => r.Status is RunStatus.Failed or RunStatus.Skipped);
        if (retry == 0)
        {
            FooterMessage = "Nothing to retry in this job";
            return;
        }

        var models = AppServices.Accounts
            .Where(a => vm.Model.Runs.Any(r => r.AccountId == a.Id))
            .ToList();
        FooterMessage = $"Retrying {retry} runs";
        await Task.Run(() =>
            AppServices.Engine.RetryFailedAsync(vm.Model, models, ParseConcurrency(), vm.Cancellation.Token));
        RefreshSelected();
        FooterMessage = $"Job {vm.Model.Status}: {vm.CountersText}";
    }

    [RelayCommand]
    private void CancelJob()
    {
        if (SelectedJob is null || SelectedJob.Model.Status != JobStatus.Running)
        {
            FooterMessage = "Select a running job to cancel";
            return;
        }

        SelectedJob.Cancellation.Cancel();
        FooterMessage = "Cancellation requested (applies between runs)";
    }

    private int ParseConcurrency() =>
        int.TryParse(Concurrency, out var value) && value > 0 ? value : 3;

    private List<Account> ResolveSelection()
    {
        if (Selection.StartsWith("Group: ", StringComparison.Ordinal))
        {
            var group = Selection["Group: ".Length..];
            return AppServices.Accounts.Where(a => a.Group == group).ToList();
        }

        if (Selection.StartsWith("Tag: ", StringComparison.Ordinal))
        {
            var tag = Selection["Tag: ".Length..];
            return AppServices.Accounts.Where(a => a.Tags.Contains(tag, StringComparer.OrdinalIgnoreCase)).ToList();
        }

        return AppServices.Accounts.ToList();
    }
}
