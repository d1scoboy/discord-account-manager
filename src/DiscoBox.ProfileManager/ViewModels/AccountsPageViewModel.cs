using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DiscoBox.ProfileManager.Views.Dialogs;
using Microsoft.Win32;

namespace DiscoBox.ProfileManager.ViewModels;

public partial class AccountRowViewModel : ObservableObject
{
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

    public string Initials => string.IsNullOrEmpty(Username)
        ? "?"
        : Username[..1].ToUpperInvariant();

    public ObservableCollection<HistoryItemViewModel> History { get; } = new();

    partial void OnUsernameChanged(string value) => OnPropertyChanged(nameof(Initials));
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
    private readonly List<AccountRowViewModel> allAccounts = new();
    private readonly Dictionary<string, bool> expandedState = new();

    public ObservableCollection<GroupSectionViewModel> Sections { get; } = new();

    public List<AccountRowViewModel> SelectedRows { get; } = new();

    public ObservableCollection<string> Groups { get; } = new() { "default", "farm", "premium" };

    public ObservableCollection<string> Tags { get; } = new() { "import:2026-10-27", "batch-1" };

    [ObservableProperty]
    private List<string> scopeFilters = new();

    public IReadOnlyList<string> StatusFilters { get; } =
        new[] { "Any status", "Valid", "Invalid", "Locked", "Unchecked" };

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

    public string SelectionLabel => HasSelection
        ? $"({SelectedRows.Count})"
        : "";

    public bool IsEmpty => !IsLoading && Sections.Sum(s => s.Accounts.Count) == 0;

    public string SummaryText => HasSelection
        ? $"{SelectedRows.Count} selected of {allAccounts.Count}"
        : $"{allAccounts.Count} accounts";

    public AccountsPageViewModel()
    {
        Seed();
        RefreshFilters();
        ApplyFilters();
    }

    public static string MaskToken(string token) =>
        token.Length <= 12 ? token : $"{token[..6]}…{token[^4..]}";

    public void RefreshView() => ApplyFilters();

    private void RefreshFilters()
    {
        var scopes = new List<string> { "All accounts" };
        scopes.AddRange(Groups.Select(g => $"Group: {g}"));
        scopes.AddRange(Tags.Select(t => $"Tag: {t}"));
        ScopeFilters = scopes;
        if (!ScopeFilters.Contains(ScopeFilter))
        {
            ScopeFilter = "All accounts";
        }
    }

    partial void OnSelectedAccountChanged(AccountRowViewModel? value)
    {
        OnPropertyChanged(nameof(HasSelection));
        OnPropertyChanged(nameof(SelectionLabel));
    }

    partial void OnSearchTextChanged(string value) => ApplyFilters();

    partial void OnScopeFilterChanged(string value) => ApplyFilters();

    partial void OnStatusFilterChanged(string value) => ApplyFilters();

    public void SetSelection(IEnumerable<AccountRowViewModel> rows)
    {
        SelectedRows.Clear();
        SelectedRows.AddRange(rows);
        SelectedAccount = SelectedRows.FirstOrDefault();
        OnPropertyChanged(nameof(SummaryText));
        OnPropertyChanged(nameof(HasSelection));
        OnPropertyChanged(nameof(SelectionLabel));
    }

    public void ClearSelection()
    {
        SelectedRows.Clear();
        SelectedAccount = null;
        OnPropertyChanged(nameof(SummaryText));
        OnPropertyChanged(nameof(HasSelection));
        OnPropertyChanged(nameof(SelectionLabel));
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

        var dialog = new AccountCardDialog(new AccountCardViewModel(row, this, 2))
        {
            Owner = System.Windows.Application.Current?.MainWindow,
        };
        dialog.ShowDialog();
    }

    [RelayCommand]
    private Task CheckSelectedAsync() => CheckAccountsAsync(SelectedRows.ToList());

