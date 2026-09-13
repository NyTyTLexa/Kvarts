#!/usr/bin/env python3
"""Рендер недостающих рисунков записки: HTML → PNG (Playwright) и диаграммы анкеты (Pillow)."""

from __future__ import annotations

import math
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont
from playwright.sync_api import sync_playwright

ROOT = Path(__file__).resolve().parent
DIAGRAMS = ROOT / "diagrams"
ASSETS = ROOT / "assets"

HTML_JOBS = [
    # Схемы 04 и 06 пересобраны после переключения маршрутов шлюза (12.5): прежние картинки
    # рисовались до cutover и подписывали вынесенные сервисы как «не переключены».
    ("distributed.html", "ris-04-distributed.png"),
    ("strangler.html", "ris-06-strangler.png"),
    ("asis.html", "ris-17-asis.png"),
    ("impact.html", "ris-18-impact.png"),
    ("context.html", "ris-19-context.png"),
    ("usecases.html", "ris-20-usecases.png"),
    ("cjm.html", "ris-21-cjm.png"),
    ("usm.html", "ris-22-usm.png"),
    ("components.html", "ris-23-components.png"),
]

INK = (31, 42, 68)
LINE = (59, 90, 138)
FILL = (214, 227, 240)
ACCENT = (43, 76, 126)
MUTED = (90, 106, 128)
WHITE = (255, 255, 255)
PALETTE = [
    (43, 76, 126),
    (84, 130, 171),
    (142, 176, 199),
    (217, 234, 211),
    (255, 242, 204),
    (244, 204, 204),
]


def font(size: int, *, bold: bool = False) -> ImageFont.FreeTypeFont:
    name = "arialbd.ttf" if bold else "arial.ttf"
    path = Path(r"C:\Windows\Fonts") / name
    return ImageFont.truetype(str(path), size)


def screenshot_html(html_path: Path, out_path: Path) -> None:
    uri = html_path.resolve().as_uri()
    with sync_playwright() as p:
        browser = None
        try:
            browser = p.chromium.launch(channel="chrome", headless=True)
        except Exception:
            browser = p.chromium.launch(headless=True)
        page = browser.new_page(
            viewport={"width": 1700, "height": 1100},
            device_scale_factor=2,
        )
        page.goto(uri, wait_until="networkidle")
        # Название рисунка несёт подпись по ГОСТ под ним, внутри картинки заголовок не нужен.
        page.add_style_tag(content=".sheet > h1 { display: none !important; }")
        page.wait_for_timeout(250)
        overflow = page.evaluate(
            """() => {
              const s = document.querySelector('.sheet');
              if (!s) return {error: 'no .sheet'};
              const offenders = [];
              for (const el of s.querySelectorAll('*')) {
                if (el.scrollWidth - el.clientWidth > 1) {
                  offenders.push({
                    tag: el.tagName,
                    cls: String(el.className || '').slice(0, 80),
                    sw: el.scrollWidth,
                    cw: el.clientWidth,
                  });
                }
              }
              return {
                scrollWidth: s.scrollWidth,
                clientWidth: s.clientWidth,
                sheetOverflow: s.scrollWidth - s.clientWidth,
                offenders: offenders.slice(0, 12),
              };
            }"""
        )
        if overflow.get("error") or overflow.get("sheetOverflow", 0) > 0 or overflow.get("offenders"):
            print(f"OVERFLOW {html_path.name}: {overflow}")
        else:
            print(f"layout-ok {html_path.name}: {overflow['clientWidth']}px")
        sheet = page.locator(".sheet")
        sheet.screenshot(path=str(out_path), type="png")
        browser.close()
    print(f"HTML → {out_path.name}")


def crop_white(img: Image.Image, margin: int = 24) -> Image.Image:
    """Обрезает пустые поля: без внутреннего заголовка картинка иначе занимает лишнюю высоту."""
    grey = img.convert("L")
    inverted = Image.eval(grey, lambda v: 255 - v)
    box = inverted.getbbox()
    if not box:
        return img
    left = max(box[0] - margin, 0)
    top = max(box[1] - margin, 0)
    right = min(box[2] + margin, img.width)
    bottom = min(box[3] + margin, img.height)
    return img.crop((left, top, right, bottom))


def _legend(draw: ImageDraw.ImageDraw, items: list[tuple[str, tuple[int, int, int]]], x: int, y: int, f) -> None:
    for i, (label, color) in enumerate(items):
        yy = y + i * 48
        draw.rounded_rectangle((x, yy, x + 34, yy + 28), 4, fill=color, outline=LINE)
        draw.text((x + 48, yy), label, font=f, fill=INK)


