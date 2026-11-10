using System.IO;
using System.Text;
using FrostUI.Designer.Models;
using FrostUI.Designer.Scripting;

namespace FrostUI.Designer.Services;

internal static class ScriptSelfTest
{
    public static async Task<int> RunAsync(string reportPath)
    {
        var report = new StringBuilder();
        var failures = 0;

        void Check(string name, bool condition, string detail)
        {
            report.AppendLine($"[{(condition ? "PASS" : "FAIL")}] {name} · {detail}");
            if (!condition)
            {
                failures++;
            }
        }

        var document = new DesignDocument();
        document.Controls.Add(new DesignControl
        {
            Name = "标签1",
            Kind = ControlKind.Label,
            Text = "初始",
            Width = 120,
            Height = 30,
        });

        var logs = new List<string>();
        var runtime = new ScriptRuntime(document)
        {
            Logger = (message, _) => logs.Add(message),
            Toast = _ => { },
        };

        var basicCode =
            """
            设 a: 整数 = 2 + 3
            设 b: 文本 = "结果="
            如果 (a == 5) {
                输出(b + 文本(a))
            } 否则 {
                输出("错误")
            }
            """;
        logs.Clear();
        var basicOk = await runtime.RunAsync(null, "基础", basicCode);
        Check("类型与条件", basicOk && logs.Contains("结果=5"), string.Join(" | ", logs));

        var truncateCode = "设 n: 整数 = 2.9\n输出(文本(n))";
        logs.Clear();
        var truncateOk = await runtime.RunAsync(null, "取整", truncateCode);
        Check("整数类型转换", truncateOk && logs.Contains("2"), string.Join(" | ", logs));

        var counterCode =
            """
            全局 计数器: 整数 = 0
            全局 计数器 = 计数器 + 1
            输出(文本(计数器))
            """;
        logs.Clear();
        await runtime.RunAsync(null, "计数1", counterCode);
        await runtime.RunAsync(null, "计数2", counterCode);
        Check("全局变量跨事件持久", logs.SequenceEqual(["1", "2"]), string.Join(" | ", logs));

        var jsonCode =
            """
            设 原始: 文本 = "{\"name\":\"FrostUI\",\"features\":[\"Acrylic\",\"Mica\"]}"
            设 数据: JSON = JSON.解析(原始)
            输出(文本(JSON.取值(数据, "name")))
            输出(文本(JSON.取值(数据, "features[1]")))
            """;
        logs.Clear();
        var jsonOk = await runtime.RunAsync(null, "JSON", jsonCode);
        Check("JSON 解析与路径", jsonOk && logs.SequenceEqual(["FrostUI", "Mica"]), string.Join(" | ", logs));

        var controlCode =
            """
            标签1.文本 = "完成"
            标签1.宽 = 200
            输出(标签1.文本)
            """;
        logs.Clear();
        var controlOk = await runtime.RunAsync(document.FindControl("标签1"), "控件", controlCode);
        Check(
            "控件属性读写",
            controlOk && document.FindControl("标签1")?.Text == "完成" &&
            Math.Abs((document.FindControl("标签1")?.Width ?? 0) - 200) < 0.1,
            string.Join(" | ", logs));

        var arrayCode =
            """
            设 数据: JSON = JSON.解析("[{\"id\":7},{\"id\":9}]")
            输出(文本(数据[1].id))
            """;
        logs.Clear();
        var arrayOk = await runtime.RunAsync(null, "数组", arrayCode);
        Check("JSON 索引与成员", arrayOk && logs.Contains("9"), string.Join(" | ", logs));

        var invalid = ScriptRuntime.Validate("如果 (1 > 0) { 输出(\"x\")");
        Check("语法错误定位", !invalid.Success && invalid.Message.Contains("第"), invalid.Message);

        var sample = DesignDocument.CreateSample();
        var sampleJson = DesignSerializer.Serialize(sample);
        var roundTrip = DesignSerializer.Deserialize(sampleJson);
        Check(
            "设计文件序列化",
            roundTrip.Controls.Count == sample.Controls.Count &&
            roundTrip.WindowTitle == sample.WindowTitle &&
            roundTrip.FindControl("按钮_计数")?.GetScript("点击").Contains("全局 计数") == true,
            $"{roundTrip.Controls.Count} 个控件，{sampleJson.Length} 字符");

        var pageDocument = DesignSerializer.Deserialize(DesignSerializer.Serialize(sample));
        pageDocument.Pages.Add(new DesignPage { Name = "设置页" });
        pageDocument.ActivatePage(1);
        pageDocument.Controls.Add(ControlCatalog.Create(ControlKind.Button, "按钮_设置", 20, 20));
        var pageSwitchOk = pageDocument.ActivePage.Name == "设置页" &&
                           pageDocument.Controls.Count == 1;
        pageDocument.ActivatePage(0);
        Check(
            "多页面 · 切换",
            pageSwitchOk && pageDocument.ActivePage.Name == "主页面" &&
            pageDocument.Controls.Count == sample.Controls.Count,
            $"{pageDocument.Pages.Count} 个页面");

        var navigationRuntime = new ScriptRuntime(sample);
        string? navigationTarget = null;
        navigationRuntime.Navigate = (action, page) =>
        {
            navigationTarget = $"{action}:{page}";
            return true;
        };
        var navigationOk = await navigationRuntime.RunAsync(null, "启动", "页面.切换(\"设置页\")");
        Check(
            "页面 · 脚本导航",
            navigationOk && navigationTarget == "Switch:设置页",
            navigationTarget ?? "无");

        var pageRuntime = new ScriptRuntime(sample);
        var pageValues = new Dictionary<string, string>();
        pageRuntime.GetPageParameter = key =>
            pageValues.TryGetValue(key, out var value) ? value : null;
        pageRuntime.SetPageParameter = (key, value) => pageValues[key] = value;
        pageRuntime.SetPageResult = value => pageValues["结果"] = value;
        var pageIoLlogs = new List<string>();
        pageRuntime.Logger = (message, _) => pageIoLlogs.Add(message);
        pageRuntime.Toast = _ => { };
        var pageIoOk = await pageRuntime.RunAsync(
            null,
            "启动",
            "页面.传参(\"id\", \"7\")\n输出(页面.参数(\"id\"))\n页面.设置结果(\"完成\")");
        Check(
            "页面 · 参数/返回",
            pageIoOk && pageValues.GetValueOrDefault("id") == "7" &&
            pageValues.GetValueOrDefault("结果") == "完成" &&
            pageIoLlogs.Any(line => line.Contains("7", StringComparison.Ordinal)),
            string.Join(" | ", pageValues.Select(pair => $"{pair.Key}={pair.Value}")));

        var aliasRuntime = new ScriptRuntime(sample);
        string? aliasAction = null;
        aliasRuntime.Navigate = (action, page) =>
        {
            aliasAction = $"{action}:{page}";
            return true;
        };
        var aliasOk = await aliasRuntime.RunAsync(null, "启动", "主页面.打开窗口(\"设置页\")");
        var aliasDiagnostics = ScriptAnalyzer.Analyze(sample, "主页面.打开窗口(\"设置页\")");
        Check(
            "页面名 · 导航别名",
            aliasOk && aliasAction == "Window:设置页" &&
            aliasDiagnostics.All(item => !item.Message.Contains("未知函数")),
            aliasAction ?? "无");

        var projectRuntime = new ScriptRuntime(sample);
        var projectValues = new Dictionary<string, string>();
        projectRuntime.GetProjectValue = key =>
            projectValues.TryGetValue(key, out var value) ? value : null;
        projectRuntime.SetProjectValue = (key, value) => projectValues[key] = value;
        var projectOk = await projectRuntime.RunAsync(
            null,
            "启动",
            "项目.设置(\"主题\", \"dark\")\n输出(项目.获取(\"主题\"))");
        Check(
            "项目 · 共享数据",
            projectOk && projectValues.GetValueOrDefault("主题") == "dark",
            projectValues.GetValueOrDefault("主题") ?? "空");

        ProjectDataStore.Set("__frostui_selftest__", "key", "value");
        var persisted = ProjectDataStore.Get("__frostui_selftest__", "key") == "value";
        ProjectDataStore.Clear("__frostui_selftest__");
        Check("项目 · 共享数据持久化", persisted, "key=value");

        var timelineControl = ControlCatalog.Create(ControlKind.Label, "时间线_测试", 20, 20);
        timelineControl.Timeline.Add(new AnimationKeyframe
        {
            Property = "Opacity",
            TimeMs = 0,
            Value = 0,
            Easing = "EaseOut",
        });
        timelineControl.Timeline.Add(new AnimationKeyframe
        {
            Property = "Opacity",
            TimeMs = 400,
            Value = 1,
            Easing = "EaseOut",
        });
        var timelineClone = timelineControl.Clone("时间线_副本");
        Check(
            "动画时间线 · 关键帧",
            timelineClone.Timeline.Count == 2 &&
            timelineClone.Timeline[1].TimeMs == 400 &&
            timelineClone.Timeline[1].Value == 1,
            $"{timelineClone.Timeline.Count} 个关键帧");

        var formKinds = new[]
        {
            ControlKind.CheckBox, ControlKind.RadioButton, ControlKind.ComboBox,
            ControlKind.PasswordBox, ControlKind.NumberBox, ControlKind.TextArea,
            ControlKind.DatePicker, ControlKind.SearchBox,
        };
        var formCatalogOk = formKinds.All(kind =>
        {
            var created = ControlCatalog.Create(kind, "表单_" + kind, 0, 0);
            return created.Kind == kind &&
                   ControlCatalog.Get(kind).Events.Count > 0 &&
                   ControlCatalog.Get(kind).Title.Length > 0;
        });
        Check("表单控件 · 目录", formCatalogOk, string.Join(",", formKinds.Select(kind => ControlCatalog.Get(kind).Title)));

        var formCheck = ControlCatalog.Create(ControlKind.CheckBox, "复选_测试", 10, 10);
        formCheck.IsOn = true;
        formCheck.GroupName = "测试组";
        var formCheckClone = formCheck.Clone("复选_副本");
        Check("表单控件 · 属性/克隆", formCheckClone.IsOn && formCheckClone.GroupName == "测试组", formCheckClone.GroupName);

        var formCombo = ControlCatalog.Create(ControlKind.ComboBox, "下拉_测试", 10, 60);
        formCombo.SelectedItem = "选项二";
        var formNumber = ControlCatalog.Create(ControlKind.NumberBox, "数字_测试", 10, 110);
        formNumber.Minimum = 1;
        formNumber.Maximum = 9;
        formNumber.Step = 2;
        Check(
            "表单控件 · 下拉/数字",
            formCombo.SelectedItem == "选项二" &&
            formNumber.Minimum == 1 && formNumber.Maximum == 9 && formNumber.Step == 2,
            $"{formCombo.SelectedItem} / {formNumber.Minimum}-{formNumber.Maximum}/{formNumber.Step}");

        var formDocument = new DesignDocument();
        formDocument.Controls.Add(formCheck);
        formDocument.Controls.Add(formCombo);
        formDocument.Controls.Add(formNumber);
        formDocument.Controls.Add(ControlCatalog.Create(ControlKind.DatePicker, "日期_测试", 10, 160));
        formDocument.Controls.Add(ControlCatalog.Create(ControlKind.Label, "标签_结果", 10, 210));
        var formDiagnostics = ScriptAnalyzer.Analyze(
            formDocument,
            "如果 (复选_测试.选中) {\n    标签_结果.文本 = 下拉_测试.选中项\n}\n数字_测试.值 = 数字_测试.最小值 + 数字_测试.步长\n输出(日期_测试.日期)");
        Check(
            "表单控件 · 脚本属性",
            formDiagnostics.Count == 0,
            formDiagnostics.Count == 0 ? "checked/selectedItem/min/step/date" : formDiagnostics[0].Message);

        var layoutKinds = new[]
        {
            ControlKind.Tabs, ControlKind.Expander, ControlKind.SplitView,
            ControlKind.Menu, ControlKind.InfoBar,
        };
        var layoutCatalogOk = layoutKinds.All(kind =>
        {
            var created = ControlCatalog.Create(kind, "布局_" + kind, 0, 0);
            return created.Kind == kind &&
                   ControlCatalog.Get(kind).Events.Count > 0 &&
                   ControlCatalog.Get(kind).Title.Length > 0;
        });
        Check("布局控件 · 目录", layoutCatalogOk, string.Join(",", layoutKinds.Select(kind => ControlCatalog.Get(kind).Title)));

        var tabs = ControlCatalog.Create(ControlKind.Tabs, "标签页_测试", 10, 10);
        tabs.SelectedIndex = 1;
        tabs.SelectedItem = "标签二";
        var tabChild = ControlCatalog.Create(ControlKind.Label, "标签页子项", 10, 10);
        tabChild.ParentName = tabs.Name;
        tabChild.TabIndex = 2;
        var splitChild = ControlCatalog.Create(ControlKind.Label, "分栏子项", 10, 10);
        splitChild.ParentName = "分栏_测试";
        splitChild.PaneIndex = 1;
        var info = ControlCatalog.Create(ControlKind.InfoBar, "通知_测试", 10, 10);
        info.Severity = "Warning";
        info.ToolTipText = "提示";
        info.ContextMenuItems = "复制\n粘贴";
        var infoClone = info.Clone("通知_副本");
        Check(
            "布局控件 · 属性/克隆",
            tabs.SelectedIndex == 1 && tabChild.TabIndex == 2 && !tabChild.IsCanvasVisible &&
            splitChild.PaneIndex == 1 && infoClone.Severity == "Warning" &&
            infoClone.ToolTipText == "提示" && infoClone.ContextMenuItems.Contains("复制"),
            $"{tabs.SelectedItem} / tab={tabChild.TabIndex} / pane={splitChild.PaneIndex}");

        var layoutDocument = new DesignDocument();
        layoutDocument.Controls.Add(tabs);
        layoutDocument.Controls.Add(ControlCatalog.Create(ControlKind.Expander, "折叠_测试", 10, 10));
        layoutDocument.Controls.Add(info);
        layoutDocument.Controls.Add(ControlCatalog.Create(ControlKind.Label, "标签_结果", 10, 210));
        var layoutDiagnostics = ScriptAnalyzer.Analyze(
            layoutDocument,
            "如果 (折叠_测试.开启) {\n    标签_结果.文本 = 标签页_测试.选中项\n}\n通知_测试.级别 = \"Error\"\n输出(文本(标签页_测试.当前标签页))");
        Check(
            "布局控件 · 脚本属性",
            layoutDiagnostics.Count == 0,
            layoutDiagnostics.Count == 0 ? "selectedItem/currentTab/expanded/severity" : layoutDiagnostics[0].Message);

        var dataKinds = new[]
        {
            ControlKind.DataGrid, ControlKind.TreeView, ControlKind.Repeater, ControlKind.Chart,
        };
        var dataCatalogOk = dataKinds.All(kind =>
        {
            var created = ControlCatalog.Create(kind, "数据_" + kind, 0, 0);
            return created.Kind == kind &&
                   ControlCatalog.Get(kind).Events.Count > 0 &&
                   ControlCatalog.Get(kind).Title.Length > 0;
        });
        Check("数据控件 · 目录", dataCatalogOk, string.Join(",", dataKinds.Select(kind => ControlCatalog.Get(kind).Title)));

        var gridControl = ControlCatalog.Create(ControlKind.DataGrid, "表格_测试", 10, 10);
        gridControl.SelectedItem = "香蕉";
        gridControl.TableColumns = "名称\n数量";
        gridControl.GridEditable = false;
        gridControl.GridSortable = false;
        var gridClone = gridControl.Clone("表格_副本");
        var chartControl = ControlCatalog.Create(ControlKind.Chart, "图表_测试", 10, 10);
        chartControl.ChartType = "Pie";
        chartControl.ListItems = "A:1\nB:2";
        var chartClone = chartControl.Clone("图表_副本");
        Check(
            "数据控件 · 属性/克隆",
            gridClone.SelectedItem == "香蕉" && gridClone.TableColumns.Contains("数量") &&
            !gridClone.GridEditable && !gridClone.GridSortable &&
            chartClone.ChartType == "Pie" && chartClone.ListItems.Contains("B:2"),
            $"{gridClone.SelectedItem} / {chartClone.ChartType}");

        var dataDocument = new DesignDocument();
        dataDocument.Controls.Add(gridControl);
        dataDocument.Controls.Add(ControlCatalog.Create(ControlKind.TreeView, "树形_测试", 10, 10));
        dataDocument.Controls.Add(ControlCatalog.Create(ControlKind.Repeater, "重复_测试", 10, 10));
        dataDocument.Controls.Add(chartControl);
        dataDocument.Controls.Add(ControlCatalog.Create(ControlKind.Label, "标签_结果", 10, 210));
        var dataDiagnostics = ScriptAnalyzer.Analyze(
            dataDocument,
            "标签_结果.文本 = 表格_测试.选中项 + \" / \" + 树形_测试.选中项 + \" / \" + 重复_测试.选中项\n图表_测试.图表类型 = \"Line\"\n输出(表格_测试.表格列)");
        Check(
            "数据控件 · 脚本属性",
            dataDiagnostics.Count == 0,
            dataDiagnostics.Count == 0 ? "selectedItem/tableColumns/chartType" : dataDiagnostics[0].Message);

        var accentControl = ControlCatalog.Create(ControlKind.Button, "颜色_测试", 10, 10);
        accentControl.AccentColor = "#63E6BE";
        var accentClone = accentControl.Clone("颜色_副本");
        var themeSource = new DesignDocument
        {
            AccentColor = "#FF9BC7",
        };
        themeSource.ThemePalette.Add("#63E6BE");
        themeSource.ThemePalette.Add("#84AEFF");
        var themedDocument = DesignSerializer.Deserialize(DesignSerializer.Serialize(themeSource));
        Check(
            "主题色 · 控件/文档/预设序列化",
            accentClone.AccentColor == "#63E6BE" &&
            ThemeColorHelper.TryParse(accentClone.AccentColor, out var parsedAccent) &&
            ThemeColorHelper.ToHex(parsedAccent) == "#63E6BE" &&
            themedDocument.AccentColor == "#FF9BC7" &&
            themedDocument.ThemePalette.Count == 2,
            $"{accentClone.AccentColor} / {themedDocument.AccentColor} / {themedDocument.ThemePalette.Count} presets");

        pageDocument.ActivePage.Transition = "SlideLeft";
        pageDocument.ActivePage.TransitionDurationMs = 420;
        var transitionRoundTrip = DesignSerializer.Deserialize(DesignSerializer.Serialize(pageDocument));
        Check(
            "页面转场 · 序列化",
            transitionRoundTrip.ActivePage.Transition == "SlideLeft" &&
            transitionRoundTrip.ActivePage.TransitionDurationMs == 420,
            $"{transitionRoundTrip.ActivePage.Transition} / {transitionRoundTrip.ActivePage.TransitionDurationMs}ms");

        var pageCopy = pageDocument.ActivePage.Clone();
        Check(
            "页面 · 复制",
            pageCopy.Controls.Count == pageDocument.ActivePage.Controls.Count &&
            pageCopy.Id != pageDocument.ActivePage.Id,
            $"{pageCopy.Controls.Count} 个控件");

        var overlayDocument = DesignSerializer.Deserialize(DesignSerializer.Serialize(sample));
        overlayDocument.Pages.Add(new DesignPage
        {
            Name = "确认弹窗",
            Kind = DesignPageKind.Dialog,
            DialogDisplay = "Overlay",
        });
        var overlayWindow = new Views.PreviewWindow(overlayDocument);
        var overlayMethod = typeof(Views.PreviewWindow).GetMethod(
            "ShowOverlayPage",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        overlayMethod?.Invoke(overlayWindow, [1]);
        var overlay = overlayWindow.FindName("DialogOverlay") as System.Windows.FrameworkElement;
        Check(
            "内嵌遮罩弹窗 · 打开",
            overlay?.Visibility == System.Windows.Visibility.Visible,
            overlay?.Visibility.ToString() ?? "未找到遮罩");
        overlayWindow.Close();

        var sampleRuntime = new ScriptRuntime(sample)
        {
            Logger = (message, _) => logs.Add(message),
            Toast = _ => { },
        };
        var counterButton = sample.FindControl("按钮_计数")!;
        logs.Clear();
        await sampleRuntime.RunAsync(counterButton, "点击", counterButton.GetScript("点击"));
        await sampleRuntime.RunAsync(counterButton, "点击", counterButton.GetScript("点击"));
        Check(
            "示例点击计数",
            sample.FindControl("标签_结果")?.Text == "点击次数：2",
            sample.FindControl("标签_结果")?.Text ?? "missing");

        var jsonButton = sample.FindControl("按钮_JSON")!;
        await sampleRuntime.RunAsync(jsonButton, "点击", jsonButton.GetScript("点击"));
        Check(
            "示例 JSON 脚本",
            sample.FindControl("标签_结果")?.Text == "JSON：FrostUI · Mica",
            sample.FindControl("标签_结果")?.Text ?? "missing");

        var greetButton = sample.FindControl("按钮_问候")!;
        await sampleRuntime.RunAsync(greetButton, "点击", greetButton.GetScript("点击"));
        Check(
            "示例条件问候",
            sample.FindControl("标签_问候")?.Text == "你好，茶总！",
            sample.FindControl("标签_问候")?.Text ?? "missing");

        var rootCompletion = ScriptCompletionService.GetCompletions(sample, "按", 1);
        Check(
            "联想 · 控件根建议",
            rootCompletion.Items.Any(x => x.Display == "按钮_计数"),
            string.Join(", ", rootCompletion.Items.Take(5).Select(x => x.Display)));

        var propertyCompletion = ScriptCompletionService.GetCompletions(sample, "按钮_计数.文", "按钮_计数.文".Length);
        Check(
            "联想 · 控件属性",
            propertyCompletion.BaseName == "按钮_计数" &&
            propertyCompletion.Items.Any(x => x.Display == "文本"),
            string.Join(", ", propertyCompletion.Items.Select(x => x.Display)));

        var jsonCompletion = ScriptCompletionService.GetCompletions(sample, "JSON.", 5);
        Check(
            "联想 · JSON 方法",
            jsonCompletion.Items.Any(x => x.Display == "解析") &&
            jsonCompletion.Items.Any(x => x.Display == "取值"),
            string.Join(", ", jsonCompletion.Items.Select(x => x.Display)));

        var httpCompletion = ScriptCompletionService.GetCompletions(sample, "HTTP.", 5);
        Check(
            "联想 · HTTP 方法",
            httpCompletion.Items.Any(x => x.Display == "获取JSON"),
            string.Join(", ", httpCompletion.Items.Select(x => x.Display)));

        var typeCompletion = ScriptCompletionService.GetCompletions(sample, "设 x: ", 5);
        Check(
            "联想 · 类型上下文",
            typeCompletion.IsTypeContext && typeCompletion.Items.Any(x => x.Display == "文本"),
            string.Join(", ", typeCompletion.Items.Select(x => x.Display)));

        var stringCompletion = ScriptCompletionService.GetCompletions(sample, "\"JSON.\"", 6);
        Check(
            "联想 · 字符串内关闭",
            stringCompletion.IsInStringOrComment && stringCompletion.Items.Count == 0,
            $"inString={stringCompletion.IsInStringOrComment}, items={stringCompletion.Items.Count}");

        var historyDocument = DesignDocument.CreateSample();
        var history = new DesignHistory();
        history.Reset(historyDocument, "按钮_计数");
        var historyButton = historyDocument.FindControl("按钮_计数")!;
        historyButton.X = 68;
        history.Capture(historyDocument, "按钮_计数", "移动控件");
        var undone = history.Undo();
        var redone = history.Redo();
        Check(
            "历史 · 撤销/重做",
            undone is not null &&
            redone is not null &&
            DesignSerializer.Deserialize(undone.Json).FindControl("按钮_计数")?.X == 60 &&
            DesignSerializer.Deserialize(redone.Json).FindControl("按钮_计数")?.X == 68,
            $"undo={(undone is null ? "null" : "ok")}, redo={(redone is null ? "null" : "ok")}");

        var guideDocument = DesignDocument.CreateSample();
        guideDocument.Guides.Add(new DesignGuide(GuideOrientation.Vertical, 100));
        guideDocument.Guides.Add(new DesignGuide(GuideOrientation.Horizontal, 220));
        var guideRoundTrip = DesignSerializer.Deserialize(
            DesignSerializer.Serialize(guideDocument));
        Check(
            "参考线序列化",
            guideRoundTrip.Guides.Count == 2 &&
            guideRoundTrip.Guides[0].Orientation == GuideOrientation.Vertical &&
            guideRoundTrip.Guides[0].Position == 100 &&
            guideRoundTrip.Guides[1].Position == 220,
            $"{guideRoundTrip.Guides.Count} 条参考线");

        var flowRuntime = new ScriptRuntime(sample)
        {
            Logger = (message, _) => logs.Add(message),
            Toast = _ => { },
        };
        var flowCode =
            """
            函数 求和(a: 整数, b: 整数) {
                返回 a + b
            }
            设 总数: 整数 = 0
            循环 3 {
                总数 = 总数 + 1
            }
            遍历 项 于 [1, 2, 3] {
                总数 = 总数 + 项
            }
            设 结果: 整数 = 求和(总数, 5)
            输出(文本(结果))
            """;
        logs.Clear();
        var flowOk = await flowRuntime.RunAsync(null, "启动", flowCode);
        Check(
            "循环 / 遍历 / 函数 / 列表",
            flowOk && logs.Any(x => x.Contains("14", StringComparison.Ordinal)),
            string.Join(" | ", logs));

        var controlFlowLogs = new List<string>();
        var controlFlowRuntime = new ScriptRuntime(sample)
        {
            Logger = (message, _) => controlFlowLogs.Add(message),
            Toast = _ => { },
        };
        var controlFlowCode =
            """
            设 总计: 整数 = 0
            循环 5 {
                如果 总计 == 1 那么 {
                    总计 = 总计 + 1
                    继续
                }
                如果 总计 == 3 那么 {
                    跳出
                }
                总计 = 总计 + 1
            }
            输出(文本(总计))
            """;
        var controlFlowOk = await controlFlowRuntime.RunAsync(null, "启动", controlFlowCode);
        Check(
            "跳出 / 继续",
            controlFlowOk && controlFlowLogs.Any(x => x.Contains("3", StringComparison.Ordinal)),
            string.Join(" | ", controlFlowLogs));

        var dictionaryLogs = new List<string>();
        var dictionaryRuntime = new ScriptRuntime(sample)
        {
            Logger = (message, _) => dictionaryLogs.Add(message),
            Toast = _ => { },
        };
        var dictionaryOk = await dictionaryRuntime.RunAsync(
            null,
            "启动",
            "设 数据: JSON = {\"名称\": \"FrostUI\", \"版本\": 1}\n输出(文本(数据.名称))");
        Check(
            "字典字面量",
            dictionaryOk && dictionaryLogs.Any(x => x.Contains("FrostUI", StringComparison.Ordinal)),
            string.Join(" | ", dictionaryLogs));

        var analyzer = ScriptAnalyzer.Analyze(
            sample,
            "按钮_计数.不存在 = 1\n未知对象.文本 = \"x\"\n未知函数(1)\n设 数量: 整数 = \"abc\"\n按钮_计数.文本 = 123");
        Check(
            "脚本静态分析",
            analyzer.Any(x => x.Message.Contains("没有属性", StringComparison.Ordinal)) &&
            analyzer.Any(x => x.Message.Contains("未定义", StringComparison.Ordinal)) &&
            analyzer.Any(x => x.Message.Contains("未知函数", StringComparison.Ordinal)) &&
            analyzer.Any(x => x.Message.Contains("期望 整数", StringComparison.Ordinal)) &&
            analyzer.Any(x => x.Message.Contains("属性", StringComparison.Ordinal) &&
                             x.Message.Contains("期望 文本", StringComparison.Ordinal)),
            string.Join(" | ", analyzer.Select(x => x.Display)));

        var bindingRuntime = new ScriptRuntime(sample);
        await bindingRuntime.RunAsync(null, "启动", "全局 绑定值: 文本 = \"已绑定\"");
        Check(
            "数据绑定 · 全局变量",
            bindingRuntime.TryGetGlobalValue("绑定值", out var bound) && bound == "已绑定",
            bound);

        var debugRuntime = new ScriptRuntime(sample);
        var debugSession = new ScriptDebugSession();
        var debugLines = new List<int>();
        debugSession.OnPause = snapshot =>
        {
            debugLines.Add(snapshot.Line);
            debugSession.Step();
            return Task.CompletedTask;
        };
        debugRuntime.DebugSession = debugSession;
        var debugOk = await debugRuntime.RunAsync(
            null,
            "启动",
            "设 调试值: 整数 = 1\n输出(文本(调试值))");
        Check(
            "调试 · 暂停/单步",
            debugOk && debugLines.Count >= 2,
            string.Join(",", debugLines));

        var conditionSnapshot = new DebugSnapshot(
            5,
            new Dictionary<string, string> { ["计数"] = "3" },
            []);
        Check(
            "条件断点 · 比较",
            BreakpointCondition.Matches("计数 >= 3", conditionSnapshot) &&
            !BreakpointCondition.Matches("计数 > 5", conditionSnapshot),
            "计数 >= 3");

        var logRuntime = new ScriptRuntime(sample);
        var logSession = new ScriptDebugSession { StepMode = false };
        logSession.SetBreakpoints([new BreakpointSpec(2, null, true)]);
        var logCount = 0;
        logSession.OnLog = (_, _) => logCount++;
        logRuntime.DebugSession = logSession;
        var logOk = await logRuntime.RunAsync(
            null,
            "启动",
            "设 日志值: 整数 = 7\n输出(文本(日志值))");
        Check(
            "日志断点 · 自动继续",
            logOk && logCount == 1,
            logCount.ToString());

        if (Environment.GetEnvironmentVariable("FROSTUI_HTTP_TEST") == "1")
        {
            var httpCode =
                """
                设 数据: JSON = HTTP.获取JSON("https://api.github.com/repos/dotnet/wpf")
                输出(文本(JSON.取值(数据, "full_name")))
                """;
            logs.Clear();
            var httpOk = await runtime.RunAsync(null, "HTTP", httpCode);
            Check("HTTP GET + JSON", httpOk && logs.Any(x => x.Contains("dotnet/wpf")), string.Join(" | ", logs));
        }

        var reportText = report.ToString();
        var directory = Path.GetDirectoryName(Path.GetFullPath(reportPath));
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(reportPath, reportText);
        return failures == 0 ? 0 : 1;
    }
}
