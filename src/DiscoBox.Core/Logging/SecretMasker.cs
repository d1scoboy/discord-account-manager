using System.Text;
using System.Text.RegularExpressions;

namespace DiscoBox.Core.Logging;

public class SecretMasker
{
    private static readonly Regex ProxyUrl = new(
        @"\b(?:https?|socks5)://[^\s:@/]+:[^\s@/]+@",
        RegexOptions.Compiled);

    private static readonly Regex DiscordToken = new(
        @"\b[\w-]{20,30}\.[\w-]{4,10}\.[\w-]{20,40}\b",
        RegexOptions.Compiled);

    private static readonly Regex BearerHeader = new(
        @"(Authorization:\s*Bearer\s+)\S+",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private readonly List<string> literals = new();

    public void Register(params string?[] secrets)
    {
        lock (literals)
        {
            foreach (var secret in secrets)
            {
                if (!string.IsNullOrEmpty(secret) && secret.Length >= 6 && !literals.Contains(secret))
                {
                    literals.Add(secret);
                }
            }

            literals.Sort((a, b) => b.Length.CompareTo(a.Length));
        }
    }

    public string Mask(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return text;
        }

        var result = text;
        lock (literals)
        {
            foreach (var literal in literals)
            {
                if (result.Contains(literal, StringComparison.Ordinal))
                {
                    result = result.Replace(literal, MaskValue(literal));
                }
            }
        }

        result = ProxyUrl.Replace(result, m =>
        {
            var value = m.Value;
            var schemeEnd = value.IndexOf("://", StringComparison.Ordinal) + 3;
            return value[..schemeEnd] + "***:***@";
        });

        result = DiscordToken.Replace(result, m => MaskValue(m.Value));
        result = BearerHeader.Replace(result, "$1***");
        return result;
    }

    private static string MaskValue(string value) =>
        value.Length <= 12 ? "***" : $"{value[..6]}…{value[^4..]}";
}
