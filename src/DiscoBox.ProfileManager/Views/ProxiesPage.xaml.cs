using System.Windows;
using System.Windows.Controls;
using DiscoBox.ProfileManager.ViewModels;

namespace DiscoBox.ProfileManager.Views;

public partial class ProxiesPage : UserControl
{
    private readonly Dictionary<object, List<ProxyRowViewModel>> selections = new();
    private readonly Dictionary<string, ListBox> sectionLists = new();
    private bool syncing;

    public ProxiesPage()
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
        if (syncing || DataContext is not ProxiesPageViewModel vm)
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

            selections[list.Tag] = list.SelectedItems.OfType<ProxyRowViewModel>().ToList();

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

    private void ClearFilters_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is ProxiesPageViewModel vm)
        {
            vm.SearchText = "";
            vm.StatusFilter = "Any status";
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

        if (DataContext is ProxiesPageViewModel vm)
        {
            vm.SetSelection(sectionLists.Values
                .SelectMany(list => list.SelectedItems.OfType<ProxyRowViewModel>())
                .ToList());
        }
    }
}
