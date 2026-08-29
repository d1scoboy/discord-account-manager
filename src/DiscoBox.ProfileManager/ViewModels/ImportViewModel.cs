using System.Collections.ObjectModel;
using System.Text.RegularExpressions;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
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
    private static readonly Regex TokenRegex =
        new(@"^[\w-]{20,30}\.[\w-]{4,10}\.[\w-]{20,40}$", RegexOptions.Compiled);

    private static readonly Regex ProxyRegex =
        new(@"^(https?|socks5)://|^[\w.-]+:\d{2,5}(:|$)", RegexOptions.Compiled);

    private readonly HashSet<string> existingTokens;

    [ObservableProperty]
    private string sourceText = "";

    [ObservableProperty]
    private string format = "Auto-detect";

    [ObservableProperty]
    private string group = "default";

    [ObservableProperty]
    private string tags = "";

    [ObservableProperty]
    private string proxyPolicy = "Random from pool";

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
        "Random from pool", "As in file", "None",
    };

    public bool CanImport => HasReport && Report.Any(l => l.Action is "new" or "update");

    public List<ParsedAccount> Applied { get; } = new();

    public ImportViewModel(IEnumerable<string> groups, IEnumerable<string> knownTokens)
    {
        Groups = new ObservableCollection<string>(groups);
        existingTokens = new HashSet<string>(knownTokens);
        Tags = $"import:{DateTime.Now:yyyy-MM-dd}";
    }

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
        var seen = new HashSet<string>();
        var counts = new Dictionary<string, int>
        {
            ["new"] = 0, ["update"] = 0, ["duplicate"] = 0, ["error"] = 0,
        };

        var lineNumber = 0;
        foreach (var rawLine in SourceText.Split('\n'))
        {
            lineNumber++;
            var line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith('#'))
            {
                continue;
            }

            var parsed = Parse(line);
            if (parsed is null)
            {
                Add(lineNumber, line, "error", "Error", "No token recognized in this line");
                counts["error"]++;
                continue;
            }

            if (!seen.Add(parsed.Token))
            {
                Add(lineNumber, line, "duplicate", "Duplicate", "Same token earlier in this file");
                counts["duplicate"]++;
                continue;
            }

            if (existingTokens.Contains(parsed.Token))
            {
                Add(lineNumber, line, "update", "Update", Fields(parsed));
                counts["update"]++;
            }
            else
            {
                Add(lineNumber, line, "new", "New", Fields(parsed));
                counts["new"]++;
            }

            Applied.Add(parsed);
        }

        Summary = $"{counts["new"]} new, {counts["update"]} update, " +
                  $"{counts["duplicate"]} duplicate, {counts["error"]} error";
        HasReport = true;
    }

    private void Add(int lineNumber, string raw, string action, string actionText, string detail) =>
        Report.Add(new ImportLineViewModel
        {
            LineNumber = lineNumber,
            Raw = raw.Length > 60 ? raw[..60] + "…" : raw,
            Action = action,
            ActionText = actionText,
            Detail = detail,
        });

    private static string Fields(ParsedAccount p)
    {
        var parts = new List<string> { "token ok" };
        if (p.Email is not null)
        {
            parts.Add("email");
        }

        if (p.Password is not null)
        {
            parts.Add("password");
        }

        if (p.Proxy is not null)
        {
            parts.Add("proxy");
        }

        return string.Join(", ", parts);
    }

    private ParsedAccount? Parse(string line)
    {
        var parts = line.Split(':');
        var result = new ParsedAccount();

        if (Format == "token")
        {
            if (!TokenRegex.IsMatch(line))
            {
                return null;
            }

            result.Token = line;
            return result;
        }

        if (Format == "email:password:token")
        {
            if (parts.Length < 3)
            {
                return null;
            }

            result.Email = parts[0];
            result.Password = string.Join(':', parts[1..^1]);
            result.Token = parts[^1];
            return TokenRegex.IsMatch(result.Token) ? result : null;
        }

        if (Format == "token:proxy")
        {
            if (parts.Length < 2 || !TokenRegex.IsMatch(parts[0]))
            {
                return null;
            }

            result.Token = parts[0];
            result.Proxy = string.Join(':', parts[1..]);
            return result;
        }

        if (Format == "email:password:token:proxy")
        {
            if (parts.Length < 4)
            {
                return null;
            }

            result.Email = parts[0];
            result.Password = string.Join(':', parts[1..^2]);
            result.Token = parts[^2];
            result.Proxy = parts[^1];
            return TokenRegex.IsMatch(result.Token) ? result : null;
        }

        foreach (var part in parts)
        {
            var trimmed = part.Trim();
            if (TokenRegex.IsMatch(trimmed))
            {
                result.Token = trimmed;
            }
            else if (trimmed.Contains('@'))
            {
                result.Email = trimmed;
            }
            else if (ProxyRegex.IsMatch(trimmed))
            {
                result.Proxy = trimmed;
            }
            else
            {
                result.Password = trimmed;
            }
        }

        return result.Token is null ? null : result;
    }
}

public class ParsedAccount
{
    public string Token { get; set; } = "";
    public string? Email { get; set; }
    public string? Password { get; set; }
    public string? Proxy { get; set; }
}
