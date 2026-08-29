using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace DiscoBox.ProfileManager.ViewModels;

public partial class ManageGroupsViewModel : ObservableObject
{
    [ObservableProperty]
    private string? selectedGroup;

    [ObservableProperty]
    private string? selectedTag;

    [ObservableProperty]
    private string newGroup = "";

    [ObservableProperty]
    private string newTag = "";

    public ObservableCollection<string> Groups { get; }

    public ObservableCollection<string> Tags { get; }

    public ManageGroupsViewModel(IEnumerable<string> groups, IEnumerable<string> tags)
    {
        Groups = new ObservableCollection<string>(groups);
        Tags = new ObservableCollection<string>(tags);
    }

    [RelayCommand]
    private void AddGroup()
    {
        var name = NewGroup.Trim();
        if (name.Length > 0 && !Groups.Contains(name))
        {
            Groups.Add(name);
        }

        NewGroup = "";
    }

    [RelayCommand]
    private void RemoveGroup()
    {
        if (SelectedGroup is not null && SelectedGroup != "default")
        {
            Groups.Remove(SelectedGroup);
        }
    }

    [RelayCommand]
    private void AddTag()
    {
        var name = NewTag.Trim();
        if (name.Length > 0 && !Tags.Contains(name))
        {
            Tags.Add(name);
        }

        NewTag = "";
    }

    [RelayCommand]
    private void RemoveTag()
    {
        if (SelectedTag is not null)
        {
            Tags.Remove(SelectedTag);
        }
    }
}
