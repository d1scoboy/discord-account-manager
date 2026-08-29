using System.Windows;
using DiscoBox.ProfileManager.ViewModels;

namespace DiscoBox.ProfileManager.Views.Dialogs;

public partial class ImportDialog : Window
{
    public ImportDialog(ImportViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    private void Import_Click(object sender, RoutedEventArgs e) => DialogResult = true;

    private void Close_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
