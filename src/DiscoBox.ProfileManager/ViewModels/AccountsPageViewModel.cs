using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DiscoBox.Core.Models;
using DiscoBox.Core.Parsing;
using DiscoBox.ProfileManager.Services;
using DiscoBox.ProfileManager.Views.Dialogs;
using Microsoft.Win32;
using RunStatus = DiscoBox.Core.Models.RunStatus;

namespace DiscoBox.ProfileManager.ViewModels;

public partial class AccountRowViewModel : ObservableObject
{
    public Account Model { get; }

    [ObservableProperty]
    private string username = "";

    [ObservableProperty]
    private string userId = "";

    [ObservableProperty]
    private string tokenMasked = "";

    [ObservableProperty]
    private string proxyLabel = "";

    [ObservableProperty]
    private string status = "unchecked";

    [ObservableProperty]
    private string statusText = "Unchecked";

    [ObservableProperty]
    private string lastChecked = "never";

    [ObservableProperty]
    private string group = "default";

    [ObservableProperty]
    private string tags = "";

    [ObservableProperty]
    private string notes = "";

    [ObservableProperty]
    private string lastTaskName = "";

    [ObservableProperty]
    private string lastTaskResult = "";

    [ObservableProperty]
    private string lastTaskTime = "";

    public bool HasLastTask => LastTaskName.Length > 0;

    public string Initials => string.IsNullOrEmpty(Username)
        ? "?"
        : Username[..1].ToUpperInvariant();

    public ObservableCollection<HistoryItemViewModel> History { get; } = new();

    public AccountRowViewModel(Account model)
    {
        Model = model;
        Sync();
    }

    partial void OnLastTaskNameChanged(string value) => OnPropertyChanged(nameof(HasLastTask));

    partial void OnUsernameChanged(string value) => OnPropertyChanged(nameof(Initials));

    public void Sync()
    {
        Username = Model.Username;
        UserId = Model.UserId;
        TokenMasked = AccountsPageViewModel.MaskToken(Model.Token);
        ProxyLabel = Model.ProxyMode == ProxyMode.Manual && !string.IsNullOrWhiteSpace(Model.ProxyUrl)
            ? Model.ProxyUrl!
            : "random from pool";
        Status = Model.Status.ToString().ToLowerInvariant();
        StatusText = Model.Status.ToString();
        LastChecked = TimeAgo.Text(Model.LastChecked);
        Group = Model.Group;
        Tags = string.Join(", ", Model.Tags);
        Notes = Model.Notes;
        LastTaskName = Model.LastTaskName;
        LastTaskResult = Model.LastTaskResult?.ToString().ToLowerInvariant() ?? "";
        LastTaskTime = TimeAgo.Text(Model.LastTaskTime);
    }

    public void LoadHistory()
    {
        History.Clear();
        var runs = AppServices.Engine.Jobs
            .SelectMany(job => job.Runs
                .Where(run => run.AccountId == Model.Id)
                .Select(run => (Job: job, Run: run)))
            .Where(entry => entry.Run.FinishedAt is not null)
            .OrderByDescending(entry => entry.Run.FinishedAt)
            .Take(20)
            .ToList();

        foreach (var entry in runs)
        {
            History.Add(new HistoryItemViewModel
            {
                Name = entry.Job.Action,
                Result = entry.Run.Status.ToString().ToLowerInvariant(),
                ResultText = entry.Run.Status.ToString(),
                Time = TimeAgo.Text(entry.Run.FinishedAt),
            });
        }

        if (History.Count == 0 && Model.LastTaskName.Length > 0)
        {
            History.Add(new HistoryItemViewModel
            {
                Name = Model.LastTaskName,
                Result = LastTaskResult,
                ResultText = Model.LastTaskResult?.ToString() ?? "",
                Time = LastTaskTime,
            });
        }
    }
}

