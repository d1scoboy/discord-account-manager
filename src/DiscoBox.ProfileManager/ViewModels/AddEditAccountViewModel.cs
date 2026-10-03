using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace DiscoBox.ProfileManager.ViewModels;

public partial class AddEditAccountViewModel : ObservableObject
{
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
        new[] { "Random from pool", "Manual" };

    public bool IsManualProxy => ProxyMode == "Manual";

    public AddEditAccountViewModel(IEnumerable<string> groups)
    {
        Groups = new ObservableCollection<string>(groups);
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
}