    public async Task CheckAccountsAsync(IReadOnlyList<AccountRowViewModel> targets)
    {
        if (targets.Count == 0)
        {
            FooterMessage = "Select accounts to check";
            return;
        }

        FooterMessage = $"Checking {targets.Count} accounts against Discord API...";
        foreach (var row in targets)
        {
            row.Status = "running";
            row.StatusText = "Running";
        }

        await Task.Delay(800);
        foreach (var row in targets)
        {
            var ok = row.Status != "invalid" && row.Status != "locked";
            row.Status = ok ? "valid" : row.Status;
            row.StatusText = ok ? "Valid" : row.StatusText;
            row.LastChecked = "just now";
            row.History.Insert(0, new HistoryItemViewModel
            {
                Name = "check_status",
                Result = ok ? "success" : "failed",
                ResultText = ok ? "Success" : "Failed",
                Time = "just now",
            });
        }

        FooterMessage = $"Checked {targets.Count} accounts";
    }

    [RelayCommand]
    private void DeleteSelected() => DeleteAccounts(SelectedRows.ToList());

    public bool DeleteAccounts(IReadOnlyList<AccountRowViewModel> targets)
    {
        if (targets.Count == 0)
        {
            FooterMessage = "Select accounts to delete";
            return false;
        }

        if (!ConfirmDialog.Ask("Delete accounts",
                $"Delete {targets.Count} account(s)? This removes them from storage and their Chrome profiles stay orphaned.",
                "Delete"))
        {
            return false;
        }

        foreach (var row in targets)
        {
            allAccounts.Remove(row);
            ReleaseProxyFor(row);
        }

        ApplyFilters();
        FooterMessage = $"Deleted {targets.Count} accounts";
        return true;
    }

    [RelayCommand]
    private void AssignRandom() => AssignRandomProxy(SelectedRows.ToList());

    public void AssignRandomProxy(IReadOnlyList<AccountRowViewModel> targets)
    {
        if (targets.Count == 0)
        {
            FooterMessage = "Select accounts first";
            return;
        }

        var assigned = 0;
        foreach (var row in targets)
        {
            ReleaseProxyFor(row);
            var line = MockPool.TakeFree();
            if (line is null)
            {
                FooterMessage = "No free proxies left in pool";
                return;
            }

            row.ProxyLabel = line.Endpoint;
            assigned++;
        }

        FooterMessage = $"Random pool proxy assigned to {assigned} account(s)";
    }

    [RelayCommand]
    private void SetProxy() => SetProxyFor(SelectedRows.ToList());

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

        foreach (var row in targets)
        {
            ReleaseProxyFor(row);
            row.ProxyLabel = dialog.ProxyUrl.Trim();
        }

