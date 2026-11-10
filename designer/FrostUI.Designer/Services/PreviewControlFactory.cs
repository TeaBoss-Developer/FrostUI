using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Animation;
using FrostUI.Controls;
using FrostUI.Designer.Converters;
using FrostUI.Designer.Models;

namespace FrostUI.Designer.Services;

internal sealed record ContainerHost(
    Panel Panel,
    ScrollViewer? ScrollViewer,
    Canvas? ScrollCanvas,
    IReadOnlyList<Panel>? Panels = null);

internal static class PreviewControlFactory
{
    private static readonly BooleanToVisibilityConverter BoolToVis = new();
    private static readonly BoolToFontWeightConverter BoolToFontWeight = new();
    private static readonly ImageCropBrushConverter ImageCropBrush = new();
    private static readonly StringLinesConverter StringLines = new();

    public static FrameworkElement Create(DesignControl control, Action<DesignControl, string> triggerEvent)
    {
        var wrapper = new Grid
        {
            Background = Brushes.Transparent,
            ClipToBounds = false,
        };

        Bind(wrapper, FrameworkElement.WidthProperty, control, nameof(DesignControl.Width));
        Bind(wrapper, FrameworkElement.HeightProperty, control, nameof(DesignControl.Height));
        Bind(wrapper, Canvas.LeftProperty, control, nameof(DesignControl.X));
        Bind(wrapper, Canvas.TopProperty, control, nameof(DesignControl.Y));
        Bind(wrapper, UIElement.VisibilityProperty, control, nameof(DesignControl.IsVisible), converter: BoolToVis);
        Bind(wrapper, UIElement.IsEnabledProperty, control, nameof(DesignControl.IsEnabled));
        Bind(wrapper, UIElement.OpacityProperty, control, nameof(DesignControl.Opacity));

        var clickable = HasScript(control, "点击") || control.PageAction != "None";
        if (clickable)
        {
            wrapper.Cursor = System.Windows.Input.Cursors.Hand;
            wrapper.MouseLeftButtonUp += (_, _) => triggerEvent(control, "点击");
        }

        FrameworkElement visual = control.Kind switch
        {
            ControlKind.Button => CreateButton(control, triggerEvent),
            ControlKind.Label => CreateLabel(control),
            ControlKind.TextBox => CreateTextBox(control, triggerEvent),
            ControlKind.PasswordBox => CreatePasswordBox(control, triggerEvent),
            ControlKind.TextArea => CreateTextArea(control, triggerEvent),
            ControlKind.SearchBox => CreateSearchBox(control, triggerEvent),
            ControlKind.NumberBox => CreateNumberBox(control, triggerEvent),
            ControlKind.DatePicker => CreateDatePicker(control, triggerEvent),
            ControlKind.ComboBox => CreateComboBox(control, triggerEvent),
            ControlKind.CheckBox => CreateCheckBox(control, triggerEvent),
            ControlKind.RadioButton => CreateRadioButton(control, triggerEvent),
            ControlKind.ToggleSwitch => CreateToggle(control, triggerEvent),
            ControlKind.Slider => CreateSlider(control, triggerEvent),
            ControlKind.ProgressBar => CreateProgressBar(control),
            ControlKind.ProgressRing => CreateProgressRing(control),
            ControlKind.Card => CreateCard(control),
            ControlKind.Image => CreateImage(control),
            ControlKind.List => CreateList(control),
            ControlKind.Stack or ControlKind.Grid or ControlKind.Dock or ControlKind.Scroll
                => CreateContainer(control),
            ControlKind.Tabs => CreateTabs(control, triggerEvent),
            ControlKind.Expander => CreateExpander(control, triggerEvent),
            ControlKind.SplitView => CreateSplitView(control),
            ControlKind.Menu => CreateMenu(control, triggerEvent),
            ControlKind.InfoBar => CreateInfoBar(control, triggerEvent),
            ControlKind.DataGrid => CreateDataGrid(control, triggerEvent),
            ControlKind.TreeView => CreateTreeView(control, triggerEvent),
            ControlKind.Repeater => CreateRepeater(control, triggerEvent),
            ControlKind.Chart => CreateChart(control),
            ControlKind.Divider => CreateDivider(control),
            _ => new Border(),
        };

        if (!AnimationTimelinePlayer.TryApply(visual, control))
        {
            ApplyAnimation(
                visual,
                control.Animation,
                control.AnimationDurationMs,
                control.AnimationDelayMs,
                control.AnimationRepeat);
        }
        if (!string.IsNullOrWhiteSpace(control.FontFamily))
        {
            var font = new FontFamily(control.FontFamily);
            switch (visual)
            {
                case Control controlVisual:
                    controlVisual.FontFamily = font;
                    break;
                case TextBlock textVisual:
                    textVisual.FontFamily = font;
                    break;
            }
        }

        AutomationProperties.SetAutomationId(wrapper, control.Name);
        AutomationProperties.SetAutomationId(visual, control.Name);
        if (ThemeColorHelper.TryParse(control.AccentColor, out var localAccent))
        {
            ThemeColorHelper.ApplyLocalAccent(wrapper, localAccent);
        }

        AttachHoverAndContextMenu(wrapper, visual, control, triggerEvent);
        if (visual.Tag is not null)
        {
            wrapper.Tag = visual.Tag;
        }

        wrapper.Children.Add(visual);
        return wrapper;
    }

