using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using TerrainBuilder.Core.Models;

namespace TerrainBuilder.App.ViewModels;

public partial class PrintListViewModel : ObservableObject
{
    public ObservableCollection<PrintListItem> Items { get; } = [];

    public int TotalPieces => Items.Sum(item => item.Quantity);
    public int UniqueModels => Items.Count;

    public void ReplaceWith(IEnumerable<PrintListItem> items)
    {
        Items.Clear();
        foreach (var item in items) Items.Add(item);
        OnPropertyChanged(nameof(TotalPieces));
        OnPropertyChanged(nameof(UniqueModels));
    }
}
