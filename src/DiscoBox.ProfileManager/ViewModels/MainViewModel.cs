using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace DiscoBox.ProfileManager.ViewModels;

public partial class MainViewModel : ObservableObject
{
    public AccountsPageViewModel Accounts { get; } = new();
    public ProxiesPageViewModel Proxies { get; } = new();
    public TasksPageViewModel Tasks { get; } = new();
    public SettingsPageViewModel Settings { get; } = new();

    [ObservableProperty]
    private object currentPage;

    public MainViewModel(string? startPage = null)
    {
        currentPage = startPage switch
        {
            "proxies" => Proxies,
            "tasks" => Tasks,
            "settings" => Settings,
            _ => Accounts,
        };
    }

    [RelayCommand]
    private void Navigate(string page) => CurrentPage = page switch
    {
        "proxies" => Proxies,
        "tasks" => Tasks,
        "settings" => Settings,
        _ => Accounts,
    };
}
