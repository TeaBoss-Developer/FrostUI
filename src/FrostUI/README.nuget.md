# FrostUI (WPF)

FrostUI 是一套面向 Windows 10 / 11 的 WPF 毛玻璃 UI 框架：真实 DWM 亚克力与云母材质、无边框圆角窗口、双主题、运行时强调色，以及一组可直接复用的玻璃控件。

## 安装

```powershell
# 本地源码打包
dotnet pack src/FrostUI/FrostUI.csproj -c Release -o artifacts
dotnet add package FrostUI --source artifacts

# 或直接引用项目
dotnet add reference src/FrostUI/FrostUI.csproj
```

NuGet 正式源发布后，可直接：

```powershell
dotnet add package FrostUI
```

## 30 秒接入

`App.xaml` 合并资源字典：

```xml
<Application.Resources>
  <ResourceDictionary>
    <ResourceDictionary.MergedDictionaries>
      <ResourceDictionary Source="pack://application:,,,/FrostUI;component/Themes/Colors.Dark.xaml" />
      <ResourceDictionary Source="pack://application:,,,/FrostUI;component/Themes/Controls.xaml" />
    </ResourceDictionary.MergedDictionaries>
  </ResourceDictionary>
</Application.Resources>
```

窗口：

```xml
<frost:FrostWindow
    xmlns:frost="clr-namespace:FrostUI.Controls;assembly=FrostUI"
    Style="{StaticResource FrostWindowStyle}"
    Backdrop="Acrylic"
    TintOpacity="0.28"
    Title="My App" Width="1200" Height="800">
  <Grid>
    <frost:GlassCard Padding="24">
      <!-- 你的内容 -->
    </frost:GlassCard>
  </Grid>
</frost:FrostWindow>
```

主题与强调色：

```csharp
using FrostUI.Theme;

ThemeManager.Initialize();
ThemeManager.SetTheme(FrostThemeMode.Dark);   // Dark / Light
ThemeManager.ToggleTheme();
ThemeManager.SetAccent(Color.FromRgb(0x84, 0xAE, 0xFF));
```

## 包含什么

- `FrostWindow`：DWM Acrylic / Mica / Mica Alt / Blur / None，圆角、拖动、双击最大化、深浅标题栏
- `GlassCard`：渐变玻璃、细高光描边、噪点、环境阴影
- `GlassToggleSwitch`、`ProgressRing`、`Sparkline`、`FrostMessageBox`
- 按钮 / 输入框 / 搜索框 / 滑杆 / 进度条 / 单选药丸 / 导航列表 / 滚动条等控件模板
- `ThemeManager`：双主题 + 强调色预设，运行时重建画刷
- `FrostUI.Demo`：四页示例应用；`FrostUI.Designer`：可视化设计器

## 文档

- 仓库首页：<https://github.com/TeaBoss-Developer/FrostUI>
- 架构说明：`docs/architecture.md`
- 设计器手册：`designer/README.md`
- 新手上路：`docs/FrostUI-Designer-零基础说明书.docx`

## 环境

- .NET 9 SDK（`net9.0-windows`）
- Windows 10 1809+ / Windows 11
- 无第三方依赖

## License

MIT © FrostUI contributors
