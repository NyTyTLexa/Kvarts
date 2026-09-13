#!/usr/bin/env python3
"""Съёмка экранов записки: Playwright + системный Chrome, скриншот элемента, не окна."""

from __future__ import annotations

import json
import os
import sys
import time
from io import BytesIO
from pathlib import Path
from typing import Any
from urllib.error import HTTPError, URLError
from urllib.parse import urlencode
from urllib.request import Request, urlopen

from PIL import Image
from playwright.sync_api import BrowserContext, Page, sync_playwright

ROOT = Path(__file__).resolve().parent
ASSETS = ROOT / "assets"

FRONT = os.environ.get("FRONT_BASE", "http://localhost:5173")
API = os.environ.get("API_BASE", "http://localhost:5165")
KC = os.environ.get("KC_BASE", "http://localhost:8088")
# SHOTS=ris-12-quotes.png — снять только эти файлы; SPEC_ID — конкретная спецификация.
SHOTS = {s.strip() for s in os.environ.get("SHOTS", "").split(",") if s.strip()}
SPEC_ID_ENV = os.environ.get("SPEC_ID") or None

# Ширина окна 1280: боковое меню ещё видно, PNG при dsf=1.5 не шире 1920 (плотность ≤ 12).
VIEWPORT = {"width": 1280, "height": 900}
DSF = 1.5
MAX_DENSITY = 12.0
PRINT_MM = 160


def g(obj: Any, *names: str, default: Any = None) -> Any:
    if not isinstance(obj, dict):
        return default
    for n in names:
        if n in obj and obj[n] is not None:
            return obj[n]
        low = n[:1].lower() + n[1:]
        if low in obj and obj[low] is not None:
            return obj[low]
    return default


def http(
    method: str,
    url: str,
    *,
    token: str | None = None,
    json_body: Any = None,
    data: bytes | None = None,
    headers: dict[str, str] | None = None,
    timeout: float = 60,
) -> tuple[int, bytes]:
    hdrs: dict[str, str] = {}
    if headers:
        hdrs.update(headers)
    body = data
    if json_body is not None:
        body = json.dumps(json_body, ensure_ascii=False).encode("utf-8")
        hdrs.setdefault("Content-Type", "application/json")
    if token:
        hdrs["Authorization"] = f"Bearer {token}"
    req = Request(url, data=body, headers=hdrs, method=method)
    try:
        with urlopen(req, timeout=timeout) as resp:
            return resp.status, resp.read()
    except HTTPError as e:
        raw = e.read() if e.fp else b""
        return e.code, raw
    except URLError as e:
        raise RuntimeError(f"{method} {url}: {e}") from e


def http_json(method: str, url: str, **kw: Any) -> tuple[int, Any]:
    code, raw = http(method, url, **kw)
    parsed: Any = None
    if raw:
        try:
            parsed = json.loads(raw.decode("utf-8"))
        except json.JSONDecodeError:
            parsed = None
    return code, parsed


def token_for(username: str) -> str:
    body = urlencode(
        {
            "client_id": "procurement-api",
            "grant_type": "password",
            "username": username,
            "password": username,
        }
    ).encode()
    code, parsed = http_json(
        "POST",
        f"{KC}/realms/procurement/protocol/openid-connect/token",
        data=body,
        headers={"Content-Type": "application/x-www-form-urlencoded"},
        timeout=20,
    )
    if code != 200 or not parsed or "access_token" not in parsed:
        raise RuntimeError(f"token {username}: HTTP {code} {parsed}")
    return parsed["access_token"]


def stitch_vertical(images: list[Image.Image], gap: int = 20) -> Image.Image:
    if not images:
        raise ValueError("no images")
    w = max(im.width for im in images)
    h = sum(im.height for im in images) + gap * (len(images) - 1)
    out = Image.new("RGB", (w, h), (255, 255, 255))
    y = 0
    for im in images:
        rgb = im.convert("RGB")
        if rgb.width != w:
            canvas = Image.new("RGB", (w, rgb.height), (255, 255, 255))
            canvas.paste(rgb, (0, 0))
            rgb = canvas
        out.paste(rgb, (0, y))
        y += rgb.height + gap
    return out


