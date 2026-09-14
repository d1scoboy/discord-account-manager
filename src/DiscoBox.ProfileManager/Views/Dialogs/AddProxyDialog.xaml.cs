using System.Windows;

namespace DiscoBox.ProfileManager.Views.Dialogs;

public partial class AddProxyDialog : Window
{
    public AddProxyDialog()
    {
        InitializeComponent();
    }

    public string ProxyUrl => UrlBox.Text;

    private void Save_Click(object sender, RoutedEventArgs e) => DialogResult = true;

    private void Close_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