public static class TimeAgo
{
    public static string Text(DateTime? utc)
    {
        if (utc is null)
        {
            return "never";
        }

        var span = DateTime.UtcNow - utc.Value;
        if (span.TotalSeconds < 90)
        {
            return "just now";
        }

        if (span.TotalMinutes < 60)
        {
            return $"{(int)span.TotalMinutes}m ago";
        }

        if (span.TotalHours < 24)
        {
            return $"{(int)span.TotalHours}h ago";
        }

        if (span.TotalDays < 14)
        {
            return $"{(int)span.TotalDays}d ago";
        }

        return utc.Value.ToLocalTime().ToString("yyyy-MM-dd");
    }
}

public partial class HistoryItemViewModel : ObservableObject
{
    [ObservableProperty]
    private string name = "";

    [ObservableProperty]
    private string result = "success";

    [ObservableProperty]
    private string resultText = "Success";

    [ObservableProperty]
    private string time = "";
}

public partial class GroupSectionViewModel : ObservableObject
{
    [ObservableProperty]
    private string name = "";

    [ObservableProperty]
    private bool isExpanded = true;

    public ObservableCollection<AccountRowViewModel> Accounts { get; } = new();

    public string CountText => Accounts.Count.ToString();

    public GroupSectionViewModel()
    {
        Accounts.CollectionChanged += (_, _) => OnPropertyChanged(nameof(CountText));
    }
}

public partial class AccountsPageViewModel : ObservableObject
{
    private readonly Dictionary<string, bool> expandedState = new();
    private readonly List<AccountRowViewModel> rows = new();

    public ObservableCollection<GroupSectionViewModel> Sections { get; } = new();

    public List<AccountRowViewModel> SelectedRows { get; } = new();

    [ObservableProperty]
    private List<string> scopeFilters = new();

    public IReadOnlyList<string> StatusFilters { get; } =
        new[] { "Any status", "Valid", "Invalid", "Locked", "Limited", "Unchecked" };

    [ObservableProperty]
    private string scopeFilter = "All accounts";

    [ObservableProperty]
    private string statusFilter = "Any status";

    [ObservableProperty]
    private AccountRowViewModel? selectedAccount;

    [ObservableProperty]
    private string searchText = "";

    [ObservableProperty]
    private bool isLoading;

    [ObservableProperty]
    private string loadingText = "Loading accounts from storage...";

    [ObservableProperty]
    private string footerMessage = "Ready";

    public bool HasSelection => SelectedRows.Count > 0;

    public bool IsEmpty => !IsLoading && Sections.Sum(s => s.Accounts.Count) == 0;

    public string EmptyTitle => rows.Count == 0
        ? "No accounts yet."
        : "No accounts match the current filters.";

    public string EmptyHint => rows.Count == 0
        ? "Import a list or add one manually to get started."
        : "Clear the filters, or import a list to fill this view.";

    public string SummaryText => HasSelection
        ? $"{SelectedRows.Count} selected of {rows.Count}"
        : $"{rows.Count} accounts";

    public AccountsPageViewModel()
    {
        foreach (var model in AppServices.Accounts)
        {
            rows.Add(new AccountRowViewModel(model));
        }

        RefreshFilters();
        ApplyFilters();
    }

    public static string MaskToken(string token) =>
        token.Length <= 12 ? token : $"{token[..6]}…{token[^4..]}";

    public void RefreshView()
    {
        foreach (var row in rows)
        {
            row.Sync();
        }

        ApplyFilters();
    }

    private void RefreshFilters()
    {
        var scopes = new List<string> { "All accounts" };
        scopes.AddRange(AppServices.Groups.Groups.Select(g => $"Group: {g}"));
        scopes.AddRange(AppServices.Groups.Tags.Select(t => $"Tag: {t}"));
        ScopeFilters = scopes;
        if (!ScopeFilters.Contains(ScopeFilter))
        {
            ScopeFilter = "All accounts";
        }
    }

    partial void OnSelectedAccountChanged(AccountRowViewModel? value) =>
        OnPropertyChanged(nameof(HasSelection));

    partial void OnSearchTextChanged(string value) => ApplyFilters();

    partial void OnScopeFilterChanged(string value) => ApplyFilters();

    partial void OnStatusFilterChanged(string value) => ApplyFilters();

