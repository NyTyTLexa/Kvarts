#!/usr/bin/env python3
"""Live E2E of the procurement stand (overnight fixes). Stdlib only. Do not change product code."""
from __future__ import annotations

import json
import os
import sys
import time
import uuid
import zipfile
from io import BytesIO
from typing import Any
from urllib.error import HTTPError, URLError
from urllib.parse import urlencode
from urllib.request import Request, urlopen

API = os.environ.get("API_BASE", "http://localhost:5165")
CATALOG = os.environ.get("CATALOG_BASE", "http://localhost:5168")
KC = os.environ.get("KC_BASE", "http://localhost:8088")
MAILHOG = os.environ.get("MAILHOG_BASE", "http://localhost:8025")
SCRATCH = os.path.dirname(os.path.abspath(__file__))
RESULTS: list[tuple[str, str, str]] = []  # name, OK/FAIL, detail


def log(msg: str) -> None:
    print(msg, flush=True)


def record(name: str, ok: bool, detail: str) -> bool:
    status = "OK" if ok else "FAIL"
    RESULTS.append((name, status, detail))
    log(f"[{status}] {name}: {detail}")
    return ok


def http(
    method: str,
    url: str,
    *,
    token: str | None = None,
    json_body: Any = None,
    data: bytes | None = None,
    headers: dict[str, str] | None = None,
    timeout: float = 60,
) -> tuple[int, dict[str, str], bytes]:
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
            raw = resp.read()
            return resp.status, {k.lower(): v for k, v in resp.headers.items()}, raw
    except HTTPError as e:
        raw = e.read() if e.fp else b""
        return e.code, {k.lower(): v for k, v in (e.headers.items() if e.headers else [])}, raw
    except URLError as e:
        raise RuntimeError(f"{method} {url} failed: {e}") from e


def http_json(method: str, url: str, **kw: Any) -> tuple[int, Any, bytes]:
    code, _hdrs, raw = http(method, url, **kw)
    parsed: Any = None
    if raw:
        try:
            parsed = json.loads(raw.decode("utf-8"))
        except json.JSONDecodeError:
            parsed = None
    return code, parsed, raw


def snippet(raw: bytes, n: int = 400) -> str:
    text = raw.decode("utf-8", errors="replace").replace("\n", " ").strip()
    return text[:n]


def token_for(username: str, password: str | None = None) -> str:
    password = password or username
    body = urlencode(
        {
            "client_id": "procurement-api",
            "grant_type": "password",
            "username": username,
            "password": password,
        }
    ).encode()
    code, parsed, raw = http_json(
        "POST",
        f"{KC}/realms/procurement/protocol/openid-connect/token",
        data=body,
        headers={"Content-Type": "application/x-www-form-urlencoded"},
        timeout=20,
    )
    if code != 200 or not parsed or "access_token" not in parsed:
        raise RuntimeError(f"token {username}: HTTP {code} {snippet(raw)}")
    return parsed["access_token"]


def multipart(fields: dict[str, str], filename: str, content: bytes, field: str = "file") -> tuple[bytes, str]:
    boundary = "----E2EBoundary7MA4YWxkTrZu0gW"
    chunks: list[bytes] = []
    for name, value in fields.items():
        chunks.append(f"--{boundary}\r\n".encode())
        chunks.append(f'Content-Disposition: form-data; name="{name}"\r\n\r\n{value}\r\n'.encode())
    chunks.append(f"--{boundary}\r\n".encode())
    chunks.append(
        f'Content-Disposition: form-data; name="{field}"; filename="{filename}"\r\n'
        "Content-Type: application/vnd.openxmlformats-officedocument.spreadsheetml.sheet\r\n\r\n".encode()
    )
    chunks.append(content)
    chunks.append(b"\r\n")
    chunks.append(f"--{boundary}--\r\n".encode())
    return b"".join(chunks), f"multipart/form-data; boundary={boundary}"


def xml_escape(s: str) -> str:
    return (
        s.replace("&", "&amp;")
        .replace("<", "&lt;")
        .replace(">", "&gt;")
        .replace('"', "&quot;")
    )


def col_letter(n: int) -> str:
    # 1-based
    s = ""
    while n:
        n, r = divmod(n - 1, 26)
        s = chr(65 + r) + s
    return s


def sheet_xml(rows: list[list[Any]]) -> str:
    """rows: list of cells; str -> inlineStr, int/float -> number, None -> skip."""
    parts = [
        '<?xml version="1.0" encoding="UTF-8" standalone="yes"?>',
        '<worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main">',
        "<sheetData>",
    ]
    for r_i, row in enumerate(rows, start=1):
        cells_xml = []
        for c_i, val in enumerate(row, start=1):
            if val is None:
                continue
            ref = f"{col_letter(c_i)}{r_i}"
            if isinstance(val, (int, float)) and not isinstance(val, bool):
                cells_xml.append(f'<c r="{ref}"><v>{val}</v></c>')
            else:
                cells_xml.append(
                    f'<c r="{ref}" t="inlineStr"><is><t>{xml_escape(str(val))}</t></is></c>'
                )
        parts.append(f'<row r="{r_i}">' + "".join(cells_xml) + "</row>")
    parts.append("</sheetData></worksheet>")
    return "".join(parts)


