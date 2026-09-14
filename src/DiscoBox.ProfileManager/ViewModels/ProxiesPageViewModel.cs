using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DiscoBox.Core.Models;
using DiscoBox.Core.Net;
using DiscoBox.ProfileManager.Services;
using DiscoBox.ProfileManager.ViewModels;

namespace DiscoBox.ProfileManager.ViewModels;

public partial class ProxyRowViewModel : ObservableObject
{
    public ProxyLine Model { get; }

    [ObservableProperty]
    private string endpoint = "";

    [ObservableProperty]
    private string country = "";

    [ObservableProperty]
    private string status = "free";

    [ObservableProperty]
    private string statusText = "Free";

    [ObservableProperty]
    private string exitIp = "";

    [ObservableProperty]
    private string lastCheck = "never";

    public ProxyRowViewModel(ProxyLine model)
    {
        Model = model;
        Sync();
    }

    public void Sync()
    {
        Endpoint = MaskedUrl(Model.Url);
        Country = string.IsNullOrWhiteSpace(Model.Country) ? "manual" : Model.Country;
        Status = Model.Status switch
        {
            ProxyStatus.InUse => "bound",
            _ => Model.Status.ToString().ToLowerInvariant(),
        };
        StatusText = Model.Status switch
        {
            ProxyStatus.Valid => "Valid",
            ProxyStatus.InUse => "In use",
            _ => "Dead",
        };
        ExitIp = Model.ExitIp ?? "";
        LastCheck = TimeAgo.Text(Model.LastCheck);
    }

    private static string MaskedUrl(string url)
    {
        try
        {
            return ProxyEndpoint.Parse(url).MaskedUrl;
        }
        catch (FormatException)
        {
            return url;
        }
    }
}

public partial class ProxySectionViewModel : ObservableObject
{
    [ObservableProperty]
    private string name = "";

    [ObservableProperty]
    private bool isExpanded = true;

    public ObservableCollection<ProxyRowViewModel> Proxies { get; } = new();

    public string CountText => Proxies.Count.ToString();

    public ProxySectionViewModel()
    {
        Proxies.CollectionChanged += (_, _) => OnPropertyChanged(nameof(CountText));
    }
}

public partial class ProxiesPageViewModel : ObservableObject
{
    private readonly List<ProxyRowViewModel> rows = new();

    public ObservableCollection<ProxySectionViewModel> Sections { get; } = new();

    public List<ProxyRowViewModel> SelectedRows { get; } = new();

    public IReadOnlyList<string> StatusFilters { get; } =
        new[] { "Any status", "Valid", "In use", "Dead" };

    [ObservableProperty]
    private string statusFilter = "Any status";

    [ObservableProperty]
    private string searchText = "";

    [ObservableProperty]
    private bool isLoading;

    [ObservableProperty]
    private string loadingText = "Checking proxies via echo services...";

    [ObservableProperty]
    private string footerMessage = "Ready";

    public bool HasSelection => SelectedRows.Count > 0;

    public bool IsEmpty => !IsLoading && Sections.Sum(s => s.Proxies.Count) == 0;

    public string SummaryText => HasSelection
        ? $"{SelectedRows.Count} selected of {rows.Count}"
        : $"{rows.Count} lines • {Count(ProxyStatus.Valid)} valid • {Count(ProxyStatus.Dead)} dead";

    public ProxiesPageViewModel()
    {
        foreach (var line in AppServices.Pool.Snapshot())
        {
            rows.Add(new ProxyRowViewModel(line));
        }

        ApplyFilters();
    }

    private int Count(ProxyStatus status) => rows.Count(r => r.Model.Status == status);

    partial void OnStatusFilterChanged(string value) => ApplyFilters();

    partial void OnSearchTextChanged(string value) => ApplyFilters();

    public void SetSelection(IEnumerable<ProxyRowViewModel> selected)
    {
        SelectedRows.Clear();
        SelectedRows.AddRange(selected);
        OnPropertyChanged(nameof(SummaryText));
        OnPropertyChanged(nameof(HasSelection));
    }

