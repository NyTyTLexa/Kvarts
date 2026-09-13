#!/usr/bin/env python3
"""Сборка пояснительной записки из Markdown без pandoc."""

from __future__ import annotations

import re
import zipfile
from pathlib import Path

from docx import Document
from docx.enum.section import WD_SECTION
from docx.enum.style import WD_STYLE_TYPE
from docx.enum.table import WD_CELL_VERTICAL_ALIGNMENT, WD_TABLE_ALIGNMENT
from docx.enum.text import WD_ALIGN_PARAGRAPH, WD_BREAK, WD_LINE_SPACING
from docx.oxml import OxmlElement
from docx.oxml.ns import qn
from docx.shared import Cm, Mm, Pt, RGBColor
from PIL import Image as PILImage


BLACK = RGBColor(0, 0, 0)

ROOT = Path(__file__).resolve().parent
SRC = ROOT / "src"
OUTPUT = ROOT / "Пояснительная_записка.docx"

TASK_SOURCE = "01_task.md"
# Порядок документа: титул -> задание -> содержание -> введение -> разделы 1-6 ->
# заключение -> список литературы -> приложения.
SOURCE_ORDER = [
    "00_intro.md",
    "02_s1_business.md",
    "03_s2_analogs.md",
    "04_s3_concept.md",
    "05_s4_design.md",
    "06_s5_impl.md",
    "07_s6_testing.md",
    "08_conclusion.md",
    "09_references.md",
    "10_appendix.md",
]

FONT = "Times New Roman"
CODE_FONT = "Courier New"
BODY_SIZE = Pt(14)
TABLE_SIZE = Pt(12)
CODE_SIZE = Pt(10)

REQUIRED_HEADINGS = [
    "ЗАДАНИЕ НА КУРСОВУЮ РАБОТУ",
    "ВВЕДЕНИЕ",
    "1 БИЗНЕС-АНАЛИЗ",
    "2 АНАЛИЗ СУЩЕСТВУЮЩИХ РЕШЕНИЙ",
    "3 КОНЦЕПЦИЯ РЕШЕНИЯ",
    "4 ПРОЕКТИРОВАНИЕ РЕШЕНИЯ",
    "5 ПРОГРАММНАЯ РЕАЛИЗАЦИЯ СИСТЕМЫ",
    "6 ФУНКЦИОНАЛЬНОЕ ТЕСТИРОВАНИЕ РЕШЕНИЯ",
    "ЗАКЛЮЧЕНИЕ",
    "СПИСОК ЛИТЕРАТУРЫ",
]
# Структурные элементы по ГОСТ 7.32 печатаются по центру и не нумеруются.
CENTERED_H1 = {
    "ЗАДАНИЕ НА КУРСОВУЮ РАБОТУ",
    "ВВЕДЕНИЕ",
    "ЗАКЛЮЧЕНИЕ",
    "СПИСОК ЛИТЕРАТУРЫ",
}
THEME = (
    "Распределённая информационная система автоматизации закупочных процессов "
    "и интеллектуального подбора оборудования"
)


def set_run_font(run, name=FONT, size=BODY_SIZE, *, bold=None, italic=None):
    run.font.name = name
    run.font.size = size
    # ГОСТ: весь текст чёрный. Без явного цвета Word берёт акцентный из темы, и заголовки синеют.
    run.font.color.rgb = BLACK
    if bold is not None:
        run.bold = bold
    if italic is not None:
        run.italic = italic
    rpr = run._element.get_or_add_rPr()
    fonts = rpr.rFonts
    if fonts is None:
        fonts = OxmlElement("w:rFonts")
        rpr.insert(0, fonts)
    for key in ("w:ascii", "w:hAnsi", "w:eastAsia", "w:cs"):
        fonts.set(qn(key), name)


def set_style_font(style, name=FONT, size=BODY_SIZE, *, bold=None, italic=None):
    style.font.name = name
    style.font.size = size
    style.font.color.rgb = BLACK
    if bold is not None:
        style.font.bold = bold
    if italic is not None:
        style.font.italic = italic
    rpr = style._element.get_or_add_rPr()
    fonts = rpr.rFonts
    if fonts is None:
        fonts = OxmlElement("w:rFonts")
        rpr.insert(0, fonts)
    for key in ("w:ascii", "w:hAnsi", "w:eastAsia", "w:cs"):
        fonts.set(qn(key), name)
    lang = rpr.find(qn("w:lang"))
    if lang is None:
        lang = OxmlElement("w:lang")
        rpr.append(lang)
    lang.set(qn("w:val"), "ru-RU")
    lang.set(qn("w:eastAsia"), "ru-RU")