def bar_chart(path: Path, title: str, rows: list[tuple[str, float]], unit: str = "%") -> None:
    w, h = 1600, 980
    img = Image.new("RGB", (w, h), WHITE)
    draw = ImageDraw.Draw(img)
    # Кегли рассчитаны на печать: при ширине 160 мм картинка уменьшается примерно в 2,6 раза.
    label_f, axis_f = font(30), font(28)
    # Заголовок внутри картинки не рисуем: название рисунка даёт подпись по ГОСТ.
    left, right, top, bottom = 470, 1440, 70, 860
    max_v = max(v for _, v in rows) or 1
    bar_h = (bottom - top) / len(rows)
    for i, (name, value) in enumerate(rows):
        y0 = top + i * bar_h + 12
        y1 = y0 + bar_h - 28
        x1 = left + (right - left) * (value / max_v)
        color = PALETTE[i % len(PALETTE)]
        draw.rounded_rectangle((left, y0, x1, y1), 6, fill=color, outline=LINE)
        draw.text((left - 16, (y0 + y1) / 2), name, font=label_f, fill=INK, anchor="rm")
        draw.text((x1 + 12, (y0 + y1) / 2), f"{value:.0f}{unit}", font=axis_f, fill=INK, anchor="lm")
    draw.line((left, top - 8, left, bottom), fill=LINE, width=2)
    note = "Модельная оценка. Полевой опрос не проводился."
    draw.text((w / 2, h - 40), note, font=font(24), fill=MUTED, anchor="mt")
    crop_white(img).save(path)
    print(f"Pillow → {path.name}")


def pie_chart(path: Path, title: str, slices: list[tuple[str, float]]) -> None:
    w, h = 1600, 980
    img = Image.new("RGB", (w, h), WHITE)
    draw = ImageDraw.Draw(img)
    # Заголовок внутри картинки не рисуем: название рисунка даёт подпись по ГОСТ.
    cx, cy, r = 560, 480, 280
    total = sum(v for _, v in slices)
    start = -90
    legend_items = []
    for i, (name, value) in enumerate(slices):
        extent = 360 * value / total
        color = PALETTE[i % len(PALETTE)]
        draw.pieslice((cx - r, cy - r, cx + r, cy + r), start, start + extent, fill=color, outline=WHITE)
        start += extent
        legend_items.append((f"{name} — {value:.0f} %", color))
    draw.ellipse((cx - 90, cy - 90, cx + 90, cy + 90), fill=WHITE)
    _legend(draw, legend_items, 940, 260, font(28))
    draw.text((w / 2, h - 40), "Модельная оценка. Полевой опрос не проводился.", font=font(24), fill=MUTED, anchor="mt")
    crop_white(img).save(path)
    print(f"Pillow → {path.name}")


def render_survey() -> None:
    bar_chart(
        ASSETS / "ris-24-survey-time.png",
        "Оценка затрат времени РП на разбор прайсов (модельная)",
        [
            ("менее 2 ч / нед.", 12),
            ("2–8 ч / нед.", 28),
            ("8–16 ч / нед.", 40),
            ("более 16 ч / нед.", 20),
        ],
    )
    pie_chart(
        ASSETS / "ris-25-survey-pain.png",
        "Основная проблема текущего процесса (модельная)",
        [
            ("Нет сравнимых вариантов КП", 32),
            ("Устаревшие прайсы", 24),
            ("Разные названия одного товара", 22),
            ("Согласование в переписке", 14),
            ("Прочее", 8),
        ],
    )
    bar_chart(
        ASSETS / "ris-26-survey-need.png",
        "Оценка потребности в функциях системы (модельная)",
        [
            ("Импорт произвольного Excel", 92),
            ("Несколько вариантов КП", 88),
            ("Сопоставление с вероятностью", 81),
            ("Маршрут согласования", 74),
            ("Снимок цены в заказе/счёте", 70),
            ("Интеграция с 1С/WMS", 61),
            ("Аналитические отчёты", 44),
            ("Мобильное приложение", 18),
        ],
    )


def render_html() -> None:
    for html_name, png_name in HTML_JOBS:
        screenshot_html(DIAGRAMS / html_name, ASSETS / png_name)


def main() -> None:
    ASSETS.mkdir(exist_ok=True)
    render_html()
    render_survey()
    print("Готово.")


if __name__ == "__main__":
    main()
