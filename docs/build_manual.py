from pathlib import Path

from docx import Document
from docx.enum.section import WD_SECTION
from docx.enum.table import WD_ALIGN_VERTICAL, WD_TABLE_ALIGNMENT
from docx.enum.text import WD_ALIGN_PARAGRAPH, WD_BREAK, WD_LINE_SPACING
from docx.oxml import OxmlElement
from docx.oxml.ns import qn
from docx.shared import Cm, Inches, Pt, RGBColor

ROOT = Path(__file__).resolve().parents[1]
SHOTS = ROOT / "designer" / "screenshots"
OUT = ROOT / "docs" / "FrostUI-Designer-零基础说明书.docx"

NAVY = RGBColor(0x12, 0x1B, 0x33)
ACCENT = RGBColor(0x4F, 0x6B, 0xE8)
TEAL = RGBColor(0x1B, 0x9E, 0x93)
TEXT = RGBColor(0x25, 0x2B, 0x38)
MUTED = RGBColor(0x67, 0x70, 0x82)
LIGHT = "F3F5FA"
CODE_FILL = "F6F7FB"
ACCENT_FILL = "EEF1FF"
TEAL_FILL = "EAF8F6"
WARN_FILL = "FFF6E8"


def set_run_font(run, name="Microsoft YaHei", size=None, bold=None, color=None):
    run.font.name = name
    run._element.get_or_add_rPr().rFonts.set(qn("w:eastAsia"), name)
    if size is not None:
        run.font.size = Pt(size)
    if bold is not None:
        run.bold = bold
    if color is not None:
        run.font.color.rgb = color


def set_paragraph_spacing(p, before=0, after=6, line=1.45):
    pf = p.paragraph_format
    pf.space_before = Pt(before)
    pf.space_after = Pt(after)
    pf.line_spacing = line


def configure_style(style, name, size, bold=False, color=TEXT, before=0, after=6):
    style.font.name = name
    style.font.size = Pt(size)
    style.font.bold = bold
    style.font.color.rgb = color
    style._element.get_or_add_rPr().rFonts.set(qn("w:eastAsia"), name)
    style.paragraph_format.space_before = Pt(before)
    style.paragraph_format.space_after = Pt(after)
    style.paragraph_format.line_spacing = 1.35


def shade(cell, fill):
    tc_pr = cell._tc.get_or_add_tcPr()
    shd = OxmlElement("w:shd")
    shd.set(qn("w:fill"), fill)
    tc_pr.append(shd)


def set_cell_margins(cell, top=100, bottom=100, left=140, right=140):
    tc = cell._tc
    tc_pr = tc.get_or_add_tcPr()
    mar = tc_pr.first_child_found_in("w:tcMar")
    if mar is None:
        mar = OxmlElement("w:tcMar")
        tc_pr.append(mar)
    for tag, value in (("top", top), ("bottom", bottom), ("left", left), ("right", right)):
        node = mar.find(qn(f"w:{tag}"))
        if node is None:
            node = OxmlElement(f"w:{tag}")
            mar.append(node)
        node.set(qn("w:w"), str(value))
        node.set(qn("w:type"), "dxa")


def set_table_borders(table, color="DDE3EF", size="6"):
    tbl_pr = table._tbl.tblPr
    borders = OxmlElement("w:tblBorders")
    for edge in ("top", "left", "bottom", "right", "insideH", "insideV"):
        element = OxmlElement(f"w:{edge}")
        element.set(qn("w:val"), "single")
        element.set(qn("w:sz"), size)
        element.set(qn("w:space"), "0")
        element.set(qn("w:color"), color)
        borders.append(element)
    tbl_pr.append(borders)


def add_caption(doc, text):
    p = doc.add_paragraph()
    p.alignment = WD_ALIGN_PARAGRAPH.CENTER
    set_paragraph_spacing(p, before=2, after=10)
    r = p.add_run(text)
    set_run_font(r, size=8.5, color=MUTED)
    r.italic = True


def add_code(doc, text, caption=None):
    lines = text.strip("\n").splitlines()
    p = doc.add_paragraph()
    set_paragraph_spacing(p, before=2, after=8, line=1.15)
    p.paragraph_format.left_indent = Cm(0.25)
    p.paragraph_format.right_indent = Cm(0.15)
    p_pr = p._p.get_or_add_pPr()
    shd = OxmlElement("w:shd")
    shd.set(qn("w:fill"), CODE_FILL)
    p_pr.append(shd)
    for index, line in enumerate(lines):
        run = p.add_run(line)
        set_run_font(run, name="Consolas", size=8.8, color=RGBColor(0x1E, 0x2A, 0x44))
        if index < len(lines) - 1:
            run.add_break()
    if caption:
        add_caption(doc, caption)


def add_callout(doc, title, body, fill=ACCENT_FILL, title_color=ACCENT):
    table = doc.add_table(rows=1, cols=1)
    table.alignment = WD_TABLE_ALIGNMENT.CENTER
    table.autofit = False
    set_table_borders(table, color="E2E7F4", size="4")
    table.rows[0]._tr.get_or_add_trPr().append(OxmlElement("w:cantSplit"))
    cell = table.cell(0, 0)
    cell.width = Cm(16.4)
    shade(cell, fill)
    set_cell_margins(cell, top=120, bottom=120, left=180, right=180)
    p = cell.paragraphs[0]
    set_paragraph_spacing(p, before=0, after=3)
    r = p.add_run(title)
    set_run_font(r, size=9.5, bold=True, color=title_color)
    p2 = cell.add_paragraph()
    set_paragraph_spacing(p2, before=0, after=0, line=1.35)
    r2 = p2.add_run(body)
    set_run_font(r2, size=9.5, color=TEXT)


def add_bullets(doc, items):
    for item in items:
        p = doc.add_paragraph(style="List Bullet")
        set_paragraph_spacing(p, before=0, after=3, line=1.35)
        p.paragraph_format.left_indent = Cm(0.7)
        r = p.add_run(item)
        set_run_font(r, size=10.5, color=TEXT)