def get_or_add_style(doc, name, style_type=WD_STYLE_TYPE.PARAGRAPH):
    try:
        return doc.styles[name]
    except KeyError:
        return doc.styles.add_style(name, style_type)


def configure_styles(doc):
    normal = doc.styles["Normal"]
    set_style_font(normal)
    pf = normal.paragraph_format
    pf.alignment = WD_ALIGN_PARAGRAPH.JUSTIFY
    pf.line_spacing_rule = WD_LINE_SPACING.ONE_POINT_FIVE
    pf.first_line_indent = Cm(1.25)
    pf.space_before = Pt(0)
    pf.space_after = Pt(0)
    pf.widow_control = True

    h1 = doc.styles["Heading 1"]
    set_style_font(h1, bold=True)
    h1.paragraph_format.first_line_indent = Cm(0)
    h1.paragraph_format.line_spacing_rule = WD_LINE_SPACING.ONE_POINT_FIVE
    h1.paragraph_format.space_before = Pt(0)
    h1.paragraph_format.space_after = Pt(18)
    h1.paragraph_format.page_break_before = True
    h1.paragraph_format.keep_with_next = True
    h1.paragraph_format.widow_control = True

    h2 = doc.styles["Heading 2"]
    set_style_font(h2, bold=True)
    h2.paragraph_format.first_line_indent = Cm(1.25)
    h2.paragraph_format.line_spacing_rule = WD_LINE_SPACING.ONE_POINT_FIVE
    h2.paragraph_format.space_before = Pt(12)
    h2.paragraph_format.space_after = Pt(6)
    h2.paragraph_format.keep_with_next = True

    h3 = doc.styles["Heading 3"]
    set_style_font(h3, bold=True)
    h3.paragraph_format.first_line_indent = Cm(1.25)
    h3.paragraph_format.line_spacing_rule = WD_LINE_SPACING.ONE_POINT_FIVE
    h3.paragraph_format.space_before = Pt(6)
    h3.paragraph_format.space_after = Pt(3)
    h3.paragraph_format.keep_with_next = True

    contents = get_or_add_style(doc, "Contents Title")
    set_style_font(contents, bold=True)
    contents.paragraph_format.alignment = WD_ALIGN_PARAGRAPH.CENTER
    contents.paragraph_format.first_line_indent = Cm(0)
    contents.paragraph_format.line_spacing_rule = WD_LINE_SPACING.ONE_POINT_FIVE
    contents.paragraph_format.space_after = Pt(18)

    caption = get_or_add_style(doc, "GOST Caption")
    set_style_font(caption)
    caption.paragraph_format.first_line_indent = Cm(0)
    caption.paragraph_format.line_spacing_rule = WD_LINE_SPACING.SINGLE
    caption.paragraph_format.space_before = Pt(6)
    caption.paragraph_format.space_after = Pt(6)
    caption.paragraph_format.keep_with_next = True

    figure_placeholder = get_or_add_style(doc, "Figure Placeholder")
    set_style_font(figure_placeholder, italic=True)
    figure_placeholder.paragraph_format.alignment = WD_ALIGN_PARAGRAPH.CENTER
    figure_placeholder.paragraph_format.first_line_indent = Cm(0)
    figure_placeholder.paragraph_format.line_spacing_rule = WD_LINE_SPACING.SINGLE
    figure_placeholder.paragraph_format.space_before = Pt(12)
    figure_placeholder.paragraph_format.space_after = Pt(6)
    figure_placeholder.paragraph_format.keep_with_next = True

    code = get_or_add_style(doc, "Code Block")
    set_style_font(code, CODE_FONT, CODE_SIZE)
    code.paragraph_format.alignment = WD_ALIGN_PARAGRAPH.LEFT
    code.paragraph_format.first_line_indent = Cm(0)
    code.paragraph_format.left_indent = Cm(0.75)
    code.paragraph_format.right_indent = Cm(0.25)
    code.paragraph_format.line_spacing_rule = WD_LINE_SPACING.SINGLE
    code.paragraph_format.space_before = Pt(0)
    code.paragraph_format.space_after = Pt(0)
    code.paragraph_format.widow_control = False

    appendix_title = get_or_add_style(doc, "Appendix Subtitle")
    set_style_font(appendix_title, bold=True)
    appendix_title.paragraph_format.alignment = WD_ALIGN_PARAGRAPH.CENTER
    appendix_title.paragraph_format.first_line_indent = Cm(0)
    appendix_title.paragraph_format.line_spacing_rule = WD_LINE_SPACING.ONE_POINT_FIVE
    appendix_title.paragraph_format.space_after = Pt(18)