    public void SetSelection(IEnumerable<AccountRowViewModel> selected)
    {
        SelectedRows.Clear();
        SelectedRows.AddRange(selected);
        SelectedAccount = SelectedRows.FirstOrDefault();
        OnPropertyChanged(nameof(SummaryText));
        OnPropertyChanged(nameof(HasSelection));
    }

    public void ClearSelection()
    {
        SelectedRows.Clear();
        SelectedAccount = null;
        OnPropertyChanged(nameof(SummaryText));
        OnPropertyChanged(nameof(HasSelection));
    }

    [RelayCommand]
    private void ToggleSection(GroupSectionViewModel section)
    {
        section.IsExpanded = !section.IsExpanded;
        expandedState[section.Name] = section.IsExpanded;
    }

    [RelayCommand]
    private void ClearFilters()
    {
        SearchText = "";
        ScopeFilter = "All accounts";
        StatusFilter = "Any status";
    }

    [RelayCommand]
    private void OpenCard(AccountRowViewModel? row)
    {
        if (row is null)
        {
            return;
        }

        row.LoadHistory();
        var dialog = new AccountCardDialog(new AccountCardViewModel(row, this))
        {
            Owner = System.Windows.Application.Current?.MainWindow,
        };
        dialog.ShowDialog();
    }

    [RelayCommand]
    private void EditAccount(AccountRowViewModel? row)
    {
        if (row is null)
        {
            return;
        }

        row.LoadHistory();
        var dialog = new AccountCardDialog(new AccountCardViewModel(row, this, 1))
        {
            Owner = System.Windows.Application.Current?.MainWindow,
        };
        dialog.ShowDialog();
    }

    [RelayCommand]
    private Task CheckSelectedAsync() => RunJobAsync("check_status", SelectedRows.ToList());

    [RelayCommand]
    private Task CheckOneAsync(AccountRowViewModel row) => RunJobAsync("check_status", new[] { row }.ToList());

    public async Task RunJobAsync(string action, IReadOnlyList<AccountRowViewModel> targets)
    {
        if (targets.Count == 0)
        {
            FooterMessage = "Select accounts first";
            return;
        }

        var models = targets.Select(t => t.Model).ToList();
        var job = AppServices.Engine.CreateJob(
            $"{action} • {models.Count} acc", action, "selection", models);
        FooterMessage = $"Task {job.Name} running ({models.Count} accounts)...";

        await Task.Run(() =>
            AppServices.Engine.RunAsync(job, models, AppServices.Settings.Concurrency, CancellationToken.None));

        foreach (var row in targets)
        {
            row.Sync();
        }

        ApplyFilters();
        var ok = job.Runs.Count(r => r.Status == RunStatus.Success);
        var failed = job.Runs.Count(r => r.Status == RunStatus.Failed);
        FooterMessage = $"Task done: {ok} ok / {failed} failed / {job.Runs.Count - ok - failed} skipped";
    }

    [RelayCommand]
    private void DeleteSelected() => DeleteAccounts(SelectedRows.ToList());

    [RelayCommand]
    private void DeleteOne(AccountRowViewModel row) => DeleteAccounts(new[] { row });

    public bool DeleteAccounts(IReadOnlyList<AccountRowViewModel> targets)
    {
        if (targets.Count == 0)
        {
            FooterMessage = "Select accounts to delete";
            return false;
        }

        if (!ConfirmDialog.Ask("Delete accounts",
                $"Delete {targets.Count} account(s) from storage? Chrome profiles on disk are not touched.",
                "Delete"))
        {
            return false;
        }

        foreach (var row in targets)
        {
            rows.Remove(row);
            AppServices.Accounts.Remove(row.Model);
        }

        AppServices.PersistAccounts();
        ApplyFilters();
        FooterMessage = $"Deleted {targets.Count} accounts";
        return true;
    }

    [RelayCommand]
    private void SetProxy() => SetProxyFor(SelectedRows.ToList());

    [RelayCommand]
    private void SetProxyOne(AccountRowViewModel row) => SetProxyFor(new[] { row });

