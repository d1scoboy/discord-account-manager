using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using DiscoBox.ProfileManager.ViewModels;

namespace DiscoBox.ProfileManager.Views;

public partial class AccountsPage : UserControl
{
    private readonly Dictionary<object, List<AccountRowViewModel>> selections = new();
    private readonly Dictionary<string, ListBox> sectionLists = new();
    private bool syncing;

    public AccountsPage()
    {
        InitializeComponent();
    }

    private void SectionList_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is ListBox list && list.Tag is string name)
        {
            sectionLists[name] = list;
        }
    }

    private void SectionList_Unloaded(object sender, RoutedEventArgs e)
    {
        if (sender is ListBox list && list.Tag is string name)
        {
            sectionLists.Remove(name);
        }
    }

    private void SectionList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (syncing || DataContext is not AccountsPageViewModel vm)
        {
            return;
        }

        syncing = true;
        try
        {
            var list = (ListBox)sender;
            foreach (var other in sectionLists.Values)
            {
                if (other != list && other.SelectedItems.Count > 0)
                {
                    other.UnselectAll();
                }
            }

            if (list.Tag is null)
            {
                return;
            }

            selections[list.Tag] = list.SelectedItems.OfType<AccountRowViewModel>().ToList();

            var alive = new HashSet<object>(vm.Sections.Select(s => s.Name));
            foreach (var key in selections.Keys.Where(k => !alive.Contains(k)).ToList())
            {
                selections.Remove(key);
            }

            vm.SetSelection(selections.Values.SelectMany(rows => rows));
        }
        finally
        {
            syncing = false;
        }
    }

    private void SectionList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is not AccountsPageViewModel vm)
        {
            return;
        }

        if (sender is ListBox { SelectedItem: AccountRowViewModel row })
        {
            vm.OpenCardCommand.Execute(row);
        }
    }

    private void SelectShown_Click(object sender, RoutedEventArgs e)
    {
        syncing = true;
        try
        {
            foreach (var list in sectionLists.Values)
            {
                list.SelectAll();
            }
        }
        finally
        {
            syncing = false;
        }

        AggregateAll();
    }

    private void GroupSelectAll_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: string name, DataContext: GroupSectionViewModel section })
        {
            return;
        }

        if (!sectionLists.TryGetValue(name, out var list))
        {
            return;
        }

        syncing = true;
        try
        {
            foreach (var other in sectionLists.Values)
            {
                if (other != list)
                {
                    other.UnselectAll();
                }
            }

            list.SelectAll();
        }
        finally
        {
            syncing = false;
        }

        AggregateAll();
    }

    private void AggregateAll()
    {
        if (DataContext is not AccountsPageViewModel vm)
        {
            return;
        }

        var rows = sectionLists.Values
            .SelectMany(list => list.SelectedItems.OfType<AccountRowViewModel>())
            .ToList();
        vm.SetSelection(rows);
    }
}
