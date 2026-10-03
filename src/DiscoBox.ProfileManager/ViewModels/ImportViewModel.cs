using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DiscoBox.Core.Parsing;
using Microsoft.Win32;

namespace DiscoBox.ProfileManager.ViewModels;

public partial class ImportLineViewModel : ObservableObject
{
    [ObservableProperty]
    private int lineNumber;

    [ObservableProperty]
    private string action = "new";

    [ObservableProperty]
    private string actionText = "New";

    [ObservableProperty]
    private string raw = "";

    [ObservableProperty]
    private string detail = "";
}

public partial class ImportViewModel : ObservableObject
{
    [ObservableProperty]
    private string sourceText = "";

    [ObservableProperty]
    private string format = "Auto-detect";

    [ObservableProperty]
    private string group = "default";

    [ObservableProperty]
    private string tags = "";

    [ObservableProperty]
    private string proxyPolicy = "Random from pool (default)";

    [ObservableProperty]
    private bool hasReport;

    [ObservableProperty]
    private string summary = "";

    public ObservableCollection<string> Groups { get; }

    public ObservableCollection<ImportLineViewModel> Report { get; } = new();

    public IReadOnlyList<string> Formats { get; } = new[]
    {
        "Auto-detect", "token", "email:password:token", "token:proxy", "email:password:token:proxy",
    };

    public IReadOnlyList<string> ProxyPolicies { get; } = new[]
    {
        "Random from pool (default)", "As in file (manual)",
    };

    public bool CanImport => HasReport && Report.Any(l => l.Action is "new" or "update");

    public List<ParsedLine> Applied { get; } = new();

    public ImportViewModel(IEnumerable<string> groups, IEnumerable<string> knownTokens)
    {
        Groups = new ObservableCollection<string>(groups);
        existingTokens = knownTokens.ToList();
        Tags = $"import:{DateTime.Now:yyyy-MM-dd}";
    }

    private readonly List<string> existingTokens;

    partial void OnHasReportChanged(bool value) => OnPropertyChanged(nameof(CanImport));

    [RelayCommand]
    private void LoadFile()
    {
        var dialog = new OpenFileDialog
        {
            Filter = "Text files (*.txt)|*.txt|All files (*.*)|*.*",
        };
        if (dialog.ShowDialog() == true)
        {
            SourceText = System.IO.File.ReadAllText(dialog.FileName);
        }
    }

    [RelayCommand]
    private void Analyze()
    {
        Report.Clear();
        Applied.Clear();

        var formatKey = Format switch
        {
            "Auto-detect" => AccountLineParser.FormatAuto,
            "token" => AccountLineParser.FormatToken,
            "email:password:token" => AccountLineParser.FormatEmailPasswordToken,
            "token:proxy" => AccountLineParser.FormatTokenProxy,
            _ => AccountLineParser.FormatEmailPasswordTokenProxy,
        };

        var lines = AccountLineParser.ParseAll(SourceText, formatKey);
        AccountLineParser.MarkDuplicates(lines, existingTokens);

        var counts = new Dictionary<string, int>
        {
            ["new"] = 0, ["update"] = 0, ["duplicate"] = 0, ["error"] = 0,
        };

        foreach (var line in lines)
        {
            if (line.Raw.Length == 0 || line.Raw.StartsWith('#'))
            {
                continue;
            }

            string action;
            string actionText;
            string detail;
            if (!line.Ok)
            {
                action = "error";
                actionText = "Error";
                detail = line.Error ?? "parse failed";
            }
            else if (line.DuplicateInFile)
            {
                action = "duplicate";
                actionText = "Duplicate";
                detail = "same token earlier in this file";
            }
            else if (line.DuplicateStored)
            {
                action = "update";
                actionText = "Update";
                detail = Fields(line);
            }
            else
            {
                action = "new";
                actionText = "New";
                detail = Fields(line);
            }

            counts[action]++;
            Report.Add(new ImportLineViewModel
            {
                LineNumber = line.LineNumber,
                Raw = line.Raw.Length > 60 ? line.Raw[..60] + "…" : line.Raw,
                Action = action,
                ActionText = actionText,
                Detail = detail,
            });

            if (line.Ok && !line.DuplicateInFile)
            {
                Applied.Add(line);
            }
        }

        Summary = $"{counts["new"]} new, {counts["update"]} update, " +
                  $"{counts["duplicate"]} duplicate, {counts["error"]} error";
        HasReport = true;
    }

    private static string Fields(ParsedLine parsed)
    {
        var parts = new List<string> { "token ok" };
        if (parsed.Email is not null)
        {
            parts.Add("email");
        }

        if (parsed.Password is not null)
        {
            parts.Add("password");
        }

        if (parsed.Proxy is not null)
        {
            parts.Add("proxy");
        }

        return string.Join(", ", parts);
    }
}