def configure_section(section):
    section.page_width = Mm(210)
    section.page_height = Mm(297)
    section.top_margin = Mm(20)
    section.bottom_margin = Mm(20)
    section.left_margin = Mm(30)
    section.right_margin = Mm(15)
    section.header_distance = Mm(10)
    section.footer_distance = Mm(10)
    section.different_first_page_header_footer = True


def add_field(paragraph, instruction, placeholder=""):
    run = paragraph.add_run()
    begin = OxmlElement("w:fldChar")
    begin.set(qn("w:fldCharType"), "begin")
    begin.set(qn("w:dirty"), "true")
    instr = OxmlElement("w:instrText")
    instr.set(qn("xml:space"), "preserve")
    instr.text = f" {instruction} "
    separate = OxmlElement("w:fldChar")
    separate.set(qn("w:fldCharType"), "separate")
    end = OxmlElement("w:fldChar")
    end.set(qn("w:fldCharType"), "end")
    run._r.extend((begin, instr, separate))
    if placeholder:
        set_run_font(run, size=BODY_SIZE)
        text = OxmlElement("w:t")
        text.text = placeholder
        run._r.append(text)
    run._r.append(end)
    return run


def add_page_number(section):
    footer = section.footer
    p = footer.paragraphs[0]
    p.alignment = WD_ALIGN_PARAGRAPH.CENTER
    p.paragraph_format.first_line_indent = Cm(0)
    p.paragraph_format.space_before = Pt(0)
    p.paragraph_format.space_after = Pt(0)
    add_field(p, "PAGE")

    first = section.first_page_footer
    fp = first.paragraphs[0]
    fp.clear()


def request_field_update(doc):
    settings = doc.settings._element
    update = settings.find(qn("w:updateFields"))
    if update is None:
        update = OxmlElement("w:updateFields")
        settings.append(update)
    update.set(qn("w:val"), "true")


def add_title_paragraph(doc, text, *, bold=False, align=WD_ALIGN_PARAGRAPH.CENTER,
                        before=0, after=0, keep=False):
    p = doc.add_paragraph()
    p.alignment = align
    p.paragraph_format.first_line_indent = Cm(0)
    p.paragraph_format.line_spacing_rule = WD_LINE_SPACING.ONE_POINT_FIVE
    p.paragraph_format.space_before = Pt(before)
    p.paragraph_format.space_after = Pt(after)
    p.paragraph_format.keep_with_next = keep
    r = p.add_run(text)
    set_run_font(r, bold=bold)
    return p


def build_title(doc, path=None):
    """Титульный лист по шаблону кафедры ИИТ ЧелГУ (поля ФИО/группы заполняются вручную)."""
    add_title_paragraph(doc, "МИНОБРНАУКИ РОССИИ", keep=True)
    add_title_paragraph(doc, "Федеральное государственное бюджетное", keep=True)
    add_title_paragraph(doc, "образовательное учреждение высшего образования", keep=True)
    add_title_paragraph(doc, "«Челябинский государственный университет»", bold=True, keep=True)
    add_title_paragraph(doc, "(ФГБОУ ВО «ЧелГУ»)", after=12)
    add_title_paragraph(doc, "Институт информационных технологий", after=6, keep=True)
    add_title_paragraph(
        doc,
        "Кафедра информационных технологий и экономической информатики",
        after=36,
    )
    add_title_paragraph(doc, "КУРСОВАЯ РАБОТА", bold=True, after=6)
    add_title_paragraph(
        doc,
        "по дисциплине «Проектирование и разработка распределенных программных систем»",
        after=18,
    )
    add_title_paragraph(doc, THEME, bold=True, after=0, keep=True)
    add_title_paragraph(doc, "(тема)", after=24)

    add_title_paragraph(
        doc,
        "Выполнил студент ________________________________",
        align=WD_ALIGN_PARAGRAPH.LEFT,
        keep=True,
    )
    add_title_paragraph(doc, "(Ф.И.О.)", align=WD_ALIGN_PARAGRAPH.LEFT, keep=True)
    add_title_paragraph(
        doc,
        "группы ____________________",
        align=WD_ALIGN_PARAGRAPH.LEFT,
        keep=True,
    )
    add_title_paragraph(doc, "заочной формы обучения", align=WD_ALIGN_PARAGRAPH.LEFT, keep=True)
    add_title_paragraph(doc, "направления подготовки", align=WD_ALIGN_PARAGRAPH.LEFT, keep=True)
    add_title_paragraph(
        doc,
        "________________________________________________",
        align=WD_ALIGN_PARAGRAPH.LEFT,
        keep=True,
    )
    add_title_paragraph(doc, "(подпись)", align=WD_ALIGN_PARAGRAPH.LEFT, keep=True)
    add_title_paragraph(
        doc,
        "«____» ____________ 20___ г.",
        align=WD_ALIGN_PARAGRAPH.LEFT,
        after=12,
    )

    table = doc.add_table(rows=1, cols=2)
    table.autofit = True
    tbl = table._tbl
    tbl_pr = tbl.tblPr if tbl.tblPr is not None else OxmlElement("w:tblPr")
    borders = OxmlElement("w:tblBorders")
    for edge in ("top", "left", "bottom", "right", "insideH", "insideV"):
        el = OxmlElement(f"w:{edge}")
        el.set(qn("w:val"), "nil")
        borders.append(el)
    tbl_pr.append(borders)
    left, right = table.rows[0].cells
    left.text = ""
    right.paragraphs[0].clear()

    rp = right.paragraphs[0]
    rp.alignment = WD_ALIGN_PARAGRAPH.LEFT
    rp.paragraph_format.first_line_indent = Cm(0)
    rp.paragraph_format.line_spacing_rule = WD_LINE_SPACING.ONE_POINT_FIVE
    rr = rp.add_run("Научный руководитель")
    set_run_font(rr, bold=True)
    for line in (
        "Фамилия, имя, отчество _________________",
        "Должность ____________________________",
        "Ученая степень ________________________",
        "Ученое звание _________________________",
        "______________________________________",
        "(подпись)",
        "«___» _________ 20____ г.",
    ):
        p = right.add_paragraph()
        p.alignment = WD_ALIGN_PARAGRAPH.LEFT
        p.paragraph_format.first_line_indent = Cm(0)
        p.paragraph_format.line_spacing_rule = WD_LINE_SPACING.ONE_POINT_FIVE
        p.paragraph_format.space_before = Pt(0)
        p.paragraph_format.space_after = Pt(0)
        r = p.add_run(line)
        set_run_font(r)

    add_title_paragraph(doc, "", after=24)
    add_title_paragraph(doc, "Челябинск", keep=True)
    add_title_paragraph(doc, "2026")
    doc.add_page_break()