    public void SetProxyFor(IReadOnlyList<AccountRowViewModel> targets)
    {
        if (targets.Count == 0)
        {
            FooterMessage = "Select accounts first";
            return;
        }

        var dialog = new AddProxyDialog
        {
            Owner = System.Windows.Application.Current?.MainWindow,
        };
        if (dialog.ShowDialog() != true || dialog.ProxyUrl.Trim().Length == 0)
        {
            return;
        }

        try
        {
            Core.Net.ProxyEndpoint.Parse(dialog.ProxyUrl.Trim());
        }
        catch (FormatException exception)
        {
            FooterMessage = $"Bad proxy url: {exception.Message}";
            return;
        }

        foreach (var row in targets)
        {
            row.Model.ProxyMode = ProxyMode.Manual;
            row.Model.ProxyUrl = dialog.ProxyUrl.Trim();
            AppServices.Masker.Register(Core.Net.ProxyEndpoint.Parse(dialog.ProxyUrl.Trim()).Password);
        }

        AppServices.PersistAccounts();
        RefreshView();
        FooterMessage = $"Manual proxy set for {targets.Count} account(s)";
    }

    [RelayCommand]
    private void UseRandomOne(AccountRowViewModel row) => UseRandomPool(new[] { row });

    public void UseRandomPool(IReadOnlyList<AccountRowViewModel> targets)
    {
        if (targets.Count == 0)
        {
            FooterMessage = "Select accounts first";
            return;
        }

        foreach (var row in targets)
        {
            row.Model.ProxyMode = ProxyMode.Random;
            row.Model.ProxyUrl = null;
        }

        AppServices.PersistAccounts();
        RefreshView();
        FooterMessage = $"{targets.Count} account(s) switched to random pool proxy";
    }

    [RelayCommand]
    private void OpenAccount()
    {
        if (SelectedAccount is null)
        {
            FooterMessage = "Select an account to open";
            return;
        }

        OpenAccountFor(SelectedAccount);
    }

    [RelayCommand]
    private void OpenAccountOne(AccountRowViewModel row) => OpenAccountFor(row);

    private string? openSession;

    public void OpenAccountFor(AccountRowViewModel row)
    {
        if (openSession == row.Username)
        {
            openSession = null;
            FooterMessage = $"Chrome session for {row.Username} closed";
            return;
        }

        if (openSession is not null)
        {
            FooterMessage = $"Close the open session ({openSession}) first: one Chrome session at a time";
            return;
        }

        openSession = row.Username;
        FooterMessage = $"Chrome session opened for {row.Username} (real launch in step 19)";
    }

    [RelayCommand]
    private void Add()
    {
        var vm = new AddEditAccountViewModel(AppServices.Groups.Groups);
        if (string.IsNullOrEmpty(vm.Tags))
        {
            vm.Tags = $"import:{DateTime.Now:yyyy-MM-dd}";
        }

        var dialog = new AddEditAccountDialog(vm)
        {
            Owner = System.Windows.Application.Current?.MainWindow,
        };
        if (dialog.ShowDialog() != true)
        {
            return;
        }

        var model = new Account
        {
            Token = vm.Token.Trim(),
            Email = string.IsNullOrWhiteSpace(vm.Email) ? null : vm.Email.Trim(),
            Password = string.IsNullOrWhiteSpace(vm.Password) ? null : vm.Password,
            Group = vm.Group,
            Tags = ParseTags(vm.Tags),
            Notes = "",
        };
        ApplyProxyFromDialog(model, vm);
        model.Username = model.Email is null ? "acc_" + model.Token[..6] : model.Email.Split('@')[0];

        AddModel(model, vm.Tags);
        FooterMessage = $"Account {model.Username} added";
    }

    public void ApplyProxyFromDialog(Account model, AddEditAccountViewModel vm)
    {
        if (vm.ProxyMode == "Manual" && !string.IsNullOrWhiteSpace(vm.ProxyUrl))
        {
            model.ProxyMode = ProxyMode.Manual;
            model.ProxyUrl = vm.ProxyUrl.Trim();
        }
        else
        {
            model.ProxyMode = ProxyMode.Random;
            model.ProxyUrl = null;
        }
    }