def save_checked(img: Image.Image, path: Path) -> None:
    img.save(path)
    dens = img.width / PRINT_MM
    flag = "OK" if dens <= MAX_DENSITY else "WIDE"
    print(f"  {path.name}: {img.width}×{img.height}  dens={dens:.2f}  {flag}")
    if dens > MAX_DENSITY:
        print(f"    предупреждение: плотность {dens:.2f} > {MAX_DENSITY}")


def shot_locator(page: Page, selector: str, *, timeout: float = 20_000) -> Image.Image:
    loc = page.locator(selector).first
    loc.wait_for(state="visible", timeout=timeout)
    png = loc.screenshot(type="png")
    return Image.open(BytesIO(png))


def png_of(loc) -> Image.Image:
    return Image.open(BytesIO(loc.screenshot(type="png")))


def cap_h(img: Image.Image, max_h: int) -> Image.Image:
    if img.height > max_h:
        return img.crop((0, 0, img.width, max_h))
    return img


def login_oidc(page: Page, username: str, password: str | None = None) -> None:
    """OIDC redirect: кнопка «учётка организации» → форма Keycloak."""
    password = password or username
    page.goto(FRONT, wait_until="domcontentloaded")
    page.wait_for_timeout(400)
    if page.locator(".app-nav").count() and page.locator(".auth-card").count() == 0:
        return
    page.get_by_text("Войти через учётку организации").click()
    page.wait_for_url("**/realms/procurement/**", timeout=20_000)
    user_box = page.locator("#username, input[name='username']").first
    user_box.wait_for(state="visible", timeout=15_000)
    user_box.fill(username)
    page.locator("#password, input[name='password']").first.fill(password)
    btn = page.locator("#kc-login, button[type='submit'], input[type='submit']").first
    btn.click()
    page.wait_for_selector(".app-frame, .app-nav", timeout=25_000)
    page.wait_for_timeout(300)


def new_session(browser, username: str) -> tuple[BrowserContext, Page]:
    ctx = browser.new_context(viewport=VIEWPORT, device_scale_factor=DSF, locale="ru-RU")
    page = ctx.new_page()
    login_oidc(page, username)
    return ctx, page


def wait_gone_loader(page: Page, timeout_ms: int = 30_000) -> None:
    page.wait_for_timeout(200)
    loaders = page.locator(".page-loader, text=Загрузка…, text=Считаем")
    try:
        loaders.first.wait_for(state="hidden", timeout=min(timeout_ms, 4000))
    except Exception:
        pass


def pick_spec(tok: dict[str, str]) -> dict[str, Any]:
    code, specs = http_json("GET", f"{API}/api/specifications", token=tok["manager"])
    if code != 200 or not isinstance(specs, list) or not specs:
        raise RuntimeError(f"specifications HTTP {code}")
    if SPEC_ID_ENV:
        for s in specs:
            if str(g(s, "id", "Id")) == SPEC_ID_ENV:
                return s
        raise RuntimeError(f"SPEC_ID {SPEC_ID_ENV} нет в списке спецификаций")
    scored: list[tuple[int, dict[str, Any]]] = []
    for s in specs:
        n = int(g(s, "itemsCount", "ItemsCount", default=0) or 0)
        unmatched = int(g(s, "unmatchedCount", "UnmatchedCount", default=0) or 0)
        if n <= 0:
            continue
        # Для скрина сопоставления лучше не 180 строк корпуса.
        score = 0
        if 3 <= n <= 20:
            score += 10
        elif n <= 40:
            score += 5
        if unmatched > 0:
            score += 3
        scored.append((score, s))
    scored.sort(key=lambda x: x[0], reverse=True)
    return (scored[0][1] if scored else specs[0])


