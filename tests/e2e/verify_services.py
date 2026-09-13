#!/usr/bin/env python3
"""Живая проверка распила: все префиксы через шлюз + catch-all в монолит."""
import json, os, sys
from urllib.parse import urlencode, quote
from urllib.request import Request, urlopen
from urllib.error import HTTPError, URLError

GW = os.environ.get("GW", "http://localhost:5160")
KC = os.environ.get("KC", "http://localhost:8088")
R = []

def rec(name, ok, detail):
    R.append((name, ok, detail)); print(f"[{'OK' if ok else 'FAIL'}] {name}: {detail}", flush=True)

def token(user):
    body = urlencode({"client_id": "procurement-api", "grant_type": "password",
                      "username": user, "password": user}).encode()
    with urlopen(Request(f"{KC}/realms/procurement/protocol/openid-connect/token", data=body,
                 headers={"Content-Type": "application/x-www-form-urlencoded"}), timeout=20) as r:
        return json.load(r)["access_token"]

def call(path, tok=None, method="GET", payload=None):
    hdr = {"Authorization": f"Bearer {tok}"} if tok else {}
    data = None
    if payload is not None:
        data = json.dumps(payload).encode(); hdr["Content-Type"] = "application/json"
    try:
        with urlopen(Request(GW + path, data=data, headers=hdr, method=method), timeout=120) as r:
            body = r.read()
            try: return r.status, json.loads(body)
            except Exception: return r.status, body[:200]
    except HTTPError as e: return e.code, None
    except URLError as e: return 0, str(e)

def n(p):
    if isinstance(p, list): return len(p)
    if isinstance(p, dict):
        for k in ("items", "data", "results", "candidates", "matches", "total", "totalCount", "unread"):
            v = p.get(k)
            if isinstance(v, list): return len(v)
            if isinstance(v, int): return v
    return -1

def main():
    t = {}
    for u in ("admin", "manager", "commercial", "accounting", "warehouse"):
        try: t[u] = token(u)
        except Exception as e: rec(f"токен {u}", False, repr(e)[:100])
    if "manager" not in t:
        print("Keycloak недоступен"); return 1
    rec("токены ролей", len(t) >= 4, f"получено {len(t)}")

    # 1. без токена всё закрыто
    s, _ = call("/api/products")
    rec("без токена 401", s == 401, f"код {s}")

    # 2. вынесенные сервисы через шлюз
    for path, role, label in [
        ("/api/products?take=5", "manager", "Catalog"),
        ("/api/specifications", "manager", "Quoting"),
        ("/api/approvals", "manager", "Commercial (approvals)"),
        ("/api/invoices", "accounting", "Commercial (invoices)"),
        ("/api/orders", "manager", "Ordering"),
        ("/api/receipts", "warehouse", "Logistics"),
        ("/api/notifications", "manager", "Notifications"),
        ("/api/retail/shops", "manager", "Retail"),
    ]:
        s, p = call(path, t.get(role, t["manager"]))
        rec(f"шлюз {label} {path.split('?')[0]}", s in (200, 204), f"код {s} n={n(p)}")

    # 3. ML-подбор через шлюз
    s, p = call("/api/matching/suggest", t["manager"], "POST", {"name": "коммутатор cisco catalyst 9300", "take": 5})
    rec("шлюз Matching /api/matching/suggest", s == 200 and n(p) > 0, f"код {s} кандидатов={n(p)}")

    # 4. catch-all в монолит
    for path, role in [("/api/audit", "admin"), ("/api/analytics/summary", "manager"),
                       ("/api/projects", "manager"), ("/api/search?q=" + quote("молоток"), "manager")]:
        s, p = call(path, t.get(role, t["manager"]))
        rec(f"catch-all {path.split('?')[0]}", s == 200, f"код {s}")

    bad = [x for x in R if not x[1]]
    print(f"\nИТОГО: {len(R)-len(bad)} из {len(R)}")
    return 1 if bad else 0

sys.exit(main())
