using System.Windows;
using DiscoBox.ProfileManager.ViewModels;

namespace DiscoBox.ProfileManager.Views.Dialogs;

public partial class AddEditAccountDialog : Window
{
    private readonly AddEditAccountViewModel vm;

    public AddEditAccountDialog(AddEditAccountViewModel viewModel)
    {
        InitializeComponent();
        vm = viewModel;
        DataContext = vm;
        PasswordBox.Password = vm.Password;
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        vm.Password = PasswordBox.Password;
        if (vm.Validate())
        {
            DialogResult = true;
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