def ensure_data() -> dict[str, Any]:
    print("API: проверяем данные…")
    tok = {
        "admin": token_for("admin"),
        "manager": token_for("manager"),
        "commercial": token_for("commercial"),
        "accounting": token_for("accounting"),
        "warehouse": token_for("warehouse"),
    }
    code, products = http_json("GET", f"{API}/api/products?page=1&pageSize=5", token=tok["admin"])
    total = int(g(products, "total", "Total", default=0) or 0) if isinstance(products, dict) else 0
    if code != 200 or total < 5:
        print("  каталог пуст — seed")
        c, _ = http_json(
            "POST",
            f"{API}/api/admin/seed?vendors=8&products=40&maxOffersPerProduct=3",
            token=tok["admin"],
            timeout=180,
        )
        if c != 200:
            print(f"  seed HTTP {c}")

    code, uploads = http_json("GET", f"{API}/api/pricelist/uploads?limit=20", token=tok["manager"])
    uploads_ok = code == 200 and isinstance(uploads, list) and len(uploads) > 0
    print(f"  архив прайсов: {len(uploads) if isinstance(uploads, list) else 0}")

    spec = pick_spec(tok)
    spec_id = g(spec, "id", "Id")
    print(f"  спецификация: {g(spec, 'title', 'Title')} id={spec_id}")

    quotes_only = bool(SHOTS) and SHOTS <= {"ris-12-quotes.png"}
    if quotes_only:
        return {
            "tok": tok,
            "spec_id": spec_id,
            "invoice_id": None,
            "invoice_number": None,
            "receipt_id": None,
            "receipt_order": None,
            "project_id": None,
            "approval_id": None,
            "uploads_ok": uploads_ok,
        }

    code, invoices = http_json("GET", f"{API}/api/invoices", token=tok["accounting"])
    invoice = None
    if code == 200 and isinstance(invoices, list):
        for i in invoices:
            num = str(g(i, "number", "Number") or "")
            if num == "NOC-2026-001":
                invoice = i
                break
        if invoice is None and invoices:
            invoice = invoices[0]
    if invoice is None:
        print("  счёта нет — создаём согласование и счёт")
        invoice = create_invoice_path(tok, spec_id)
    inv_id = g(invoice, "id", "Id")
    inv_num = g(invoice, "number", "Number")
    print(f"  счёт: {inv_num} id={inv_id}")

    code, atts = http_json("GET", f"{API}/api/invoices/{inv_id}/attachments", token=tok["accounting"])
    if code == 200 and isinstance(atts, list) and len(atts) == 0:
        attach_dummy(tok["accounting"], inv_id)

    code, receipts = http_json("GET", f"{API}/api/receipts", token=tok["warehouse"])
    receipt = None
    if code == 200 and isinstance(receipts, list):
        for r in receipts:
            if int(g(r, "discrepancyCount", "DiscrepancyCount", default=0) or 0) > 0:
                receipt = r
                break
        if receipt is None and receipts:
            receipt = receipts[0]
    if receipt is None or int(g(receipt, "discrepancyCount", "DiscrepancyCount", default=0) or 0) == 0:
        receipt = ensure_discrepancy(tok, spec_id)
    print(f"  приёмка: {g(receipt, 'orderNumber', 'OrderNumber')} id={g(receipt, 'id', 'Id')} disc={g(receipt, 'discrepancyCount', 'DiscrepancyCount')}")

    code, projects = http_json("GET", f"{API}/api/projects", token=tok["manager"])
    project = None
    if code == 200 and isinstance(projects, list) and projects:
        project = next((p for p in projects if g(p, "specificationId", "SpecificationId")), projects[0])
    if project is None:
        c, project = http_json(
            "POST",
            f"{API}/api/projects",
            token=tok["manager"],
            json_body={"name": "СКС корпус А", "rp": "Смирнов А.", "specificationId": spec_id, "dueDateUtc": None},
        )
        if c not in (200, 201) or not project:
            raise RuntimeError(f"create project HTTP {c}")
    print(f"  проект: {g(project, 'name', 'Name')} id={g(project, 'id', 'Id')}")

    code, approvals = http_json("GET", f"{API}/api/approvals", token=tok["commercial"])
    approval = None
    if code == 200 and isinstance(approvals, list) and approvals:
        approval = approvals[0]
        for a in approvals:
            if g(a, "specificationId", "SpecificationId") == spec_id:
                approval = a
                break
    print(f"  согласование: {g(approval, 'id', 'Id') if approval else 'нет'}")

    return {
        "tok": tok,
        "spec_id": spec_id,
        "invoice_id": inv_id,
        "invoice_number": inv_num,
        "receipt_id": g(receipt, "id", "Id"),
        "receipt_order": g(receipt, "orderNumber", "OrderNumber"),
        "project_id": g(project, "id", "Id"),
        "approval_id": g(approval, "id", "Id") if approval else None,
        "uploads_ok": uploads_ok,
    }


