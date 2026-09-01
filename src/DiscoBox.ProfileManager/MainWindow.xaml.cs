using System.Windows;
using DiscoBox.ProfileManager.ViewModels;

namespace DiscoBox.ProfileManager;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainViewModel(App.StartupPage);
        StateChanged += OnStateChanged;

        if (App.StartupPage == "card")
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                var vm = (MainViewModel)DataContext;
                var row = vm.Accounts.Sections.FirstOrDefault()?.Accounts.FirstOrDefault();
                vm.Accounts.OpenCardCommand.Execute(row);
            }), System.Windows.Threading.DispatcherPriority.Loaded);
        }

        if (App.StartupPage == "metrics")
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                var lines = new List<string>();
                Walk(this, lines);
                System.IO.File.WriteAllLines(
                    System.IO.Path.Combine(System.IO.Path.GetTempPath(), "discobox-metrics.txt"), lines);
            }), System.Windows.Threading.DispatcherPriority.Loaded);
        }
    }

    private static void Walk(System.Windows.DependencyObject node, List<string> lines)
    {
        switch (node)
        {
            case System.Windows.Controls.TextBox t when lines.Count(l => l.StartsWith("TextBox")) < 3:
                lines.Add($"TextBox h={t.ActualHeight:0.##} w={t.ActualWidth:0.##}");
                break;
            case System.Windows.Controls.ComboBox c when lines.Count(l => l.StartsWith("ComboBox")) < 3:
                lines.Add($"ComboBox h={c.ActualHeight:0.##} w={c.ActualWidth:0.##}");
                break;
            case System.Windows.Controls.Button b when lines.Count(l => l.StartsWith("Button")) < 4:
                lines.Add($"Button '{b.Content}' h={b.ActualHeight:0.##}");
                break;
        }

        for (var i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(node); i++)
        {
            Walk(System.Windows.Media.VisualTreeHelper.GetChild(node, i), lines);
        }
    }

    private void OnStateChanged(object? sender, EventArgs e)
    {
        MaximizeButton.Content = WindowState == WindowState.Maximized ? "\uE923" : "\uE922";
        MaximizeButton.ToolTip = WindowState == WindowState.Maximized ? "Restore" : "Maximize";
    }

    private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void Maximize_Click(object sender, RoutedEventArgs e) =>
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
