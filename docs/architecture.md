# FrostUI 架构说明

本文记录 FrostUI 的关键实现路径，方便二次开发时判断“应该改哪一层”。

## 1. 渲染层次

一个 FrostUI 窗口从底到顶大致是：

```
桌面 / 其它窗口
        │
        ▼
DWM 合成器（Acrylic / Mica / Blur）        ← 原生材质，由 Windows 绘制
        │
        ▼
WPF 窗口表面（WindowSurfaceBrush）         ← 半透明渐变 + 噪点
        │
        ├─ 自定义标题栏（TitleBarBrush）
        ├─ 侧边栏 / 页面背景（透明）
        └─ GlassCard / 控件                    ← 半透明玻璃、细边、内容
```

DWM 材质是真实的系统合成。WPF 只绘制内容层，不负责“把桌面截图做高斯模糊”。因此：

- `FrostWindow` 必须保持 `AllowsTransparency=false`；
- 必须把 `HwndSource.CompositionTarget.BackgroundColor` 设为 `Transparent`；
- 必须通过 `WindowChrome` 自定义标题栏，同时保留系统缩放与窗口行为；
- 任意控件无法单独采样窗口背后的桌面，`GlassCard` 用材质模拟来做视觉分层。

## 2. 窗口启动序列

`FrostWindow` 构造函数：

1. 设置 `WindowStyle=None`、`AllowsTransparency=false`、`Background=Transparent`；
2. 创建 `WindowChrome`，设置 `CaptionHeight=TitleBarHeight`、`ResizeBorderThickness=7`、`GlassFrameThickness=0`；
3. 订阅 `Loaded`、`StateChanged`、`Closed`、`ThemeManager.ThemeChanged`。

`OnSourceInitialized`：

1. 取 HWND 与 `HwndSource`；
2. 设置 `CompositionTarget.BackgroundColor = Colors.Transparent`；
3. 添加窗口消息钩子（监听 `WM_DWMCOMPOSITIONCHANGED`）；
4. 调用 `ApplyBackdrop()`。

`ApplyBackdrop()` 的公共步骤：

1. 写入 `DWMWA_USE_IMMERSIVE_DARK_MODE`（主题联动标题栏/边框）；
2. 写入 `DWMWA_WINDOW_CORNER_PREFERENCE = ROUND`；
3. 检查 `DwmIsCompositionEnabled`；
4. 根据 `Backdrop` 选择实现，并在失败时逐级回退。

### Acrylic

```csharp
var policy = new AccentPolicy {
    AccentState = ACCENT_ENABLE_ACRYLICBLURBEHIND, // 4
    AccentFlags = 2,
    GradientColor = AABBGGRR,                      // 原生 tint
};
SetWindowCompositionAttribute(hwnd, policy);
```

GradientColor 的字节顺序是 **AABBGGRR**，不是 WPF 的 ARGB。`FrostWindow` 里由 `TintColor` + `TintOpacity` 计算。

切换自 Mica 时，会先把 DWM backdrop 设为 None 并把 `DwmExtendFrameIntoClientArea` 的 margins 归零，避免两套材质叠加。

### Mica / Mica Alt

```csharp
DwmExtendFrameIntoClientArea(hwnd, -1, -1, -1, -1);
DwmSetWindowAttribute(hwnd, DWMWA_SYSTEMBACKDROP_TYPE, 2 /* Mica */);
```

- `Mica = 2`，`MicaAlt = 4`；
- FrostUI 保守地要求 Windows 11 22H2（Build 22621）以上；
- 旧版本会回退到 Acrylic。

### Blur / None

- `Blur` 使用 `ACCENT_ENABLE_BLURBEHIND`；
- `None` 禁用 accent policy 和 DWM backdrop，模板中的 `FallbackSurface` 显示为不透明回退材质。

## 3. 自定义标题栏

`FrostWindow` 的 `WindowChrome.CaptionHeight` 等于 `TitleBarHeight`，因此：

- 空白标题栏区域由系统 HTCAPTION 处理，天然支持拖动、双击最大化、吸附；
- `TitleBarContent` 和窗口按钮使用 `WindowChrome.IsHitTestVisibleInChrome="True"`，可以接收点击；
- 最小化 / 最大化 / 还原 / 关闭由 `FrostWindow` 自己暴露的 `MinimizeCommand`、`MaximizeCommand`、`RestoreCommand`、`CloseCommand` 执行。`SystemCommands` 在同一套自定义模板里曾出现 `CanExecute=false`、按钮显示却不可点击的问题，因此没有采用；
- 最大化时模板把最大化按钮隐藏、还原按钮显示，并移除窗口表面边框。

标题栏模板位于 `Themes/Controls.xaml` 的 `FrostWindowStyle`。

## 4. 主题与资源令牌

### 颜色字典

- `Themes/Colors.Dark.xaml`
- `Themes/Colors.Light.xaml`

两个文件必须保持相同的 key 集合。常用分组：