def add_toc(doc):
    p = doc.add_paragraph(style="Contents Title")
    p.add_run("СОДЕРЖАНИЕ")
    for run in p.runs:
        set_run_font(run, bold=True)

    field_p = doc.add_paragraph()
    field_p.paragraph_format.first_line_indent = Cm(0)
    field_p.paragraph_format.line_spacing_rule = WD_LINE_SPACING.ONE_POINT_FIVE
    add_field(
        field_p,
        'TOC \\o "1-3" \\h \\z \\u',
        "Обновите оглавление в Microsoft Word клавишей F9.",
    )


INLINE_RE = re.compile(r"(`[^`]+`|\*\*[^*]+\*\*|(?<!\*)\*[^*]+\*(?!\*))")
IMAGE_RE = re.compile(r"^!\[(.+?)\]\((.+?)\)$")

def gost_dash(text: str) -> str:
    """В подписях по ГОСТ 7.32 короткое тире: «Рисунок 1 – Название», «Таблица 2 – Название»."""
    return re.sub(r"^(Рисунок|Таблица|Листинг)(\s+[\dА-Я.]+)\s*[—–-]\s*", r"\1\2 – ", text)


# Рабочее поле А4: 210 − 30 − 15 = 165 мм. Рисунок чуть уже, чтобы поля не обрезались.
MAX_FIG_WIDTH_MM = 160.0
MAX_FIG_HEIGHT_MM = 205.0


def add_inline_runs(paragraph, text, *, size=BODY_SIZE, force_bold=False):
    pos = 0
    for match in INLINE_RE.finditer(text):
        if match.start() > pos:
            run = paragraph.add_run(text[pos:match.start()])
            set_run_font(run, size=size, bold=force_bold)
        token = match.group(0)
        if token.startswith("`"):
            run = paragraph.add_run(token[1:-1])
            set_run_font(run, CODE_FONT, Pt(max(10, size.pt - 1)), bold=force_bold)
        elif token.startswith("**"):
            run = paragraph.add_run(token[2:-2])
            set_run_font(run, size=size, bold=True)
        else:
            run = paragraph.add_run(token[1:-1])
            set_run_font(run, size=size, bold=force_bold, italic=True)
        pos = match.end()
    if pos < len(text):
        run = paragraph.add_run(text[pos:])
        set_run_font(run, size=size, bold=force_bold)
    if not text:
        run = paragraph.add_run("")
        set_run_font(run, size=size, bold=force_bold)


def resolve_image_path(rel: str) -> Path:
    path = Path(rel)
    if path.is_absolute():
        return path
    return (ROOT / rel).resolve()