    public void AddModel(Account model, string tagsRaw)
    {
        AppServices.Accounts.Add(model);
        rows.Add(new AccountRowViewModel(model));
        AppServices.Masker.Register(model.Token, model.Password);
        foreach (var tag in ParseTags(tagsRaw))
        {
            if (!AppServices.Groups.Tags.Contains(tag))
            {
                AppServices.Groups.Tags.Add(tag);
            }
        }

        if (!AppServices.Groups.Groups.Contains(model.Group))
        {
            AppServices.Groups.Groups.Add(model.Group);
        }

        AppServices.PersistAccounts();
        AppServices.PersistGroups();
        RefreshFilters();
        ApplyFilters();
    }

    public static List<string> ParseTags(string raw) =>
        raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct()
            .ToList();

    [RelayCommand]
    private void Import()
    {
        var vm = new ImportViewModel(AppServices.Groups.Groups,
            AppServices.Accounts.Select(a => a.Token));
        var dialog = new ImportDialog(vm)
        {
            Owner = System.Windows.Application.Current?.MainWindow,
        };
        if (dialog.ShowDialog() != true)
        {
            return;
        }

        var added = 0;
        var updated = 0;
        foreach (var parsed in vm.Applied)
        {
            var existing = AppServices.Accounts.FirstOrDefault(a => a.Token == parsed.Token);
            if (existing is not null)
            {
                if (parsed.Email is not null)
                {
                    existing.Email = parsed.Email;
                }

                if (parsed.Password is not null)
                {
                    existing.Password = parsed.Password;
                }

                if (vm.ProxyPolicy == "As in file (manual)" && parsed.Proxy is not null)
                {
                    existing.ProxyMode = ProxyMode.Manual;
                    existing.ProxyUrl = parsed.Proxy;
                }

                updated++;
                continue;
            }

            var model = new Account
            {
                Token = parsed.Token!,
                Email = parsed.Email,
                Password = parsed.Password,
                Group = vm.Group,
                Tags = ParseTags(vm.Tags),
            };
            model.Username = model.Email is null ? "acc_" + model.Token[..6] : model.Email.Split('@')[0];
            if (vm.ProxyPolicy == "As in file (manual)" && parsed.Proxy is not null)
            {
                model.ProxyMode = ProxyMode.Manual;
                model.ProxyUrl = parsed.Proxy;
            }

            AppServices.Accounts.Add(model);
            rows.Add(new AccountRowViewModel(model));
            added++;
        }

        foreach (var tag in ParseTags(vm.Tags))
        {
            if (!AppServices.Groups.Tags.Contains(tag))
            {
                AppServices.Groups.Tags.Add(tag);
            }
        }

        if (!AppServices.Groups.Groups.Contains(vm.Group))
        {
            AppServices.Groups.Groups.Add(vm.Group);
        }

        foreach (var model in AppServices.Accounts)
        {
            AppServices.Masker.Register(model.Token, model.Password);
        }

        AppServices.PersistAccounts();
        AppServices.PersistGroups();
        RefreshFilters();
        ApplyFilters();
        FooterMessage = $"Import applied: {added} added, {updated} updated";
    }

    [RelayCommand]
    private void ManageGroups()
    {
        var vm = new ManageGroupsViewModel(AppServices.Groups.Groups, AppServices.Groups.Tags);
        var dialog = new ManageGroupsDialog(vm)
        {
            Owner = System.Windows.Application.Current?.MainWindow,
        };
        dialog.ShowDialog();

        AppServices.Groups.Groups.Clear();
        AppServices.Groups.Groups.AddRange(vm.Groups);
        AppServices.Groups.Tags.Clear();
        AppServices.Groups.Tags.AddRange(vm.Tags);
        if (!AppServices.Groups.Groups.Contains("default"))
        {
            AppServices.Groups.Groups.Insert(0, "default");
        }

        AppServices.PersistGroups();
        RefreshFilters();
        ApplyFilters();
        FooterMessage = "Groups and tags updated";
    }

