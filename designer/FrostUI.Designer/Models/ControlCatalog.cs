namespace FrostUI.Designer.Models;

public static class ControlCatalog
{
    private static readonly ControlDefinition[] Definitions =
    [
        new()
        {
            Kind = ControlKind.Button,
            Title = "按钮",
            Icon = "按",
            Description = "触发点击逻辑",
            Category = "基础",
            NamePrefix = "按钮",
            DefaultText = "按钮",
            DefaultWidth = 120,
            DefaultHeight = 36,
            Events = ["点击"],
        },
        new()
        {
            Kind = ControlKind.Label,
            Title = "标签",
            Icon = "字",
            Description = "标题与说明文字",
            Category = "基础",
            NamePrefix = "标签",
            DefaultText = "标签文字",
            DefaultWidth = 180,
            DefaultHeight = 30,
            DefaultFontSize = 15,
            Events = ["点击"],
        },
        new()
        {
            Kind = ControlKind.TextBox,
            Title = "输入框",
            Icon = "入",
            Description = "接收用户文本",
            Category = "基础",
            NamePrefix = "输入框",
            DefaultText = "请输入…",
            DefaultWidth = 220,
            DefaultHeight = 34,
            Events = ["文本改变"],
        },
        new()
        {
            Kind = ControlKind.CheckBox,
            Title = "复选框",
            Icon = "勾",
            Description = "多选 / 记住选项",
            Category = "表单",
            NamePrefix = "复选",
            DefaultText = "记住我",
            DefaultWidth = 160,
            DefaultHeight = 30,
            Events = ["切换"],
        },
        new()
        {
            Kind = ControlKind.RadioButton,
            Title = "单选框",
            Icon = "圆",
            Description = "同一组内单选",
            Category = "表单",
            NamePrefix = "单选",
            DefaultText = "选项一",
            DefaultWidth = 160,
            DefaultHeight = 30,
            Events = ["切换"],
        },
        new()
        {
            Kind = ControlKind.ComboBox,
            Title = "下拉框",
            Icon = "下",
            Description = "从选项中选择一项",
            Category = "表单",
            NamePrefix = "下拉",
            DefaultText = "请选择",
            DefaultWidth = 200,
            DefaultHeight = 34,
            DefaultListItems = "选项一\n选项二\n选项三",
            DefaultSelectedItem = "选项一",
            Events = ["选择改变"],
        },
        new()
        {
            Kind = ControlKind.PasswordBox,
            Title = "密码框",
            Icon = "密",
            Description = "隐藏输入的文本",
            Category = "表单",
            NamePrefix = "密码",
            DefaultText = "密码",
            DefaultWidth = 220,
            DefaultHeight = 34,
            Events = ["文本改变"],
        },
        new()
        {
            Kind = ControlKind.NumberBox,
            Title = "数字框",
            Icon = "数",
            Description = "数字输入 / 上下限",
            Category = "表单",
            NamePrefix = "数字",
            DefaultValue = 0,
            DefaultWidth = 140,
            DefaultHeight = 34,
            DefaultMinimum = 0,
            DefaultMaximum = 100,
            DefaultStep = 1,
            Events = ["值改变"],
        },
        new()
        {
            Kind = ControlKind.TextArea,
            Title = "多行文本",
            Icon = "段",
            Description = "备注 / 说明 / 多行输入",
            Category = "表单",
            NamePrefix = "多行",
            DefaultText = "请输入多行文本…",
            DefaultWidth = 260,
            DefaultHeight = 100,
            Events = ["文本改变"],
        },
        new()
        {
            Kind = ControlKind.DatePicker,
            Title = "日期",
            Icon = "日",
            Description = "选择日期",
            Category = "表单",
            NamePrefix = "日期",
            DefaultText = DateTime.Today.ToString("yyyy-MM-dd"),
            DefaultWidth = 180,
            DefaultHeight = 34,
            Events = ["日期改变"],
        },
        new()
        {
            Kind = ControlKind.SearchBox,
            Title = "搜索框",
            Icon = "搜",
            Description = "带清空和回车提交",
            Category = "表单",
            NamePrefix = "搜索",
            DefaultText = "搜索…",
            DefaultWidth = 220,
            DefaultHeight = 34,
            Events = ["文本改变", "提交"],
        },
        new()
        {
            Kind = ControlKind.Card,
            Title = "卡片",
            Icon = "卡",
            Description = "玻璃内容容器",
            Category = "基础",
            NamePrefix = "卡片",
            DefaultText = "玻璃卡片",
            DefaultWidth = 320,
            DefaultHeight = 180,
            DefaultFontSize = 16,
            DefaultCornerRadius = 16,
            Events = ["点击"],
        },
        new()
        {
            Kind = ControlKind.Divider,
            Title = "分隔线",
            Icon = "线",
            Description = "内容分隔",
            Category = "基础",
            NamePrefix = "分隔线",
            DefaultWidth = 300,
            DefaultHeight = 8,
            Events = ["点击"],
        },
        new()
        {
            Kind = ControlKind.Image,
            Title = "图片",
            Icon = "图",
            Description = "本地图片 / 图标",
            Category = "媒体",
            NamePrefix = "图片",
            DefaultText = "图片",
            DefaultWidth = 220,
            DefaultHeight = 140,
            Events = ["点击"],
        },
        new()
        {
            Kind = ControlKind.Stack,
            Title = "Stack",
            Icon = "栈",
            Description = "纵向/横向堆叠容器",
            Category = "容器",
            NamePrefix = "Stack",
            DefaultWidth = 320,
            DefaultHeight = 240,
            Events = ["点击"],
        },
        new()
        {
            Kind = ControlKind.Grid,
            Title = "Grid",
            Icon = "格",
            Description = "行列网格容器",
            Category = "容器",
            NamePrefix = "Grid",
            DefaultWidth = 320,
            DefaultHeight = 240,
            Events = ["点击"],
        },
        new()
        {
            Kind = ControlKind.Dock,
            Title = "Dock",
            Icon = "泊",
            Description = "上下左右停靠容器",
            Category = "容器",
            NamePrefix = "Dock",
            DefaultWidth = 320,
            DefaultHeight = 240,
            Events = ["点击"],
        },
        new()
        {
            Kind = ControlKind.Scroll,
            Title = "Scroll",
            Icon = "滚",
            Description = "可滚动容器",
            Category = "容器",
            NamePrefix = "Scroll",
            DefaultWidth = 320,
            DefaultHeight = 240,
            Events = ["点击"],
        },
        new()
        {
            Kind = ControlKind.Tabs,
            Title = "标签页",
            Icon = "页",
            Description = "多标签切换内容",
            Category = "布局",
            NamePrefix = "标签页",
            DefaultText = "标签页",
            DefaultWidth = 420,
            DefaultHeight = 260,
            DefaultListItems = "标签一\n标签二",
            Events = ["标签切换"],
        },
        new()
        {
            Kind = ControlKind.Expander,
            Title = "折叠面板",
            Icon = "展",
            Description = "展开 / 收起内容",
            Category = "布局",
            NamePrefix = "折叠",
            DefaultText = "高级设置",
            DefaultWidth = 320,
            DefaultHeight = 180,
            DefaultIsOn = true,
            Events = ["展开改变"],
        },
        new()
        {
            Kind = ControlKind.SplitView,
            Title = "分栏",
            Icon = "分",
            Description = "左右两栏 + 拖动分隔",
            Category = "布局",
            NamePrefix = "分栏",
            DefaultWidth = 420,
            DefaultHeight = 260,
            Events = ["点击"],
        },
        new()
        {
            Kind = ControlKind.Menu,
            Title = "菜单",
            Icon = "单",
            Description = "横向菜单 / 右键菜单",
            Category = "交互",
            NamePrefix = "菜单",
            DefaultText = "菜单",
            DefaultWidth = 280,
            DefaultHeight = 38,
            DefaultListItems = "文件\n编辑\n帮助",
            DefaultSelectedItem = "文件",
            Events = ["菜单点击"],
        },
        new()
        {
            Kind = ControlKind.InfoBar,
            Title = "通知条",
            Icon = "通",
            Description = "成功 / 警告 / 错误提示",
            Category = "反馈",
            NamePrefix = "通知",
            DefaultText = "这是一条提示信息",
            DefaultWidth = 360,
            DefaultHeight = 48,
            Events = ["关闭", "点击"],
        },
        new()
        {
            Kind = ControlKind.DataGrid,
            Title = "数据表格",
            Icon = "表",
            Description = "多列表格，逗号/竖线分隔",
            Category = "数据",
            NamePrefix = "表格",
            DefaultWidth = 460,
            DefaultHeight = 220,
            DefaultColumns = "名称\n状态\n数量",
            DefaultListItems = "苹果|正常|12\n香蕉|缺货|0\n橙子|正常|8",
            Events = ["行点击", "单元格改变", "点击"],
        },
        new()
        {
            Kind = ControlKind.TreeView,
            Title = "树形列表",
            Icon = "树",
            Description = "缩进表示层级",
            Category = "数据",
            NamePrefix = "树形",
            DefaultWidth = 280,
            DefaultHeight = 220,
            DefaultListItems = "项目\n  文档\n    设计稿\n  图片\n设置",
            Events = ["选择改变", "点击"],
        },
        new()
        {
            Kind = ControlKind.Repeater,
            Title = "重复卡片",
            Icon = "复",
            Description = "每行数据生成一张卡片",
            Category = "数据",
            NamePrefix = "重复",
            DefaultWidth = 340,
            DefaultHeight = 230,
            DefaultListItems = "设计稿|待审核\n图标包|已完成\n组件库|进行中",
            Events = ["项点击", "点击"],
        },
        new()
        {
            Kind = ControlKind.Chart,
            Title = "图表",
            Icon = "图",
            Description = "柱状 / 折线 / 饼图",
            Category = "数据",
            NamePrefix = "图表",
            DefaultWidth = 380,
            DefaultHeight = 240,
            DefaultListItems = "一月:12\n二月:18\n三月:9\n四月:22",
            DefaultChartType = "Bar",
            Events = ["点击"],
        },
        new()
        {
            Kind = ControlKind.ToggleSwitch,
            Title = "开关",
            Icon = "开",
            Description = "二态切换",
            Category = "交互",
            NamePrefix = "开关",
            DefaultWidth = 52,
            DefaultHeight = 28,
            Events = ["切换"],
        },
        new()
        {
            Kind = ControlKind.Slider,
            Title = "滑杆",
            Icon = "滑",
            Description = "连续数值输入",
            Category = "交互",
            NamePrefix = "滑杆",
            DefaultWidth = 220,
            DefaultHeight = 22,
            DefaultValue = 50,
            Events = ["值改变"],
        },
        new()
        {
            Kind = ControlKind.List,
            Title = "列表",
            Icon = "列",
            Description = "按行渲染列表",
            Category = "数据",
            NamePrefix = "列表",
            DefaultText = "项目一\n项目二",
            DefaultWidth = 260,
            DefaultHeight = 160,
            Events = ["点击"],
        },
        new()
        {
            Kind = ControlKind.ProgressBar,
            Title = "进度条",
            Icon = "进",
            Description = "线性进度",
            Category = "数据",
            NamePrefix = "进度条",
            DefaultWidth = 220,
            DefaultHeight = 6,
            DefaultValue = 60,
            Events = ["点击"],
        },
        new()
        {
            Kind = ControlKind.ProgressRing,
            Title = "环形进度",
            Icon = "环",
            Description = "环形百分比",
            Category = "数据",
            NamePrefix = "环形",
            DefaultWidth = 96,
            DefaultHeight = 96,
            DefaultValue = 72,
            Events = ["点击"],
        },
    ];

