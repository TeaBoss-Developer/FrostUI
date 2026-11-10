using System.Windows;
using System.Windows.Controls;

namespace FrostUI.Controls;

public enum FrostMessageBoxButtons
{
    OK,
    OKCancel,
    YesNo,
}

public enum FrostMessageBoxResult
{
    OK,
    Cancel,
    Yes,
    No,
}

public partial class FrostMessageBox : FrostWindow
{
    private FrostMessageBoxResult _result = FrostMessageBoxResult.Cancel;

    public FrostMessageBox()
    {
        InitializeComponent();
    }

    public static FrostMessageBoxResult Show(
        Window? owner,
        string message,
        string title = "FrostUI",
        FrostMessageBoxButtons buttons = FrostMessageBoxButtons.OK,
        bool danger = false)
    {
        var dialog = new FrostMessageBox
        {
            Owner = owner,
            Title = title,
            WindowStartupLocation = owner is null
                ? WindowStartupLocation.CenterScreen
                : WindowStartupLocation.CenterOwner,
        };
        dialog.TitleText.Text = title;
        dialog.DialogKindText.Text = danger ? "确认操作" : "提示";
        dialog.MessageText.Text = message;
        dialog.BuildButtons(buttons, danger);
        dialog.ShowDialog();
        return dialog._result;
    }

    private void BuildButtons(FrostMessageBoxButtons buttons, bool danger)
    {
        switch (buttons)
        {
            case FrostMessageBoxButtons.OKCancel:
                AddButton("取消", FrostMessageBoxResult.Cancel, primary: false);
                AddButton("确定", FrostMessageBoxResult.OK, primary: true, danger);
                break;
            case FrostMessageBoxButtons.YesNo:
                AddButton("否", FrostMessageBoxResult.No, primary: false);
                AddButton("是", FrostMessageBoxResult.Yes, primary: true, danger);
                break;
            default:
                AddButton("确定", FrostMessageBoxResult.OK, primary: true, danger);
                break;
        }
    }

    private void AddButton(
        string content,
        FrostMessageBoxResult result,
        bool primary,
        bool danger = false)
    {
        var styleKey = danger
            ? "DangerButtonStyle"
            : primary ? "PrimaryButtonStyle" : "GhostButtonStyle";
        var button = new Button
        {
            Content = content,
            MinWidth = 78,
            Height = 32,
            Margin = new Thickness(8, 0, 0, 0),
            IsDefault = result is FrostMessageBoxResult.OK or FrostMessageBoxResult.Yes,
            IsCancel = result is FrostMessageBoxResult.Cancel or FrostMessageBoxResult.No,
        };
        if (TryFindResource(styleKey) is Style style)
        {
            button.Style = style;
        }

        button.Click += (_, _) =>
        {
            _result = result;
            Close();
        };
        ButtonsPanel.Children.Add(button);
    }
}
