using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DiscoBox.ProfileManager.Services;
using Microsoft.Win32;

namespace DiscoBox.ProfileManager.ViewModels;

public partial class SettingsPageViewModel : ObservableObject
{
    [ObservableProperty]
    private string dataDir;

    [ObservableProperty]
    private string chromePath;

    [ObservableProperty]
    private string gostPath;

    [ObservableProperty]
    private string concurrency;

    [ObservableProperty]
    private string logLevel;

    [ObservableProperty]
    private string footerMessage = "Changes apply after Save; data dir change applies on restart";

    public IReadOnlyList<string> LogLevels { get; } =
        new[] { "Debug", "Info", "Warning", "Error" };

    public SettingsPageViewModel()
    {
        var settings = AppServices.Settings;
        dataDir = AppServices.DataDir;
        chromePath = settings.ChromePath;
        gostPath = settings.GostPath;
        concurrency = settings.Concurrency.ToString();
        logLevel = settings.LogLevel;
    }

    [RelayCommand]
    private void Save()
    {
        var settings = AppServices.Settings;
        settings.DataDir = DataDir.Trim();
        settings.ChromePath = ChromePath.Trim();
        settings.GostPath = GostPath.Trim();
        settings.Concurrency = int.TryParse(Concurrency, out var workers) && workers > 0 ? workers : 3;
        settings.LogLevel = LogLevel;
        AppServices.PersistSettings();
        AppServices.Logger.SetLevel(Enum.TryParse<Core.Logging.LogLevel>(LogLevel, true, out var level)
            ? level
            : Core.Logging.LogLevel.Info);
        FooterMessage = "Settings saved";
    }

    [RelayCommand]
    private void BrowseDataDir()
    {
        var dialog = new OpenFolderDialog { InitialDirectory = DataDir };
        if (dialog.ShowDialog() == true)
        {
            DataDir = dialog.FolderName;
        }
    }

    [RelayCommand]
    private void BrowseChrome()
    {
        var dialog = new OpenFileDialog
        {
            Filter = "Chrome executable (chrome.exe)|chrome.exe",
        };
        if (dialog.ShowDialog() == true)
        {
            ChromePath = dialog.FileName;
        }
    }

    [RelayCommand]
    private void DetectChrome()
    {
        var candidates = new[]
        {
            @"C:\Program Files\Google\Chrome\Application\chrome.exe",
            @"C:\Program Files (x86)\Google\Chrome\Application\chrome.exe",
            System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                @"Google\Chrome\Application\chrome.exe"),
        };
        var found = candidates.FirstOrDefault(System.IO.File.Exists);
        ChromePath = found ?? "";
        FooterMessage = found is null
            ? "Chrome not found in default locations, pick the exe manually"
            : "Chrome detected";
    }

    [RelayCommand]
    private void BrowseGost()
    {
        var dialog = new OpenFileDialog
        {
            Filter = "gost executable (gost.exe)|gost.exe",
        };
        if (dialog.ShowDialog() == true)
        {
            GostPath = dialog.FileName;
        }
    }
}
