using System.Windows;
using DiscoBox.ProfileManager.ViewModels;

namespace DiscoBox.ProfileManager.Views.Dialogs;

public partial class AccountCardDialog : Window
{
    private readonly AccountCardViewModel vm;

    public AccountCardDialog(AccountCardViewModel viewModel)
    {
        InitializeComponent();
        vm = viewModel;
        DataContext = vm;
        vm.Close = () => DialogResult = true;
    }

    private void CopyToken_Click(object sender, RoutedEventArgs e) =>
        Clipboard.SetText(vm.Row.TokenMasked);

    private void Close_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