def add_numbered(doc, items):
    for index, item in enumerate(items, start=1):
        p = doc.add_paragraph()
        set_paragraph_spacing(p, before=0, after=3, line=1.35)
        p.paragraph_format.left_indent = Cm(0.78)
        p.paragraph_format.first_line_indent = Cm(-0.55)
        prefix = p.add_run(f"{index}. ")
        set_run_font(prefix, size=10.5, bold=True, color=ACCENT)
        r = p.add_run(item)
        set_run_font(r, size=10.5, color=TEXT)


def add_table(doc, headers, rows, widths=None, font_size=9):
    table = doc.add_table(rows=1, cols=len(headers))
    table.alignment = WD_TABLE_ALIGNMENT.CENTER
    table.autofit = False
    set_table_borders(table)
    header_pr = table.rows[0]._tr.get_or_add_trPr()
    header_flag = OxmlElement("w:tblHeader")
    header_flag.set(qn("w:val"), "true")
    header_pr.append(header_flag)
    table.rows[0]._tr.get_or_add_trPr().append(OxmlElement("w:cantSplit"))
    for index, header in enumerate(headers):
        cell = table.rows[0].cells[index]
        cell.width = Cm(widths[index]) if widths else Cm(16 / len(headers))
        shade(cell, "1B2540")
        set_cell_margins(cell, top=90, bottom=90, left=110, right=110)
        p = cell.paragraphs[0]
        p.alignment = WD_ALIGN_PARAGRAPH.LEFT
        r = p.add_run(header)
        set_run_font(r, size=font_size, bold=True, color=RGBColor(0xFF, 0xFF, 0xFF))
    for row_data in rows:
        row = table.add_row()
        row._tr.get_or_add_trPr().append(OxmlElement("w:cantSplit"))
        cells = row.cells
        for index, value in enumerate(row_data):
            cell = cells[index]
            cell.width = Cm(widths[index]) if widths else Cm(16 / len(headers))
            set_cell_margins(cell, top=80, bottom=80, left=110, right=110)
            p = cell.paragraphs[0]
            set_paragraph_spacing(p, before=0, after=0, line=1.25)
            r = p.add_run(str(value))
            set_run_font(r, size=font_size, color=TEXT)
    return table


def add_heading(doc, text, level=1):
    p = doc.add_heading(level=level)
    p.paragraph_format.keep_with_next = True
    r = p.add_run(text)
    if level == 1:
        set_run_font(r, name="Microsoft YaHei", size=18, bold=True, color=NAVY)
        p.paragraph_format.space_before = Pt(12)
        p.paragraph_format.space_after = Pt(10)
        p.paragraph_format.page_break_before = True
    elif level == 2:
        set_run_font(r, name="Microsoft YaHei", size=13, bold=True, color=ACCENT)
        p.paragraph_format.space_before = Pt(14)
        p.paragraph_format.space_after = Pt(6)
    else:
        set_run_font(r, name="Microsoft YaHei", size=11, bold=True, color=NAVY)
        p.paragraph_format.space_before = Pt(9)
        p.paragraph_format.space_after = Pt(4)
    return p


def add_body(doc, text, indent=False):
    p = doc.add_paragraph()
    set_paragraph_spacing(p, before=0, after=4, line=1.4)
    if indent:
        p.paragraph_format.first_line_indent = Cm(0.74)
    r = p.add_run(text)
    set_run_font(r, size=10.5, color=TEXT)
    return p


def add_page_number_footer(section):
    footer = section.footer
    footer.is_linked_to_previous = False
    p = footer.paragraphs[0]
    p.alignment = WD_ALIGN_PARAGRAPH.CENTER
    r = p.add_run("FrostUI Designer · 零基础说明书     ")
    set_run_font(r, size=8, color=MUTED)
    run = p.add_run()
    fld_begin = OxmlElement("w:fldChar")
    fld_begin.set(qn("w:fldCharType"), "begin")
    instr = OxmlElement("w:instrText")
    instr.set(qn("xml:space"), "preserve")
    instr.text = " PAGE "
    fld_end = OxmlElement("w:fldChar")
    fld_end.set(qn("w:fldCharType"), "end")
    run._r.append(fld_begin)
    run._r.append(instr)
    run._r.append(fld_end)
    set_run_font(run, size=8, color=MUTED)


