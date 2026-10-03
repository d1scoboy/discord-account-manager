using System.Windows;

namespace DiscoBox.ProfileManager.Views.Dialogs;

public partial class AddProxyDialog : Window
{
    public AddProxyDialog()
    {
        InitializeComponent();
    }

    public List<string> ProxyUrls => UrlBox.Text
        .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Where(line => line.Length > 0 && !line.StartsWith('#'))
        .ToList();

    private void Save_Click(object sender, RoutedEventArgs e) => DialogResult = true;

    private void Close_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
