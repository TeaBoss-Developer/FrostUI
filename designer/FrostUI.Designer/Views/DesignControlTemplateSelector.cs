using System.Windows;
using System.Windows.Controls;
using FrostUI.Designer.Models;

namespace FrostUI.Designer.Views;

public sealed class DesignControlTemplateSelector : DataTemplateSelector
{
    public DataTemplate? ButtonTemplate { get; set; }

    public DataTemplate? LabelTemplate { get; set; }

    public DataTemplate? TextBoxTemplate { get; set; }

    public DataTemplate? PasswordTemplate { get; set; }

    public DataTemplate? TextAreaTemplate { get; set; }

    public DataTemplate? SearchTemplate { get; set; }

    public DataTemplate? NumberTemplate { get; set; }

    public DataTemplate? DateTemplate { get; set; }

    public DataTemplate? ComboBoxTemplate { get; set; }

    public DataTemplate? CheckBoxTemplate { get; set; }

    public DataTemplate? RadioTemplate { get; set; }

    public DataTemplate? ToggleTemplate { get; set; }

    public DataTemplate? SliderTemplate { get; set; }

    public DataTemplate? ProgressBarTemplate { get; set; }

    public DataTemplate? ProgressRingTemplate { get; set; }

    public DataTemplate? CardTemplate { get; set; }

    public DataTemplate? ImageTemplate { get; set; }

    public DataTemplate? ContainerTemplate { get; set; }

    public DataTemplate? TabsTemplate { get; set; }

    public DataTemplate? ExpanderTemplate { get; set; }

    public DataTemplate? SplitViewTemplate { get; set; }

    public DataTemplate? MenuTemplate { get; set; }

    public DataTemplate? InfoBarTemplate { get; set; }

    public DataTemplate? DataGridTemplate { get; set; }

    public DataTemplate? TreeViewTemplate { get; set; }

    public DataTemplate? RepeaterTemplate { get; set; }

    public DataTemplate? ChartTemplate { get; set; }

    public DataTemplate? ListTemplate { get; set; }

    public DataTemplate? DividerTemplate { get; set; }

    public override DataTemplate? SelectTemplate(object? item, DependencyObject container)
    {
        return item is not DesignControl control
            ? base.SelectTemplate(item, container)
            : control.Kind switch
            {
                ControlKind.Button => ButtonTemplate,
                ControlKind.Label => LabelTemplate,
                ControlKind.TextBox => TextBoxTemplate,
                ControlKind.PasswordBox => PasswordTemplate,
                ControlKind.TextArea => TextAreaTemplate,
                ControlKind.SearchBox => SearchTemplate,
                ControlKind.NumberBox => NumberTemplate,
                ControlKind.DatePicker => DateTemplate,
                ControlKind.ComboBox => ComboBoxTemplate,
                ControlKind.CheckBox => CheckBoxTemplate,
                ControlKind.RadioButton => RadioTemplate,
                ControlKind.ToggleSwitch => ToggleTemplate,
                ControlKind.Slider => SliderTemplate,
                ControlKind.ProgressBar => ProgressBarTemplate,
                ControlKind.ProgressRing => ProgressRingTemplate,
                ControlKind.Card => CardTemplate,
                ControlKind.Image => ImageTemplate,
                ControlKind.Stack or ControlKind.Grid or ControlKind.Dock or ControlKind.Scroll => ContainerTemplate,
                ControlKind.Tabs => TabsTemplate,
                ControlKind.Expander => ExpanderTemplate,
                ControlKind.SplitView => SplitViewTemplate,
                ControlKind.Menu => MenuTemplate,
                ControlKind.InfoBar => InfoBarTemplate,
                ControlKind.DataGrid => DataGridTemplate,
                ControlKind.TreeView => TreeViewTemplate,
                ControlKind.Repeater => RepeaterTemplate,
                ControlKind.Chart => ChartTemplate,
                ControlKind.List => ListTemplate,
                ControlKind.Divider => DividerTemplate,
                _ => base.SelectTemplate(item, container),
            };
    }
}