def create_invoice_path(tok: dict[str, str], spec_id: str) -> dict[str, Any]:
    c, approval = http_json(
        "POST",
        f"{API}/api/approvals",
        token=tok["manager"],
        json_body={"specificationId": spec_id, "strategy": "Balanced", "markupPercent": 18},
    )
    if c not in (200, 201) or not approval:
        raise RuntimeError(f"create approval HTTP {c} {approval}")
    aid = g(approval, "id", "Id")
    http_json(
        "POST",
        f"{API}/api/approvals/{aid}/decision",
        token=tok["commercial"],
        json_body={"approved": True, "comment": "к печати"},
    )
    time.sleep(0.4)
    c, invoices = http_json("GET", f"{API}/api/invoices", token=tok["commercial"])
    if c != 200 or not isinstance(invoices, list):
        raise RuntimeError(f"list invoices HTTP {c}")
    match = [i for i in invoices if g(i, "approvalId", "ApprovalId") == aid]
    if match:
        return match[0]
    c, inv = http_json(
        "POST",
        f"{API}/api/invoices",
        token=tok["manager"],
        json_body={"approvalId": aid, "contract": None, "dueDateUtc": None},
    )
    if c not in (200, 201) or not inv:
        raise RuntimeError(f"create invoice HTTP {c}")
    return inv


def attach_dummy(token: str, invoice_id: str) -> None:
    boundary = "----ZapiskaShot7MA4YWxk"
    content = (
        f"--{boundary}\r\n"
        'Content-Disposition: form-data; name="file"; filename="schet-scan.pdf"\r\n'
        "Content-Type: application/pdf\r\n\r\n"
    ).encode() + b"%PDF-1.1\n1 0 obj<<>>endobj\ntrailer<<>>\n%%EOF\n"
    content += f"\r\n--{boundary}--\r\n".encode()
    code, _ = http(
        "POST",
        f"{API}/api/invoices/{invoice_id}/attachments",
        token=token,
        data=content,
        headers={"Content-Type": f"multipart/form-data; boundary={boundary}"},
    )
    print(f"  вложение счёта HTTP {code}")


def ensure_discrepancy(tok: dict[str, str], spec_id: str) -> dict[str, Any]:
    c, orders = http_json("GET", f"{API}/api/orders", token=tok["manager"])
    order_id = None
    if c == 200 and isinstance(orders, list) and orders:
        mine = [o for o in orders if g(o, "specificationId", "SpecificationId") == spec_id]
        order_id = g((mine or orders)[0], "id", "Id")
    if not order_id:
        c, order = http_json(
            "POST",
            f"{API}/api/orders/from-quote?specId={spec_id}&strategy=Balanced",
            token=tok["manager"],
        )
        if c not in (200, 201) or not order:
            raise RuntimeError(f"create order HTTP {c} {order}")
        order_id = g(order, "id", "Id")
    c, rec = http_json(
        "POST",
        f"{API}/api/receipts/from-order?orderId={order_id}",
        token=tok["warehouse"],
    )
    if c not in (200, 201) or not rec:
        raise RuntimeError(f"create receipt HTTP {c} {rec}")
    rid = g(rec, "id", "Id")
    lines = g(rec, "lines", "Lines") or []
    if lines:
        line = lines[0]
        lid = g(line, "id", "Id")
        ordered = int(g(line, "orderedQty", "OrderedQty", default=1) or 1)
        new_qty = 0 if ordered <= 1 else ordered - 1
        http_json(
            "POST",
            f"{API}/api/receipts/{rid}/lines/{lid}",
            token=tok["warehouse"],
            json_body={"receivedQty": new_qty},
        )
    http_json("POST", f"{API}/api/receipts/{rid}/complete", token=tok["warehouse"])
    c, rec = http_json("GET", f"{API}/api/receipts/{rid}", token=tok["warehouse"])
    if c != 200 or not rec:
        raise RuntimeError(f"get receipt HTTP {c}")
    return rec


