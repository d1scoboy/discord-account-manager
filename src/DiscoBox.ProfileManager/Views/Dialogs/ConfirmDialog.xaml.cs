using System.Windows;

namespace DiscoBox.ProfileManager.Views.Dialogs;

public partial class ConfirmDialog : Window
{
    private ConfirmDialog(string title, string message, string confirmLabel)
    {
        InitializeComponent();
        TitleText.Text = title;
        MessageText.Text = message;
        ConfirmButton.Content = confirmLabel;
    }

    public static bool Ask(string title, string message, string confirmLabel = "Confirm")
    {
        var dialog = new ConfirmDialog(title, message, confirmLabel)
        {
            Owner = Application.Current?.MainWindow,
        };
        return dialog.ShowDialog() == true;
    }

    private void Confirm_Click(object sender, RoutedEventArgs e) => DialogResult = true;

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