    [RelayCommand]
    private void Export()
    {
        var targets = SelectedRows.Count > 0 ? SelectedRows.ToList() : rows.ToList();
        if (targets.Count == 0)
        {
            FooterMessage = "Nothing to export";
            return;
        }

        var dialog = new SaveFileDialog
        {
            Filter = "Text files (*.txt)|*.txt|JSON files (*.json)|*.json",
            FileName = $"accounts-{DateTime.Now:yyyy-MM-dd}",
        };
        if (dialog.ShowDialog() != true)
        {
            return;
        }

        var builder = new StringBuilder();
        if (dialog.FileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
        {
            builder.AppendLine("[");
            for (var i = 0; i < targets.Count; i++)
            {
                var model = targets[i].Model;
                builder.AppendLine("  {");
                builder.AppendLine($"    \"username\": \"{model.Username}\",");
                builder.AppendLine($"    \"token\": \"{model.Token}\",");
                builder.AppendLine($"    \"proxy_mode\": \"{model.ProxyMode.ToString().ToLowerInvariant()}\",");
                builder.AppendLine($"    \"proxy\": \"{model.ProxyUrl ?? ""}\",");
                builder.AppendLine($"    \"status\": \"{model.Status.ToString().ToLowerInvariant()}\",");
                builder.AppendLine($"    \"group\": \"{model.Group}\",");
                builder.AppendLine($"    \"tags\": \"{string.Join(",", model.Tags)}\"");
                builder.AppendLine(i == targets.Count - 1 ? "  }" : "  },");
            }

            builder.AppendLine("]");
        }
        else
        {
            foreach (var row in targets)
            {
                var model = row.Model;
                var proxy = model.ProxyMode == ProxyMode.Manual ? model.ProxyUrl ?? "" : "";
                builder.AppendLine(proxy.Length > 0
                    ? $"{model.Email ?? ""}:{model.Password ?? ""}:{model.Token}:{proxy}"
                    : $"{model.Email ?? ""}:{model.Password ?? ""}:{model.Token}");
            }
        }

        File.WriteAllText(dialog.FileName, builder.ToString());
        FooterMessage = $"Exported {targets.Count} accounts to {Path.GetFileName(dialog.FileName)}";
    }

    [RelayCommand]
    private void CopyToken(AccountRowViewModel? row)
    {
        if (row is null)
        {
            return;
        }

        System.Windows.Clipboard.SetText(row.Model.Token);
        FooterMessage = "Token copied to clipboard";
    }

    private void ApplyFilters()
    {
        var query = rows.AsEnumerable();

        if (ScopeFilter.StartsWith("Group: ", StringComparison.Ordinal))
        {
            var group = ScopeFilter["Group: ".Length..];
            query = query.Where(a => a.Group == group);
        }
        else if (ScopeFilter.StartsWith("Tag: ", StringComparison.Ordinal))
        {
            var tag = ScopeFilter["Tag: ".Length..];
            query = query.Where(a => a.Model.Tags.Contains(tag, StringComparer.OrdinalIgnoreCase));
        }

        if (StatusFilter != "Any status")
        {
            query = query.Where(a => a.StatusText == StatusFilter);
        }

        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            var term = SearchText.Trim();
            query = query.Where(a =>
                a.Username.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                a.TokenMasked.Contains(term, StringComparison.OrdinalIgnoreCase));
        }

        var filtered = query.ToList();
        var order = AppServices.Groups.Groups.ToList();
        foreach (var group in filtered.Select(a => a.Group).Distinct())
        {
            if (!order.Contains(group))
            {
                order.Add(group);
            }
        }

        Sections.Clear();
        foreach (var groupName in order)
        {
            var groupRows = filtered.Where(a => a.Group == groupName).ToList();
            if (groupRows.Count == 0)
            {
                continue;
            }

            var section = new GroupSectionViewModel
            {
                Name = groupName,
                IsExpanded = expandedState.GetValueOrDefault(groupName, true),
            };
            foreach (var row in groupRows)
            {
                section.Accounts.Add(row);
            }

            Sections.Add(section);
        }

        ClearSelection();
        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(EmptyTitle));
        OnPropertyChanged(nameof(EmptyHint));
        OnPropertyChanged(nameof(SummaryText));
    }
}