def shot_prices(page: Page) -> Image.Image:
    page.goto(f"{FRONT}/prices", wait_until="domcontentloaded")
    wait_gone_loader(page)
    page.get_by_role("heading", name="Прайсы").wait_for(timeout=20_000)
    page.wait_for_timeout(600)
    parts: list[Image.Image] = [shot_locator(page, ".page-head")]
    label = page.locator(".field-label", has_text="Импорт прайс-листа")
    if label.count():
        parts.append(png_of(label.locator("xpath=..")))
    hist = page.get_by_text("История загрузок", exact=True)
    if hist.count():
        parts.append(cap_h(png_of(hist.locator("xpath=..")), 1100))
    return stitch_vertical(parts)


def shot_match(page: Page, spec_id: str) -> Image.Image:
    page.goto(f"{FRONT}/specifications/{spec_id}/match", wait_until="domcontentloaded")
    page.locator(".page-head-title").wait_for(timeout=40_000)
    page.locator(".panel").first.wait_for(timeout=60_000)
    page.wait_for_timeout(400)
    parts = [shot_locator(page, ".page-head")]
    if page.locator(".filter-chip").count():
        parts.append(png_of(page.locator(".filter-chip").first.locator("xpath=..")))
    n = min(page.locator(".panel").count(), 3)
    for i in range(n):
        parts.append(png_of(page.locator(".panel").nth(i)))
    return stitch_vertical(parts)


def shot_quotes(page: Page, spec_id: str) -> Image.Image:
    page.goto(f"{FRONT}/specifications/{spec_id}/quote", wait_until="domcontentloaded")
    page.locator(".quote-board").wait_for(timeout=40_000)
    page.wait_for_timeout(400)
    parts = [shot_locator(page, ".page-head"), shot_locator(page, ".quote-board")]
    if page.locator(".quote-sum").count():
        parts.append(shot_locator(page, ".quote-sum"))
    if page.locator(".doc-list table").count():
        parts.append(cap_h(png_of(page.locator(".doc-list").first), 900))
    return stitch_vertical(parts)


def shot_approval(page: Page, approval_id: str | None) -> Image.Image:
    url = f"{FRONT}/approval"
    if approval_id:
        url += f"?id={approval_id}"
    page.goto(url, wait_until="domcontentloaded")
    page.get_by_role("heading", name="Маржа").wait_for(timeout=20_000)
    page.wait_for_timeout(500)
    if page.locator(".doc-list tbody tr").count():
        page.locator(".doc-list tbody tr").first.click()
        page.wait_for_timeout(400)
    parts = [shot_locator(page, ".page-head")]
    if page.locator(".doc-list").count():
        parts.append(cap_h(png_of(page.locator(".doc-list").first), 520))
    if page.locator(".calc-row").count():
        card = page.locator(".calc-row").locator("xpath=ancestor::div[contains(@style,'padding')][1]")
        try:
            parts.append(png_of(card))
        except Exception:
            parts.append(shot_locator(page, ".calc-row"))
    return stitch_vertical(parts)


def shot_invoice(page: Page, invoice_id: str, number: str | None) -> Image.Image:
    page.goto(f"{FRONT}/invoice?id={invoice_id}", wait_until="domcontentloaded")
    page.get_by_role("heading", name="Счета").wait_for(timeout=20_000)
    page.wait_for_timeout(700)
    if number and page.get_by_text(number).count():
        page.get_by_text(number).first.click()
        page.wait_for_timeout(500)
    elif page.locator(".doc-list tbody tr").count():
        page.locator(".doc-list tbody tr").first.click()
        page.wait_for_timeout(400)
    page.get_by_text("Позиции", exact=True).wait_for(timeout=15_000)
    # Карточка: заголовок «Счёт NOC-…» — прямой родитель с padding.
    title = page.get_by_text("Счёт ", exact=False).filter(has_text="NOC-").first
    if not title.count():
        title = page.locator("div").filter(has_text="Позиции").filter(has_text="Документы").last
    card = title.locator("xpath=ancestor::div[contains(@style,'padding')][1]")
    img = png_of(card)
    # Отрезаем широкую ленту ЖЦ — нужны номер, позиции и вложение.
    return cap_h(img, 1600)