        FooterMessage = $"Proxy set for {targets.Count} account(s)";
    }

    [RelayCommand]
    private void ReleaseProxy() => ReleaseProxy(SelectedRows.ToList());

    public void ReleaseProxy(IReadOnlyList<AccountRowViewModel> targets)
    {
        if (targets.Count == 0)
        {
            FooterMessage = "Select accounts first";
            return;
        }

        if (!ConfirmDialog.Ask("Release proxies",
                $"Release bound proxies for {targets.Count} account(s)? Accounts switch to 'no proxy'.",
                "Release"))
        {
            return;
        }

        foreach (var row in targets)
        {
            ReleaseProxyFor(row);
        }

        FooterMessage = $"Released proxies for {targets.Count} account(s)";
    }

    private static void ReleaseProxyFor(AccountRowViewModel row)
    {
        var line = MockPool.FindByEndpoint(row.ProxyLabel);
        MockPool.Release(line);
        row.ProxyLabel = "no proxy";
    }

    [RelayCommand]
    private void OpenAccount()
    {
        if (SelectedAccount is null)
        {
            FooterMessage = "Select an account to open";
            return;
        }

        FooterMessage = $"Opening Chrome profile for {SelectedAccount.Username} (implemented in step 19)";
    }

    [RelayCommand]
    private void Add()
    {
        var vm = new AddEditAccountViewModel(Groups);
        if (string.IsNullOrEmpty(vm.Tags))
        {
            vm.Tags = $"import:{DateTime.Now:yyyy-MM-dd}";
        }

        var dialog = new AddEditAccountDialog(vm) { Owner = System.Windows.Application.Current?.MainWindow };
        if (dialog.ShowDialog() != true)
        {
            return;
        }

        var row = new AccountRowViewModel { UserId = "pending", LastChecked = "never" };
        vm.ApplyTo(row);
        allAccounts.Add(row);
        ApplyFilters();
        FooterMessage = $"Account {row.Username} added";
    }

    [RelayCommand]
    private void Import()
    {
        var vm = new ImportViewModel(Groups, allAccounts.Select(a => a.TokenMasked));
        var dialog = new ImportDialog(vm) { Owner = System.Windows.Application.Current?.MainWindow };
        if (dialog.ShowDialog() != true)
        {
            return;
        }

        var touched = 0;
        foreach (var parsed in vm.Applied)
        {
            var masked = MaskToken(parsed.Token);
            var existing = allAccounts.FirstOrDefault(a => a.TokenMasked == masked);
            var row = existing ?? new AccountRowViewModel
            {
                UserId = "pending",
                Status = "unchecked",
                StatusText = "Unchecked",
                LastChecked = "never",
            };

            row.TokenMasked = masked;
            row.Username = parsed.Email is null
                ? "acc_" + parsed.Token[..6]
                : parsed.Email.Split('@')[0];
            row.Group = vm.Group;
            row.Tags = vm.Tags;
            row.ProxyLabel = vm.ProxyPolicy switch
            {
                "As in file" => parsed.Proxy ?? "no proxy",
                "None" => "no proxy",
                _ => parsed.Proxy ?? MockPool.TakeFree()?.Endpoint ?? "no proxy",
            };

            if (existing is null)
            {
                allAccounts.Add(row);
            }

            touched++;
        }

        ApplyFilters();
        FooterMessage = $"Import applied: {touched} accounts";
    }

    [RelayCommand]
    private void ManageGroups()
    {
        var vm = new ManageGroupsViewModel(Groups, Tags);
        var dialog = new ManageGroupsDialog(vm) { Owner = System.Windows.Application.Current?.MainWindow };
        dialog.ShowDialog();

        Groups.Clear();
        foreach (var group in vm.Groups)
        {
            Groups.Add(group);
        }

        Tags.Clear();
        foreach (var tag in vm.Tags)
        {
            Tags.Add(tag);
        }

        RefreshFilters();
        ApplyFilters();
        FooterMessage = "Groups and tags updated";
    }

    [RelayCommand]
    private void Export()
    {
        var rows = SelectedRows.Count > 0 ? SelectedRows.ToList() : allAccounts.ToList();
        if (rows.Count == 0)
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
            for (var i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                builder.AppendLine("  {");
                builder.AppendLine($"    \"username\": \"{row.Username}\",");
                builder.AppendLine($"    \"token\": \"{row.TokenMasked}\",");
                builder.AppendLine($"    \"proxy\": \"{row.ProxyLabel}\",");
                builder.AppendLine($"    \"status\": \"{row.Status}\",");
                builder.AppendLine($"    \"group\": \"{row.Group}\",");
                builder.AppendLine($"    \"tags\": \"{row.Tags}\"");
                builder.AppendLine(i == rows.Count - 1 ? "  }" : "  },");
            }

            builder.AppendLine("]");
        }
        else
        {
            foreach (var row in rows)
            {
                builder.AppendLine(
                    $"{row.Username}\t{row.TokenMasked}\t{row.ProxyLabel}\t{row.Status}\t{row.Group}\t{row.Tags}");
            }
        }

        File.WriteAllText(dialog.FileName, builder.ToString());
        FooterMessage = $"Exported {rows.Count} accounts to {Path.GetFileName(dialog.FileName)}";
    }

    [RelayCommand]
    private void CopyToken(AccountRowViewModel? row)
    {
        if (row is null)
        {
            return;
        }

        System.Windows.Clipboard.SetText(row.TokenMasked);
        FooterMessage = "Token copied to clipboard";
    }

    [RelayCommand]
    private Task CheckOneAsync(AccountRowViewModel row) => CheckAccountsAsync(new[] { row });

    [RelayCommand]
    private void AssignRandomOne(AccountRowViewModel row) => AssignRandomProxy(new[] { row });

    [RelayCommand]
    private void SetProxyOne(AccountRowViewModel row) => SetProxyFor(new[] { row });

    [RelayCommand]
    private void ReleaseOne(AccountRowViewModel row) => ReleaseProxy(new[] { row });

    [RelayCommand]
    private void DeleteOne(AccountRowViewModel row) => DeleteAccounts(new[] { row });

    private void ApplyFilters()
    {
        var query = allAccounts.AsEnumerable();

        if (ScopeFilter.StartsWith("Group: ", StringComparison.Ordinal))
        {
            var group = ScopeFilter["Group: ".Length..];
            query = query.Where(a => a.Group == group);
        }
        else if (ScopeFilter.StartsWith("Tag: ", StringComparison.Ordinal))
        {
            var tag = ScopeFilter["Tag: ".Length..];
            query = query.Where(a => a.Tags.Contains(tag, StringComparison.OrdinalIgnoreCase));
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
        var order = Groups.ToList();
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
            var rows = filtered.Where(a => a.Group == groupName).ToList();
            if (rows.Count == 0)
            {
                continue;
            }

            var section = new GroupSectionViewModel
            {
                Name = groupName,
                IsExpanded = expandedState.GetValueOrDefault(groupName, true),
            };
            foreach (var row in rows)
            {
                section.Accounts.Add(row);
            }

            Sections.Add(section);
        }

        ClearSelection();
        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(SummaryText));
    }

    private void Seed()
    {
        allAccounts.AddRange(new[]
        {
            Row("nightowl_88", "198612043311778816", "MTk4Nj…ZmJh", "US • pool-us:10023", "valid", "Valid", "2h ago", "default", "import:2026-10-27"),
            Row("k.marlow", "204118776554332672", "MjA0M…RkYw", "GB • pool-gb:10107", "valid", "Valid", "2h ago", "default", "import:2026-10-27"),
            Row("pixiedust", "211903445221900288", "MjExO…MjA4", "DE • pool-de:10311", "locked", "Locked", "1d ago", "default", "import:2026-10-27, batch-1"),
            Row("ravenclaw22", "219887120334556160", "MjE5O…xNjAw", "US • pool-us:10044", "invalid", "Invalid", "3h ago", "default", "import:2026-10-27"),
            Row("moonpie_x", "226554908112334848", "MjI2N…4ODQ4", "no proxy", "unchecked", "Unchecked", "never", "default", "import:2026-10-27"),
            Row("silentbob", "233221667890113536", "MjMzM…zNTM2", "FR • pool-fr:10219", "valid", "Valid", "5h ago", "premium", "batch-1"),
            Row("quartz77", "240998445667892224", "MjQwO…yMjI0", "NL • pool-nl:10402", "limited", "Limited", "20m ago", "farm", "batch-1"),
            Row("velvet_fox", "247665223445670912", "MjQ3N…wOTEy", "US • pool-us:10071", "valid", "Valid", "1h ago", "farm", "import:2026-10-27, batch-1"),
        });

        foreach (var row in allAccounts)
        {
            row.History.Add(new HistoryItemViewModel
            {
                Name = "check_status",
                Result = row.Status is "valid" or "unchecked" ? "success" : "failed",
                ResultText = row.Status is "valid" or "unchecked" ? "Success" : "Failed",
                Time = row.LastChecked,
            });
        }
    }

    private static AccountRowViewModel Row(string username, string userId, string token, string proxy,
        string status, string statusText, string lastChecked, string group, string tags) => new()
        {
            Username = username,
            UserId = userId,
            TokenMasked = token,
            ProxyLabel = proxy,
            Status = status,
            StatusText = statusText,
            LastChecked = lastChecked,
            Group = group,
            Tags = tags,
        };
}