def add_figure(doc, caption: str, rel: str):
    """Вставляет PNG/JPEG по центру и подпись ГОСТ «Рисунок N — Название»."""
    path = resolve_image_path(rel)
    if not path.exists():
        p = doc.add_paragraph(style="Figure Placeholder")
        add_inline_runs(p, f"[Рисунок не найден: {rel}]")
        cap = doc.add_paragraph(style="GOST Caption")
        cap.alignment = WD_ALIGN_PARAGRAPH.CENTER
        cap.paragraph_format.keep_with_next = False
        add_inline_runs(cap, caption)
        return

    with PILImage.open(path) as image:
        width_px, height_px = image.size
    if width_px <= 0 or height_px <= 0:
        raise ValueError(f"Пустое изображение: {path}")

    width_mm = MAX_FIG_WIDTH_MM
    height_mm = width_mm * height_px / width_px
    if height_mm > MAX_FIG_HEIGHT_MM:
        height_mm = MAX_FIG_HEIGHT_MM
        width_mm = height_mm * width_px / height_px

    picture = doc.add_paragraph()
    picture.alignment = WD_ALIGN_PARAGRAPH.CENTER
    pf = picture.paragraph_format
    pf.first_line_indent = Cm(0)
    pf.line_spacing_rule = WD_LINE_SPACING.SINGLE
    pf.space_before = Pt(12)
    pf.space_after = Pt(0)
    pf.keep_with_next = True
    picture.add_run().add_picture(str(path), width=Mm(width_mm))

    cap = doc.add_paragraph(style="GOST Caption")
    cap.alignment = WD_ALIGN_PARAGRAPH.CENTER
    cap.paragraph_format.keep_with_next = False
    cap.paragraph_format.space_before = Pt(6)
    cap.paragraph_format.space_after = Pt(12)
    add_inline_runs(cap, gost_dash(caption))
    return picture


def add_body_paragraph(doc, text, *, appendix_subtitle=False):
    if text.startswith("[Рисунок ") and text.endswith("]"):
        p = doc.add_paragraph(style="Figure Placeholder")
        add_inline_runs(p, text)
        return p
    if text.startswith("Рисунок "):
        p = doc.add_paragraph(style="GOST Caption")
        p.alignment = WD_ALIGN_PARAGRAPH.CENTER
        p.paragraph_format.keep_with_next = False
        add_inline_runs(p, gost_dash(text))
        return p
    if text.startswith("Таблица ") or text.startswith("Листинг "):
        # Подпись таблицы и листинга ставится слева и над самим объектом.
        p = doc.add_paragraph(style="GOST Caption")
        p.alignment = WD_ALIGN_PARAGRAPH.LEFT
        p.paragraph_format.keep_with_next = True
        add_inline_runs(p, gost_dash(text))
        return p
    if appendix_subtitle:
        p = doc.add_paragraph(style="Appendix Subtitle")
        add_inline_runs(p, text, force_bold=True)
        return p
    p = doc.add_paragraph()
    add_inline_runs(p, text)
    return p


def set_num_properties(paragraph, num_id, level=0):
    ppr = paragraph._p.get_or_add_pPr()
    numpr = ppr.find(qn("w:numPr"))
    if numpr is None:
        numpr = OxmlElement("w:numPr")
        ppr.append(numpr)
    ilvl = OxmlElement("w:ilvl")
    ilvl.set(qn("w:val"), str(level))
    nid = OxmlElement("w:numId")
    nid.set(qn("w:val"), str(num_id))
    numpr.extend((ilvl, nid))


def create_numbering(doc, *, ordered, start=1):
    root = doc.part.numbering_part.element
    abs_ids = [int(x.get(qn("w:abstractNumId"))) for x in root.findall(qn("w:abstractNum"))]
    num_ids = [int(x.get(qn("w:numId"))) for x in root.findall(qn("w:num"))]
    abstract_id = max(abs_ids, default=-1) + 1
    num_id = max(num_ids, default=0) + 1

    abstract = OxmlElement("w:abstractNum")
    abstract.set(qn("w:abstractNumId"), str(abstract_id))
    multi = OxmlElement("w:multiLevelType")
    multi.set(qn("w:val"), "singleLevel")
    abstract.append(multi)

    lvl = OxmlElement("w:lvl")
    lvl.set(qn("w:ilvl"), "0")
    start_el = OxmlElement("w:start")
    start_el.set(qn("w:val"), str(start))
    fmt = OxmlElement("w:numFmt")
    fmt.set(qn("w:val"), "decimal" if ordered else "bullet")
    text_el = OxmlElement("w:lvlText")
    # ГОСТ 7.32: перечисления оформляют дефисом (короткое тире), а не маркером-точкой.
    text_el.set(qn("w:val"), "%1." if ordered else "–")
    suff = OxmlElement("w:suff")
    suff.set(qn("w:val"), "tab")
    ppr = OxmlElement("w:pPr")
    tabs = OxmlElement("w:tabs")
    tab = OxmlElement("w:tab")
    tab.set(qn("w:val"), "num")
    tab.set(qn("w:pos"), "709")
    tabs.append(tab)
    ind = OxmlElement("w:ind")
    ind.set(qn("w:left"), "709")
    ind.set(qn("w:hanging"), "354")
    ppr.extend((tabs, ind))
    lvl.extend((start_el, fmt, text_el, suff, ppr))
    if not ordered:
        rpr = OxmlElement("w:rPr")
        fonts = OxmlElement("w:rFonts")
        fonts.set(qn("w:ascii"), FONT)
        fonts.set(qn("w:hAnsi"), FONT)
        rpr.append(fonts)
        lvl.append(rpr)
    abstract.append(lvl)
    root.append(abstract)

    num = OxmlElement("w:num")
    num.set(qn("w:numId"), str(num_id))
    abs_ref = OxmlElement("w:abstractNumId")
    abs_ref.set(qn("w:val"), str(abstract_id))
    num.append(abs_ref)
    root.append(num)
    return num_id