def shot_warehouse(page: Page, order_number: str | None) -> Image.Image:
    page.goto(f"{FRONT}/warehouse", wait_until="domcontentloaded")
    page.get_by_role("heading", name="Склад").wait_for(timeout=20_000)
    page.wait_for_timeout(500)
    row = None
    if order_number:
        row = page.locator(".doc-list tbody tr", has_text=str(order_number)).first
    if row is None or row.count() == 0:
        row = page.locator(".doc-list tbody tr").first
    row.get_by_text("Открыть").click()
    page.get_by_text("Приёмка заказа").wait_for(timeout=15_000)
    page.wait_for_timeout(400)
    parts = [shot_locator(page, ".page-head")]
    title = page.get_by_text("Приёмка заказа")
    if title.count():
        parts.append(png_of(title.locator("xpath=ancestor::div[1]")))
    tables = page.locator("#main table")
    if tables.count():
        parts.append(cap_h(png_of(tables.last), 1100))
    return stitch_vertical(parts)


def shot_projects(page: Page, project_id: str) -> Image.Image:
    page.goto(f"{FRONT}/", wait_until="domcontentloaded")
    page.get_by_role("heading", name="Проекты").wait_for(timeout=20_000)
    page.wait_for_timeout(500)
    # Журнал с боковым меню — общий вид интерфейса.
    journal = cap_h(shot_locator(page, ".app-frame"), 1400)
    page.goto(f"{FRONT}/projects/{project_id}", wait_until="domcontentloaded")
    page.locator(".page-kicker, .page-head-title").first.wait_for(timeout=20_000)
    page.wait_for_timeout(800)
    card = cap_h(shot_locator(page, "#main"), 1400)
    return stitch_vertical([journal, card], gap=24)


def main() -> int:
    ASSETS.mkdir(exist_ok=True)
    ids = ensure_data()
    print("Playwright: Chrome, channel=chrome")
    with sync_playwright() as p:
        try:
            browser = p.chromium.launch(channel="chrome", headless=True)
        except Exception as e:
            print(f"channel=chrome не стартовал ({e}), fallback chromium")
            browser = p.chromium.launch(headless=True)

        jobs: list[tuple[str, str, Any]] = [
            ("manager", "ris-10-prices.png", lambda pg: shot_prices(pg)),
            ("manager", "ris-11-match.png", lambda pg: shot_match(pg, ids["spec_id"])),
            ("manager", "ris-12-quotes.png", lambda pg: shot_quotes(pg, ids["spec_id"])),
            ("manager", "ris-13-approval.png", lambda pg: shot_approval(pg, ids["approval_id"])),
            ("accounting", "ris-14-invoice.png", lambda pg: shot_invoice(pg, ids["invoice_id"], ids["invoice_number"])),
            ("warehouse", "ris-15-warehouse.png", lambda pg: shot_warehouse(pg, ids["receipt_order"])),
            ("manager", "ris-16-projects.png", lambda pg: shot_projects(pg, ids["project_id"])),
        ]
        if SHOTS:
            jobs = [j for j in jobs if j[1] in SHOTS]
            if not jobs:
                raise RuntimeError(f"SHOTS={sorted(SHOTS)} не совпали ни с одним заданием")
        # Группируем по пользователю, чтобы не логиниться семь раз.
        by_user: dict[str, list[tuple[str, Any]]] = {}
        for user, name, fn in jobs:
            by_user.setdefault(user, []).append((name, fn))

        for user, items in by_user.items():
            print(f"вход {user} (OIDC)")
            ctx, page = new_session(browser, user)
            try:
                for name, fn in items:
                    print(f"съёмка {name}")
                    img = fn(page)
                    save_checked(img, ASSETS / name)
            finally:
                ctx.close()
        browser.close()
    print("Готово.")
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except Exception as e:
        print(f"FAIL: {e}", file=sys.stderr)
        raise