    public static IReadOnlyList<ControlDefinition> All { get; } = Definitions;

    public static IReadOnlyList<ControlDefinition> Basic { get; } =
        Definitions.Where(x => x.Category is "基础" or "媒体" or "容器" or "布局").ToArray();

    public static IReadOnlyList<ControlDefinition> Interactive { get; } =
        Definitions.Where(x => x.Category is "表单" or "交互" or "数据" or "反馈").ToArray();

    public static ControlDefinition Get(ControlKind kind) =>
        Definitions.First(x => x.Kind == kind);

    public static DesignControl Create(ControlKind kind, string name, double x, double y)
    {
        var definition = Get(kind);
        var control = new DesignControl
        {
            Name = name,
            Kind = kind,
            X = Math.Round(x),
            Y = Math.Round(y),
            Width = definition.DefaultWidth,
            Height = definition.DefaultHeight,
            Text = definition.DefaultText,
            FontSize = definition.DefaultFontSize,
            Value = definition.DefaultValue,
            SelectedItem = definition.DefaultSelectedItem,
            TableColumns = string.IsNullOrWhiteSpace(definition.DefaultColumns)
                ? "名称\n状态\n数量"
                : definition.DefaultColumns,
            ChartType = definition.DefaultChartType,
            Minimum = definition.DefaultMinimum,
            Maximum = definition.DefaultMaximum,
            Step = definition.DefaultStep,
            CornerRadius = definition.DefaultCornerRadius,
            IsVisible = true,
            IsEnabled = true,
            IsOn = definition.DefaultIsOn,
            Scripts = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [definition.PrimaryEvent] = DefaultScript(definition, name, definition.PrimaryEvent),
            },
        };