| 前缀 | 用途 |
| --- | --- |
| `Text*Brush` | 主/次/三级/禁用文字 |
| `Window*Brush` | 窗口表面、回退表面、边缘、叠光 |
| `Card*Brush` | 卡片填充、hover、边框、阴影 |
| `TitleBar*` / `Nav*` | 标题栏与导航状态 |
| `Accent*` | 强调色、渐变、柔光、轨道 |
| `Success/Warning/Danger/Info*` | 语义色 |
| `Ambient*Brush` / `OrbBrush` | 页面环境光 |
| `*TextStyle` | 排版样式 |

### 运行时强调色

`ThemeManager.SetAccent` 在 Application 根资源层重新写入 `AccentBrush`、`AccentGradientBrush`、`AccentSoftBrush`、`AccentGlowBrush`、`AccentTrackBrush`、`AccentBorderBrush`、`AccentContrastBrush`、`BrandGradientBrush`、`AvatarGradientBrush`、`OrbBrush` 和 `SoftGlowEffect`。

浅色主题会自动把 `AccentBrush` 加深 30%，保证小字号文字与图标有足够对比度；主按钮仍使用亮度更高的渐变。

### 切换主题

`ThemeManager.SetTheme`：

1. 在 `Application.Resources.MergedDictionaries` 里递归查找 `Colors.Dark.xaml` / `Colors.Light.xaml`；
2. 原地替换对应字典；
3. 重新应用强调色（根层覆盖优先于合并字典）；
4. 触发 `ThemeChanged`，由 `FrostWindow` 重新设置 DWM 深色模式与 tint。

## 5. 玻璃卡片

`GlassCard` 的模板由五层组成：

1. `Shadow`（可选）：卡片下方的柔和阴影；
2. `Aura`（可选）：强调色环境光晕；
3. `Surface`：半透明渐变 + 用户 `Background`；
4. 噪点层：`FrostMaterials.NoiseBrush`，2–3% 透明度的生成式纹理；
5. `Edge`：1px 渐变高光边框，顶部还有一条 `CardSheenBrush` 高光。

`IsInteractive=true` 时才启用 hover 背景与手型光标，避免静态卡片产生错误的可点击暗示。

## 6. 自定义控件

### `GlassToggleSwitch`

继承 `ToggleButton`，完全复用 WPF 的 `IsChecked`、键盘空间键、自动化对等体。模板中的 Thumb 使用 `TranslateTransform` 动画，Checked/Unchecked 均由 Trigger 驱动。

### `ProgressRing`

直接继承 `FrameworkElement` 并在 `OnRender` 中用 `DrawingContext` 绘制：

- 外圈轨道；
- 可选的宽发光弧；
- 带圆头端点的进度弧；
- `Value` 在 `Minimum`/`Maximum` 变化时强制重新 coerce。

不需要任何图片资源，任意 DPI 都清晰。

### `Sparkline`

用 Catmull-Rom 转三次贝塞尔曲线生成平滑折线，可选半透明区域填充和末端圆点。`Values` 是 `DoubleCollection`，控件在 `AffectsRender` 回调中重绘。

## 7. 示例程序的截图模式

`samples/FrostUI.Demo` 支持：

```
--screenshot <path> [--page ...] [--light] [--accent ...] [--backdrop ...] [--no-showcase]
```

截图模式流程：

1. 创建 `ShowcaseBackdropWindow`：一张全屏、带高饱和光斑与低调文字的“演示壁纸”；
2. 创建主窗口并置顶；
3. 使用 `SetWindowPos` 把演示壁纸放到主窗口正下方；
4. 等 DWM 与 WPF 稳定后，用 GDI `BitBlt` 抓取主窗口矩形；
5. 强制丢弃 GDI 未定义的 alpha 通道，再在 WPF 里重新加圆角遮罩，生成带透明角的 PNG；
6. 恢复鼠标位置并退出。

> `BitBlt` 抓屏的 alpha 通道并不可靠；有些 DWM 区域会返回 alpha=0，表现为 PNG 中的透明空洞。`ScreenshotService` 先转换到 `Bgr32` 再绘制，解决这个问题。

## 8. 扩展建议

- **新增材质**：在 `WindowBackdrop` 增加枚举值，然后在 `FrostWindow.ApplyBackdrop` 增加分支；确保失败时回退到 Acrylic 或 None。
- **新增颜色令牌**：同时修改 Dark / Light 两个字典，并优先在 `Controls.xaml` 用 `DynamicResource` 引用。
- **新增控件样式**：在 `Controls.xaml` 末尾追加，不要用 `StaticResource` 引用会被主题替换的颜色，否则切换主题不会刷新。
- **新增自定义控件**：继承 `FrameworkElement` 时实现 `AffectsRender`；继承 `Control` 时提供模板，并把颜色绑定到 `DynamicResource`。
- **性能**：优先使用半透明渐变与 1px 边框，少用大面积 `BlurEffect` / `DropShadowEffect`。DWM 材质本身是系统合成，不占 WPF 的离屏预算。

## 9. 已知取舍

- 整窗材质是 Windows 的能力，无法做到每个卡片独立采样桌面；
- `Mica` 对背后窗口不敏感；
- 无边框窗口的圆角由 DWM 控制，Windows 10 上表现为直角；
- 截图模式会短暂显示全屏演示壁纸，只用于生成展示图，正常模式不会创建；
- 主题切换依赖合并字典被正确加载；如果项目把颜色直接内联到 View 中，动态资源不会跟随。