def add_list(doc, items, *, ordered, start=1):
    num_id = create_numbering(doc, ordered=ordered, start=start)
    for item in items:
        p = doc.add_paragraph()
        p.paragraph_format.first_line_indent = Cm(0)
        p.paragraph_format.left_indent = Cm(1.25)
        p.paragraph_format.line_spacing_rule = WD_LINE_SPACING.ONE_POINT_FIVE
        set_num_properties(p, num_id)
        add_inline_runs(p, item)


def split_table_row(line):
    return [cell.strip() for cell in line.strip().strip("|").split("|")]


def is_table_separator(line):
    cells = split_table_row(line)
    return bool(cells) and all(re.fullmatch(r":?-{3,}:?", cell) for cell in cells)


def set_cell_margins(cell, top=70, start=90, bottom=70, end=90):
    tc = cell._tc
    tcpr = tc.get_or_add_tcPr()
    margins = tcpr.first_child_found_in("w:tcMar")
    if margins is None:
        margins = OxmlElement("w:tcMar")
        tcpr.append(margins)
    for name, value in (("top", top), ("start", start), ("bottom", bottom), ("end", end)):
        node = margins.find(qn(f"w:{name}"))
        if node is None:
            node = OxmlElement(f"w:{name}")
            margins.append(node)
        node.set(qn("w:w"), str(value))
        node.set(qn("w:type"), "dxa")


def forbid_row_split(row):
    """Запрещает разрывать строку таблицы между страницами (ГОСТ 7.32, п. 6.2)."""
    trpr = row._tr.get_or_add_trPr()
    cant = OxmlElement("w:cantSplit")
    cant.set(qn("w:val"), "true")
    trpr.append(cant)


def set_fixed_layout(table):
    """Фиксированная раскладка: Word не сжимает колонки произвольно и не рвёт слова."""
    tbl_pr = table._tbl.tblPr
    layout = tbl_pr.find(qn("w:tblLayout"))
    if layout is None:
        layout = OxmlElement("w:tblLayout")
        tbl_pr.append(layout)
    layout.set(qn("w:type"), "fixed")


def column_widths_mm(rows, total_mm=165.0, min_mm=16.0):
    """Ширины колонок пропорционально длине самого длинного слова и объёму текста в колонке."""
    cols = max(len(r) for r in rows)
    weights = []
    for ci in range(cols):
        cells = [r[ci] if ci < len(r) else "" for r in rows]
        longest_word = max((len(w) for c in cells for w in c.split()), default=1)
        avg_len = sum(len(c) for c in cells) / max(len(cells), 1)
        weights.append(max(longest_word * 1.6, avg_len, 6.0))
    scale = (total_mm - min_mm * cols) / max(sum(weights), 1e-6)
    widths = [min_mm + w * scale for w in weights]
    factor = total_mm / sum(widths)
    return [w * factor for w in widths]


def repeat_table_header(row):
    trpr = row._tr.get_or_add_trPr()
    tbl_header = OxmlElement("w:tblHeader")
    tbl_header.set(qn("w:val"), "true")
    trpr.append(tbl_header)