        if (!string.IsNullOrWhiteSpace(definition.DefaultListItems))
        {
            control.ListItems = definition.DefaultListItems;
        }

        if (kind == ControlKind.Menu)
        {
            control.StackOrientation = "Horizontal";
        }

        return control;
    }

    public static string NextName(ControlKind kind, IEnumerable<DesignControl> existing)
    {
        var prefix = Get(kind).NamePrefix;
        var names = existing.Select(x => x.Name).ToHashSet(StringComparer.Ordinal);
        for (var index = 1; index < 10_000; index++)
        {
            var candidate = $"{prefix}{index}";
            if (!names.Contains(candidate))
            {
                return candidate;
            }
        }

        return $"{prefix}{Guid.NewGuid():N}";
    }

    public static string DefaultScript(ControlDefinition definition, string controlName, string eventName)
    {
        return eventName switch
        {
            "点击" => $"// {controlName} · 点击\n输出(\"{controlName} 被点击了\")\n",
            "文本改变" => $"// {controlName} · 文本改变\n输出(\"{controlName}：\" + {controlName}.文本)\n",
            "切换" => $"// {controlName} · 切换\n如果 ({controlName}.开启) {{\n    输出(\"{controlName} 已选中\")\n}} 否则 {{\n    输出(\"{controlName} 已取消\")\n}}\n",
            "值改变" => $"// {controlName} · 值改变\n输出(\"{controlName}：\" + 文本({controlName}.值))\n",
            "选择改变" => $"// {controlName} · 选择改变\n输出(\"{controlName}：\" + {controlName}.选中项)\n",
            "日期改变" => $"// {controlName} · 日期改变\n输出(\"{controlName}：\" + {controlName}.文本)\n",
            "提交" => $"// {controlName} · 提交\n输出(\"搜索：\" + {controlName}.文本)\n",
            "标签切换" => $"// {controlName} · 标签切换\n输出(\"{controlName}：\" + {controlName}.选中项)\n",
            "展开改变" => $"// {controlName} · 展开改变\n如果 ({controlName}.开启) {{\n    输出(\"{controlName} 已展开\")\n}} 否则 {{\n    输出(\"{controlName} 已收起\")\n}}\n",
            "菜单点击" => $"// {controlName} · 菜单点击\n输出(\"菜单：\" + {controlName}.选中项)\n",
            "关闭" => $"// {controlName} · 关闭\n{controlName}.可见 = 假\n",
            "行点击" => $"// {controlName} · 行点击\n输出(\"选中行：\" + {controlName}.选中项)\n",
            "单元格改变" => $"// {controlName} · 单元格改变\n输出(\"表格已修改\")\n",
            "项点击" => $"// {controlName} · 项点击\n输出(\"选中项：\" + {controlName}.选中项)\n",
            _ => $"// {controlName} · {eventName}\n输出(\"{controlName} · {eventName}\")\n",
        };
    }
}
