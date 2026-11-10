using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Runtime.Serialization;
using System.Text.Json.Serialization;
using FrostUI.Interop;

namespace FrostUI.Designer.Models;

public sealed class DesignDocument : ObservableObject
{
    private string _name = "未命名设计";
    private string _windowTitle = "FrostUI 设计";
    private double _windowWidth = 980;
    private double _windowHeight = 620;
    private WindowBackdrop _backdrop = WindowBackdrop.Acrylic;
    private double _tintOpacity = 0.28;
    private string _accentColor = "#84AEFF";
    private ObservableCollection<string> _themePalette = [];
    private double _gridSize = 8;
    private bool _scaleContent;
    private bool _anchorLayout;
    private string _breakpointSpec = string.Empty;
    private string _titleBinding = string.Empty;
    private string _description = "用 FrostUI Designer 创建。";
    private string _version = "1.0.0";
    private string _company = string.Empty;
    private string _iconPath = string.Empty;
    private ObservableCollection<DesignControl> _controls = [];
    private ObservableCollection<DesignGuide> _guides = [];
    private ObservableCollection<DesignToken> _tokens = [];
    private Dictionary<string, string> _windowScripts = new(StringComparer.Ordinal)
    {
        ["启动"] = "// 窗口 · 启动\n输出(\"窗口启动\")\n",
    };
    private ObservableCollection<DesignPage> _pages = [];
    private int _activePageIndex;

    public DesignDocument()
    {
        HookControls();
        HookGuides();
        _pages.Add(new DesignPage
        {
            Name = "主页面",
            Controls = _controls,
            Guides = _guides,
            Scripts = _windowScripts,
        });
    }

    public ObservableCollection<DesignPage> Pages
    {
        get => _pages;
        set
        {
            if (ReferenceEquals(_pages, value))
            {
                return;
            }

            _pages = value ?? [];
            if (_pages.Count == 0)
            {
                _pages.Add(new DesignPage
                {
                    Name = "主页面",
                    Controls = _controls,
                    Guides = _guides,
                    Scripts = _windowScripts,
                });
            }

            _activePageIndex = Math.Clamp(_activePageIndex, 0, _pages.Count - 1);
            ActivatePage(_activePageIndex);
            OnPropertyChanged();
        }
    }

    [JsonIgnore]
    public DesignPage ActivePage => _pages[Math.Clamp(_activePageIndex, 0, _pages.Count - 1)];

    public int ActivePageIndex
    {
        get => _activePageIndex;
        set
        {
            if (value < 0 || value >= _pages.Count || value == _activePageIndex)
            {
                return;
            }

            ActivatePage(value);
        }
    }

    public void ActivatePage(int index)
    {
        if (index < 0 || index >= _pages.Count)
        {
            return;
        }

        _controls.CollectionChanged -= OnControlsChanged;
        _guides.CollectionChanged -= OnGuidesChanged;
        _activePageIndex = index;
        var page = _pages[index];
        _controls = page.Controls;
        _guides = page.Guides;
        _windowScripts = page.Scripts;
        HookControls();
        HookGuides();
        OnPropertyChanged(nameof(ActivePage));
        OnPropertyChanged(nameof(ActivePageIndex));
        OnPropertyChanged(nameof(Controls));
        OnPropertyChanged(nameof(Guides));
        OnPropertyChanged(nameof(WindowScripts));
        OnPropertyChanged(nameof(ControlCountText));
        OnPropertyChanged(nameof(HasControls));
    }

    public string Name
    {
        get => _name;
        set => Set(ref _name, value);
    }

    public string WindowTitle
    {
        get => _windowTitle;
        set => Set(ref _windowTitle, value);
    }

    public double WindowWidth
    {
        get => _windowWidth;
        set
        {
            if (Set(ref _windowWidth, Math.Clamp(value, 360, 4096)))
            {
                OnPropertyChanged(nameof(ContentHeight));
                OnPropertyChanged(nameof(WindowSizeText));
            }
        }
    }

    public double WindowHeight
    {
        get => _windowHeight;
        set
        {
            if (Set(ref _windowHeight, Math.Clamp(value, 280, 4096)))
            {
                OnPropertyChanged(nameof(ContentHeight));
                OnPropertyChanged(nameof(WindowSizeText));
            }
        }
    }

    public WindowBackdrop Backdrop
    {
        get => _backdrop;
        set => Set(ref _backdrop, value);
    }

    public double TintOpacity
    {
        get => _tintOpacity;
        set => Set(ref _tintOpacity, Math.Clamp(value, 0, 1));
    }