def add_table(doc, rows):
    width = max(len(r) for r in rows)
    normalized = [r + [""] * (width - len(r)) for r in rows]
    table = doc.add_table(rows=len(normalized), cols=width)
    table.style = "Table Grid"          # только чёрные линии, без заливки и цвета
    table.alignment = WD_TABLE_ALIGNMENT.CENTER
    table.autofit = False
    set_fixed_layout(table)

    # Чем больше колонок, тем мельче кегль: ГОСТ 7.32 разрешает в таблицах меньший размер.
    size = TABLE_SIZE if width <= 4 else (Pt(11) if width <= 6 else Pt(10))
    widths = column_widths_mm(normalized)

    for ri, values in enumerate(normalized):
        row = table.rows[ri]
        forbid_row_split(row)
        if ri == 0:
            repeat_table_header(row)
        for ci, value in enumerate(values):
            cell = row.cells[ci]
            cell.width = Mm(widths[ci])
            cell.vertical_alignment = WD_CELL_VERTICAL_ALIGNMENT.CENTER
            set_cell_margins(cell)
            p = cell.paragraphs[0]
            p.alignment = WD_ALIGN_PARAGRAPH.CENTER if ri == 0 else WD_ALIGN_PARAGRAPH.LEFT
            p.paragraph_format.first_line_indent = Cm(0)
            p.paragraph_format.line_spacing_rule = WD_LINE_SPACING.SINGLE
            p.paragraph_format.space_before = Pt(0)
            p.paragraph_format.space_after = Pt(0)
            add_inline_runs(p, value, size=size, force_bold=(ri == 0))
    doc.add_paragraph().paragraph_format.space_after = Pt(0)
    return table


def is_structural(line):
    stripped = line.strip()
    return (
        not stripped
        or stripped.startswith("#")
        or stripped.startswith("```")
        or stripped.startswith("![")
        or bool(re.match(r"^[-*]\s+", stripped))
        or bool(re.match(r"^\d+\.\s+", stripped))
        or (stripped.startswith("|") and stripped.endswith("|"))
    )


def add_markdown(doc, path):
    lines = path.read_text(encoding="utf-8").splitlines()
    i = 0
    last_h1 = ""
    expect_appendix_subtitle = False
    while i < len(lines):
        raw = lines[i]
        line = raw.strip()
        if not line:
            i += 1
            continue

        image_match = IMAGE_RE.match(line)
        if image_match:
            add_figure(doc, image_match.group(1).strip(), image_match.group(2).strip())
            i += 1
            if i < len(lines) and lines[i].strip() == image_match.group(1).strip():
                i += 1
            continue

        if line.startswith("```"):
            i += 1
            code_lines = []
            while i < len(lines) and not lines[i].strip().startswith("```"):
                code_lines.append(lines[i].rstrip())
                i += 1
            if i < len(lines):
                i += 1
            for code_line in code_lines or [""]:
                p = doc.add_paragraph(style="Code Block")
                r = p.add_run(code_line or " ")
                set_run_font(r, CODE_FONT, CODE_SIZE)
            continue

        heading = re.match(r"^(#{1,3})\s+(.+)$", line)
        if heading:
            level = len(heading.group(1))
            text = heading.group(2).strip()
            if level == 1:
                text = text.upper()
            p = doc.add_paragraph(style=f"Heading {level}")
            if level == 1:
                last_h1 = text
                expect_appendix_subtitle = text.startswith("ПРИЛОЖЕНИЕ ")
                p.alignment = (
                    WD_ALIGN_PARAGRAPH.CENTER
                    if text in CENTERED_H1 or text.startswith("ПРИЛОЖЕНИЕ ")
                    else WD_ALIGN_PARAGRAPH.LEFT
                )
                if not getattr(add_markdown, "_page_break_h1", True):
                    p.paragraph_format.page_break_before = False
                    add_markdown._page_break_h1 = True
            add_inline_runs(p, text, force_bold=True)
            i += 1
            continue

        if line.startswith("|") and line.endswith("|") and i + 1 < len(lines) and is_table_separator(lines[i + 1]):
            rows = [split_table_row(line)]
            i += 2
            while i < len(lines):
                candidate = lines[i].strip()
                if not (candidate.startswith("|") and candidate.endswith("|")):
                    break
                rows.append(split_table_row(candidate))
                i += 1
            add_table(doc, rows)
            continue

        bullet = re.match(r"^[-*]\s+(.+)$", line)
        if bullet:
            items = []
            while i < len(lines):
                match = re.match(r"^\s*[-*]\s+(.+)$", lines[i])
                if not match:
                    break
                items.append(match.group(1).strip())
                i += 1
            add_list(doc, items, ordered=False)
            continue

        numbered = re.match(r"^(\d+)\.\s+(.+)$", line)
        if numbered:
            start = int(numbered.group(1))
            items = []
            while i < len(lines):
                match = re.match(r"^\s*\d+\.\s+(.+)$", lines[i])
                if not match:
                    break
                items.append(match.group(1).strip())
                i += 1
            add_list(doc, items, ordered=True, start=start)
            continue

        parts = [line]
        i += 1
        while i < len(lines) and not is_structural(lines[i]):
            parts.append(lines[i].strip())
            i += 1
        paragraph_text = " ".join(parts)
        use_appendix_subtitle = (
            expect_appendix_subtitle
            and last_h1.startswith("ПРИЛОЖЕНИЕ ")
            and paragraph_text.isupper()
        )
        add_body_paragraph(doc, paragraph_text, appendix_subtitle=use_appendix_subtitle)
        expect_appendix_subtitle = False