def build_xlsx(sheets: list[tuple[str, list[list[Any]]]]) -> bytes:
    """Minimal xlsx (OOXML) with N worksheets. ClosedXML-friendly."""
    content_types_overrides = []
    workbook_sheets = []
    rels = []
    files: dict[str, str] = {}
    for i, (name, rows) in enumerate(sheets, start=1):
        rid = f"rId{i}"
        files[f"xl/worksheets/sheet{i}.xml"] = sheet_xml(rows)
        content_types_overrides.append(
            f'<Override PartName="/xl/worksheets/sheet{i}.xml" '
            'ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/>'
        )
        workbook_sheets.append(
            f'<sheet name="{xml_escape(name)}" sheetId="{i}" r:id="{rid}"/>'
        )
        rels.append(
            f'<Relationship Id="{rid}" '
            'Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" '
            f'Target="worksheets/sheet{i}.xml"/>'
        )
    n = len(sheets)
    rels.append(
        f'<Relationship Id="rId{n + 1}" '
        'Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles" '
        'Target="styles.xml"/>'
    )
    files["[Content_Types].xml"] = (
        '<?xml version="1.0" encoding="UTF-8" standalone="yes"?>'
        '<Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">'
        '<Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>'
        '<Default Extension="xml" ContentType="application/xml"/>'
        '<Override PartName="/xl/workbook.xml" '
        'ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/>'
        + "".join(content_types_overrides)
        + '<Override PartName="/xl/styles.xml" '
        'ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml"/>'
        "</Types>"
    )
    files["_rels/.rels"] = (
        '<?xml version="1.0" encoding="UTF-8" standalone="yes"?>'
        '<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">'
        '<Relationship Id="rId1" '
        'Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" '
        'Target="xl/workbook.xml"/>'
        "</Relationships>"
    )
    files["xl/workbook.xml"] = (
        '<?xml version="1.0" encoding="UTF-8" standalone="yes"?>'
        '<workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" '
        'xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">'
        "<sheets>" + "".join(workbook_sheets) + "</sheets></workbook>"
    )
    files["xl/_rels/workbook.xml.rels"] = (
        '<?xml version="1.0" encoding="UTF-8" standalone="yes"?>'
        '<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">'
        + "".join(rels)
        + "</Relationships>"
    )
    files["xl/styles.xml"] = (
        '<?xml version="1.0" encoding="UTF-8" standalone="yes"?>'
        '<styleSheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main">'
        "<fonts count=\"1\"><font><sz val=\"11\"/><name val=\"Calibri\"/></font></fonts>"
        "<fills count=\"1\"><fill><patternFill patternType=\"none\"/></fill></fills>"
        "<borders count=\"1\"><border/></borders>"
        "<cellStyleXfs count=\"1\"><xf/></cellStyleXfs>"
        "<cellXfs count=\"1\"><xf/></cellXfs>"
        "</styleSheet>"
    )
    buf = BytesIO()
    with zipfile.ZipFile(buf, "w", compression=zipfile.ZIP_DEFLATED) as zf:
        for path, xml in files.items():
            zf.writestr(path, xml.encode("utf-8"))
    return buf.getvalue()


def mailhog_messages() -> tuple[int, list[dict[str, Any]]]:
    code, parsed, raw = http_json("GET", f"{MAILHOG}/api/v2/messages?limit=250", timeout=15)
    if code != 200 or not isinstance(parsed, dict):
        raise RuntimeError(f"MailHog HTTP {code} {snippet(raw)}")
    items = parsed.get("items") or []
    total = int(parsed.get("total") or len(items))
    return total, items


def mail_addr(item: dict[str, Any]) -> list[str]:
    headers = ((item.get("Content") or {}).get("Headers") or {})
    tos: list[str] = []
    for key in ("To", "to"):
        val = headers.get(key)
        if isinstance(val, list):
            tos.extend(val)
        elif isinstance(val, str):
            tos.append(val)
    # envelope
    to_env = ((item.get("Raw") or {}).get("To")) or []
    if isinstance(to_env, list):
        tos.extend(to_env)
    return [t.strip() for t in tos if t]


def mail_subject(item: dict[str, Any]) -> str:
    headers = ((item.get("Content") or {}).get("Headers") or {})
    sub = headers.get("Subject") or headers.get("subject") or []
    if isinstance(sub, list):
        return sub[0] if sub else ""
    return str(sub)


def wait_health(url: str, seconds: float = 90) -> bool:
    deadline = time.time() + seconds
    last = ""
    while time.time() < deadline:
        try:
            code, parsed, raw = http_json("GET", url, timeout=5)
            last = f"{code} {snippet(raw)}"
            if code == 200:
                return True
        except Exception as e:  # noqa: BLE001
            last = str(e)
        time.sleep(1.5)
    log(f"health timeout {url}: {last}")
    return False


