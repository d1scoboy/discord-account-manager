using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace DiscoBox.ProfileManager.ViewModels;

public partial class AddEditAccountViewModel : ObservableObject
{
    private readonly AccountRowViewModel? existing;

    [ObservableProperty]
    private string title = "Add account";

    [ObservableProperty]
    private string token = "";

    [ObservableProperty]
    private string email = "";

    [ObservableProperty]
    private string password = "";

    [ObservableProperty]
    private string group = "default";

    [ObservableProperty]
    private string tags = "";

    [ObservableProperty]
    private string proxyMode = "Random from pool";

    [ObservableProperty]
    private string proxyUrl = "";

    [ObservableProperty]
    private string error = "";

    public ObservableCollection<string> Groups { get; }

    public IReadOnlyList<string> ProxyModes { get; } =
        new[] { "Random from pool", "Pinned", "Manual" };

    public bool IsManualProxy => ProxyMode == "Manual";

    public AddEditAccountViewModel(IEnumerable<string> groups, AccountRowViewModel? edit = null)
    {
        Groups = new ObservableCollection<string>(groups);
        existing = edit;
        if (edit is not null)
        {
            Title = "Edit account";
            Token = edit.TokenMasked;
            Email = "";
            Group = edit.Group;
            Tags = edit.Tags;
            ProxyUrl = edit.ProxyLabel;
        }
    }

    partial void OnProxyModeChanged(string value) => OnPropertyChanged(nameof(IsManualProxy));

    public bool Validate()
    {
        if (Token.Trim().Length < 20)
        {
            Error = "Token is required (at least 20 characters)";
            return false;
        }

        if (IsManualProxy && string.IsNullOrWhiteSpace(ProxyUrl))
        {
            Error = "Proxy URL is required in manual mode";
            return false;
        }

        Error = "";
        return true;
    }

    public void ApplyTo(AccountRowViewModel row)
    {
        row.TokenMasked = AccountsPageViewModel.MaskToken(Token.Trim());
        row.Group = Group;
        row.Tags = Tags;
        row.ProxyLabel = ProxyMode switch
        {
            "Manual" => ProxyUrl.Trim(),
            "Pinned" => "US • pool-us:10000",
            _ => "no proxy",
        };

        if (existing is null)
        {
            row.Username = string.IsNullOrEmpty(Email)
                ? "acc_" + Token.Trim()[..6]
                : Email.Split('@')[0];
            row.Status = "unchecked";
            row.StatusText = "Unchecked";
        }
    }
}