def source_word_count():
    names = [TASK_SOURCE, *SOURCE_ORDER]
    text = "\n".join((SRC / name).read_text(encoding="utf-8") for name in names)
    return len(re.findall(r"[A-Za-zА-Яа-яЁё0-9]+(?:[-–][A-Za-zА-Яа-яЁё0-9]+)*", text))


def verify_output(path):
    with zipfile.ZipFile(path) as archive:
        bad = archive.testzip()
        if bad:
            raise RuntimeError(f"Повреждённый элемент DOCX: {bad}")
        document_xml = archive.read("word/document.xml").decode("utf-8")
        footer_xml = "".join(
            archive.read(name).decode("utf-8")
            for name in archive.namelist()
            if re.fullmatch(r"word/footer\d+\.xml", name)
        )
        if 'TOC \\o "1-3"' not in document_xml:
            raise RuntimeError("Поле TOC не найдено")
        if "PAGE" not in footer_xml:
            raise RuntimeError("Поле номера страницы не найдено")
        if "w:titlePg" not in document_xml:
            raise RuntimeError("Не настроен отдельный первый колонтитул")

    check = Document(path)
    texts = [p.text.strip() for p in check.paragraphs if p.text.strip()]
    missing = [h for h in REQUIRED_HEADINGS if h not in texts]
    if missing:
        raise RuntimeError("Не найдены обязательные разделы: " + ", ".join(missing))
    heading_count = sum(1 for p in check.paragraphs if p.style.name.startswith("Heading"))
    cell_paragraphs = sum(len(cell.paragraphs) for table in check.tables for row in table.rows for cell in row.cells)
    captions = [p.text.strip() for p in check.paragraphs if p.text.strip().startswith("Рисунок ")]
    placeholders = [p.text.strip() for p in check.paragraphs if p.text.strip().startswith("[Рисунок ")]
    missing_images = [p.text.strip() for p in check.paragraphs if p.text.strip().startswith("[Рисунок не найден:")]
    inline_shapes = len(check.inline_shapes)
    print(f"Создан: {path}")
    print(f"Размер: {path.stat().st_size:,} байт")
    print(f"Слов в Markdown (без титула): {source_word_count():,}")
    print(f"Абзацев основного потока: {len(check.paragraphs):,}")
    print(f"Абзацев в ячейках: {cell_paragraphs:,}")
    print(f"Таблиц: {len(check.tables)}")
    print(f"Заголовков: {heading_count}")
    print(f"Рисунков (InlineShape): {inline_shapes}")
    print(f"Подписей рисунков: {len(captions)}")
    for caption in captions:
        print(f"  {caption}")
    if placeholders:
        print(f"Заглушек [Рисунок …]: {len(placeholders)}")
        for item in placeholders:
            print(f"  {item}")
        raise RuntimeError("В документе остались заглушки [Рисунок …]")
    if missing_images:
        print(f"Отсутствующие файлы рисунков: {len(missing_images)}")
        for item in missing_images:
            print(f"  {item}")
        raise RuntimeError("Не найдены файлы рисунков")
    if inline_shapes != len(captions):
        print(f"Предупреждение: InlineShape={inline_shapes}, подписей={len(captions)}")
    print("Обязательные разделы: OK")
    print("ZIP/OOXML, TOC и PAGE: OK")


def build():
    missing_sources = [name for name in [TASK_SOURCE, *SOURCE_ORDER] if not (SRC / name).exists()]
    if missing_sources:
        raise FileNotFoundError("Нет исходников: " + ", ".join(missing_sources))

    doc = Document()
    configure_styles(doc)
    section = doc.sections[0]
    configure_section(section)
    add_page_number(section)
    request_field_update(doc)

    props = doc.core_properties
    props.title = THEME
    props.subject = "Пояснительная записка к курсовой работе"
    props.author = "[ФИО студента]"
    props.keywords = "закупки, коммерческое предложение, микросервисы, TF-IDF, логистическая регрессия"

    build_title(doc)
    add_markdown._page_break_h1 = False
    add_markdown(doc, SRC / TASK_SOURCE)
    doc.add_page_break()
    add_toc(doc)
    for name in SOURCE_ORDER:
        add_markdown(doc, SRC / name)

    doc.save(OUTPUT)
    verify_output(OUTPUT)


if __name__ == "__main__":
    build()