def pick_products_with_offers(token: str, need: int = 3) -> list[dict[str, Any]]:
    """Return catalog cards that have at least 2 offers with differing price/lead."""
    code, parsed, raw = http_json(
        "GET", f"{API}/api/catalog?page=1&pageSize=48&sort=name", token=token
    )
    if code != 200 or not parsed:
        raise RuntimeError(f"catalog browse HTTP {code} {snippet(raw)}")
    items = parsed.get("items") or parsed.get("Items") or []
    chosen: list[dict[str, Any]] = []
    for card in items:
        oid = card.get("id") or card.get("Id")
        offers_n = card.get("offerCount") or card.get("OfferCount") or 0
        if not oid or offers_n < 1:
            continue
        c2, offers, _ = http_json("GET", f"{API}/api/pricelist/by-product/{oid}", token=token)
        if c2 != 200 or not isinstance(offers, list) or not offers:
            continue
        prices = {float(o.get("price") or o.get("Price") or 0) for o in offers}
        leads = {int(o.get("leadTimeDays") or o.get("LeadTimeDays") or 0) for o in offers}
        card["_offers"] = offers
        card["_diverse"] = len(offers) >= 2 and (len(prices) > 1 or len(leads) > 1)
        if card["_diverse"]:
            chosen.append(card)
        if len(chosen) >= need:
            break
    if len(chosen) < need:
        # fallback: any product with at least one offer
        for card in items:
            if card in chosen:
                continue
            oid = card.get("id") or card.get("Id")
            if not oid:
                continue
            c2, offers, _ = http_json("GET", f"{API}/api/pricelist/by-product/{oid}", token=token)
            if c2 == 200 and isinstance(offers, list) and offers:
                card["_offers"] = offers
                card["_diverse"] = False
                chosen.append(card)
            if len(chosen) >= need:
                break
    return chosen


def ensure_diverse_offers(token: str, products: list[dict[str, Any]]) -> None:
    """Guarantee two vendors with inverse price/lead so 3 quote strategies differ."""
    code, vendors, raw = http_json("GET", f"{API}/api/vendors?page=1&pageSize=20", token=token)
    if code != 200 or not vendors:
        raise RuntimeError(f"vendors HTTP {code} {snippet(raw)}")
    v_items = vendors.get("items") if isinstance(vendors, dict) else vendors
    if not v_items:
        c, v1, r = http_json(
            "POST",
            f"{API}/api/vendors",
            token=token,
            json_body={"name": "E2E CheapSlow", "inn": None, "defaultLeadTimeDays": 21},
        )
        if c not in (200, 201) or not v1:
            raise RuntimeError(f"create vendor HTTP {c} {snippet(r)}")
        c, v2, r = http_json(
            "POST",
            f"{API}/api/vendors",
            token=token,
            json_body={"name": "E2E FastExpensive", "inn": None, "defaultLeadTimeDays": 2},
        )
        if c not in (200, 201) or not v2:
            raise RuntimeError(f"create vendor2 HTTP {c} {snippet(r)}")
        v_items = [v1, v2]
    cheap = v_items[0]
    fast = v_items[1] if len(v_items) > 1 else v_items[0]
    cheap_id = cheap.get("id") or cheap.get("Id")
    fast_id = fast.get("id") or fast.get("Id")
    if cheap_id == fast_id:
        c, v2, r = http_json(
            "POST",
            f"{API}/api/vendors",
            token=token,
            json_body={"name": f"E2E Fast {uuid.uuid4().hex[:6]}", "inn": None, "defaultLeadTimeDays": 2},
        )
        if c in (200, 201) and v2:
            fast_id = v2.get("id") or v2.get("Id")
    for card in products:
        pid = card.get("id") or card.get("Id")
        offers = card.get("_offers") or []
        vendor_ids = {(o.get("vendorId") or o.get("VendorId")) for o in offers}
        prices = {float(o.get("price") or o.get("Price") or 0) for o in offers}
        leads = {int(o.get("leadTimeDays") or o.get("LeadTimeDays") or 0) for o in offers}
        if len(offers) >= 2 and len(prices) > 1 and len(leads) > 1:
            continue
        # add complementary offers
        if cheap_id not in vendor_ids:
            http_json(
                "POST",
                f"{API}/api/pricelist",
                token=token,
                json_body={
                    "productId": pid,
                    "vendorId": cheap_id,
                    "price": 100.0,
                    "leadTimeDays": 21,
                    "stockQuantity": 50,
                },
            )
        if fast_id not in vendor_ids and fast_id != cheap_id:
            http_json(
                "POST",
                f"{API}/api/pricelist",
                token=token,
                json_body={
                    "productId": pid,
                    "vendorId": fast_id,
                    "price": 180.0,
                    "leadTimeDays": 2,
                    "stockQuantity": 50,
                },
            )


