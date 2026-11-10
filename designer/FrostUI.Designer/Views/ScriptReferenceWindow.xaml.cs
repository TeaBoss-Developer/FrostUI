using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using FrostUI.Controls;
using FrostUI.Designer.Models;
using FrostUI.Designer.Services;

namespace FrostUI.Designer.Views;

public partial class ScriptReferenceWindow : FrostWindow
{
    private readonly ObservableCollection<ScriptReferenceEntry> _items = [];
    private readonly List<ScriptReferenceEntry> _all = [.. ScriptReferenceCatalog.All];

    public event Action<string>? InsertRequested;

    public ScriptReferenceWindow()
    {
        InitializeComponent();
        CategoryBox.ItemsSource = new[] { "全部" }
            .Concat(ScriptReferenceCatalog.Categories)
            .ToArray();
        CategoryBox.SelectedIndex = 0;
        ReferenceList.ItemsSource = _items;
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        var search = SearchBox?.Text?.Trim() ?? string.Empty;
        var category = CategoryBox?.SelectedItem as string ?? "全部";
        _items.Clear();
        foreach (var entry in _all)
        {
            if (category != "全部" &&
                !string.Equals(entry.Category, category, StringComparison.CurrentCulture))
            {
                continue;
            }

            if (search.Length > 0 &&
                !entry.Name.Contains(search, StringComparison.CurrentCultureIgnoreCase) &&
                !entry.Signature.Contains(search, StringComparison.CurrentCultureIgnoreCase) &&
                !entry.Description.Contains(search, StringComparison.CurrentCultureIgnoreCase) &&
                !entry.Category.Contains(search, StringComparison.CurrentCultureIgnoreCase))
            {
                continue;
            }

            _items.Add(entry);
        }

        CountText.Text = $"{_items.Count} / {_all.Count} 条";
        if (_items.Count > 0)
        {
            ReferenceList.SelectedIndex = 0;
        }
        else
        {
            DetailName.Text = "没有匹配条目";
            DetailCategory.Text = string.Empty;
            DetailSignature.Text = string.Empty;
            DetailDescription.Text = string.Empty;
        }
    }

    private void SearchBox_OnTextChanged(object sender, TextChangedEventArgs e) => ApplyFilter();

    private void CategoryBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e) => ApplyFilter();

    private void ReferenceList_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ReferenceList.SelectedItem is not ScriptReferenceEntry entry)
        {
            return;
        }

        DetailName.Text = entry.Name;
        DetailCategory.Text = entry.Category;
        DetailSignature.Text = entry.Signature;
        DetailDescription.Text = entry.Description;
    }

    private void ReferenceList_OnMouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        InsertSelected();
        e.Handled = true;
    }

    private void Insert_OnClick(object sender, RoutedEventArgs e) => InsertSelected();

    private void InsertSelected()
    {
        if (ReferenceList.SelectedItem is ScriptReferenceEntry entry)
        {
            InsertRequested?.Invoke(entry.Signature);
        }
    }

    private void PinToggle_OnChanged(object sender, RoutedEventArgs e) =>
        Topmost = PinToggle.IsChecked == true;

    private void Close_OnClick(object sender, RoutedEventArgs e) => Close();
}