    private static void ApplyAnimation(
        FrameworkElement visual,
        string animation,
        int durationMs,
        int delayMs,
        bool repeat)
    {
        if (string.IsNullOrWhiteSpace(animation) || animation == "None")
        {
            return;
        }

        var duration = TimeSpan.FromMilliseconds(Math.Clamp(durationMs, 80, 5000));
        var beginTime = TimeSpan.FromMilliseconds(Math.Clamp(delayMs, 0, 5000));
        var repeatBehavior = repeat ? RepeatBehavior.Forever : new RepeatBehavior(1);
        var easing = new QuadraticEase { EasingMode = EasingMode.EaseOut };
        switch (animation)
        {
            case "FadeIn":
                visual.Opacity = 0;
                visual.Loaded += (_, _) =>
                    visual.BeginAnimation(
                        UIElement.OpacityProperty,
                        new DoubleAnimation(0, 1, duration)
                        {
                            BeginTime = beginTime,
                            EasingFunction = easing,
                            FillBehavior = FillBehavior.HoldEnd,
                            RepeatBehavior = repeatBehavior,
                        });
                break;

            case "SlideUp":
                visual.Opacity = 0;
                visual.RenderTransform = new TranslateTransform(0, 14);
                visual.Loaded += (_, _) =>
                {
                    var transform = (TranslateTransform)visual.RenderTransform;
                    transform.BeginAnimation(
                        TranslateTransform.YProperty,
                        new DoubleAnimation(14, 0, duration)
                        {
                            BeginTime = beginTime,
                            EasingFunction = easing,
                            FillBehavior = FillBehavior.HoldEnd,
                            RepeatBehavior = repeatBehavior,
                        });
                    visual.BeginAnimation(
                        UIElement.OpacityProperty,
                        new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(280))
                        {
                            EasingFunction = easing,
                            FillBehavior = FillBehavior.HoldEnd,
                        });
                };
                break;

            case "ScaleIn":
                visual.Opacity = 0;
                visual.RenderTransformOrigin = new Point(0.5, 0.5);
                visual.RenderTransform = new ScaleTransform(0.96, 0.96);
                visual.Loaded += (_, _) =>
                {
                    var transform = (ScaleTransform)visual.RenderTransform;
                    var scaleAnimation = new DoubleAnimation(0.96, 1, TimeSpan.FromMilliseconds(320))
                    {
                        BeginTime = beginTime,
                        EasingFunction = easing,
                        FillBehavior = FillBehavior.HoldEnd,
                        RepeatBehavior = repeatBehavior,
                    };
                    transform.BeginAnimation(ScaleTransform.ScaleXProperty, scaleAnimation);
                    transform.BeginAnimation(ScaleTransform.ScaleYProperty, scaleAnimation);
                    visual.BeginAnimation(
                        UIElement.OpacityProperty,
                        new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(260))
                        {
                            EasingFunction = easing,
                            FillBehavior = FillBehavior.HoldEnd,
                        });
                };
                break;
        }
    }

    private static FrameworkElement CreateButton(DesignControl control, Action<DesignControl, string> triggerEvent)
    {
        var button = new Button { HorizontalAlignment = HorizontalAlignment.Stretch };
        if (FindStyle("PrimaryButtonStyle") is { } style)
        {
            button.Style = style;
        }

        Bind(button, ContentControl.ContentProperty, control, nameof(DesignControl.Text));
        Bind(button, Control.FontSizeProperty, control, nameof(DesignControl.FontSize));
        Bind(button, FrameworkElement.WidthProperty, control, nameof(DesignControl.Width));
        Bind(button, FrameworkElement.HeightProperty, control, nameof(DesignControl.Height));
        if (HasScript(control, "点击"))
        {
            button.Click += (_, _) => triggerEvent(control, "点击");
        }

        return button;
    }

    private static FrameworkElement CreateLabel(DesignControl control)
    {
        var text = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            TextTrimming = TextTrimming.CharacterEllipsis,
            FontFamily = FindFont(),
            VerticalAlignment = VerticalAlignment.Top,
        };
        text.SetResourceReference(TextBlock.ForegroundProperty, "TextPrimaryBrush");
        Bind(text, FrameworkElement.WidthProperty, control, nameof(DesignControl.Width));
        Bind(text, FrameworkElement.HeightProperty, control, nameof(DesignControl.Height));
        Bind(text, TextBlock.TextProperty, control, nameof(DesignControl.Text));
        Bind(text, TextBlock.FontSizeProperty, control, nameof(DesignControl.FontSize));
        Bind(text, TextBlock.FontWeightProperty, control, nameof(DesignControl.IsBold), converter: BoolToFontWeight);
        return text;
    }

    private static FrameworkElement CreateTextBox(DesignControl control, Action<DesignControl, string> triggerEvent)
    {
        var textBox = new TextBox
        {
            VerticalContentAlignment = VerticalAlignment.Center,
        };
        if (FindStyle("GlassTextBoxStyle") is { } style)
        {
            textBox.Style = style;
        }

        Bind(textBox, TextBox.TextProperty, control, nameof(DesignControl.Text),
            BindingMode.TwoWay, updateTrigger: UpdateSourceTrigger.PropertyChanged);
        Bind(textBox, Control.FontSizeProperty, control, nameof(DesignControl.FontSize));
        Bind(textBox, FrameworkElement.WidthProperty, control, nameof(DesignControl.Width));
        Bind(textBox, FrameworkElement.HeightProperty, control, nameof(DesignControl.Height));
        if (HasScript(control, "文本改变"))
        {
            textBox.TextChanged += (_, _) => triggerEvent(control, "文本改变");
        }

        textBox.TextChanged += (_, _) => ValidateTextBox(textBox, control);
        ValidateTextBox(textBox, control);
        return textBox;
    }

    private static void ValidateTextBox(TextBox textBox, DesignControl control)
    {
        var value = textBox.Text?.Trim() ?? string.Empty;
        var invalid = control.ValidationRule switch
        {
            "Required" => value.Length == 0,
            "Email" => !value.Contains('@') || !value.Contains('.'),
            "Number" => value.Length > 0 &&
                        !double.TryParse(value, System.Globalization.NumberStyles.Float,
                            System.Globalization.CultureInfo.CurrentCulture, out _),
            _ => false,
        };
        if (!invalid)
        {
            textBox.ClearValue(Control.BorderBrushProperty);
            textBox.ToolTip = null;
            return;
        }

        textBox.BorderBrush = Brushes.IndianRed;
        textBox.ToolTip = string.IsNullOrWhiteSpace(control.ValidationMessage)
            ? control.ValidationRule switch
            {
                "Required" => "此项必填",
                "Email" => "请输入有效邮箱",
                "Number" => "请输入数字",
                _ => "输入无效",
            }
            : control.ValidationMessage;
    }

    private static FrameworkElement CreateCheckBox(DesignControl control, Action<DesignControl, string> triggerEvent)
    {
        var checkBox = new CheckBox
        {
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
        };
        if (FindStyle("FrostCheckBoxStyle") is { } style)
        {
            checkBox.Style = style;
        }

        checkBox.SetResourceReference(Control.ForegroundProperty, "TextPrimaryBrush");
        var checkLabel = CreateChoiceLabel(control);
        checkBox.Content = checkLabel;
        Bind(checkBox, System.Windows.Controls.Primitives.ToggleButton.IsCheckedProperty, control,
            nameof(DesignControl.IsOn), BindingMode.TwoWay);
        Bind(checkBox, Control.FontSizeProperty, control, nameof(DesignControl.FontSize));
        Bind(checkBox, FrameworkElement.WidthProperty, control, nameof(DesignControl.Width));
        Bind(checkBox, FrameworkElement.HeightProperty, control, nameof(DesignControl.Height));
        if (HasScript(control, "切换"))
        {
            checkBox.Checked += (_, _) => triggerEvent(control, "切换");
            checkBox.Unchecked += (_, _) => triggerEvent(control, "切换");
        }

        return checkBox;
    }

    private static FrameworkElement CreateRadioButton(DesignControl control, Action<DesignControl, string> triggerEvent)
    {
        var radio = new RadioButton
        {
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
        };
        if (FindStyle("FrostRadioButtonStyle") is { } style)
        {
            radio.Style = style;
        }

        radio.SetResourceReference(Control.ForegroundProperty, "TextPrimaryBrush");
        radio.Width = 18;
        radio.Height = 18;
        radio.VerticalAlignment = VerticalAlignment.Center;
        AutomationProperties.SetName(radio, control.Text);
        AutomationProperties.SetAutomationId(radio, control.Name);
        AutomationProperties.SetHelpText(radio, "单选框");
        var radioLabel = CreateChoiceLabel(control);
        radioLabel.Margin = new Thickness(8, 0, 0, 0);
        var panel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center,
            Background = Brushes.Transparent,
            Cursor = System.Windows.Input.Cursors.Hand,
        };
        panel.Children.Add(radio);
        panel.Children.Add(radioLabel);
        panel.MouseLeftButtonUp += (_, _) => radio.IsChecked = true;
        Bind(radio, System.Windows.Controls.Primitives.ToggleButton.IsCheckedProperty, control,
            nameof(DesignControl.IsOn), BindingMode.TwoWay);
        Bind(radio, RadioButton.GroupNameProperty, control, nameof(DesignControl.GroupName));
        Bind(radioLabel, TextBlock.FontSizeProperty, control, nameof(DesignControl.FontSize));
        Bind(panel, FrameworkElement.WidthProperty, control, nameof(DesignControl.Width));
        Bind(panel, FrameworkElement.HeightProperty, control, nameof(DesignControl.Height));
        if (HasScript(control, "切换"))
        {
            radio.Checked += (_, _) => triggerEvent(control, "切换");
            radio.Unchecked += (_, _) => triggerEvent(control, "切换");
        }

        return panel;
    }

    private static TextBlock CreateChoiceLabel(DesignControl control)
    {
        var label = new TextBlock
        {
            VerticalAlignment = VerticalAlignment.Center,
        };
        label.SetResourceReference(TextBlock.ForegroundProperty, "TextPrimaryBrush");
        Bind(label, TextBlock.TextProperty, control, nameof(DesignControl.Text));
        Bind(label, TextBlock.FontSizeProperty, control, nameof(DesignControl.FontSize));
        return label;
    }

    private static FrameworkElement CreateComboBox(DesignControl control, Action<DesignControl, string> triggerEvent)
    {
        var combo = new ComboBox
        {
            IsEditable = false,
            VerticalContentAlignment = VerticalAlignment.Center,
        };
        if (FindStyle("FrostComboBoxStyle") is { } style)
        {
            combo.Style = style;
        }

        Bind(combo, ItemsControl.ItemsSourceProperty, control, nameof(DesignControl.ListItems),
            converter: StringLines);
        Bind(combo, System.Windows.Controls.Primitives.Selector.SelectedItemProperty, control,
            nameof(DesignControl.SelectedItem), BindingMode.TwoWay);
        Bind(combo, Control.FontSizeProperty, control, nameof(DesignControl.FontSize));
        Bind(combo, FrameworkElement.WidthProperty, control, nameof(DesignControl.Width));
        Bind(combo, FrameworkElement.HeightProperty, control, nameof(DesignControl.Height));
        AutomationProperties.SetName(combo, control.SelectedItem);
        AutomationProperties.SetAutomationId(combo, control.Name);
        AutomationProperties.SetHelpText(combo, "下拉框");
        var initialized = false;
        combo.Loaded += (_, _) => initialized = true;
        combo.SelectionChanged += (_, _) =>
        {
            AutomationProperties.SetName(combo, control.SelectedItem);
            if (initialized && HasScript(control, "选择改变"))
            {
                triggerEvent(control, "选择改变");
            }
        };
        return combo;
    }

    private static FrameworkElement CreatePasswordBox(DesignControl control, Action<DesignControl, string> triggerEvent)
    {
        var password = new PasswordBox
        {
            PasswordChar = '●',
            VerticalContentAlignment = VerticalAlignment.Center,
        };
        if (FindStyle("GlassPasswordBoxStyle") is { } style)
        {
            password.Style = style;
        }

        password.Password = control.Text;
        Bind(password, Control.FontSizeProperty, control, nameof(DesignControl.FontSize));
        Bind(password, FrameworkElement.WidthProperty, control, nameof(DesignControl.Width));
        Bind(password, FrameworkElement.HeightProperty, control, nameof(DesignControl.Height));
        password.PasswordChanged += (_, _) =>
        {
            if (control.Text == password.Password)
            {
                return;
            }

            control.Text = password.Password;
            if (HasScript(control, "文本改变"))
            {
                triggerEvent(control, "文本改变");
            }
        };
        System.ComponentModel.PropertyChangedEventHandler handler = (_, args) =>
        {
            if (args.PropertyName == nameof(DesignControl.Text) && password.Password != control.Text)
            {
                password.Password = control.Text;
            }
        };
        control.PropertyChanged += handler;
        password.Unloaded += (_, _) => control.PropertyChanged -= handler;
        return password;
    }

    private static FrameworkElement CreateTextArea(DesignControl control, Action<DesignControl, string> triggerEvent)
    {
        var textBox = new TextBox
        {
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            VerticalContentAlignment = VerticalAlignment.Top,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Padding = new Thickness(10, 7, 10, 7),
        };
        if (FindStyle("GlassTextBoxStyle") is { } style)
        {
            textBox.Style = style;
        }

        Bind(textBox, TextBox.TextProperty, control, nameof(DesignControl.Text),
            BindingMode.TwoWay, updateTrigger: UpdateSourceTrigger.PropertyChanged);
        Bind(textBox, Control.FontSizeProperty, control, nameof(DesignControl.FontSize));
        Bind(textBox, FrameworkElement.WidthProperty, control, nameof(DesignControl.Width));
        Bind(textBox, FrameworkElement.HeightProperty, control, nameof(DesignControl.Height));
        textBox.TextChanged += (_, _) =>
        {
            ValidateTextBox(textBox, control);
            if (HasScript(control, "文本改变"))
            {
                triggerEvent(control, "文本改变");
            }
        };
        ValidateTextBox(textBox, control);
        return textBox;
    }

    private static FrameworkElement CreateNumberBox(DesignControl control, Action<DesignControl, string> triggerEvent)
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var textBox = new TextBox
        {
            VerticalContentAlignment = VerticalAlignment.Center,
            TextAlignment = TextAlignment.Left,
        };
        if (FindStyle("GlassTextBoxStyle") is { } textStyle)
        {
            textBox.Style = textStyle;
        }

        textBox.Text = FormatNumber(control.Value);
        AutomationProperties.SetAutomationId(textBox, control.Name + "_输入");
        AutomationProperties.SetName(textBox, "数字输入");
        var buttons = new StackPanel
        {
            Margin = new Thickness(5, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
        };
        var up = MakeStepButton("▲");
        var down = MakeStepButton("▼");
        buttons.Children.Add(up);
        buttons.Children.Add(down);
        Grid.SetColumn(buttons, 1);
        grid.Children.Add(textBox);
        grid.Children.Add(buttons);

        Bind(grid, FrameworkElement.WidthProperty, control, nameof(DesignControl.Width));
        Bind(grid, FrameworkElement.HeightProperty, control, nameof(DesignControl.Height));
        Bind(textBox, Control.FontSizeProperty, control, nameof(DesignControl.FontSize));

        var updating = false;
        void ApplyText()
        {
            if (updating)
            {
                return;
            }

            if (!double.TryParse(textBox.Text, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.CurrentCulture, out var value))
            {
                return;
            }

            var clamped = Math.Clamp(value, Math.Min(control.Minimum, control.Maximum),
                Math.Max(control.Minimum, control.Maximum));
            updating = true;
            if (Math.Abs(control.Value - clamped) > 0.0001)
            {
                control.Value = clamped;
                if (HasScript(control, "值改变"))
                {
                    triggerEvent(control, "值改变");
                }
            }

            textBox.Text = FormatNumber(clamped);
            textBox.CaretIndex = textBox.Text.Length;
            updating = false;
        }

        textBox.TextChanged += (_, _) => ApplyText();
        textBox.LostKeyboardFocus += (_, _) =>
        {
            textBox.Text = FormatNumber(control.Value);
        };
        up.Click += (_, _) =>
        {
            control.Value = Math.Min(Math.Max(control.Minimum, control.Maximum), control.Value + control.Step);
            textBox.Text = FormatNumber(control.Value);
            if (HasScript(control, "值改变"))
            {
                triggerEvent(control, "值改变");
            }
        };
        down.Click += (_, _) =>
        {
            control.Value = Math.Max(Math.Min(control.Minimum, control.Maximum), control.Value - control.Step);
            textBox.Text = FormatNumber(control.Value);
            if (HasScript(control, "值改变"))
            {
                triggerEvent(control, "值改变");
            }
        };
        System.ComponentModel.PropertyChangedEventHandler numberHandler = (_, args) =>
        {
            if (args.PropertyName == nameof(DesignControl.Value) && !textBox.IsKeyboardFocusWithin)
            {
                textBox.Text = FormatNumber(control.Value);
            }
        };
        control.PropertyChanged += numberHandler;
        grid.Unloaded += (_, _) => control.PropertyChanged -= numberHandler;
        return grid;
    }

    private static Button MakeStepButton(string text)
    {
        var button = new Button
        {
            Content = text,
            Width = 22,
            Height = 15,
            Padding = new Thickness(0),
            FontSize = 6,
            Margin = new Thickness(0, 1, 0, 1),
        };
        if (FindStyle("GhostButtonStyle") is { } style)
        {
            button.Style = style;
        }

        return button;
    }

    private static string FormatNumber(double value) =>
        value.ToString("0.####", System.Globalization.CultureInfo.CurrentCulture);

    private static FrameworkElement CreateDatePicker(DesignControl control, Action<DesignControl, string> triggerEvent)
    {
        var border = new Border
        {
            CornerRadius = new CornerRadius(9),
            Background = FindBrush("InputBrush"),
            BorderBrush = FindBrush("InputBorderBrush"),
            BorderThickness = new Thickness(1),
        };
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(28) });
        var textBox = new TextBox
        {
            VerticalContentAlignment = VerticalAlignment.Center,
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(10, 0, 0, 0),
        };
        if (FindStyle("SearchTextBoxStyle") is { } textStyle)
        {
            textBox.Style = textStyle;
        }

        Bind(textBox, TextBox.TextProperty, control, nameof(DesignControl.Text),
            BindingMode.TwoWay, updateTrigger: UpdateSourceTrigger.PropertyChanged);
        Bind(textBox, Control.FontSizeProperty, control, nameof(DesignControl.FontSize));
        AutomationProperties.SetAutomationId(textBox, control.Name + "_输入");
        AutomationProperties.SetName(textBox, "日期输入");
        var calendarButton = new Button
        {
            Content = "日",
            Width = 24,
            Height = 24,
            Padding = new Thickness(0),
            FontSize = 10,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        if (FindStyle("GhostButtonStyle") is { } buttonStyle)
        {
            calendarButton.Style = buttonStyle;
        }

        Grid.SetColumn(calendarButton, 1);
        grid.Children.Add(textBox);
        grid.Children.Add(calendarButton);
        border.Child = grid;
        Bind(border, FrameworkElement.WidthProperty, control, nameof(DesignControl.Width));
        Bind(border, FrameworkElement.HeightProperty, control, nameof(DesignControl.Height));

        var calendar = new Calendar
        {
            Background = FindBrush("ToastBrush"),
            BorderBrush = FindBrush("CardBorderStrongBrush"),
            BorderThickness = new Thickness(1),
            Foreground = FindBrush("TextPrimaryBrush"),
        };
        var popup = new Popup
        {
            PlacementTarget = border,
            Placement = PlacementMode.Bottom,
            StaysOpen = false,
            AllowsTransparency = true,
            Child = calendar,
        };
        calendar.SelectedDatesChanged += (_, _) =>
        {
            if (calendar.SelectedDate is not { } date)
            {
                return;
            }

            control.Text = date.ToString("yyyy-MM-dd");
            if (HasScript(control, "日期改变"))
            {
                triggerEvent(control, "日期改变");
            }
        };
        calendarButton.Click += (_, _) =>
        {
            if (DateTime.TryParse(control.Text, out var date))
            {
                calendar.SelectedDate = date;
                calendar.DisplayDate = date;
            }

            popup.IsOpen = !popup.IsOpen;
        };
        textBox.LostKeyboardFocus += (_, _) =>
        {
            if (DateTime.TryParse(textBox.Text, out var date))
            {
                control.Text = date.ToString("yyyy-MM-dd");
            }
        };
        textBox.KeyDown += (_, args) =>
        {
            if (args.Key == System.Windows.Input.Key.Enter && HasScript(control, "日期改变"))
            {
                triggerEvent(control, "日期改变");
                args.Handled = true;
            }
        };
        popup.Closed += (_, _) => textBox.Focus();
        return border;
    }

    private static FrameworkElement CreateSearchBox(DesignControl control, Action<DesignControl, string> triggerEvent)
    {
        var border = new Border
        {
            CornerRadius = new CornerRadius(9),
            Background = FindBrush("InputBrush"),
            BorderBrush = FindBrush("InputBorderBrush"),
            BorderThickness = new Thickness(1),
        };
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(28) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(28) });
        var icon = new TextBlock
        {
            Text = "⌕",
            FontSize = 14,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = FindBrush("TextTertiaryBrush"),
            IsHitTestVisible = false,
        };
        var textBox = new TextBox
        {
            VerticalContentAlignment = VerticalAlignment.Center,
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(0),
            CaretBrush = FindBrush("AccentBrush"),
        };
        if (FindStyle("SearchTextBoxStyle") is { } style)
        {
            textBox.Style = style;
        }

        Bind(textBox, TextBox.TextProperty, control, nameof(DesignControl.Text),
            BindingMode.TwoWay, updateTrigger: UpdateSourceTrigger.PropertyChanged);
        Bind(textBox, Control.FontSizeProperty, control, nameof(DesignControl.FontSize));
        AutomationProperties.SetAutomationId(textBox, control.Name + "_输入");
        AutomationProperties.SetName(textBox, "搜索输入");
        Grid.SetColumn(textBox, 1);
        var clear = new Button
        {
            Content = "×",
            FontSize = 12,
            Width = 24,
            Height = 24,
            Padding = new Thickness(0),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Visibility = Visibility.Collapsed,
        };
        if (FindStyle("GhostButtonStyle") is { } clearStyle)
        {
            clear.Style = clearStyle;
        }

        Grid.SetColumn(clear, 2);
        grid.Children.Add(icon);
        grid.Children.Add(textBox);
        grid.Children.Add(clear);
        border.Child = grid;
        Bind(border, FrameworkElement.WidthProperty, control, nameof(DesignControl.Width));
        Bind(border, FrameworkElement.HeightProperty, control, nameof(DesignControl.Height));

        textBox.TextChanged += (_, _) =>
        {
            clear.Visibility = string.IsNullOrEmpty(textBox.Text) ? Visibility.Collapsed : Visibility.Visible;
            if (HasScript(control, "文本改变"))
            {
                triggerEvent(control, "文本改变");
            }
        };
        textBox.KeyDown += (_, args) =>
        {
            if (args.Key == System.Windows.Input.Key.Enter && HasScript(control, "提交"))
            {
                triggerEvent(control, "提交");
                args.Handled = true;
            }
        };
        clear.Click += (_, _) =>
        {
            textBox.Text = string.Empty;
            textBox.Focus();
        };
        clear.Visibility = string.IsNullOrEmpty(textBox.Text) ? Visibility.Collapsed : Visibility.Visible;
        return border;
    }

    private static FrameworkElement CreateToggle(DesignControl control, Action<DesignControl, string> triggerEvent)
    {
        var toggle = new GlassToggleSwitch
        {
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center,
        };
        Bind(toggle, System.Windows.Controls.Primitives.ToggleButton.IsCheckedProperty, control,
            nameof(DesignControl.IsOn), BindingMode.TwoWay);
        Bind(toggle, FrameworkElement.WidthProperty, control, nameof(DesignControl.Width));
        Bind(toggle, FrameworkElement.HeightProperty, control, nameof(DesignControl.Height));

        if (HasScript(control, "切换"))
        {
            toggle.Checked += (_, _) => triggerEvent(control, "切换");
            toggle.Unchecked += (_, _) => triggerEvent(control, "切换");
        }

        return toggle;
    }

    private static FrameworkElement CreateSlider(DesignControl control, Action<DesignControl, string> triggerEvent)
    {
        var slider = new Slider
        {
            Minimum = 0,
            Maximum = 100,
            VerticalAlignment = VerticalAlignment.Center,
        };
        if (FindStyle("GlassSliderStyle") is { } style)
        {
            slider.Style = style;
        }

        Bind(slider, System.Windows.Controls.Primitives.RangeBase.ValueProperty, control,
            nameof(DesignControl.Value), BindingMode.TwoWay);
        Bind(slider, FrameworkElement.WidthProperty, control, nameof(DesignControl.Width));
        Bind(slider, FrameworkElement.HeightProperty, control, nameof(DesignControl.Height));
        if (HasScript(control, "值改变"))
        {
            slider.ValueChanged += (_, _) => triggerEvent(control, "值改变");
        }

        return slider;
    }

    private static FrameworkElement CreateProgressBar(DesignControl control)
    {
        var progress = new ProgressBar
        {
            VerticalAlignment = VerticalAlignment.Center,
        };
        if (FindStyle("GlassProgressBarStyle") is { } style)
        {
            progress.Style = style;
        }

        Bind(progress, System.Windows.Controls.Primitives.RangeBase.ValueProperty, control,
            nameof(DesignControl.Value));
        Bind(progress, FrameworkElement.WidthProperty, control, nameof(DesignControl.Width));
        Bind(progress, FrameworkElement.HeightProperty, control, nameof(DesignControl.Height));
        return progress;
    }

    private static FrameworkElement CreateProgressRing(DesignControl control)
    {
        var grid = new Grid();
        Bind(grid, FrameworkElement.WidthProperty, control, nameof(DesignControl.Width));
        Bind(grid, FrameworkElement.HeightProperty, control, nameof(DesignControl.Height));

        var ring = new ProgressRing { StrokeThickness = 8 };
        ring.SetResourceReference(ProgressRing.TrackBrushProperty, "InsetSurfaceStrongBrush");
        ring.SetResourceReference(ProgressRing.ProgressBrushProperty, "AccentGradientBrush");
        ring.SetResourceReference(ProgressRing.GlowBrushProperty, "AccentGlowBrush");
        Bind(ring, ProgressRing.ValueProperty, control, nameof(DesignControl.Value));

        var stack = new StackPanel
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        var value = new TextBlock
        {
            FontFamily = FindFont(),
            FontSize = 17,
            FontWeight = FontWeights.SemiBold,
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        value.SetResourceReference(TextBlock.ForegroundProperty, "TextPrimaryBrush");
        Bind(value, TextBlock.TextProperty, control, nameof(DesignControl.Value), format: "{0:0}%");
        var caption = new TextBlock
        {
            Text = "完成度",
            FontSize = 7.5,
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        caption.SetResourceReference(TextBlock.ForegroundProperty, "TextTertiaryBrush");
        stack.Children.Add(value);
        stack.Children.Add(caption);

        grid.Children.Add(ring);
        grid.Children.Add(stack);
        return grid;
    }

    private static FrameworkElement CreateCard(DesignControl control)
    {
        var card = new GlassCard { Padding = new Thickness(16) };
        Bind(card, GlassCard.CornerRadiusProperty, control, nameof(DesignControl.CornerRadius));
        Bind(card, FrameworkElement.WidthProperty, control, nameof(DesignControl.Width));
        Bind(card, FrameworkElement.HeightProperty, control, nameof(DesignControl.Height));

        var text = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            TextTrimming = TextTrimming.CharacterEllipsis,
            FontFamily = FindFont(),
        };
        text.SetResourceReference(TextBlock.ForegroundProperty, "TextPrimaryBrush");
        Bind(text, TextBlock.TextProperty, control, nameof(DesignControl.Text));
        Bind(text, TextBlock.FontSizeProperty, control, nameof(DesignControl.FontSize));
        Bind(text, TextBlock.FontWeightProperty, control, nameof(DesignControl.IsBold), converter: BoolToFontWeight);
        card.Content = text;
        return card;
    }

    private static FrameworkElement CreateImage(DesignControl control)
    {
        var background = new MultiBinding { Converter = ImageCropBrush };
        background.Bindings.Add(new Binding(nameof(DesignControl.ImagePath)) { Source = control });
        background.Bindings.Add(new Binding(nameof(DesignControl.ImageCrop)) { Source = control });
        background.Bindings.Add(new Binding(nameof(DesignControl.ImageStretch)) { Source = control });
        var border = new Border
        {
            ClipToBounds = true,
            Background = FindBrush("InsetSurfaceBrush"),
            BorderBrush = FindBrush("CardBorderBrush"),
            BorderThickness = new Thickness(1),
        };
        border.SetBinding(Border.BackgroundProperty, background);
        Bind(border, FrameworkElement.WidthProperty, control, nameof(DesignControl.Width));
        Bind(border, FrameworkElement.HeightProperty, control, nameof(DesignControl.Height));
        Bind(border, Border.CornerRadiusProperty, control, nameof(DesignControl.CornerRadius));
        return border;
    }

    private static FrameworkElement CreateList(DesignControl control)
    {
        var list = new ListBox
        {
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(8, 5, 8, 5),
        };
        Bind(list, ItemsControl.ItemsSourceProperty, control, nameof(DesignControl.ListItems),
            converter: StringLines);
        Bind(list, Control.FontSizeProperty, control, nameof(DesignControl.FontSize));
        Bind(list, FrameworkElement.WidthProperty, control, nameof(DesignControl.Width));
        Bind(list, FrameworkElement.HeightProperty, control, nameof(DesignControl.Height));
        var border = new Border
        {
            CornerRadius = new CornerRadius(control.CornerRadius),
            ClipToBounds = true,
            Background = FindBrush("InsetSurfaceBrush"),
            BorderBrush = FindBrush("CardBorderBrush"),
            BorderThickness = new Thickness(1),
            Child = list,
        };
        return border;
    }

    private static FrameworkElement CreateContainer(DesignControl control)
    {
        Panel panel;
        ScrollViewer? scrollViewer = null;
        Canvas? scrollCanvas = null;
        switch (control.Kind)
        {
            case ControlKind.Stack:
                panel = new StackPanel
                {
                    Orientation = control.StackOrientation == "Horizontal"
                        ? Orientation.Horizontal
                        : Orientation.Vertical,
                };
                break;
            case ControlKind.Dock:
                panel = new DockPanel { LastChildFill = true };
                break;
            case ControlKind.Scroll:
                scrollCanvas = new Canvas();
                scrollViewer = new ScrollViewer
                {
                    Content = scrollCanvas,
                    VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                    HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                };
                panel = new DockPanel { LastChildFill = true };
                panel.Children.Add(scrollViewer);
                break;
            default:
                var grid = new Grid();
                for (var i = 0; i < control.GridRows; i++)
                {
                    grid.RowDefinitions.Add(new RowDefinition());
                }

                for (var i = 0; i < control.GridColumns; i++)
                {
                    grid.ColumnDefinitions.Add(new ColumnDefinition());
                }

                panel = grid;
                break;
        }

        var border = new Border
        {
            ClipToBounds = true,
            CornerRadius = new CornerRadius(control.CornerRadius),
            Background = FindBrush("InsetSurfaceBrush"),
            BorderBrush = FindBrush("CardBorderBrush"),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(control.ContainerPadding),
            Child = panel,
            Tag = control.Kind == ControlKind.Scroll
                ? new ContainerHost(scrollCanvas!, scrollViewer, scrollCanvas)
                : new ContainerHost(panel, scrollViewer, scrollCanvas),
        };
        Bind(border, FrameworkElement.WidthProperty, control, nameof(DesignControl.Width));
        Bind(border, FrameworkElement.HeightProperty, control, nameof(DesignControl.Height));
        return border;
    }

    private static FrameworkElement CreateTabs(DesignControl control, Action<DesignControl, string> triggerEvent)
    {
        var titles = SplitLines(control.ListItems);
        if (titles.Count == 0)
        {
            titles.Add("标签一");
        }

        var tabControl = new TabControl
        {
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(0),
        };
        if (FindStyle("FrostTabControlStyle") is { } style)
        {
            tabControl.Style = style;
        }

        var panels = new List<Panel>();
        foreach (var title in titles)
        {
            var canvas = new Canvas { ClipToBounds = true, Background = Brushes.Transparent };
            var item = new TabItem { Header = title, Content = canvas };
            if (FindStyle("FrostTabItemStyle") is { } itemStyle)
            {
                item.Style = itemStyle;
            }

            tabControl.Items.Add(item);
            panels.Add(canvas);
        }

        Bind(tabControl, FrameworkElement.WidthProperty, control, nameof(DesignControl.Width));
        Bind(tabControl, FrameworkElement.HeightProperty, control, nameof(DesignControl.Height));
        Bind(tabControl, Selector.SelectedIndexProperty, control, nameof(DesignControl.SelectedIndex),
            BindingMode.TwoWay);
        var initialized = false;
        tabControl.Loaded += (_, _) =>
        {
            initialized = true;
            if (tabControl.SelectedIndex >= 0 && tabControl.SelectedIndex < titles.Count)
            {
                control.SelectedItem = titles[tabControl.SelectedIndex];
            }
        };
        tabControl.SelectionChanged += (_, _) =>
        {
            if (tabControl.SelectedIndex >= 0 && tabControl.SelectedIndex < titles.Count)
            {
                control.SelectedItem = titles[tabControl.SelectedIndex];
            }

            if (initialized && HasScript(control, "标签切换"))
            {
                triggerEvent(control, "标签切换");
            }
        };
        var host = new ContainerHost(panels[0], null, null, panels);
        var border = new Border
        {
            ClipToBounds = true,
            Background = FindBrush("InsetSurfaceBrush"),
            BorderBrush = FindBrush("CardBorderBrush"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(control.CornerRadius),
            Child = tabControl,
            Tag = host,
        };
        Bind(border, FrameworkElement.WidthProperty, control, nameof(DesignControl.Width));
        Bind(border, FrameworkElement.HeightProperty, control, nameof(DesignControl.Height));
        return border;
    }

    private static FrameworkElement CreateExpander(DesignControl control, Action<DesignControl, string> triggerEvent)
    {
        var header = new TextBlock
        {
            Text = control.Text,
            FontFamily = FindFont(),
            FontSize = control.FontSize,
            FontWeight = FontWeights.SemiBold,
            Foreground = FindBrush("TextPrimaryBrush"),
        };
        var canvas = new Canvas { ClipToBounds = true, Background = Brushes.Transparent };
        var expander = new Expander
        {
            Header = header,
            Content = canvas,
            IsExpanded = control.IsOn,
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(10, 8, 10, 8),
        };
        Bind(expander, Expander.IsExpandedProperty, control, nameof(DesignControl.IsOn),
            BindingMode.TwoWay);
        Bind(expander, Control.FontSizeProperty, control, nameof(DesignControl.FontSize));
        Bind(expander, FrameworkElement.WidthProperty, control, nameof(DesignControl.Width));
        Bind(expander, FrameworkElement.HeightProperty, control, nameof(DesignControl.Height));
        var initialized = false;
        expander.Loaded += (_, _) => initialized = true;
        expander.Expanded += (_, _) =>
        {
            if (initialized && HasScript(control, "展开改变"))
            {
                triggerEvent(control, "展开改变");
            }
        };
        expander.Collapsed += (_, _) =>
        {
            if (initialized && HasScript(control, "展开改变"))
            {
                triggerEvent(control, "展开改变");
            }
        };
        var host = new ContainerHost(canvas, null, null);
        var border = new Border
        {
            ClipToBounds = true,
            Background = FindBrush("InsetSurfaceBrush"),
            BorderBrush = FindBrush("CardBorderBrush"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(control.CornerRadius),
            Child = expander,
            Tag = host,
        };
        Bind(border, FrameworkElement.WidthProperty, control, nameof(DesignControl.Width));
        Bind(border, FrameworkElement.HeightProperty, control, nameof(DesignControl.Height));
        return border;
    }

    private static FrameworkElement CreateSplitView(DesignControl control)
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(5) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var left = new Canvas { ClipToBounds = true, Background = Brushes.Transparent };
        var right = new Canvas { ClipToBounds = true, Background = Brushes.Transparent };
        var leftBorder = new Border
        {
            Background = FindBrush("InsetSurfaceBrush"),
            BorderBrush = FindBrush("CardBorderBrush"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Child = left,
        };
        var rightBorder = new Border
        {
            Background = FindBrush("InsetSurfaceBrush"),
            BorderBrush = FindBrush("CardBorderBrush"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Child = right,
        };
        var splitter = new GridSplitter
        {
            Width = 5,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            Background = FindBrush("DividerBrush"),
            ResizeBehavior = GridResizeBehavior.PreviousAndNext,
            ResizeDirection = GridResizeDirection.Columns,
            ShowsPreview = false,
        };
        Grid.SetColumn(leftBorder, 0);
        Grid.SetColumn(splitter, 1);
        Grid.SetColumn(rightBorder, 2);
        grid.Children.Add(leftBorder);
        grid.Children.Add(splitter);
        grid.Children.Add(rightBorder);
        var host = new ContainerHost(left, null, null, [left, right]);
        var border = new Border
        {
            ClipToBounds = true,
            Child = grid,
            Tag = host,
        };
        Bind(border, FrameworkElement.WidthProperty, control, nameof(DesignControl.Width));
        Bind(border, FrameworkElement.HeightProperty, control, nameof(DesignControl.Height));
        return border;
    }

    private static FrameworkElement CreateMenu(DesignControl control, Action<DesignControl, string> triggerEvent)
    {
        var items = SplitLines(control.ListItems);
        var stack = new StackPanel
        {
            Orientation = control.StackOrientation == "Vertical"
                ? Orientation.Vertical
                : Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center,
        };
        foreach (var text in items)
        {
            var button = new Button
            {
                Content = text,
                Height = 30,
                MinWidth = 54,
                Padding = new Thickness(10, 0, 10, 0),
                Margin = new Thickness(0, 0, 2, 0),
                FontSize = control.FontSize,
            };
            if (FindStyle("GhostButtonStyle") is { } style)
            {
                button.Style = style;
            }

            if (text == control.SelectedItem)
            {
                button.SetResourceReference(Control.BackgroundProperty, "AccentSoftBrush");
            }

            var itemText = text;
            button.Click += (_, _) =>
            {
                control.SelectedItem = itemText;
                if (HasScript(control, "菜单点击"))
                {
                    triggerEvent(control, "菜单点击");
                }
            };
            stack.Children.Add(button);
        }

        var border = new Border
        {
            Background = FindBrush("ToastBrush"),
            BorderBrush = FindBrush("CardBorderBrush"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(9),
            Padding = new Thickness(5, 3, 5, 3),
            Child = stack,
        };
        Bind(border, FrameworkElement.WidthProperty, control, nameof(DesignControl.Width));
        Bind(border, FrameworkElement.HeightProperty, control, nameof(DesignControl.Height));
        return border;
    }

    private static FrameworkElement CreateInfoBar(DesignControl control, Action<DesignControl, string> triggerEvent)
    {
        var (accentKey, softKey, icon) = control.Severity switch
        {
            "Success" => ("SuccessBrush", "SuccessSoftBrush", "✓"),
            "Warning" => ("WarningBrush", "WarningSoftBrush", "!"),
            "Error" => ("DangerBrush", "DangerSoftBrush", "×"),
            _ => ("AccentBrush", "AccentSoftBrush", "i"),
        };
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(28) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(28) });
        var iconText = new TextBlock
        {
            Text = icon,
            FontFamily = FindFont(),
            FontSize = 14,
            FontWeight = FontWeights.Bold,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        iconText.SetResourceReference(TextBlock.ForegroundProperty, accentKey);
        var text = new TextBlock
        {
            Text = control.Text,
            TextWrapping = TextWrapping.Wrap,
            VerticalAlignment = VerticalAlignment.Center,
        };
        text.SetResourceReference(TextBlock.ForegroundProperty, "TextPrimaryBrush");
        Bind(text, TextBlock.TextProperty, control, nameof(DesignControl.Text));
        var close = new Button
        {
            Content = "×",
            Width = 22,
            Height = 22,
            Padding = new Thickness(0),
            FontSize = 13,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        if (FindStyle("GhostButtonStyle") is { } closeStyle)
        {
            close.Style = closeStyle;
        }

        close.Click += (_, _) =>
        {
            control.IsVisible = false;
            if (HasScript(control, "关闭"))
            {
                triggerEvent(control, "关闭");
            }
        };
        Grid.SetColumn(text, 1);
        Grid.SetColumn(close, 2);
        grid.Children.Add(iconText);
        grid.Children.Add(text);
        grid.Children.Add(close);
        var border = new Border
        {
            Background = FindBrush(softKey),
            BorderBrush = FindBrush(accentKey),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(9),
            Padding = new Thickness(10, 7, 8, 7),
            Child = grid,
        };
        Bind(border, FrameworkElement.WidthProperty, control, nameof(DesignControl.Width));
        Bind(border, FrameworkElement.HeightProperty, control, nameof(DesignControl.Height));
        return border;
    }

    private static FrameworkElement CreateDataGrid(DesignControl control, Action<DesignControl, string> triggerEvent)
    {
        var columns = SplitLines(control.TableColumns);
        if (columns.Count == 0)
        {
            columns.Add("值");
        }

        var rows = SplitLines(control.ListItems)
            .Select(line => NormalizeRow(line.Split('|'), columns.Count))
            .ToList();
        var table = new StackPanel();
        var rowBorders = new List<Border>();
        var sortColumn = -1;
        var sortAscending = true;

        void SaveRows() =>
            control.ListItems = string.Join("\n", rows.Select(row => string.Join("|", row)));

        void SelectRow(int rowIndex)
        {
            control.SelectedItem = rows[rowIndex].Length > 0 ? rows[rowIndex][0] : string.Empty;
            for (var index = 0; index < rowBorders.Count; index++)
            {
                rowBorders[index].Background = index == rowIndex
                    ? FindBrush("AccentSoftBrush")
                    : Brushes.Transparent;
            }

            if (HasScript(control, "行点击"))
            {
                triggerEvent(control, "行点击");
            }
        }

        void BeginEdit(int rowIndex, int columnIndex, Border cell)
        {
            if (!control.GridEditable)
            {
                return;
            }

            var editor = new TextBox
            {
                Text = rows[rowIndex][columnIndex],
                Padding = new Thickness(7, 0, 7, 0),
                Height = 27,
                FontSize = 9,
                VerticalContentAlignment = VerticalAlignment.Center,
                Background = FindBrush("InputFocusBrush"),
                BorderBrush = FindBrush("AccentBorderBrush"),
                BorderThickness = new Thickness(1),
                Foreground = FindBrush("TextPrimaryBrush"),
                CaretBrush = FindBrush("AccentBrush"),
            };
            var finished = false;
            void Finish(bool commit)
            {
                if (finished)
                {
                    return;
                }

                finished = true;
                if (commit)
                {
                    rows[rowIndex][columnIndex] = editor.Text.Trim();
                    SaveRows();
                    control.SelectedItem = rows[rowIndex].Length > 0 ? rows[rowIndex][0] : string.Empty;
                    if (HasScript(control, "单元格改变"))
                    {
                        triggerEvent(control, "单元格改变");
                    }
                }

                Render();
            }

            editor.KeyDown += (_, args) =>
            {
                if (args.Key == System.Windows.Input.Key.Enter)
                {
                    Finish(true);
                    args.Handled = true;
                }
                else if (args.Key == System.Windows.Input.Key.Escape)
                {
                    Finish(false);
                    args.Handled = true;
                }
            };
            editor.LostKeyboardFocus += (_, _) => Finish(true);
            cell.Child = editor;
            editor.Focus();
            editor.SelectAll();
        }

        void Render()
        {
            table.Children.Clear();
            rowBorders.Clear();
            var header = new Grid { Height = 32, Background = FindBrush("InsetSurfaceStrongBrush") };
            for (var columnIndex = 0; columnIndex < columns.Count; columnIndex++)
            {
                header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                var headerCell = new Border { Background = Brushes.Transparent };
                var headerText = new TextBlock
                {
                    Text = columns[columnIndex],
                    FontSize = 9.5,
                    FontWeight = FontWeights.SemiBold,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(8, 0, 8, 0),
                    TextTrimming = TextTrimming.CharacterEllipsis,
                };
                headerText.SetResourceReference(TextBlock.ForegroundProperty, "TextSecondaryBrush");
                var headerContent = new Grid();
                headerContent.Children.Add(headerText);
                if (sortColumn == columnIndex)
                {
                    var arrow = new TextBlock
                    {
                        Text = sortAscending ? "▲" : "▼",
                        FontSize = 6,
                        HorizontalAlignment = HorizontalAlignment.Right,
                        VerticalAlignment = VerticalAlignment.Center,
                        Margin = new Thickness(0, 0, 5, 0),
                    };
                    arrow.SetResourceReference(TextBlock.ForegroundProperty, "AccentBrush");
                    headerContent.Children.Add(arrow);
                }

                headerCell.Child = headerContent;
                if (control.GridSortable)
                {
                    var index = columnIndex;
                    headerCell.Cursor = System.Windows.Input.Cursors.Hand;
                    headerCell.MouseLeftButtonUp += (_, _) => SortBy(index);
                }

                Grid.SetColumn(headerCell, columnIndex);
                header.Children.Add(headerCell);
            }

            table.Children.Add(header);

            for (var rowIndex = 0; rowIndex < rows.Count; rowIndex++)
            {
                var values = rows[rowIndex];
                var rowGrid = new Grid { Height = 30 };
                for (var columnIndex = 0; columnIndex < columns.Count; columnIndex++)
                {
                    rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    var cell = new Border
                    {
                        Background = Brushes.Transparent,
                        Padding = new Thickness(8, 0, 8, 0),
                        Cursor = System.Windows.Input.Cursors.Hand,
                    };
                    var value = new TextBlock
                    {
                        Text = values[columnIndex],
                        FontSize = 9,
                        VerticalAlignment = VerticalAlignment.Center,
                        TextTrimming = TextTrimming.CharacterEllipsis,
                    };
                    value.SetResourceReference(TextBlock.ForegroundProperty, "TextPrimaryBrush");
                    cell.Child = value;
                    var row = rowIndex;
                    var column = columnIndex;
                    value.MouseLeftButtonDown += (_, args) =>
                    {
                        if (args.ClickCount == 2)
                        {
                            BeginEdit(row, column, cell);
                            args.Handled = true;
                        }
                    };
                    Grid.SetColumn(cell, columnIndex);
                    rowGrid.Children.Add(cell);
                }

                var rowBorder = new Border
                {
                    Background = values.Length > 0 && values[0] == control.SelectedItem
                        ? FindBrush("AccentSoftBrush")
                        : Brushes.Transparent,
                    BorderBrush = FindBrush("DividerBrush"),
                    BorderThickness = new Thickness(0, 0, 0, 1),
                    Child = rowGrid,
                    Cursor = System.Windows.Input.Cursors.Hand,
                };
                var index = rowIndex;
                rowBorder.MouseLeftButtonDown += (_, _) => SelectRow(index);
                rowBorders.Add(rowBorder);
                table.Children.Add(rowBorder);
            }

        }

        void SortBy(int columnIndex)
        {
            if (!control.GridSortable)
            {
                return;
            }

            if (sortColumn == columnIndex)
            {
                sortAscending = !sortAscending;
            }
            else
            {
                sortColumn = columnIndex;
                sortAscending = true;
            }

            rows.Sort((left, right) => CompareGridValues(left[columnIndex], right[columnIndex]) *
                (sortAscending ? 1 : -1));
            SaveRows();
            Render();
        }

        Render();
        var scroll = new ScrollViewer
        {
            Content = table,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
        };
        var border = new Border
        {
            Background = FindBrush("InsetSurfaceBrush"),
            BorderBrush = FindBrush("CardBorderBrush"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(control.CornerRadius),
            ClipToBounds = true,
            Child = scroll,
        };
        Bind(border, FrameworkElement.WidthProperty, control, nameof(DesignControl.Width));
        Bind(border, FrameworkElement.HeightProperty, control, nameof(DesignControl.Height));
        return border;
    }

    private static string[] NormalizeRow(string[] cells, int count)
    {
        var result = new string[count];
        for (var index = 0; index < count; index++)
        {
            result[index] = index < cells.Length ? cells[index].Trim() : string.Empty;
        }

        return result;
    }

    private static int CompareGridValues(string left, string right)
    {
        if (double.TryParse(left, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.CurrentCulture, out var leftNumber) &&
            double.TryParse(right, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.CurrentCulture, out var rightNumber))
        {
            return leftNumber.CompareTo(rightNumber);
        }

        return string.Compare(left, right, StringComparison.CurrentCultureIgnoreCase);
    }

    private static Grid CreateTableRow(IReadOnlyList<string> values, bool header)
    {
        var grid = new Grid { Height = header ? 32 : 30 };
        for (var index = 0; index < values.Count; index++)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            var text = new TextBlock
            {
                Text = values[index],
                FontSize = header ? 9.5 : 9,
                FontWeight = header ? FontWeights.SemiBold : FontWeights.Normal,
                TextTrimming = TextTrimming.CharacterEllipsis,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(8, 0, 8, 0),
            };
            text.SetResourceReference(TextBlock.ForegroundProperty,
                header ? "TextSecondaryBrush" : "TextPrimaryBrush");
            Grid.SetColumn(text, index);
            grid.Children.Add(text);
        }

        if (header)
        {
            grid.Background = FindBrush("InsetSurfaceStrongBrush");
        }

        return grid;
    }

    private static FrameworkElement CreateTreeView(DesignControl control, Action<DesignControl, string> triggerEvent)
    {
        var tree = new TreeView
        {
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(4),
        };
        if (FindStyle("FrostTreeViewStyle") is { } style)
        {
            tree.Style = style;
        }

        var stack = new List<TreeViewItem>();
        foreach (var raw in SplitRawLines(control.ListItems))
        {
            var depth = 0;
            while (depth < raw.Length && raw[depth] == ' ')
            {
                depth++;
            }

            depth /= 2;
            var name = raw.Trim();
            var header = new TextBlock
            {
                Text = name,
                FontSize = control.FontSize,
                VerticalAlignment = VerticalAlignment.Center,
            };
            header.SetResourceReference(TextBlock.ForegroundProperty, "TextPrimaryBrush");
            var item = new TreeViewItem
            {
                Header = header,
                Tag = name,
                IsExpanded = true,
                FontSize = control.FontSize,
            };
            if (depth > 0 && depth <= stack.Count)
            {
                stack[depth - 1].Items.Add(item);
            }
            else
            {
                tree.Items.Add(item);
            }

            if (stack.Count > depth)
            {
                stack[depth] = item;
            }
            else
            {
                while (stack.Count < depth)
                {
                    stack.Add(item);
                }

                stack.Add(item);
            }
        }

        Bind(tree, Control.FontSizeProperty, control, nameof(DesignControl.FontSize));
        Bind(tree, FrameworkElement.WidthProperty, control, nameof(DesignControl.Width));
        Bind(tree, FrameworkElement.HeightProperty, control, nameof(DesignControl.Height));
        var initialized = false;
        tree.Loaded += (_, _) => initialized = true;
        tree.SelectedItemChanged += (_, args) =>
        {
            if (args.NewValue is TreeViewItem item)
            {
                control.SelectedItem = item.Tag?.ToString() ?? string.Empty;
                if (initialized && HasScript(control, "选择改变"))
                {
                    triggerEvent(control, "选择改变");
                }
            }
        };
        var border = new Border
        {
            Background = FindBrush("InsetSurfaceBrush"),
            BorderBrush = FindBrush("CardBorderBrush"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(control.CornerRadius),
            ClipToBounds = true,
            Child = tree,
        };
        Bind(border, FrameworkElement.WidthProperty, control, nameof(DesignControl.Width));
        Bind(border, FrameworkElement.HeightProperty, control, nameof(DesignControl.Height));
        return border;
    }

    private static FrameworkElement CreateRepeater(DesignControl control, Action<DesignControl, string> triggerEvent)
    {
        var stack = new StackPanel { Margin = new Thickness(6) };
        foreach (var raw in SplitLines(control.ListItems))
        {
            var parts = raw.Split('|');
            var title = parts[0].Trim();
            var subtitle = parts.Length > 1 ? parts[1].Trim() : string.Empty;
            var card = new Border
            {
                Background = title == control.SelectedItem
                    ? FindBrush("AccentSoftBrush")
                    : FindBrush("InputBrush"),
                BorderBrush = title == control.SelectedItem
                    ? FindBrush("AccentBorderBrush")
                    : FindBrush("CardBorderBrush"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(7),
                Padding = new Thickness(10, 7, 10, 7),
                Margin = new Thickness(0, 0, 0, 5),
                Cursor = System.Windows.Input.Cursors.Hand,
            };
            var content = new StackPanel();
            var titleText = new TextBlock
            {
                Text = title,
                FontSize = control.FontSize,
                FontWeight = FontWeights.SemiBold,
                TextTrimming = TextTrimming.CharacterEllipsis,
            };
            titleText.SetResourceReference(TextBlock.ForegroundProperty, "TextPrimaryBrush");
            content.Children.Add(titleText);
            if (!string.IsNullOrWhiteSpace(subtitle))
            {
                var subtitleText = new TextBlock
                {
                    Text = subtitle,
                    FontSize = Math.Max(7, control.FontSize - 3),
                    Margin = new Thickness(0, 2, 0, 0),
                    TextTrimming = TextTrimming.CharacterEllipsis,
                };
                subtitleText.SetResourceReference(TextBlock.ForegroundProperty, "TextTertiaryBrush");
                content.Children.Add(subtitleText);
            }

            card.Child = content;
            card.MouseLeftButtonUp += (_, _) =>
            {
                control.SelectedItem = title;
                if (HasScript(control, "项点击"))
                {
                    triggerEvent(control, "项点击");
                }
            };
            stack.Children.Add(card);
        }

        var scroll = new ScrollViewer
        {
            Content = stack,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
        };
        var border = new Border
        {
            Background = FindBrush("InsetSurfaceBrush"),
            BorderBrush = FindBrush("CardBorderBrush"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(control.CornerRadius),
            ClipToBounds = true,
            Child = scroll,
        };
        Bind(border, FrameworkElement.WidthProperty, control, nameof(DesignControl.Width));
        Bind(border, FrameworkElement.HeightProperty, control, nameof(DesignControl.Height));
        return border;
    }

    private static FrameworkElement CreateChart(DesignControl control)
    {
        var values = new List<(string Label, double Value)>();
        foreach (var raw in SplitLines(control.ListItems))
        {
            var parts = raw.Split(':');
            if (parts.Length < 2 ||
                !double.TryParse(parts[1].Trim(), System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.CurrentCulture, out var value))
            {
                continue;
            }

            values.Add((parts[0].Trim(), value));
        }

        var chart = new FrostChartElement
        {
            ChartType = control.ChartType,
            Values = values,
            TextBrush = FindBrush("TextSecondaryBrush"),
            MutedBrush = FindBrush("TextTertiaryBrush"),
            GridBrush = FindBrush("DividerBrush"),
            AccentBrush = FindBrush("AccentBrush"),
            FontFamily = FindFont(),
        };
        Bind(chart, FrameworkElement.WidthProperty, control, nameof(DesignControl.Width));
        Bind(chart, FrameworkElement.HeightProperty, control, nameof(DesignControl.Height));
        return chart;
    }

    private static void AttachHoverAndContextMenu(
        FrameworkElement wrapper,
        FrameworkElement visual,
        DesignControl control,
        Action<DesignControl, string> triggerEvent)
    {
        if (!string.IsNullOrWhiteSpace(control.ToolTipText))
        {
            var toolTip = new ToolTip { Content = control.ToolTipText };
            ToolTipService.SetToolTip(wrapper, toolTip);
            ToolTipService.SetToolTip(visual, toolTip);
        }

        if (string.IsNullOrWhiteSpace(control.ContextMenuItems))
        {
            return;
        }

        var menu = new ContextMenu();
        foreach (var text in SplitLines(control.ContextMenuItems))
        {
            var item = new MenuItem { Header = text };
            var itemText = text;
            item.Click += (_, _) =>
            {
                control.SelectedItem = itemText;
                if (HasScript(control, "菜单点击"))
                {
                    triggerEvent(control, "菜单点击");
                }
            };
            menu.Items.Add(item);
        }

        wrapper.ContextMenu = menu;
        visual.ContextMenu = menu;
    }

    private static List<string> SplitLines(string value) =>
        value.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Trim())
            .Where(line => line.Length > 0)
            .ToList();

    private static List<string> SplitRawLines(string value) =>
        value.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .ToList();

    private static FrameworkElement CreateDivider(DesignControl control)
    {
        var grid = new Grid();
        Bind(grid, FrameworkElement.WidthProperty, control, nameof(DesignControl.Width));
        Bind(grid, FrameworkElement.HeightProperty, control, nameof(DesignControl.Height));

        var line = new Border { Height = 1, VerticalAlignment = VerticalAlignment.Center };
        line.SetResourceReference(Border.BackgroundProperty, "DividerBrush");
        grid.Children.Add(line);
        return grid;
    }

    private static bool HasScript(DesignControl control, string eventName) =>
        control.Scripts.TryGetValue(eventName, out var code) && !string.IsNullOrWhiteSpace(code);

    private static void Bind(
        FrameworkElement target,
        DependencyProperty property,
        DesignControl source,
        string path,
        BindingMode mode = BindingMode.OneWay,
        IValueConverter? converter = null,
        string? format = null,
        UpdateSourceTrigger updateTrigger = UpdateSourceTrigger.Default)
    {
        var binding = new Binding(path)
        {
            Source = source,
            Mode = mode,
            Converter = converter,
            StringFormat = format,
        };
        if (updateTrigger != UpdateSourceTrigger.Default)
        {
            binding.UpdateSourceTrigger = updateTrigger;
        }

        target.SetBinding(property, binding);
    }

    private static Style? FindStyle(string key) =>
        Application.Current.TryFindResource(key) as Style;

    private static Brush FindBrush(string key) =>
        Application.Current.TryFindResource(key) as Brush ?? Brushes.Transparent;

    private static FontFamily FindFont() =>
        Application.Current.TryFindResource("UiFont") as FontFamily ?? new FontFamily("Segoe UI");
}

internal sealed class FrostChartElement : FrameworkElement
{
    private static readonly Color[] Palette =
    [
        Color.FromRgb(0x84, 0xAE, 0xFF),
        Color.FromRgb(0x63, 0xE6, 0xBE),
        Color.FromRgb(0xFF, 0xC4, 0x6B),
        Color.FromRgb(0xFF, 0x7E, 0x94),
        Color.FromRgb(0xA8, 0x8B, 0xFF),
        Color.FromRgb(0x7D, 0xD3, 0xFC),
    ];

    public string ChartType { get; init; } = "Bar";

    public IReadOnlyList<(string Label, double Value)> Values { get; init; } = [];

    public Brush TextBrush { get; init; } = Brushes.White;

    public Brush MutedBrush { get; init; } = Brushes.Gray;

    public Brush GridBrush { get; init; } = Brushes.DimGray;

    public Brush AccentBrush { get; init; } = Brushes.CornflowerBlue;

    public FontFamily FontFamily { get; init; } = new("Segoe UI");

    private readonly List<(Rect Area, string Text)> _hitAreas = [];

    public FrostChartElement()
    {
        ToolTipService.SetInitialShowDelay(this, 80);
        ToolTipService.SetShowDuration(this, 5000);
    }

    protected override void OnMouseMove(System.Windows.Input.MouseEventArgs e)
    {
        base.OnMouseMove(e);
        var point = e.GetPosition(this);
        var hit = _hitAreas.FirstOrDefault(item => item.Area.Contains(point));
        ToolTip = string.IsNullOrWhiteSpace(hit.Text) ? null : hit.Text;
    }

    protected override void OnMouseLeave(System.Windows.Input.MouseEventArgs e)
    {
        base.OnMouseLeave(e);
        ToolTip = null;
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        _hitAreas.Clear();
        var width = ActualWidth;
        var height = ActualHeight;
        if (width < 30 || height < 30)
        {
            return;
        }

        if (Values.Count == 0)
        {
            var empty = Text("暂无数据", 11, MutedBrush);
            dc.DrawText(empty, new Point((width - empty.Width) / 2, (height - empty.Height) / 2));
            return;
        }

        switch (ChartType)
        {
            case "Line":
                DrawLine(dc, width, height);
                break;
            case "Pie":
                DrawPie(dc, width, height);
                break;
            default:
                DrawBar(dc, width, height);
                break;
        }
    }

    private void DrawBar(DrawingContext dc, double width, double height)
    {
        var left = 12.0;
        var right = 12.0;
        var top = 20.0;
        var bottom = 26.0;
        var plotWidth = Math.Max(1, width - left - right);
        var plotHeight = Math.Max(1, height - top - bottom);
        var max = Math.Max(1, Values.Max(item => item.Value));
        var gap = 8.0;
        var barWidth = Math.Max(4, (plotWidth - gap * (Values.Count - 1)) / Values.Count);
        var origin = top + plotHeight;

        for (var index = 0; index < Values.Count; index++)
        {
            var item = Values[index];
            var barHeight = Math.Max(1, item.Value / max * (plotHeight - 16));
            var x = left + index * (barWidth + gap);
            var y = origin - barHeight;
            var brush = new SolidColorBrush(Palette[index % Palette.Length]);
            dc.DrawRoundedRectangle(brush, null, new Rect(x, y, barWidth, barHeight), 4, 4);
            _hitAreas.Add((new Rect(x, top, barWidth, plotHeight), $"{item.Label}：{item.Value:0.#}"));
            var value = Text(item.Value.ToString("0.#"), 8.5, TextBrush);
            dc.DrawText(value, new Point(x + (barWidth - value.Width) / 2, Math.Max(1, y - value.Height - 3)));
            var label = Text(item.Label, 8.5, MutedBrush);
            dc.DrawText(label, new Point(x + (barWidth - label.Width) / 2, origin + 7));
        }

        var linePen = new Pen(GridBrush, 1);
        for (var i = 0; i <= 3; i++)
        {
            var y = top + plotHeight / 3 * i;
            dc.DrawLine(linePen, new Point(left, y), new Point(width - right, y));
        }
    }

    private void DrawLine(DrawingContext dc, double width, double height)
    {
        var left = 14.0;
        var right = 14.0;
        var top = 20.0;
        var bottom = 26.0;
        var plotWidth = Math.Max(1, width - left - right);
        var plotHeight = Math.Max(1, height - top - bottom);
        var max = Math.Max(1, Values.Max(item => item.Value));
        var origin = top + plotHeight;
        var step = Values.Count <= 1 ? 0 : plotWidth / (Values.Count - 1);
        var points = new List<Point>();
        for (var index = 0; index < Values.Count; index++)
        {
            var x = left + step * index;
            var y = origin - Values[index].Value / max * (plotHeight - 12);
            points.Add(new Point(x, y));
            _hitAreas.Add((new Rect(x - 8, y - 8, 16, 16),
                $"{Values[index].Label}：{Values[index].Value:0.#}"));
            var label = Text(Values[index].Label, 8.5, MutedBrush);
            dc.DrawText(label, new Point(x - label.Width / 2, origin + 7));
        }

        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
        {
            context.BeginFigure(points[0], false, false);
            for (var index = 1; index < points.Count; index++)
            {
                context.LineTo(points[index], true, false);
            }
        }

        dc.DrawGeometry(null, new Pen(AccentBrush, 2), geometry);
        foreach (var point in points)
        {
            dc.DrawEllipse(new SolidColorBrush(Palette[0]), null, point, 3.5, 3.5);
        }

        var gridPen = new Pen(GridBrush, 1);
        for (var i = 0; i <= 3; i++)
        {
            var y = top + plotHeight / 3 * i;
            dc.DrawLine(gridPen, new Point(left, y), new Point(width - right, y));
        }
    }

    private void DrawPie(DrawingContext dc, double width, double height)
    {
        var total = Values.Sum(item => Math.Max(0, item.Value));
        if (total <= 0)
        {
            return;
        }

        var radius = Math.Max(10, Math.Min(width * 0.24, height * 0.34));
        var center = new Point(width * 0.32, height / 2);
        var angle = -Math.PI / 2;
        for (var index = 0; index < Values.Count; index++)
        {
            var value = Math.Max(0, Values[index].Value);
            var sweep = value / total * Math.PI * 2;
            var brush = new SolidColorBrush(Palette[index % Palette.Length]);
            if (Values.Count == 1 || sweep >= Math.PI * 2 - 0.0001)
            {
                dc.DrawEllipse(brush, null, center, radius, radius);
            }
            else
            {
                var start = new Point(
                    center.X + Math.Cos(angle) * radius,
                    center.Y + Math.Sin(angle) * radius);
                var end = new Point(
                    center.X + Math.Cos(angle + sweep) * radius,
                    center.Y + Math.Sin(angle + sweep) * radius);
                var geometry = new StreamGeometry();
                using (var context = geometry.Open())
                {
                    context.BeginFigure(center, true, true);
                    context.LineTo(start, true, false);
                    context.ArcTo(end, new Size(radius, radius), 0, sweep > Math.PI,
                        SweepDirection.Clockwise, true, false);
                }

                dc.DrawGeometry(brush, null, geometry);
            }

            angle += sweep;
        }

        var legendX = width * 0.62;
        var legendY = Math.Max(8, height / 2 - Values.Count * 9);
        for (var index = 0; index < Values.Count; index++)
        {
            var brush = new SolidColorBrush(Palette[index % Palette.Length]);
            dc.DrawRoundedRectangle(brush, null, new Rect(legendX, legendY + index * 18 + 3, 8, 8), 2, 2);
            _hitAreas.Add((new Rect(legendX, legendY + index * 18, width - legendX - 4, 16),
                $"{Values[index].Label}：{Values[index].Value:0.#}"));
            var label = Text($"{Values[index].Label}  {Values[index].Value:0.#}", 8.5, TextBrush);
            dc.DrawText(label, new Point(legendX + 14, legendY + index * 18));
        }
    }

    private FormattedText Text(string text, double size, Brush brush) =>
        new(
            text,
            System.Globalization.CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            new Typeface(FontFamily, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal),
            size,
            brush,
            VisualTreeHelper.GetDpi(this).PixelsPerDip);
}
