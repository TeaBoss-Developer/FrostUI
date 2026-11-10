using FrostUI.Designer.Models;

namespace FrostUI.Designer.Services;

public static class ScriptReferenceCatalog
{
    private static readonly ScriptReferenceEntry[] Entries = Build();

    public static IReadOnlyList<ScriptReferenceEntry> All => Entries;

    public static IReadOnlyList<string> Categories =>
        Entries.Select(entry => entry.Category).Distinct().ToArray();

    private static ScriptReferenceEntry[] Build()
    {
        var entries = new List<ScriptReferenceEntry>
        {
            Entry("关键字", "如果 / 否则", "如果 (条件) { ... } 否则 { ... }", "条件分支。"),
            Entry("关键字", "循环", "循环 n { ... }", "按次数循环；支持 跳出 / 继续。"),
            Entry("关键字", "遍历", "遍历 项 于 列表 { ... }", "遍历 JSON 数组或列表。"),
            Entry("关键字", "函数", "函数 名称(a: 类型) { 返回 ... }", "定义函数，支持返回值和递归。"),
            Entry("关键字", "跳出 / 继续", "跳出\n继续", "只作用于最内层循环。"),
            Entry("关键字", "变量", "设 名称: 类型 = 值\n全局 名称: 类型 = 值", "局部变量和全局变量。"),
            Entry("关键字", "逻辑值", "真  假  空", "逻辑值和空值常量。"),
            Entry("关键字", "列表 / 字典", "[1, 2, 3]\n{\"键\": 值}", "列表和字典字面量。"),

            Entry("类型", "基本类型", "整数  小数  文本  逻辑  JSON  列表  任意", "变量声明类型。"),

            Entry("函数", "输出", "输出(值)", "写入运行输出日志。"),
            Entry("函数", "提示", "提示(文本)", "在预览窗口显示 Toast。"),
            Entry("函数", "长度", "长度(值)", "文本、列表或 JSON 的长度。"),
            Entry("函数", "类型", "类型(值)", "返回值的类型名。"),
            Entry("函数", "转换", "文本(值)  整数(值)  小数(值)  逻辑(值)", "基础类型转换。"),
            Entry("函数", "随机", "随机(最小, 最大)", "返回随机整数。"),
            Entry("函数", "等待", "等待(毫秒)", "等待指定毫秒。"),

            Entry("JSON", "JSON.解析", "JSON.解析(文本)", "解析 JSON 文本。"),
            Entry("JSON", "JSON.取值", "JSON.取值(数据, \"路径\")", "读取路径，如 items[0].name。"),
            Entry("JSON", "JSON.文本", "JSON.文本(值)", "把 JSON 转成文本。"),
            Entry("JSON", "成员访问", "数据.字段\n数据[\"字段\"]", "读取 JSON 对象字段。"),

            Entry("HTTP", "HTTP.获取", "HTTP.获取(url)", "GET，返回文本。"),
            Entry("HTTP", "HTTP.获取JSON", "HTTP.获取JSON(url)", "GET，返回 JSON。"),
            Entry("HTTP", "HTTP.提交JSON", "HTTP.提交JSON(url, 数据)", "POST JSON。"),

            Entry("页面", "页面.切换", "页面.切换(\"页面名\")", "当前窗口切换页面。"),
            Entry("页面", "页面.打开", "页面.打开(\"页面名\")", "按页面类型自动切换/开窗/弹窗。"),
            Entry("页面", "页面.打开窗口", "页面.打开窗口(\"页面名\")", "打开独立窗口。"),
            Entry("页面", "页面.打开弹窗", "页面.打开弹窗(\"页面名\")", "打开模态窗口或遮罩弹窗。"),
            Entry("页面", "页面.关闭", "页面.关闭()", "关闭当前窗口。"),
            Entry("页面", "页面.传参", "页面.传参(\"键\", 值)", "设置页面参数。"),
            Entry("页面", "页面.参数", "页面.参数(\"键\")", "读取页面参数。"),
            Entry("页面", "页面.设置结果", "页面.设置结果(值)", "设置弹窗返回值并关闭。"),
            Entry("页面", "页面.结果", "页面.结果()", "读取弹窗返回值。"),

            Entry("项目", "项目.设置", "项目.设置(\"键\", 值)", "写入项目级共享数据。"),
            Entry("项目", "项目.获取", "项目.获取(\"键\")", "读取项目级共享数据。"),
        };

        var properties = new[]
        {
            ("名称", "只读控件名"),
            ("类型", "只读控件类型"),
            ("文本", "可读写文本"),
            ("左", "可读写 X"),
            ("顶", "可读写 Y"),
            ("宽", "可读写宽度"),
            ("高", "可读写高度"),
            ("可见", "可读写可见性"),
            ("启用", "可读写启用状态"),
            ("字号", "可读写字号"),
            ("粗体", "可读写粗体"),
            ("圆角", "可读写圆角"),
            ("开启", "开关 / 复选 / 单选状态"),
            ("选中", "同“开启”，可读写"),
            ("选中项", "下拉框 / 菜单 / 标签页当前项"),
            ("选项", "选项或标签标题，换行分隔"),
            ("分组", "单选框分组名"),
            ("标签页序号", "子控件所属标签页，从 1 开始"),
            ("分栏面板", "子控件所属分栏，0 左 1 右"),
            ("当前标签页", "标签页控件当前索引，0 开始"),
            ("级别", "通知条级别：Info/Success/Warning/Error"),
            ("表格列", "数据表格列名，换行分隔"),
            ("图表类型", "Bar / Line / Pie"),
            ("可编辑", "数据表格是否允许双击改单元格"),
            ("可排序", "数据表格是否允许点击表头排序"),
            ("最小值", "数字框最小值"),
            ("最大值", "数字框最大值"),
            ("步长", "数字框步长"),
            ("日期", "日期文本，yyyy-MM-dd"),
            ("值", "数值 / 开关值"),
        };
        foreach (var (name, description) in properties)
        {
            entries.Add(Entry("控件属性", name, $"控件.{name}", description));
        }

        foreach (var definition in ControlCatalog.All)
        {
            entries.Add(Entry(
                "控件种类",
                definition.Title,
                $"{definition.Title} · {definition.Description}",
                $"事件：{string.Join(" / ", definition.Events)}"));
            foreach (var eventName in definition.Events)
            {
                entries.Add(Entry(
                    "控件事件",
                    $"{definition.Title}.{eventName}",
                    $"{definition.NamePrefix}.{eventName}",
                    $"{definition.Title} 的“{eventName}”事件脚本入口。"));
            }
        }

        return entries
            .GroupBy(entry => (entry.Category, entry.Name, entry.Signature))
            .Select(group => group.First())
            .OrderBy(entry => entry.Category, StringComparer.CurrentCulture)
            .ThenBy(entry => entry.Name, StringComparer.CurrentCulture)
            .ToArray();
    }

    private static ScriptReferenceEntry Entry(
        string category,
        string name,
        string signature,
        string description) =>
        new(category, name, signature, description);
}