    [RelayCommand]
    private void ToggleSection(ProxySectionViewModel section) =>
        section.IsExpanded = !section.IsExpanded;

    [RelayCommand]
    private void AddProxy()
    {
        var dialog = new Views.Dialogs.AddProxyDialog
        {
            Owner = System.Windows.Application.Current?.MainWindow,
        };
        if (dialog.ShowDialog() != true || dialog.ProxyUrls.Count == 0)
        {
            return;
        }

        var added = 0;
        var bad = 0;
        foreach (var url in dialog.ProxyUrls)
        {
            try
            {
                var line = AppServices.Pool.Add(url);
                AppServices.Masker.Register(ProxyEndpoint.Parse(line.Url).Password);
                added++;
            }
            catch (FormatException)
            {
                bad++;
            }
        }

        rows.Clear();
        foreach (var line in AppServices.Pool.Snapshot())
        {
            rows.Add(new ProxyRowViewModel(line));
        }

        ApplyFilters();
        FooterMessage = bad > 0
            ? $"Added {added} proxies, {bad} invalid lines skipped"
            : $"Added {added} proxies";
    }

    [RelayCommand]
    private async Task CheckSelectedAsync()
    {
        if (SelectedRows.Count == 0)
        {
            FooterMessage = "Select proxies to check";
            return;
        }

        var targets = SelectedRows.ToList();
        IsLoading = true;
        FooterMessage = $"Checking {targets.Count} proxies via echo services...";

        foreach (var row in targets)
        {
            ProxyEndpoint endpoint;
            try
            {
                endpoint = ProxyEndpoint.Parse(row.Model.Url);
            }
            catch (FormatException)
            {
                row.Model.Status = ProxyStatus.Dead;
                row.Sync();
                continue;
            }

            var check = await EchoChecker.CheckAsync(endpoint);
            row.Model.LastCheck = DateTime.UtcNow;
            if (check.Ok)
            {
                row.Model.ExitIp = check.ExitIp;
                if (row.Model.Status == ProxyStatus.Dead)
                {
                    row.Model.Status = ProxyStatus.Valid;
                }
            }
            else if (!check.Transient)
            {
                row.Model.Status = ProxyStatus.Dead;
            }

            row.Sync();
        }

        AppServices.Pool.Persist();
        IsLoading = false;
        ApplyFilters();
        FooterMessage = $"Checked {targets.Count} proxies";
    }

    [RelayCommand]
    private void DeleteSelected()
    {
        if (SelectedRows.Count == 0)
        {
            FooterMessage = "Select proxies to delete";
            return;
        }

        if (!Views.Dialogs.ConfirmDialog.Ask("Delete proxies",
                $"Delete {SelectedRows.Count} proxy line(s) from the pool?", "Delete"))
        {
            return;
        }

        var count = SelectedRows.Count;
        foreach (var row in SelectedRows.ToList())
        {
            AppServices.Pool.Remove(row.Model.Id);
            rows.Remove(row);
        }

        SelectedRows.Clear();
        ApplyFilters();
        FooterMessage = $"Deleted {count} proxies";
    }

    private void ApplyFilters()
    {
        var query = rows.AsEnumerable();
        if (StatusFilter != "Any status")
        {
            query = query.Where(p => p.StatusText == StatusFilter);
        }

        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            var term = SearchText.Trim();
            query = query.Where(p =>
                p.Endpoint.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                p.Country.Contains(term, StringComparison.OrdinalIgnoreCase));
        }

        var filtered = query.ToList();
        var order = filtered.Select(p => p.Country).Distinct().ToList();

        Sections.Clear();
        foreach (var country in order)
        {
            var section = new ProxySectionViewModel { Name = country };
            foreach (var row in filtered.Where(p => p.Country == country))
            {
                section.Proxies.Add(row);
            }

            Sections.Add(section);
        }

        SetSelection(SelectedRows.Where(filtered.Contains).ToList());
        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(SummaryText));
    }
}