def build():
    doc = Document()
    section = doc.sections[0]
    section.page_width = Cm(21)
    section.page_height = Cm(29.7)
    section.top_margin = Cm(2.1)
    section.bottom_margin = Cm(1.9)
    section.left_margin = Cm(2.3)
    section.right_margin = Cm(2.3)
    section.header_distance = Cm(1.0)
    section.footer_distance = Cm(0.9)

    configure_style(doc.styles["Normal"], "Microsoft YaHei", 10.5)
    for style_name, size in (("Heading 1", 18), ("Heading 2", 13), ("Heading 3", 11)):
        configure_style(doc.styles[style_name], "Microsoft YaHei", size, bold=True, color=NAVY)

    # Header / footer; no chrome on the cover.
    section.different_first_page_header_footer = True
    header = section.header
    header.is_linked_to_previous = False
    hp = header.paragraphs[0]
    hp.alignment = WD_ALIGN_PARAGRAPH.RIGHT
    hr = hp.add_run("FrostUI Designer · 零基础页面设计与交互说明书")
    set_run_font(hr, size=8, color=MUTED)
    add_page_number_footer(section)
    section.first_page_header.paragraphs[0].text = ""
    section.first_page_footer.paragraphs[0].text = ""

    # ---------------- Cover ----------------
    p = doc.add_paragraph()
    p.paragraph_format.space_before = Pt(28)
    p.paragraph_format.space_after = Pt(4)
    r = p.add_run("FROSTUI DESIGNER")
    set_run_font(r, name="Consolas", size=11, bold=True, color=ACCENT)

    p = doc.add_paragraph()
    p.paragraph_format.space_after = Pt(2)
    r = p.add_run("零基础页面设计与交互说明书")
    set_run_font(r, size=28, bold=True, color=NAVY)

    p = doc.add_paragraph()
    p.paragraph_format.space_after = Pt(18)
    r = p.add_run("从拖控件，到让页面会跳转、会判断、会响应")
    set_run_font(r, size=13, color=TEAL)

    rule = doc.add_table(rows=1, cols=1)
    rule.autofit = False
    rcell = rule.cell(0, 0)
    rcell.width = Cm(16.2)
    shade(rcell, "4F6BE8")
    rcell.paragraphs[0].text = ""
    rcell.paragraphs[0].paragraph_format.space_after = Pt(0)

    if (SHOTS / "preview.png").exists():
        p = doc.add_paragraph()
        p.alignment = WD_ALIGN_PARAGRAPH.CENTER
        p.paragraph_format.space_before = Pt(18)
        p.paragraph_format.space_after = Pt(3)
        p.add_run().add_picture(str(SHOTS / "preview.png"), width=Cm(15.8))
        add_caption(doc, "FrostUI 真实预览：毛玻璃、控件、脚本和动画按最终效果运行。")

    p = doc.add_paragraph()
    p.alignment = WD_ALIGN_PARAGRAPH.CENTER
    p.paragraph_format.space_before = Pt(8)
    r = p.add_run("适用：FrostUI Designer 1.0 · Windows 10 / 11")
    set_run_font(r, size=9, color=MUTED)

    p = doc.add_paragraph()
    p.alignment = WD_ALIGN_PARAGRAPH.CENTER
    r = p.add_run("阅读时间：约 35 分钟    ·    不需要编程基础")
    set_run_font(r, size=9, color=MUTED)



    # ---------------- 写在前面 ----------------
    add_heading(doc, "写在前面：你不需要先学会编程", 1)
    add_body(doc, "很多设计工具把“页面”和“代码”分得很开：页面是画出来的，逻辑要另外写。FrostUI Designer 把这两件事放在同一个画面里：你先把页面画出来，再给某个按钮指定“被点击时做什么”。", indent=True)
    add_body(doc, "你可以把设计一个应用想成布置一个房间：", indent=True)
    add_bullets(doc, [
        "页面 = 房间。主页面、设置页、详情窗口、确认弹窗，各是一个房间。",
        "控件 = 家具。按钮、文字、输入框、图片、列表，都是房间里的东西。",
        "属性 = 家具的尺寸和文字。改宽度、改颜色、改内容。",
        "事件 = 什么时候发生。比如“按钮被点击”“输入框内容改变”。",
        "脚本 = 发生这件事时说什么、做什么。几条中文句子就能完成。",
    ])
    add_callout(doc, "先记住这一句", "页面负责“长什么样”，事件负责“什么时候”，脚本负责“做什么”。三者接起来，页面就活了。", ACCENT_FILL, ACCENT)

    add_heading(doc, "你会得到什么", 2)
    add_body(doc, "读完这份说明书，你至少能独立完成：")
    add_table(doc, ["你会做", "用到的能力"], [
        ["做出一个带标题、说明文字、按钮的页面", "控件、属性、布局"],
        ["点按钮后修改文字、计数、显示/隐藏", "事件脚本、变量、条件"],
        ["从主页面跳到设置页", "多页面、导航动作"],
        ["打开一个详情窗口或确认弹窗", "页面类型、Window / Dialog"],
        ["把一组控件保存成组件，重复使用", "组件 / 片段库"],
        ["预览并导出一个可运行的 EXE", "预览、导出、发布"],
    ], widths=[10.2, 5.8])

    add_heading(doc, "建议阅读路线", 2)
    add_table(doc, ["时间", "读哪几章", "目标"], [
        ["0–10 分钟", "第 1–2 章", "认识界面，做出第一个页面"],
        ["10–20 分钟", "第 3 章", "会做页面切换、窗口和弹窗"],
        ["20–35 分钟", "第 4–5 章", "会写最常用的点击逻辑，会用数据绑定"],
        ["需要时", "第 6–10 章 + 附录", "组件、图片、动画、导出、排错"],
    ], widths=[3.2, 4.2, 8.6])

    add_heading(doc, "六个核心名词", 2)
    add_table(doc, ["名词", "用生活的话解释", "在 FrostUI 里在哪里"], [
        ["页面", "应用里的一屏内容", "左侧页面列表"],
        ["控件", "页面上的一个元素", "左侧控件库 / 画板"],
        ["属性", "这个元素的尺寸、文字、颜色等", "右侧检查器"],
        ["事件", "用户做了什么", "检查器 → 事件脚本"],
        ["脚本", "事件发生时执行的步骤", "底部脚本区"],
        ["变量", "一个可以记住数值或文字的盒子", "脚本里用“设 / 全局”声明"],
    ], widths=[2.8, 7.8, 5.4])



    # ---------------- 1 ----------------
    add_heading(doc, "第 1 章  先认识设计器", 1)
    add_body(doc, "启动方式：在 frost-ui 目录运行 run-designer.cmd。它是推荐方式，会优先通过已签名的 dotnet 宿主启动已构建的 Designer。", indent=True)
    add_callout(doc, "第一次启动的提示", "如果 Windows 或安全软件提示“未知发布者”，这是未签名程序的正常提示。不要使用“以管理员身份运行”；导出和设计都不需要管理员权限。若火绒/360 拦截写入，请把 Designer 或 dotnet 加入信任，或使用 run-designer.cmd。", WARN_FILL, RGBColor(0xB0, 0x6A, 0x00))

    if (SHOTS / "designer.png").exists():
        p = doc.add_paragraph()
        p.alignment = WD_ALIGN_PARAGRAPH.CENTER
        p.paragraph_format.space_after = Pt(2)
        p.add_run().add_picture(str(SHOTS / "designer.png"), width=Cm(15.8))
        add_caption(doc, "设计器主界面。建议先按下面的顺序认识五个区域。")

    add_heading(doc, "五个区域", 2).paragraph_format.page_break_before = True
    add_table(doc, ["区域", "位置", "你可以在这里做什么"], [
        ["页面 / 控件库", "最左侧", "切换页面；把按钮、文字、图片等拖到画板。"],
        ["画板", "中间", "摆放控件、调整尺寸、看标尺和参考线。"],
        ["检查器", "右侧", "修改名称、文字、位置、尺寸、样式、导航、绑定。"],
        ["事件脚本", "底部左侧", "给当前控件的当前事件写逻辑。"],
        ["运行输出", "底部右侧", "看脚本日志和错误，是排错的第一现场。"],
    ], widths=[3.2, 2.8, 10.2])

    add_heading(doc, "第一次操作顺序", 2)
    add_numbered(doc, [
        "启动设计器。默认会打开一个示例设计。",
        "在左侧“页面”里看到“主页面”，说明当前正在编辑这一页。",
        "在画板上点一个按钮，右侧检查器会显示它的属性。",
        "在底部“事件脚本”里看到这个按钮的点击脚本。",
        "点右上角“运行预览”，看真实效果。",
        "点“保存”保存 .frostui.json；点“导出 EXE”生成可运行程序。",
    ])

    add_heading(doc, "三个最重要的按钮", 2)
    add_table(doc, ["按钮", "作用", "什么时候用"], [
        ["运行预览", "在真实 FrostUI 窗口中运行当前设计", "每次改完布局或脚本"],
        ["保存", "保存设计文件 .frostui.json", "阶段完成时；自动保存只是兜底"],
        ["导出 EXE", "生成独立发布目录", "确认预览没问题后"],
    ], widths=[3.2, 8.0, 5.2])



    # ---------------- 2 ----------------
    add_heading(doc, "第 2 章  做出第一个页面", 1)
    add_body(doc, "我们做一个最简单的“问候卡片”：一个标题、一个输入框、一个按钮、一行结果文字。点按钮后，结果文字变成“你好，某某”。", indent=True)

    add_heading(doc, "步骤 1：新建或打开设计", 2)
    add_bullets(doc, [
        "新建设计：顶部“新建”。",
        "打开已有设计：顶部“打开”，选择 .frostui.json。",
        "示例设计：默认打开，适合边看边改。",
    ])

    add_heading(doc, "步骤 2：设置窗口", 2)
    add_body(doc, "点右上角“窗口属性”或左侧“窗口属性与材质”。修改：")
    add_table(doc, ["属性", "建议值", "说明"], [
        ["窗口标题", "问候卡片", "显示在标题栏和任务栏"],
        ["宽度 / 高度", "720 × 480", "先做小一点，便于观察"],
        ["材质", "Acrylic 或 Mica", "毛玻璃效果；Mica 更偏桌面壁纸"],
        ["网格大小", "8", "拖动吸附更整齐"],
    ], widths=[3.4, 4.0, 8.8])

    add_heading(doc, "步骤 3：放控件", 2)
    add_body(doc, "从左侧控件库拖入，或者双击控件快速添加。按顺序放：")
    add_numbered(doc, [
        "卡片：放大一点，作为背景容器。",
        "标签：文字改成“输入你的名字”。",
        "输入框：放在标签下方。",
        "按钮：文字改成“生成问候”。",
        "标签：文字先留空，后面显示结果。",
    ])
    add_body(doc, "控件的 X / Y 是左上角坐标，宽 / 高是尺寸。先摆整齐，不必追求像素级完美，后面可以用对齐和参考线。")

    add_heading(doc, "步骤 4：给控件起好名字", 2)
    add_body(doc, "脚本通过“控件名”找到控件。名字不清楚，逻辑一定难写。", indent=True)
    add_table(doc, ["控件", "建议名称", "原因"], [
        ["输入框", "输入框_名字", "能看出是输入名字的框"],
        ["按钮", "按钮_问候", "能看出是生成问候的按钮"],
        ["结果标签", "标签_结果", "能看出显示结果"],
    ], widths=[4.0, 5.0, 7.0])
    add_callout(doc, "命名规则", "同一页面内不要重名；不要用空格和标点；中文可以。脚本里写“标签_结果.文本 = ...”，名字必须完全一致。", TEAL_FILL, TEAL)

    add_heading(doc, "步骤 5：预览与保存", 2)
    add_body(doc, "先点“运行预览”确认布局，再点“保存”。预览是深拷贝，不会因为预览而改坏设计文件。")



    # ---------------- 3 ----------------
    add_heading(doc, "第 3 章  页面、窗口与跳转", 1)
    add_body(doc, "一个应用通常不止一屏。FrostUI 用“页面”来组织：主页面、设置页、详情页、确认弹窗，都可以是页面。", indent=True)

    add_heading(doc, "页面列表怎么用", 2)
    add_bullets(doc, [
        "新建页面：左侧“+”。",
        "切换页面：单击页面列表中的页面。",
        "改名：页面名称输入框。",
        "复制 / 上移 / 下移：页面列表下方按钮。",
        "删除：至少保留一个页面。",
    ])
    add_body(doc, "每个页面有独立的控件、脚本和参考线；页面还可以单独设置标题、宽高、材质和进入转场。")

    add_heading(doc, "普通页面、窗口、弹窗", 2)
    add_table(doc, ["页面类型", "看起来像", "适合"], [
        ["Page", "当前窗口直接切换内容", "设置页、列表页、主页面"],
        ["Window", "打开一个新的独立窗口", "详情窗口、工具窗口"],
        ["Dialog", "在当前窗口上弹出，通常带遮罩", "确认框、编辑框、登录框"],
    ], widths=[3.2, 7.0, 6.2])

    add_heading(doc, "给按钮设置跳转", 2)
    add_numbered(doc, [
        "选中按钮。",
        "右侧检查器找到“导航”。",
        "动作选择：Switch / Window / Dialog / Open / Close。",
        "目标页面选择你要去的页面。",
        "需要带参数时，在导航参数里写 key=value;id=1。",
        "运行预览，点按钮测试。",
    ])
    add_table(doc, ["动作", "通俗解释"], [
        ["Switch", "当前窗口换一页"],
        ["Window", "开一个新窗口"],
        ["Dialog", "在当前窗口上弹出来"],
        ["Open", "看目标页面的类型，自动决定切换 / 开窗 / 弹窗"],
        ["Close", "关闭当前窗口"],
    ], widths=[3.0, 13.0])

    add_callout(doc, "最容易犯的错", "动作选 Window 但目标页面是 Page，仍然会按你选的动作开窗；想“自动判断”就用 Open。页面类型只是给 Open 和页面管理用的。", WARN_FILL, RGBColor(0xB0, 0x6A, 0x00))

    add_heading(doc, "页面参数与返回", 2)
    add_body(doc, "页面可以带参数。比如主页面打开确认框时带一个 id，确认框启动时读取它。")
    add_code(doc, """// 打开方：传递参数
页面.传参("id", "42")
页面.打开弹窗("确认框")

// 确认框启动脚本：读取参数
设 编号: 文本 = 页面.参数("id")
输出("收到编号：" + 编号)

// 确认框确认按钮：返回结果
页面.设置结果("已确认")""", "页面参数和返回结果的最小示例。")
    add_callout(doc, "重要", "遮罩弹窗不会像系统对话框那样阻塞调用方脚本。不要在打开弹窗后立刻假设结果已经存在；把依赖结果的逻辑放在弹窗自己的脚本里，或使用独立窗口模式。", WARN_FILL, RGBColor(0xB0, 0x6A, 0x00))

    add_heading(doc, "页面启动脚本", 2)
    add_body(doc, "每个页面可以有自己的“启动”脚本。切换到该页面时执行，适合初始化默认值、读取参数、刷新文字。")



    # ---------------- 4 ----------------
    add_heading(doc, "第 4 章  脚本：让页面会思考", 1)
    add_body(doc, "脚本不是从第一行开始背的。先想清楚三件事：什么时候、找到谁、做什么。", indent=True)

    add_heading(doc, "4.1 事件：什么时候", 2)
    add_body(doc, "选中控件，底部会显示这个控件的事件标签。按钮通常是“点击”，输入框通常是“文本改变”，开关是“切换”。在哪个事件里写，就只在那个时刻执行。")
    add_callout(doc, "不要把所有代码都写进“启动”", "启动只在页面打开时执行一次。按钮的点击逻辑要写在按钮的“点击”事件里。", ACCENT_FILL, ACCENT)

    add_heading(doc, "4.2 第一个脚本：输出一句话", 2)
    add_code(doc, """输出("你好，FrostUI")""", "运行预览后，在底部“运行输出”看到这句话。")

    add_heading(doc, "4.3 修改控件文字", 2)
    add_code(doc, """标签_结果.文本 = "你好"
按钮_问候.文本 = "已点击\"""", "格式：控件名.属性 = 新值。")
    add_body(doc, "注意：控件名和属性名都必须是设计器里真实存在的。写错一个字符，就会报“未定义的控件”或“没有属性”。")

    add_heading(doc, "4.4 变量：记住一个值", 2)
    add_code(doc, """设 名字: 文本 = 输入框_名字.文本
标签_结果.文本 = "你好，" + 名字""", "先把输入框的文字放进变量，再拼成结果。")
    add_table(doc, ["类型", "放什么", "例子"], [
        ["文本", "文字", "设 名字: 文本 = \"小明\""],
        ["整数", "整数", "设 数量: 整数 = 3"],
        ["小数", "带小数点的数", "设 价格: 小数 = 9.9"],
        ["逻辑", "真 / 假", "设 已登录: 逻辑 = 真"],
        ["JSON", "结构化数据", "设 数据: JSON = {\"名称\":\"FrostUI\"}"],
    ], widths=[2.6, 5.0, 8.8])

    add_heading(doc, "4.5 条件：如果……否则……", 2)
    add_code(doc, """如果 (计数 >= 3) {
    标签_结果.文本 = "已经点了三次"
} 否则 {
    标签_结果.文本 = "继续点"
}""", "比较运算：== 等于，!= 不等于，> 大于，< 小于，>= 大于等于，<= 小于等于。")

    add_heading(doc, "4.6 完整例子：点击计数", 2)
    add_body(doc, "这是最值得先练熟的一个例子：按钮每点一次，计数加一，并把结果显示出来。")
    add_code(doc, """// 全局变量：切换页面后仍然保留
全局 计数: 整数 = 0

// 每次点击执行
计数 = 计数 + 1
标签_结果.文本 = "点击次数：" + 文本(计数)

如果 (计数 >= 3) {
    标签_结果.文本 = "已经点了三次"
}

输出("当前计数：" + 文本(计数))""", "把这段写在“按钮_计数”的“点击”事件里。")
    add_body(doc, "这里出现了两个新东西：全局变量和文本()。全局变量让计数在页面切换后仍然保留；文本() 把数字变成可以拼接的字符串。")

    add_heading(doc, "4.7 循环：重复做", 2)
    add_code(doc, """循环 3 {
    输出("执行一次")
}

遍历 项 于 [1, 2, 3] {
    输出(文本(项))
}""", "循环适合重复动作；遍历适合逐项处理列表。")
    add_body(doc, "循环里还可以用“跳出”提前结束，“继续”跳过本轮。初学阶段先不用急着用，遇到“只处理前几个”再查附录。")

    add_heading(doc, "4.8 函数：把常用步骤打包", 2)
    add_code(doc, """函数 打招呼(名字: 文本) {
    返回 "你好，" + 名字
}

标签_结果.文本 = 打招呼("小明")""", "函数适合反复使用的逻辑；返回把结果交回调用处。")

    add_heading(doc, "4.9 字典：带名字的一盒数据", 2)
    add_code(doc, """设 用户: JSON = {"名字": "小明", "年龄": 18}
标签_结果.文本 = 用户.名字
输出(文本(用户.年龄))""", "字典用“键”取值；JSON 数组用 [索引] 取值。")

    add_heading(doc, "4.10 出错时先看哪里", 2)
    add_order = [
        "底部“运行输出”的最后一条错误。",
        "错误里的行号，回到脚本对应行。",
        "控件名是否和检查器完全一致。",
        "属性名是否拼对，例如“文本”不是“内容”。",
        "括号、花括号是否成对。",
    ]
    add_numbered(doc, add_order)
    add_callout(doc, "不要猜", "FrostUI 的静态检查会在编辑器里标出未知控件、未知属性、未知函数和基础类型不匹配。先修第一处红色，再看后面的错误。", TEAL_FILL, TEAL)



    # ---------------- 5 ----------------
    add_heading(doc, "第 5 章  数据绑定：少写脚本，让文字自己变", 1)
    add_body(doc, "如果只是把一个变量显示到标签上，不必每次写“标签.文本 = ...”。可以用绑定。", indent=True)

    add_heading(doc, "最短的绑定流程", 2)
    add_numbered(doc, [
        "在脚本里准备一个全局变量，例如 全局 计数: 整数 = 0。",
        "选中要显示它的标签。",
        "检查器“内容”里找到“绑定全局变量”，填 计数。",
        "需要前缀/后缀时，在“绑定格式”里写 点击 {0} 次。",
        "运行预览并点击按钮，标签会自动刷新。",
    ])
    add_code(doc, """全局 计数: 整数 = 0

// 按钮点击
计数 = 计数 + 1""", "标签绑定“计数”，格式填“点击 {0} 次”，不需要在按钮里写标签.文本。")

    add_heading(doc, "可绑定的内容", 2)
    add_table(doc, ["绑定", "适合", "例子"], [
        ["文本", "标签、按钮文字", "显示计数、用户名、状态"],
        ["图片路径", "图片控件", "根据数据切换头像"],
        ["可见性", "任何控件", "登录后显示用户区"],
        ["启用状态", "按钮、输入框", "表单未填完时禁用按钮"],
        ["窗口标题", "整个窗口", "显示当前项目名"],
        ["X / Y / 宽 / 高", "需要动态布局的控件", "根据数据移动/改变尺寸"],
        ["透明度 / 字号 / 圆角 / 数值 / 开关", "视觉和状态", "淡出、放大、进度、开关"],
    ], widths=[5.0, 5.4, 6.0])

    add_callout(doc, "绑定的边界", "绑定是在“启动脚本”和“控件事件执行完”后刷新，不是实时监听每一帧。变量不存在时可以填回退文本。当前不支持双向绑定、表达式和转换器。", WARN_FILL, RGBColor(0xB0, 0x6A, 0x00))



    # ---------------- 6 ----------------
    add_heading(doc, "第 6 章  组件：做一次，用很多次", 1)
    add_body(doc, "一组控件经常重复出现，就可以保存成组件。组件库在左侧“组件 / 片段库”。", indent=True)

    add_heading(doc, "保存组件", 2)
    add_numbered(doc, [
        "选中一个或多个控件。",
        "打开“组件 / 片段库”。",
        "输入名称，例如“问候卡片”。",
        "点“保存选中”。",
    ])
    add_body(doc, "组件保存到当前运行目录下的 components 文件夹。每个组件是一个 .frostui.json，可以导入、导出、备份。")

    add_heading(doc, "插入副本与插入实例", 2)
    add_table(doc, ["方式", "结果", "什么时候用"], [
        ["插入", "普通副本，改一个不影响另一个", "一次性复用"],
        ["插入实例", "记录来源、变体和实例 ID", "希望以后从组件库统一更新"],
    ], widths=[3.2, 7.2, 6.0])

    add_heading(doc, "变体", 2)
    add_body(doc, "组件可以命名为“按钮@主要”“按钮@危险”。插入实例时在“变体名”填“主要”或“危险”，就会优先使用对应变体。找不到变体时回退到基础组件。")

    add_heading(doc, "刷新与覆盖", 2)
    add_bullets(doc, [
        "刷新实例：按组件源重新生成控件，实例 ID 不变。",
        "自动刷新：组件文件保存后，引用它的实例会自动更新。",
        "记录覆盖：保存实例自己的文字、尺寸、字号、粗体、圆角、透明度；刷新时保留。",
        "清覆盖：恢复组件源值。",
        "分离实例：保留控件，断开来源，之后不再自动更新。",
    ])




    # ---------------- 7 ----------------
    add_heading(doc, "第 7 章  图片、列表和表单", 1)
    add_heading(doc, "图片", 2)
    add_numbered(doc, [
        "从控件库拖入“图片”。",
        "检查器“图片资源”里点“选择”，选 PNG/JPG/BMP/GIF。",
        "选择缩放：Uniform 完整显示，UniformToFill 填满并裁切，Fill 拉伸填满。",
        "需要裁掉原图的一部分时，填“裁剪区域”，格式是 左,上,右,下，取值 0–1。",
        "圆角可调；导出时图片会复制到 release 的 assets。",
    ])
    add_callout(doc, "图片路径", "设计文件记录图片路径。导出时会把图片复制到 assets 并改成相对路径；但组件 JSON 不会嵌入图片，换机器要一起带图片。", ACCENT_FILL, ACCENT)

    add_heading(doc, "列表", 2)
    add_body(doc, "列表控件每行一个项目。检查器“列表数据”里一行写一个：")
    add_code(doc, """首页
设置
关于""", "列表目前是行文本列表，不是数据绑定 Repeater。")

    add_heading(doc, "表单校验", 2)
    add_body(doc, "文本框可以设置校验规则：")
    add_table(doc, ["规则", "检查什么", "适合"], [
        ["Required", "不能为空", "用户名、必填项"],
        ["Email", "包含 @ 和 .", "邮箱"],
        ["Number", "能解析为数字", "年龄、数量"],
    ], widths=[3.0, 7.0, 6.0])
    add_body(doc, "自定义错误提示会显示在 ToolTip；校验失败时边框变红。它是提示，不阻止事件脚本执行。")



    # ---------------- 8 ----------------
    add_heading(doc, "第 8 章  动画与页面转场", 1)
    add_body(doc, "动画分两层：单个控件的入场/关键帧动画，和页面之间的转场。", indent=True)

    add_heading(doc, "入场动画", 2)
    add_table(doc, ["选项", "效果", "建议"], [
        ["None", "不动", "静态内容"],
        ["FadeIn", "淡入", "卡片、文字"],
        ["SlideUp", "从下往上滑入", "列表项、提示"],
        ["ScaleIn", "从略小放大", "弹窗、图片"],
    ], widths=[3.2, 7.0, 6.0])
    add_body(doc, "时长建议 200–400ms，延迟用来做错峰入场。循环播放适合等待/呼吸效果，不适合正文。")

    add_heading(doc, "关键帧时间线", 2)
    add_body(doc, "选中控件 → 检查器“编辑动画时间线”。添加关键帧：属性、时间（毫秒）、值、缓动。可用的属性：Opacity、X、Y、Scale、Rotation。")
    add_code(doc, """Opacity = 0   @ 0ms
Opacity = 1   @ 400ms
Scale = 1.05  @ 600ms
Scale = 1     @ 800ms""")

    add_heading(doc, "页面转场", 2)
    add_body(doc, "窗口属性 → 当前页面：设置进入转场和时长。页面切换、遮罩弹窗、独立窗口内容出现时会播放。")
    add_table(doc, ["转场", "效果"], [
        ["None", "直接出现"],
        ["Fade", "淡入"],
        ["SlideLeft / SlideRight", "从一侧滑入"],
        ["Scale", "从略小放大"],
    ], widths=[4.6, 11.2])



    # ---------------- 9 ----------------
    add_heading(doc, "第 9 章  预览、保存和导出", 1)
    add_heading(doc, "预览", 2)
    add_body(doc, "右上角“运行预览”会打开真实 FrostUI 窗口，执行事件脚本和数据绑定。预览使用设计文档的深拷贝，不会直接改坏当前设计。")
    if (SHOTS / "preview.png").exists():
        p = doc.add_paragraph()
        p.alignment = WD_ALIGN_PARAGRAPH.CENTER
        p.paragraph_format.space_after = Pt(2)
        p.add_run().add_picture(str(SHOTS / "preview.png"), width=Cm(14.8))
        add_caption(doc, "真实预览：毛玻璃、控件和脚本都按最终效果运行。")

    add_heading(doc, "保存", 2)
    add_body(doc, "保存为 .frostui.json。下次用“打开”继续编辑。建议在重要阶段手动保存；自动保存只是异常恢复，不是版本管理。")

    add_heading(doc, "导出 EXE", 2).paragraph_format.page_break_before = True
    add_body(doc, "导出对话框先选“发布方式”：")
    add_table(doc, ["方式", "需要源码/SDK", "适合"], [
        ["内置模板 · 单文件", "不需要", "普通用户，直接分发"],
        ["内置模板 · 文件夹", "不需要", "想减少单文件启动开销"],
        ["开发者编译", "需要", "开发调试、自定义运行时"],
    ], widths=[5.0, 4.2, 6.8])
    add_body(doc, "内置模板会复制自包含 Player，不调用 dotnet publish。开发者模式才支持自包含/框架依赖、单文件、ReadyToRun。")
    add_body(doc, "默认导出到当前运行目录下的 <应用名>-release。导出目录会包含 Player、design.frostui.json、assets、README、安装/卸载脚本、ZIP、SHA256 和构建报告。")

    add_callout(doc, "导出被拒绝时", "如果 Windows 或火绒/360 拦截写入，先用 run-designer.cmd 启动，或把 Designer / dotnet 加入信任。不要用管理员权限硬顶 HIPS；换目录也不一定能绕过程序级规则。", WARN_FILL, RGBColor(0xB0, 0x6A, 0x00))

    add_heading(doc, "导出目录里有什么", 3)
    add_table(doc, ["文件 / 目录", "用途"], [
        ["FrostUI.Player.exe", "运行程序，双击启动"],
        ["design.frostui.json", "你的设计，Player 读取它"],
        ["assets/", "导出时复制过来的图片"],
        ["启动.cmd / README.txt", "启动脚本和快速说明"],
        ["安装.cmd / 卸载.cmd", "安装到当前用户、创建快捷方式、卸载"],
        ["checksums.txt / build-report.json", "SHA256 校验和构建记录"],
    ], widths=[7.4, 8.4], font_size=8.5)



    # ---------------- 10 ----------------
    add_heading(doc, "第 10 章  推荐工作流与排错", 1)
    add_heading(doc, "从想法到成品：7 步", 2)
    add_numbered(doc, [
        "纸上画页面结构：哪几屏、每屏放什么。",
        "先只做静态布局：控件、文字、尺寸、对齐。",
        "确定页面关系：哪些是 Page、Window、Dialog。",
        "接导航：按钮动作、目标页面、必要参数。",
        "写最少逻辑：点击后改文字、计数、显示/隐藏。",
        "做数据绑定和组件复用，减少重复脚本。",
        "预览 → 修错 → 保存 → 导出 → 在干净目录运行一次。",
    ])

    add_heading(doc, "常见错误对照表", 2)
    add_table(doc, ["现象", "最可能原因", "怎么修"], [
        ["未定义的控件/对象", "控件名拼错，或不在当前页", "对照检查器名称，逐字复制"],
        ["控件没有属性", "属性名写错，例如“内容”", "用“文本”“可见”等真实属性"],
        ["未知函数", "函数名写错，或页面名别名误用", "查脚本参考窗口；页面 API 可用“页面.方法”"],
        ["类型不匹配", "把文字赋给整数", "用 整数()/文本() 转换"],
        ["页面打不开", "目标页面名/ID 不对", "用页面选择器确认目标"],
        ["图片不见了", "路径失效或资源没复制", "重新选图；导出检查 assets"],
        ["导出的 EXE 不能启动", "缺少 design.frostui.json 或运行时被拦截", "把设计文件放在 EXE 旁；看 player-error.log"],
        ["保存/导出被拒绝", "安全软件 HIPS 按程序拦截", "信任 Designer/dotnet，或换非受控目录"],
    ], widths=[4.4, 5.4, 6.2], font_size=8.3)

    add_heading(doc, "快捷键速查", 2)
    add_table(doc, ["快捷键", "作用"], [
        ["Ctrl+K", "打开命令面板"],
        ["Ctrl+S", "保存设计"],
        ["Ctrl+Z / Ctrl+Y", "撤销 / 重做"],
        ["Ctrl+F / Ctrl+H", "查找 / 查找替换"],
        ["Ctrl+/", "切换选中行注释"],
        ["F5", "运行预览"],
        ["F9", "在当前行切换断点"],
        ["Ctrl+滚轮", "缩放画布"],
        ["中键拖动", "平移画布"],
    ], widths=[4.0, 11.8])

    add_heading(doc, "三个练习", 2)
    add_numbered(doc, [
        "做一个“点击计数”按钮：每点一次加一，到 3 次时显示“达标”。",
        "做一个“输入名字 → 生成问候”的页面，结果用数据绑定而不是每次写标签.文本。",
        "做一个主页面 + 设置页 + 确认弹窗：主页面按钮用 Open 打开设置页，用 Dialog 打开确认框，并传一个 id。",
    ])



    # ---------------- Appendix ----------------
    add_heading(doc, "附录 A  脚本速查", 1)
    add_table(doc, ["写法", "作用"], [
        ["设 名字: 文本 = \"小明\"", "局部变量"],
        ["全局 计数: 整数 = 0", "全局变量，切换页面仍保留"],
        ["如果 (条件) { ... } 否则 { ... }", "条件分支"],
        ["循环 3 { ... }", "重复 3 次"],
        ["遍历 项 于 列表 { ... }", "逐项处理"],
        ["函数 名称(a: 整数) { 返回 ... }", "定义函数"],
        ["跳出 / 继续", "结束本轮 / 跳过本轮"],
        ["控件名.文本 = \"值\"", "修改控件文字"],
        ["控件名.可见 = 假", "隐藏控件"],
        ["点击次数 = 点击次数 + 1", "累加"],
        ["文本(计数) / 整数(文本)", "类型转换"],
        ["输出(值) / 提示(值)", "日志 / 预览 Toast"],
        ["标签_结果.文本 = \"你好，\" + 名字", "字符串拼接"],
        ["页面.切换(\"页面名\")", "切换页面"],
        ["页面.打开窗口(\"页面名\")", "打开独立窗口"],
        ["页面.打开弹窗(\"页面名\")", "打开弹窗"],
        ["页面.传参(\"键\", 值) / 页面.参数(\"键\")", "页面参数"],
        ["页面.设置结果(值) / 页面.结果()", "弹窗返回"],
        ["项目.设置(\"键\", 值) / 项目.获取(\"键\")", "项目级共享数据"],
        ["JSON.解析(文本)", "解析 JSON"],
        ["JSON.取值(数据, \"路径\")", "读取 JSON 字段"],
        ["HTTP.获取(url) / HTTP.获取JSON(url)", "HTTP GET"],
        ["HTTP.提交JSON(url, 数据)", "HTTP POST JSON"],
    ], widths=[9.2, 6.6], font_size=8.4)

    add_heading(doc, "附录 B  名词表", 1)
    add_table(doc, ["名词", "解释"], [
        ["控件", "页面上的一个元素，如按钮、标签、图片。"],
        ["属性", "控件的文字、位置、尺寸、样式等。"],
        ["事件", "用户操作触发的时机，如点击、文本改变。"],
        ["脚本", "事件发生时执行的中文指令。"],
        ["全局变量", "跨事件、跨页面保留的变量。"],
        ["数据绑定", "让控件属性自动跟随变量，不必每次手写赋值。"],
        ["组件", "一组可重复插入的控件。"],
        ["实例", "带来源和实例 ID 的组件副本，可刷新。"],
        ["页面", "一屏内容，可切换、开窗或弹窗。"],
        ["发布/导出", "生成可运行的 EXE 和依赖文件。"],
    ], widths=[4.0, 11.8])

    add_heading(doc, "附录 C  发布前检查清单", 1)
    add_bullets(doc, [
        "所有控件名唯一、可读。",
        "所有按钮的导航动作和目标页面正确。",
        "每个页面至少预览一次，点了所有主要按钮。",
        "图片路径存在；导出后 assets 里有图片。",
        "脚本输出里没有红色错误。",
        "窗口标题、版本、公司、图标已填写。",
        "在空目录导出，并双击 FrostUI.Player.exe 运行一次。",
        "把 release 复制到另一台机器测试（特别是图片和字体）。",
        "需要分发时，考虑代码签名，避免 SmartScreen 警告。",
    ])

    p = doc.add_paragraph()
    p.alignment = WD_ALIGN_PARAGRAPH.CENTER
    p.paragraph_format.space_before = Pt(22)
    r = p.add_run("先把一页做顺，再让页面跳起来。")
    set_run_font(r, size=11, bold=True, color=TEAL)

    doc.core_properties.title = "FrostUI Designer 零基础页面设计与交互说明书"
    doc.core_properties.author = "FrostUI"
    doc.core_properties.subject = "FrostUI Designer 新手教程"
    doc.core_properties.language = "zh-CN"

    OUT.parent.mkdir(parents=True, exist_ok=True)
    doc.save(OUT)
    print(OUT)
    print(f"paragraphs={len(doc.paragraphs)} tables={len(doc.tables)} sections={len(doc.sections)}")


if __name__ == "__main__":
    build()
