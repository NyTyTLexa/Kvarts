#!/usr/bin/env python3
"""Живая проверка cutover: списки читаются через шлюз из вынесенных сервисов."""
import json, os, sys
from urllib.parse import urlencode, quote
from urllib.request import Request, urlopen
from urllib.error import HTTPError, URLError

GW = os.environ.get("GW", "http://localhost:5160")
API = os.environ.get("API", "http://localhost:5165")
KC = os.environ.get("KC", "http://localhost:8088")
R = []

def token(user):
    body = urlencode({"client_id": "procurement-api", "grant_type": "password",
                      "username": user, "password": user}).encode()
    req = Request(f"{KC}/realms/procurement/protocol/openid-connect/token", data=body,
                  headers={"Content-Type": "application/x-www-form-urlencoded"})
    with urlopen(req, timeout=20) as r:
        return json.load(r)["access_token"]

def get(base, path, tok):
    req = Request(base + path, headers={"Authorization": f"Bearer {tok}"})
    try:
        with urlopen(req, timeout=30) as r:
            return r.status, json.load(r)
    except HTTPError as e:
        return e.code, None
    except URLError as e:
        return 0, str(e)

def count(payload):
    if isinstance(payload, list): return len(payload)
    if isinstance(payload, dict):
        for k in ("items", "data", "results", "total", "totalCount"):
            v = payload.get(k)
            if isinstance(v, list): return len(v)
            if isinstance(v, int): return v
    return -1

def rec(name, ok, detail):
    R.append((name, "OK" if ok else "FAIL", detail))
    print(f"[{'OK' if ok else 'FAIL'}] {name}: {detail}", flush=True)

def main():
    toks = {}
    for u in ("admin", "manager", "commercial", "accounting", "warehouse"):
        try:
            toks[u] = token(u)
        except Exception as e:
            rec(f"токен {u}", False, repr(e)[:120]); 
    if "manager" not in toks:
        print("нет токенов — Keycloak недоступен"); return 1
    rec("токены ролей", len(toks) >= 4, f"получено {len(toks)}: {', '.join(toks)}")

    # переключённые префиксы: читаем через шлюз и напрямую из монолита
    checks = [("/api/specifications", "manager"), ("/api/approvals", "manager"),
              ("/api/invoices", "accounting"), ("/api/orders", "manager"),
              ("/api/receipts", "warehouse")]
    for path, role in checks:
        tok = toks.get(role) or toks["manager"]
        gs, gp = get(GW, path, tok)
        ms, mp = get(API, path, tok)
        gc, mc = count(gp), count(mp)
        ok = gs == 200 and gc >= 0 and gc == mc
        rec(f"шлюз {path}", ok, f"шлюз {gs} n={gc} | монолит {ms} n={mc}")

    # catch-all остаётся в монолите
    for path, role in [("/api/audit", "admin"), ("/api/analytics/summary", "manager"),
                       ("/api/notifications", "manager")]:
        tok = toks.get(role) or toks["manager"]
        s, p = get(GW, path, tok)
        rec(f"catch-all {path}", s == 200, f"через шлюз {s}")

    # каталог — уже вынесен раньше
    s, p = get(GW, "/api/products?search=" + quote("молоток") + "&take=5", toks["manager"])
    rec("шлюз /api/products", s == 200 and count(p) != -1, f"{s} n={count(p)}")

    bad = [r for r in R if r[1] == "FAIL"]
    print(f"\nИТОГО: {len(R)-len(bad)} из {len(R)}")
    return 1 if bad else 0

sys.exit(main())
