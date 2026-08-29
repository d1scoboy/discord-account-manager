using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace DiscoBox.ProfileManager.ViewModels;

public partial class AccountCardViewModel : ObservableObject
{
    private readonly AccountsPageViewModel page;

    public AccountRowViewModel Row { get; }

    public ObservableCollection<string> Groups => page.Groups;

    public ObservableCollection<HistoryItemViewModel> Log => Row.History;

    public IReadOnlyList<string> ProxyModes { get; } = new[] { "Random from pool", "Manual" };

    [ObservableProperty]
    private int selectedTab;

    [ObservableProperty]
    private string token;

    [ObservableProperty]
    private string email = "";

    [ObservableProperty]
    private string password = "";

    [ObservableProperty]
    private string group;

    [ObservableProperty]
    private string tags;

    [ObservableProperty]
    private string notes;

    [ObservableProperty]
    private string proxyMode;

    [ObservableProperty]
    private string proxyUrl = "";

    public bool IsManual => ProxyMode == "Manual";

    public Action? Close { get; set; }

    public AccountCardViewModel(AccountRowViewModel row, AccountsPageViewModel page, int startTab = 0)
    {
        Row = row;
        this.page = page;
        token = row.TokenMasked;
        group = row.Group;
        tags = row.Tags;
        notes = row.Notes;
        proxyMode = row.ProxyLabel == "no proxy" ? "Random from pool" : "Manual";
        proxyUrl = row.ProxyLabel == "no proxy" ? "" : row.ProxyLabel;
        selectedTab = startTab;
    }

    partial void OnProxyModeChanged(string value) => OnPropertyChanged(nameof(IsManual));

    [RelayCommand]
    private Task CheckAsync() => page.CheckAccountsAsync(new[] { Row });

    [RelayCommand]
    private void AssignRandom() => page.AssignRandomProxy(new[] { Row });

    [RelayCommand]
    private void SetProxy() => page.SetProxyFor(new[] { Row });

    [RelayCommand]
    private void ReleaseProxy() => page.ReleaseProxy(new[] { Row });

    [RelayCommand]
    private void Delete()
    {
        if (page.DeleteAccounts(new[] { Row }))
        {
            Close?.Invoke();
        }
    }

    [RelayCommand]
    private void Save()
    {
        Row.Group = Group;
        Row.Tags = Tags;
        Row.Notes = Notes;
        if (Token.Trim().Length >= 20)
        {
            Row.TokenMasked = AccountsPageViewModel.MaskToken(Token.Trim());
        }

        if (IsManual && ProxyUrl.Trim().Length > 0)
        {
            Row.ProxyLabel = ProxyUrl.Trim();
        }

        page.FooterMessage = $"Account {Row.Username} updated";
        page.RefreshView();
        Close?.Invoke();
    }
}
