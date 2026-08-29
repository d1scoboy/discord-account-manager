using System.Windows;
using DiscoBox.ProfileManager.ViewModels;

namespace DiscoBox.ProfileManager.Views.Dialogs;

public partial class ManageGroupsDialog : Window
{
    public ManageGroupsDialog(ManageGroupsViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    private void Close_Click(object sender, RoutedEventArgs e) => DialogResult = true;
}