    public string AccentColor
    {
        get => _accentColor;
        set => Set(ref _accentColor,
            string.IsNullOrWhiteSpace(value) ? "#84AEFF" : value.Trim());
    }

    public ObservableCollection<string> ThemePalette
    {
        get => _themePalette;
        set => Set(ref _themePalette, value ?? []);
    }

    public double GridSize
    {
        get => _gridSize;
        set => Set(ref _gridSize, Math.Clamp(Math.Round(value), 2, 64));
    }

    public string Description
    {
        get => _description;
        set => Set(ref _description, value ?? string.Empty);
    }

    public bool ScaleContent
    {
        get => _scaleContent;
        set => Set(ref _scaleContent, value);
    }

    public string BreakpointSpec
    {
        get => _breakpointSpec;
        set => Set(ref _breakpointSpec, value ?? string.Empty);
    }

    public string TitleBinding
    {
        get => _titleBinding;
        set => Set(ref _titleBinding, value ?? string.Empty);
    }

    public bool AnchorLayout
    {
        get => _anchorLayout;
        set => Set(ref _anchorLayout, value);
    }

    public string Version
    {
        get => _version;
        set => Set(ref _version, string.IsNullOrWhiteSpace(value) ? "1.0.0" : value.Trim());
    }

    public string Company
    {
        get => _company;
        set => Set(ref _company, value ?? string.Empty);
    }

    public string IconPath
    {
        get => _iconPath;
        set => Set(ref _iconPath, value ?? string.Empty);
    }

    [JsonIgnore]
    public ObservableCollection<DesignControl> Controls
    {
        get => _controls;
        set
        {
            if (ReferenceEquals(_controls, value))
            {
                return;
            }

            _controls.CollectionChanged -= OnControlsChanged;
            _controls = value ?? [];
            if (_pages.Count > 0)
            {
                ActivePage.Controls = _controls;
            }

            HookControls();
            OnPropertyChanged();
            OnPropertyChanged(nameof(ControlCountText));
        }
    }

    public ObservableCollection<DesignToken> Tokens
    {
        get => _tokens;
        set
        {
            _tokens = value ?? [];
            OnPropertyChanged();
        }
    }

    [JsonIgnore]
    public ObservableCollection<DesignGuide> Guides
    {
        get => _guides;
        set
        {
            if (ReferenceEquals(_guides, value))
            {
                return;
            }

            _guides.CollectionChanged -= OnGuidesChanged;
            _guides = value ?? [];
            if (_pages.Count > 0)
            {
                ActivePage.Guides = _guides;
            }

            HookGuides();
            OnPropertyChanged();
        }
    }

    [JsonIgnore]
    public Dictionary<string, string> WindowScripts
    {
        get => _windowScripts;
        set
        {
            var normalized = value is null
                ? new Dictionary<string, string>(StringComparer.Ordinal)
                : new Dictionary<string, string>(value, StringComparer.Ordinal);
            if (Set(ref _windowScripts, normalized) && _pages.Count > 0)
            {
                ActivePage.Scripts = normalized;
            }
        }
    }

    [JsonIgnore]
    public double ContentHeight => Math.Max(120, WindowHeight - 54);

    [JsonIgnore]
    public string ControlCountText => $"{Controls.Count} 个控件";

    [JsonIgnore]
    public bool HasControls => Controls.Count > 0;

    [JsonIgnore]
    public string WindowSizeText => $"{WindowWidth:0} × {WindowHeight:0}";

    public DesignControl? FindControl(string name) =>
        Controls.FirstOrDefault(x => string.Equals(x.Name, name, StringComparison.Ordinal));