def main() -> int:
    stamp = time.strftime("%Y%m%d-%H%M%S")
    unique = uuid.uuid4().hex[:8]
    log(f"E2E start {stamp} unique={unique} API={API}")

    if not wait_health(f"{API}/health", 20):
        record("1. health", False, f"{API}/health не ответил 200 — стенд не поднят")
        dump_results()
        return 1

    # ── 1. Health + OpenAPI ──────────────────────────────────────────
    code, parsed, raw = http_json("GET", f"{API}/health")
    ok_h = code == 200 and (not parsed or parsed.get("status") in (None, "ok"))
    record("1a. GET /health", ok_h, f"HTTP {code} body={snippet(raw)}")

    openapi_ok = False
    openapi_detail = ""
    for path in ("/openapi/v1.json", "/swagger/v1/swagger.json", "/scalar"):
        c, _, r = http_json("GET", f"{API}{path}")
        if c == 200 and r:
            openapi_ok = True
            openapi_detail = f"{path} HTTP 200, {len(r)} bytes"
            break
        openapi_detail += f"{path}={c}; "
    record("1b. OpenAPI/Swagger", openapi_ok, openapi_detail)

    # tokens
    try:
        tok = {
            "admin": token_for("admin"),
            "manager": token_for("manager"),
            "viewer": token_for("viewer"),
            "commercial": token_for("commercial"),
            "accounting": token_for("accounting"),
            "warehouse": token_for("warehouse"),
        }
        record("tokens", True, "password grant: admin/manager/viewer/commercial/accounting/warehouse")
    except Exception as e:  # noqa: BLE001
        record("tokens", False, str(e))
        dump_results()
        return 1

    # ── 2. RBAC ──────────────────────────────────────────────────────
    c, _, r = http_json("GET", f"{API}/api/products")
    record("2a. без токена /api/*", c == 401, f"GET /api/products → HTTP {c} {snippet(r)}")

    c, _, r = http_json(
        "POST",
        f"{API}/api/specifications",
        token=tok["viewer"],
        json_body={"title": "viewer-should-fail", "customer": "x"},
    )
    record("2b. viewer мутация", c == 403, f"POST /api/specifications → HTTP {c} {snippet(r)}")

    # ── seed if catalog empty ────────────────────────────────────────
    c, products_page, raw = http_json(
        "GET", f"{API}/api/products?page=1&pageSize=5", token=tok["admin"]
    )
    total_products = 0
    if c == 200 and isinstance(products_page, dict):
        total_products = int(products_page.get("total") or 0)
    if total_products < 10:
        log(f"catalog small (total={total_products}), seeding modest dataset…")
        c, seed, raw = http_json(
            "POST",
            f"{API}/api/admin/seed?vendors=8&products=40&maxOffersPerProduct=3",
            token=tok["admin"],
            timeout=180,
        )
        record(
            "seed",
            c == 200,
            f"POST /api/admin/seed → HTTP {c} {json.dumps(seed, ensure_ascii=False) if seed else snippet(raw)}",
        )
    else:
        record("seed", True, f"каталог уже есть, total={total_products}, сид пропущен")

    # ── 3. сквозной путь: spec → КП → согласование ───────────────────
    spec_id = None
    approval_id = None
    invoice_id = None
    try:
        products = pick_products_with_offers(tok["manager"], need=3)
        if len(products) < 2:
            raise RuntimeError(f"в каталоге мало товаров с офферами: {len(products)}")
        ensure_diverse_offers(tok["manager"], products)

        c, spec, raw = http_json(
            "POST",
            f"{API}/api/specifications",
            token=tok["manager"],
            json_body={"title": f"E2E night {unique}", "customer": "ООО Ночной прогон"},
        )
        if c not in (200, 201) or not spec:
            raise RuntimeError(f"create spec HTTP {c} {snippet(raw)}")
        spec_id = spec.get("id") or spec.get("Id")

        added = []
        for card in products[:3]:
            pid = card.get("id") or card.get("Id")
            name = card.get("name") or card.get("Name")
            sku = card.get("sku") or card.get("Sku")
            c, item, raw = http_json(
                "POST",
                f"{API}/api/specifications/{spec_id}/items",
                token=tok["manager"],
                json_body={"productId": pid, "sku": sku, "name": name, "quantity": 2},
            )
            if c not in (200, 201) or not item:
                raise RuntimeError(f"add item HTTP {c} {snippet(raw)}")
            added.append(item)
        record(
            "3a. спецификация + позиции",
            len(added) >= 2,
            f"spec={spec_id} items={len(added)} skus={[p.get('sku') or p.get('Sku') for p in products[:3]]}",
        )

        c, quotes, raw = http_json(
            "GET", f"{API}/api/specifications/{spec_id}/quote/compare", token=tok["manager"]
        )
        if c != 200 or not isinstance(quotes, list) or len(quotes) < 3:
            record("3b. генерация КП (3 стратегии)", False, f"HTTP {c} count={None if not quotes else len(quotes)} {snippet(raw)}")
        else:
            triples = []
            for q in quotes:
                st = q.get("strategy") or q.get("Strategy")
                total = q.get("totalCost") if "totalCost" in q else q.get("TotalCost")
                triples.append((st, total))
            # user asked 3 strategies with different sums — take MinCost / MinLeadTime / Balanced
            wanted = {"MinCost", "MinLeadTime", "Balanced"}
            by_st = {str(st): tot for st, tot in triples}
            present = wanted & set(by_st)
            sums = [by_st[s] for s in present]
            distinct = len(set(sums)) >= 2  # at least two differ; ideally 3
            all_three_differ = len(set(sums)) == len(sums) and len(sums) >= 3
            record(
                "3b. генерация КП (3 стратегии)",
                len(present) >= 3 and distinct,
                f"стратегии={triples}; уникальных сумм среди MinCost/MinLeadTime/Balanced={len(set(sums))}"
                + ("" if all_three_differ else " (не все три суммы различны)"),
            )

        c, approval, raw = http_json(
            "POST",
            f"{API}/api/approvals",
            token=tok["manager"],
            json_body={"specificationId": spec_id, "strategy": "Balanced", "markupPercent": 12},
        )
        if c not in (200, 201) or not approval:
            record("3c. согласование КБ (create)", False, f"POST /api/approvals HTTP {c} {snippet(raw)}")
        else:
            approval_id = approval.get("id") or approval.get("Id")
            status = approval.get("status") or approval.get("Status")
            record(
                "3c. согласование КБ (create)",
                True,
                f"approval={approval_id} status={status} cost={approval.get('costPrice')} sell={approval.get('sellPrice')}",
            )
            # optional margin → В коммерческом блоке
            http_json(
                "POST",
                f"{API}/api/approvals/{approval_id}/margin",
                token=tok["manager"],
                json_body={"markupPercent": 15},
            )
    except Exception as e:  # noqa: BLE001
        record("3. сквозной путь", False, f"исключение: {e}")

    # ── 4. UC-07 автосоздание счёта ──────────────────────────────────
    try:
        if not approval_id:
            raise RuntimeError("нет approval_id — шаг 3 не создал согласование")
        mail_before_total, _ = mailhog_messages()

        c, _, raw = http_json(
            "POST",
            f"{API}/api/approvals/{approval_id}/decision",
            token=tok["commercial"],
            json_body={"approved": True, "comment": "E2E ок"},
        )
        if c not in (200, 204):
            record("4a. decision commercial approved=true", False, f"HTTP {c} {snippet(raw)}")
            raise RuntimeError("decision failed")
        record("4a. decision commercial approved=true", True, f"HTTP {c}")

        c, invoices, raw = http_json("GET", f"{API}/api/invoices", token=tok["commercial"])
        if c != 200 or not isinstance(invoices, list):
            raise RuntimeError(f"list invoices HTTP {c} {snippet(raw)}")
        match = [
            i
            for i in invoices
            if (i.get("approvalId") or i.get("ApprovalId")) == approval_id
        ]
        if not match:
            record("4b. автосчёт NOC", False, f"после decision счёт с approvalId={approval_id} не найден, invoices={len(invoices)}")
        else:
            inv = match[0]
            invoice_id = inv.get("id") or inv.get("Id")
            created_by = inv.get("createdBy") or inv.get("CreatedBy")
            number = inv.get("number") or inv.get("Number")
            auto_ok = created_by == "Система (авто)"
            record(
                "4b. автосчёт NOC",
                auto_ok,
                f"id={invoice_id} number={number} createdBy={created_by!r} (ожидали «Система (авто)»)",
            )

            c2, again, raw2 = http_json(
                "POST",
                f"{API}/api/invoices",
                token=tok["manager"],
                json_body={"approvalId": approval_id, "contract": None, "dueDateUtc": None},
            )
            same_id = None
            if isinstance(again, dict):
                same_id = again.get("id") or again.get("Id")
            record(
                "4c. повторный POST /api/invoices идемпотентен",
                c2 in (200, 201) and same_id == invoice_id,
                f"HTTP {c2} returned id={same_id} expected={invoice_id} {snippet(raw2) if c2 >= 400 else ''}",
            )
            # count invoices for this approval — should stay 1
            c3, invoices2, _ = http_json("GET", f"{API}/api/invoices", token=tok["admin"])
            match2 = [
                i
                for i in (invoices2 or [])
                if (i.get("approvalId") or i.get("ApprovalId")) == approval_id
            ]
            if len(match2) != 1:
                record(
                    "4d. нет дубля счёта",
                    False,
                    f"счетов по approval={approval_id}: {len(match2)}",
                )
    except Exception as e:  # noqa: BLE001
        record("4. UC-07", False, f"исключение: {e}")
        mail_before_total = 0

    # ── 5. 10.11 маршрут счёта ───────────────────────────────────────
    try:
        if not invoice_id:
            raise RuntimeError("нет invoice_id")
        c, _, raw = http_json(
            "POST",
            f"{API}/api/invoices/{invoice_id}/status",
            token=tok["accounting"],
            json_body={"status": "Согласован"},
        )
        record("5a. accounting → Согласован", c == 403, f"HTTP {c} {snippet(raw)}")

        c, _, raw = http_json(
            "POST",
            f"{API}/api/invoices/{invoice_id}/status",
            token=tok["commercial"],
            json_body={"status": "Согласован"},
        )
        record("5b. commercial → Согласован", c == 204, f"HTTP {c} {snippet(raw)}")

        c, _, raw = http_json(
            "POST",
            f"{API}/api/invoices/{invoice_id}/status",
            token=tok["commercial"],
            json_body={"status": "ОжиданиеОплаты"},
        )
        record("5c. commercial → следующая стадия", c == 403, f"HTTP {c} {snippet(raw)}")

        c, _, raw = http_json(
            "POST",
            f"{API}/api/invoices/{invoice_id}/status",
            token=tok["accounting"],
            json_body={"status": "ОжиданиеОплаты"},
        )
        record("5d. accounting → ОжиданиеОплаты", c == 204, f"HTTP {c} {snippet(raw)}")

        c, _, raw = http_json(
            "POST",
            f"{API}/api/invoices/{invoice_id}/status",
            token=tok["accounting"],
            json_body={"status": "Оплачено"},
        )
        record("5e. прыжок стадии ОжиданиеОплаты→Оплачено", c == 409, f"HTTP {c} {snippet(raw)}")

        c, _, raw = http_json(
            "POST",
            f"{API}/api/invoices/{invoice_id}/status",
            token=tok["accounting"],
            json_body={"status": "ЧастичнаяОплата"},
        )
        record("5f. accounting следующая стадия ЧастичнаяОплата", c == 204, f"HTTP {c} {snippet(raw)}")
    except Exception as e:  # noqa: BLE001
        record("5. маршрут счёта 10.11", False, f"исключение: {e}")

    # ── 6. аудит old/new ─────────────────────────────────────────────
    try:
        # discount: create then update percent
        c, vendors, raw = http_json("GET", f"{API}/api/vendors?page=1&pageSize=5", token=tok["manager"])
        v_items = (vendors or {}).get("items") if isinstance(vendors, dict) else vendors
        vendor_id = None
        if v_items:
            vendor_id = v_items[0].get("id") or v_items[0].get("Id")
        c, disc, raw = http_json(
            "POST",
            f"{API}/api/discounts",
            token=tok["manager"],
            json_body={
                "vendorId": vendor_id,
                "manufacturer": None,
                "percent": 5,
                "validFromUtc": None,
                "validToUtc": None,
            },
        )
        if c not in (200, 201) or not disc:
            record("6a. создать скидку", False, f"HTTP {c} {snippet(raw)}")
            disc_id = None
        else:
            disc_id = disc.get("id") or disc.get("Id")
            record("6a. создать скидку", True, f"id={disc_id} percent=5 vendor={vendor_id}")
            c, _, raw = http_json(
                "PUT",
                f"{API}/api/discounts/{disc_id}",
                token=tok["manager"],
                json_body={"percent": 8.5, "validFromUtc": None, "validToUtc": None},
            )
            record("6b. PUT /api/discounts/{id} percent 5→8.5", c in (200, 204), f"HTTP {c} {snippet(raw)}")

        # order status change
        if spec_id:
            c, order, raw = http_json(
                "POST",
                f"{API}/api/orders/from-quote?specId={spec_id}&strategy=Balanced",
                token=tok["manager"],
            )
            if c not in (200, 201) or not order:
                record("6c. заказ из КП", False, f"HTTP {c} {snippet(raw)}")
                order_id = None
            else:
                order_id = order.get("id") or order.get("Id")
                record("6c. заказ из КП", True, f"id={order_id} status={order.get('status')}")
                c, _, raw = http_json(
                    "POST",
                    f"{API}/api/orders/{order_id}/status",
                    token=tok["manager"],
                    json_body={"status": "Placed"},
                )
                record("6d. статус заказа Draft→Placed", c in (200, 204), f"HTTP {c} {snippet(raw)}")
        else:
            order_id = None
            record("6c. заказ из КП", False, "нет spec_id")

        c, audit, raw = http_json("GET", f"{API}/api/audit?limit=200", token=tok["admin"])
        if c != 200 or not isinstance(audit, list):
            record("6e. GET /api/audit old/new", False, f"HTTP {c} {snippet(raw)}")
        else:
            changes = []
            for entry in audit:
                ch = entry.get("changes") or entry.get("Changes") or []
                if not ch:
                    continue
                for item in ch:
                    changes.append(item)
            def_old = lambda x: x.get("oldValue") if "oldValue" in x else x.get("OldValue")
            def_new = lambda x: x.get("newValue") if "newValue" in x else x.get("NewValue")
            def_prop = lambda x: x.get("property") or x.get("Property")
            def_ent = lambda x: x.get("entity") or x.get("Entity")
            discount_hits = [
                x
                for x in changes
                if def_ent(x) == "Discount" and def_prop(x) == "Percent" and def_old(x) is not None and def_new(x) is not None
            ]
            order_hits = [
                x
                for x in changes
                if def_ent(x) == "Order" and def_prop(x) == "Status" and def_old(x) is not None and def_new(x) is not None
            ]
            ok_audit = bool(discount_hits) and bool(order_hits)
            sample = []
            if discount_hits:
                x = discount_hits[0]
                sample.append(f"Discount.Percent {def_old(x)}→{def_new(x)}")
            if order_hits:
                x = order_hits[0]
                sample.append(f"Order.Status {def_old(x)}→{def_new(x)}")
            record(
                "6e. GET /api/audit old/new",
                ok_audit,
                f"записей={len(audit)} discount_hits={len(discount_hits)} order_hits={len(order_hits)} sample={sample}",
            )
    except Exception as e:  # noqa: BLE001
        record("6. аудит", False, f"исключение: {e}")

    # ── 8. stock/adjust (catalog service) ────────────────────────────
    catalog_up = False
    try:
        c, _, _ = http_json("GET", f"{CATALOG}/health", timeout=5)
        catalog_up = c == 200
    except Exception:  # noqa: BLE001
        catalog_up = False
    if not catalog_up:
        record("8. stock/adjust", False, "сервис каталога :5168 не отвечает /health — проверку пропустили")
    else:
        try:
            c, v, raw = http_json(
                "POST",
                f"{CATALOG}/api/vendors",
                token=tok["manager"],
                json_body={"name": f"E2E Stock {unique}", "inn": None, "defaultLeadTimeDays": 5},
            )
            if c not in (200, 201) or not v:
                raise RuntimeError(f"catalog create vendor HTTP {c} {snippet(raw)}")
            vid = v.get("id") or v.get("Id")
            sku = f"E2E-STK-{unique}"
            c, p, raw = http_json(
                "POST",
                f"{CATALOG}/api/products",
                token=tok["manager"],
                json_body={"sku": sku, "name": "Товар для adjust", "manufacturer": "E2E", "category": "E2E"},
            )
            if c not in (200, 201) or not p:
                raise RuntimeError(f"catalog create product HTTP {c} {snippet(raw)}")
            pid = p.get("id") or p.get("Id")
            c, offer, raw = http_json(
                "POST",
                f"{CATALOG}/api/pricelist",
                token=tok["manager"],
                json_body={
                    "productId": pid,
                    "vendorId": vid,
                    "price": 50,
                    "leadTimeDays": 5,
                    "stockQuantity": 10,
                },
            )
            if c not in (200, 201) or not offer:
                raise RuntimeError(f"catalog create offer HTTP {c} {snippet(raw)}")

            def read_stock() -> int | None:
                c2, offers, _ = http_json(
                    "GET", f"{CATALOG}/api/pricelist/by-product/{pid}", token=tok["manager"]
                )
                if c2 != 200 or not offers:
                    return None
                for o in offers:
                    if (o.get("vendorId") or o.get("VendorId")) == vid:
                        return int(o.get("stockQuantity") if "stockQuantity" in o else o.get("StockQuantity"))
                return None

            def adjust(qty: int, sign: int) -> int:
                return http_json(
                    "POST",
                    f"{CATALOG}/api/catalog/stock/adjust",
                    token=tok["manager"],
                    json_body={
                        "items": [
                            {"productId": pid, "vendorId": vid, "quantity": qty, "sign": sign}
                        ]
                    },
                )[0]

            s0 = read_stock()
            c_dec = adjust(3, -1)
            s1 = read_stock()
            c_inc = adjust(3, +1)
            s2 = read_stock()
            c_floor = adjust(99, -1)
            s3 = read_stock()
            ok8 = (
                c_dec in (200, 204)
                and c_inc in (200, 204)
                and c_floor in (200, 204)
                and s0 == 10
                and s1 == 7
                and s2 == 10
                and s3 == 0
            )
            record(
                "8. stock/adjust",
                ok8,
                f"HTTP dec/inc/floor={c_dec}/{c_inc}/{c_floor} stock 10→{s1}→{s2}→{s3} (ожидали 7, 10, 0)",
            )
        except Exception as e:  # noqa: BLE001
            record("8. stock/adjust", False, f"исключение: {e}")

    # ── 9. импорт прайса (второй лист) ───────────────────────────────
    import_sku = f"E2E-XLSX-{unique}-1"
    import_sku2 = f"E2E-XLSX-{unique}-2"
    try:
        c, vendors, raw = http_json("GET", f"{API}/api/vendors?page=1&pageSize=5", token=tok["manager"])
        v_items = (vendors or {}).get("items") if isinstance(vendors, dict) else vendors
        if not v_items:
            raise RuntimeError("нет поставщиков для импорта")
        vendor_id = v_items[0].get("id") or v_items[0].get("Id")
        xlsx = build_xlsx(
            [
                (
                    "Титульный лист",
                    [["Прайс-лист"], ["служебный лист", "без таблицы товаров"]],
                ),
                (
                    "Товары",
                    [
                        ["Артикул", "Наименование", "Цена", "Остаток"],
                        [import_sku, f"Ночной товар {unique} А", 321.5, 12],
                        [import_sku2, f"Ночной товар {unique} Б", 654.0, 4],
                    ],
                ),
            ]
        )
        xlsx_path = os.path.join(SCRATCH, f"pricelist-second-sheet-{unique}.xlsx")
        with open(xlsx_path, "wb") as f:
            f.write(xlsx)
        body, ctype = multipart({}, "second-sheet.xlsx", xlsx)
        c, result, raw = http_json(
            "POST",
            f"{API}/api/pricelist/import?vendorId={vendor_id}",
            token=tok["manager"],
            data=body,
            headers={"Content-Type": ctype},
            timeout=90,
        )
        upserted = (result or {}).get("offersUpserted") if isinstance(result, dict) else None
        created = (result or {}).get("productsCreated") if isinstance(result, dict) else None
        errors = (result or {}).get("errors") if isinstance(result, dict) else None
        rows = (result or {}).get("rowsProcessed") if isinstance(result, dict) else None
        ok_imp = c == 200 and isinstance(result, dict) and (upserted or 0) >= 2
        record(
            "9a. импорт прайса (товары на 2-м листе)",
            ok_imp,
            f"HTTP {c} rows={rows} created={created} upserted={upserted} errors={errors} file={xlsx_path}",
        )

        broken = b"this is not an excel file at all \x00\x01\x02"
        body_b, ctype_b = multipart({}, "broken.xlsx", broken)
        c, result_b, raw_b = http_json(
            "POST",
            f"{API}/api/pricelist/import?vendorId={vendor_id}",
            token=tok["manager"],
            data=body_b,
            headers={"Content-Type": ctype_b},
            timeout=30,
        )
        not_500 = c != 500
        record(
            "9b. битый файл не роняет 500",
            not_500,
            f"HTTP {c} body={snippet(raw_b) if not isinstance(result_b, dict) else json.dumps(result_b, ensure_ascii=False)[:300]}",
        )
    except Exception as e:  # noqa: BLE001
        record("9. импорт прайса", False, f"исключение: {e}")

    # ── 10. поиск после импорта ──────────────────────────────────────
    try:
        log("ждём индексатор 8 с…")
        time.sleep(8)
        c, page, raw = http_json(
            "GET",
            f"{API}/api/products?search={import_sku}&page=1&pageSize=10",
            token=tok["manager"],
        )
        items = (page or {}).get("items") if isinstance(page, dict) else None
        found_sql = False
        if items:
            found_sql = any(
                (it.get("sku") or it.get("Sku")) == import_sku for it in items
            )
        record(
            "10a. GET /api/products?search= после импорта",
            c == 200 and found_sql,
            f"HTTP {c} found={found_sql} total={(page or {}).get('total') if isinstance(page, dict) else None} sku={import_sku}",
        )
        c2, hits, raw2 = http_json(
            "GET", f"{API}/api/search?q={import_sku}&limit=10", token=tok["manager"]
        )
        found_meili = False
        if isinstance(hits, list):
            found_meili = any(
                (h.get("sku") or h.get("Sku") or h.get("id")) == import_sku
                or import_sku in json.dumps(h, ensure_ascii=False)
                for h in hits
            )
        record(
            "10b. GET /api/search (Meilisearch/Worker)",
            c2 == 200 and found_meili,
            f"HTTP {c2} found={found_meili} hits={len(hits) if isinstance(hits, list) else None} {snippet(raw2) if not found_meili else ''}",
        )
    except Exception as e:  # noqa: BLE001
        record("10. поиск", False, f"исключение: {e}")

    # ── 7. email (после событий; ждём диспетчер) ─────────────────────
    try:
        log("ждём MailHog ~25 с после событий…")
        time.sleep(25)
        total1, items1 = mailhog_messages()
        addrs1 = []
        for it in items1:
            addrs1.extend(mail_addr(it))
        roles_expected = {
            "commercial@procurement.local",
            "accounting@procurement.local",
        }
        present = {a.lower() for a in addrs1}
        hit_roles = roles_expected & present
        # subjects for duplicate detection
        fingerprints = sorted(
            (mail_subject(it), tuple(sorted(mail_addr(it)))) for it in items1
        )
        record(
            "7a. письма в MailHog на адреса ролей",
            bool(hit_roles) and total1 > 0,
            f"total={total1} to={sorted(present)} hit_roles={sorted(hit_roles)} subjects={[mail_subject(it) for it in items1[:8]]}",
        )
        time.sleep(18)  # второй проход диспетчера (poll 15s)
        total2, items2 = mailhog_messages()
        fingerprints2 = sorted(
            (mail_subject(it), tuple(sorted(mail_addr(it)))) for it in items2
        )
        # no new duplicate of the same (subject, recipients) beyond first snapshot growth of 0
        grew = total2 - total1
        record(
            "7b. повторный проход диспетчера без дубля",
            grew == 0,
            f"total {total1}→{total2} delta={grew}; unique fingerprints {len(set(fingerprints))}→{len(set(fingerprints2))}",
        )
    except Exception as e:  # noqa: BLE001
        record("7. email", False, f"исключение: {e}")

    dump_results()
    fails = sum(1 for _, s, _ in RESULTS if s == "FAIL")
    return 1 if fails else 0


def dump_results() -> None:
    lines = ["проверка\tстатус\tдеталь"]
    for name, status, detail in RESULTS:
        lines.append(f"{name}\t{status}\t{detail}")
    ok_n = sum(1 for _, s, _ in RESULTS if s == "OK")
    lines.append(f"ИТОГО\t{ok_n} из {len(RESULTS)}")
    text = "\n".join(lines) + "\n"
    out = os.path.join(SCRATCH, "e2e_results.txt")
    with open(out, "w", encoding="utf-8") as f:
        f.write(text)
    log("\n===== RESULTS =====")
    log(text)
    log(f"written {out}")


if __name__ == "__main__":
    try:
        sys.exit(main())
    except Exception as e:  # noqa: BLE001
        log(f"FATAL {e}")
        record("fatal", False, str(e))
        dump_results()
        sys.exit(2)
