using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DiscoBox.Core.Models;
using DiscoBox.ProfileManager.Services;

namespace DiscoBox.ProfileManager.ViewModels;

public partial class AccountCardViewModel : ObservableObject
{
    private readonly AccountsPageViewModel page;

    public AccountRowViewModel Row { get; }

    public Account Model => Row.Model;

    public List<string> Groups => AppServices.Groups.Groups;

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
        token = row.Model.Token;
        email = row.Model.Email ?? "";
        password = row.Model.Password ?? "";
        group = row.Model.Group;
        tags = string.Join(", ", row.Model.Tags);
        notes = row.Model.Notes;
        proxyMode = row.Model.ProxyMode == Core.Models.ProxyMode.Manual ? "Manual" : "Random from pool";
        proxyUrl = row.Model.ProxyUrl ?? "";
        selectedTab = startTab;
    }

    partial void OnProxyModeChanged(string value) => OnPropertyChanged(nameof(IsManual));

    [RelayCommand]
    private Task CheckAsync() => page.RunJobAsync("check_status", new[] { Row });

    [RelayCommand]
    private Task OpenInChromeAsync() => page.OpenAccountFor(Row);

    [RelayCommand]
    private void SetProxy() => page.SetProxyFor(new[] { Row });

    [RelayCommand]
    private void UseRandom() => page.UseRandomPool(new[] { Row });

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
        if (Token.Trim().Length >= 20)
        {
            Model.Token = Token.Trim();
        }

        Model.Email = string.IsNullOrWhiteSpace(Email) ? null : Email.Trim();
        Model.Password = string.IsNullOrWhiteSpace(Password) ? null : Password;
        Model.Group = Group;
        Model.Tags = AccountsPageViewModel.ParseTags(Tags);
        Model.Notes = Notes;

        if (IsManual && ProxyUrl.Trim().Length > 0)
        {
            Model.ProxyMode = Core.Models.ProxyMode.Manual;
            Model.ProxyUrl = ProxyUrl.Trim();
        }
        else
        {
            Model.ProxyMode = Core.Models.ProxyMode.Random;
            Model.ProxyUrl = null;
        }

        AppServices.Masker.Register(Model.Token, Model.Password);
        AppServices.PersistAccounts();
        Row.Sync();
        page.RefreshView();
        page.FooterMessage = $"Account {Model.Username} updated";
        Close?.Invoke();
    }
}