    public static DesignDocument CreateSample()
    {
        var document = new DesignDocument
        {
            Name = "FrostUI 示例设计",
            WindowTitle = "FrostUI · Script Lab",
            WindowWidth = 980,
            WindowHeight = 620,
            Backdrop = WindowBackdrop.Acrylic,
            TintOpacity = 0.28,
            Description = "一个可以直接运行的设计：布局、属性与事件脚本都是真实数据。",
        };

        document.WindowScripts["启动"] =
            """
            // 窗口 · 启动
            输出("预览启动：窗口脚本已执行")
            """;

        document.Add(new DesignControl
        {
            Name = "卡片_主",
            Kind = ControlKind.Card,
            X = 32,
            Y = 32,
            Width = 916,
            Height = 212,
            Text = string.Empty,
            CornerRadius = 20,
            FontSize = 16,
        });
        document.Add(new DesignControl
        {
            Name = "标签_标题",
            Kind = ControlKind.Label,
            X = 60,
            Y = 56,
            Width = 700,
            Height = 48,
            Text = "把想法，拖进画板。",
            FontSize = 30,
            IsBold = true,
        });
        document.Add(new DesignControl
        {
            Name = "标签_副标题",
            Kind = ControlKind.Label,
            X = 62,
            Y = 108,
            Width = 760,
            Height = 26,
            Text = "拖入控件、调整参数，再给每个事件写一段脚本。",
            FontSize = 12.5,
        });
        document.Add(new DesignControl
        {
            Name = "按钮_计数",
            Kind = ControlKind.Button,
            X = 60,
            Y = 150,
            Width = 116,
            Height = 36,
            Text = "点击计数",
            Scripts = new Dictionary<string, string>
            {
                ["点击"] =
                    """
                    // 点击计数：全局变量在本次预览中持续存在
                    全局 计数: 整数 = 0
                    全局 计数 = 计数 + 1
                    设 结果: 文本 = "点击次数：" + 文本(计数)
                    标签_结果.文本 = 结果
                    如果 (计数 >= 5) {
                        标签_结果.文本 = 结果 + " · 达标"
                    }
                    输出("按钮_计数 => " + 结果)
                    """,
            },
        });
        document.Add(new DesignControl
        {
            Name = "按钮_JSON",
            Kind = ControlKind.Button,
            X = 188,
            Y = 150,
            Width = 116,
            Height = 36,
            Text = "解析 JSON",
            Scripts = new Dictionary<string, string>
            {
                ["点击"] =
                    """
                    // 离线 JSON 解析：对象、数组与类型转换
                    设 原始: 文本 = "{\"name\":\"FrostUI\",\"version\":1.0,\"features\":[\"Acrylic\",\"Mica\",\"Script\"]}"
                    设 数据: JSON = JSON.解析(原始)
                    设 名称: 文本 = 文本(JSON.取值(数据, "name"))
                    设 特性: 文本 = 文本(JSON.取值(数据, "features[1]"))
                    标签_结果.文本 = "JSON：" + 名称 + " · " + 特性
                    输出("JSON 解析成功：" + 名称 + " / " + 特性)
                    """,
            },
        });
        document.Add(new DesignControl
        {
            Name = "按钮_API",
            Kind = ControlKind.Button,
            X = 316,
            Y = 150,
            Width = 116,
            Height = 36,
            Text = "请求 API",
            Scripts = new Dictionary<string, string>
            {
                ["点击"] =
                    """
                    // 真实 HTTP GET，并把 JSON 字段写入界面
                    提示("正在请求 GitHub API…")
                    设 响应: JSON = HTTP.获取JSON("https://api.github.com/repos/dotnet/wpf")
                    设 名称: 文本 = 文本(JSON.取值(响应, "full_name"))
                    设 星数: 文本 = 文本(JSON.取值(响应, "stargazers_count"))
                    标签_结果.文本 = "API：" + 名称 + " · ★ " + 星数
                    输出("API 请求成功：" + 名称)
                    """,
            },
        });
        document.Add(new DesignControl
        {
            Name = "标签_结果",
            Kind = ControlKind.Label,
            X = 60,
            Y = 204,
            Width = 760,
            Height = 24,
            Text = "等待操作…",
            FontSize = 11,
        });

        document.Add(new DesignControl
        {
            Name = "卡片_输入",
            Kind = ControlKind.Card,
            X = 32,
            Y = 264,
            Width = 448,
            Height = 270,
            Text = string.Empty,
            CornerRadius = 18,
        });
        document.Add(new DesignControl
        {
            Name = "标签_输入标题",
            Kind = ControlKind.Label,
            X = 56,
            Y = 284,
            Width = 300,
            Height = 24,
            Text = "输入你的名字",
            FontSize = 13,
            IsBold = true,
        });
        document.Add(new DesignControl
        {
            Name = "输入框_名字",
            Kind = ControlKind.TextBox,
            X = 56,
            Y = 314,
            Width = 300,
            Height = 34,
            Text = "茶总",
            FontSize = 12,
            Scripts = new Dictionary<string, string>
            {
                ["文本改变"] = "// 输入框 · 文本改变\n输出(\"输入框现在的内容：\" + 输入框_名字.文本)\n",
            },
        });
        document.Add(new DesignControl
        {
            Name = "按钮_问候",
            Kind = ControlKind.Button,
            X = 56,
            Y = 360,
            Width = 116,
            Height = 34,
            Text = "生成问候",
            Scripts = new Dictionary<string, string>
            {
                ["点击"] =
                    """
                    // 条件判断 + 跨控件读写
                    如果 (输入框_名字.文本 == "") {
                        标签_问候.文本 = "请先输入名字"
                    } 否则 {
                        标签_问候.文本 = "你好，" + 输入框_名字.文本 + "！"
                        输出("生成问候：" + 标签_问候.文本)
                    }
                    """,
            },
        });
        document.Add(new DesignControl
        {
            Name = "标签_问候",
            Kind = ControlKind.Label,
            X = 56,
            Y = 404,
            Width = 380,
            Height = 24,
            Text = "你好，茶总。",
            FontSize = 11,
        });
        document.Add(new DesignControl
        {
            Name = "标签_动效",
            Kind = ControlKind.Label,
            X = 56,
            Y = 446,
            Width = 180,
            Height = 24,
            Text = "启用动效",
            FontSize = 11,
        });
        document.Add(new DesignControl
        {
            Name = "开关_动效",
            Kind = ControlKind.ToggleSwitch,
            X = 360,
            Y = 444,
            Width = 52,
            Height = 28,
            IsOn = true,
            Scripts = new Dictionary<string, string>
            {
                ["切换"] =
                    """
                    如果 (开关_动效.开启) {
                        输出("动效已开启")
                    } 否则 {
                        输出("动效已关闭")
                    }
                    """,
            },
        });
        document.Add(new DesignControl
        {
            Name = "标签_浓度",
            Kind = ControlKind.Label,
            X = 56,
            Y = 475,
            Width = 180,
            Height = 22,
            Text = "脚本示例浓度",
            FontSize = 10,
        });
        document.Add(new DesignControl
        {
            Name = "滑杆_浓度",
            Kind = ControlKind.Slider,
            X = 56,
            Y = 500,
            Width = 300,
            Height = 22,
            Value = 36,
        });

        document.Add(new DesignControl
        {
            Name = "卡片_脚本",
            Kind = ControlKind.Card,
            X = 500,
            Y = 264,
            Width = 448,
            Height = 270,
            Text = string.Empty,
            CornerRadius = 18,
        });
        document.Add(new DesignControl
        {
            Name = "环形_完成度",
            Kind = ControlKind.ProgressRing,
            X = 532,
            Y = 292,
            Width = 100,
            Height = 100,
            Value = 78,
        });
        document.Add(new DesignControl
        {
            Name = "标签_能力1",
            Kind = ControlKind.Label,
            X = 656,
            Y = 288,
            Width = 260,
            Height = 24,
            Text = "变量 · 条件 · 函数",
            FontSize = 12,
            IsBold = true,
        });
        document.Add(new DesignControl
        {
            Name = "标签_能力2",
            Kind = ControlKind.Label,
            X = 656,
            Y = 318,
            Width = 260,
            Height = 24,
            Text = "JSON 解析 / HTTP 请求",
            FontSize = 11,
        });
        document.Add(new DesignControl
        {
            Name = "标签_能力3",
            Kind = ControlKind.Label,
            X = 656,
            Y = 346,
            Width = 260,
            Height = 24,
            Text = "按控件隔离事件脚本",
            FontSize = 11,
        });
        document.Add(new DesignControl
        {
            Name = "分隔线_1",
            Kind = ControlKind.Divider,
            X = 532,
            Y = 404,
            Width = 388,
            Height = 8,
        });
        document.Add(new DesignControl
        {
            Name = "进度_示例",
            Kind = ControlKind.ProgressBar,
            X = 532,
            Y = 432,
            Width = 388,
            Height = 6,
            Value = 82,
        });
        document.Add(new DesignControl
        {
            Name = "标签_脚本提示",
            Kind = ControlKind.Label,
            X = 532,
            Y = 452,
            Width = 388,
            Height = 24,
            Text = "中文关键字 · 静态类型 · 异步 API",
            FontSize = 10,
        });

        return document;
    }

    private void Add(DesignControl control) => Controls.Add(control);

    [OnDeserialized]
    private void OnDeserialized(StreamingContext context)
    {
        if (_pages.Count == 0)
        {
            _pages.Add(new DesignPage
            {
                Name = "主页面",
                Controls = _controls,
                Guides = _guides,
                Scripts = _windowScripts,
            });
        }

        _activePageIndex = Math.Clamp(_activePageIndex, 0, _pages.Count - 1);
        ActivatePage(_activePageIndex);
    }

    private void HookControls()
    {
        _controls.CollectionChanged += OnControlsChanged;
    }

    private void HookGuides()
    {
        _guides.CollectionChanged += OnGuidesChanged;
    }

    private void OnGuidesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        OnPropertyChanged(nameof(Guides));
    }

    private void OnControlsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        OnPropertyChanged(nameof(ControlCountText));
        OnPropertyChanged(nameof(HasControls));
        OnPropertyChanged(nameof(Controls));
    }
}
