#!/usr/bin/env python3
"""Перенос public.* → схемы quoting/commercial/ordering/logistics/notifications/retail (12.6). Stdlib.

По умолчанию гоняет SQL через `docker exec ps-postgres psql`.
Хостовый psql:  python tools/cutover/migrate.py --psql
Счётчики без записи:  python tools/cutover/migrate.py --counts-only
Полный пересъём (TRUNCATE приёмника):  python tools/cutover/migrate.py --replace
"""
from __future__ import annotations

import argparse
import os
import subprocess
import sys
from pathlib import Path

HERE = Path(__file__).resolve().parent
SQL_FILE = HERE / "migrate_public_to_services.sql"

# Дети раньше родителей — для TRUNCATE.
TRUNCATE_SQL = """
TRUNCATE TABLE
  quoting."QuoteOverrides",
  quoting."SpecificationItems",
  quoting."Specifications",
  commercial."InvoiceAttachments",
  commercial."InvoiceLines",
  commercial."Invoices",
  commercial."Approvals",
  ordering."OrderLines",
  ordering."Orders",
  logistics."GoodsReceiptLines",
  logistics."GoodsReceipts",
  notifications."Notifications",
  retail."RetailShops"
RESTART IDENTITY CASCADE;
"""

COUNTS_SQL = r"""
SELECT schema, "table", public_n, dest_n
FROM (
    SELECT 1 AS ord, 'quoting' AS schema, 'Specifications' AS table,
        (SELECT count(*) FROM public."Specifications") AS public_n,
        (SELECT count(*) FROM quoting."Specifications") AS dest_n
    UNION ALL
    SELECT 2, 'quoting', 'SpecificationItems',
        (SELECT count(*) FROM public."SpecificationItems"),
        (SELECT count(*) FROM quoting."SpecificationItems")
    UNION ALL
    SELECT 3, 'quoting', 'QuoteOverrides',
        (SELECT count(*) FROM public."QuoteOverrides"),
        (SELECT count(*) FROM quoting."QuoteOverrides")
    UNION ALL
    SELECT 4, 'commercial', 'Approvals',
        (SELECT count(*) FROM public."Approvals"),
        (SELECT count(*) FROM commercial."Approvals")
    UNION ALL
    SELECT 5, 'commercial', 'Invoices',
        (SELECT count(*) FROM public."Invoices"),
        (SELECT count(*) FROM commercial."Invoices")
    UNION ALL
    SELECT 6, 'commercial', 'InvoiceLines',
        (SELECT count(*) FROM public."InvoiceLines"),
        (SELECT count(*) FROM commercial."InvoiceLines")
    UNION ALL
    SELECT 7, 'commercial', 'InvoiceAttachments',
        (SELECT count(*) FROM public."InvoiceAttachments"),
        (SELECT count(*) FROM commercial."InvoiceAttachments")
    UNION ALL
    SELECT 8, 'ordering', 'Orders',
        (SELECT count(*) FROM public."Orders"),
        (SELECT count(*) FROM ordering."Orders")
    UNION ALL
    SELECT 9, 'ordering', 'OrderLines',
        (SELECT count(*) FROM public."OrderLines"),
        (SELECT count(*) FROM ordering."OrderLines")
    UNION ALL
    SELECT 10, 'logistics', 'GoodsReceipts',
        (SELECT count(*) FROM public."GoodsReceipts"),
        (SELECT count(*) FROM logistics."GoodsReceipts")
    UNION ALL
    SELECT 11, 'logistics', 'GoodsReceiptLines',
        (SELECT count(*) FROM public."GoodsReceiptLines"),
        (SELECT count(*) FROM logistics."GoodsReceiptLines")
    UNION ALL
    SELECT 12, 'notifications', 'Notifications',
        (SELECT count(*) FROM public."Notifications"),
        (SELECT count(*) FROM notifications."Notifications")
    UNION ALL
    SELECT 13, 'retail', 'RetailShops',
        (SELECT count(*) FROM public."RetailShops"),
        (SELECT count(*) FROM retail."RetailShops")
) c
ORDER BY ord;
"""


def log(msg: str) -> None:
    print(msg, flush=True)


def run_psql(sql: str, *, use_docker: bool, container: str, tuples_only: bool = False) -> str:
    extra = ["-A", "-F", "\t", "-q", "-t"] if tuples_only else ["-q"]
    if use_docker:
        cmd = [
            "docker", "exec", "-i", container,
            "psql", "-U", os.environ.get("POSTGRES_USER", "procurement"),
            "-d", os.environ.get("POSTGRES_DB", "procurement"),
            "-v", "ON_ERROR_STOP=1", *extra,
        ]
        env = None
    else:
        host = os.environ.get("PGHOST", "localhost")
        port = os.environ.get("PGPORT", "5433")
        user = os.environ.get("PGUSER", "procurement")
        db = os.environ.get("PGDATABASE", "procurement")
        cmd = [
            "psql",
            "-h", host, "-p", port, "-U", user, "-d", db,
            "-v", "ON_ERROR_STOP=1", *extra,
        ]
        env = os.environ.copy()
        env.setdefault("PGPASSWORD", "procurement")
    proc = subprocess.run(
        cmd,
        input=sql,
        capture_output=True,
        text=True,
        encoding="utf-8",
        env=env,
    )
    if proc.returncode != 0:
        err = (proc.stderr or proc.stdout or "").strip()
        raise RuntimeError(f"psql exit {proc.returncode}: {err}")
    return proc.stdout


def parse_counts(raw: str) -> list[tuple[str, str, int, int]]:
    rows: list[tuple[str, str, int, int]] = []
    for line in raw.splitlines():
        line = line.strip()
        if not line or line.startswith("schema"):
            continue
        parts = line.split("\t")
        if len(parts) != 4:
            continue
        schema, table, public_n, dest_n = parts
        rows.append((schema, table, int(public_n), int(dest_n)))
    return rows


def print_table(rows: list[tuple[str, str, int, int]]) -> bool:
    ok = True
    log(f"{'schema':<12} {'table':<22} {'public':>8} {'dest':>8} {'delta':>8}")
    log("-" * 62)
    for schema, table, public_n, dest_n in rows:
        delta = dest_n - public_n
        # Плюс — норма: после переключения префикса сервис пишет свои новые строки,
        # а копия в public осталась на момент снятия. Провал переноса — только минус.
        if delta == 0:
            mark = "OK"
        elif delta > 0:
            mark = "OK (+ свои)"
        else:
            mark = "НЕДОСТАЁТ"
            ok = False
        log(f"{schema:<12} {table:<22} {public_n:>8} {dest_n:>8} {delta:>+8}  {mark}")
    return ok


def main() -> int:
    parser = argparse.ArgumentParser(description="Перенос public.* в схемы сервисов.")
    parser.add_argument("--psql", action="store_true", help="Хостовый psql (localhost:5433), не docker exec")
    parser.add_argument("--container", default="ps-postgres", help="Имя контейнера Postgres")
    parser.add_argument("--counts-only", action="store_true", help="Только сверка счётчиков")
    parser.add_argument(
        "--replace",
        action="store_true",
        help="TRUNCATE таблиц приёмника, затем копия. public.* не трогает.",
    )
    args = parser.parse_args()
    use_docker = not args.psql

    if not SQL_FILE.is_file():
        log(f"нет файла {SQL_FILE}")
        return 2

    try:
        if args.replace and not args.counts_only:
            log("TRUNCATE схем сервисов (public не трогаем)…")
            run_psql(TRUNCATE_SQL, use_docker=use_docker, container=args.container)

        if not args.counts_only:
            log(f"копия {SQL_FILE.name}…")
            run_psql(SQL_FILE.read_text(encoding="utf-8"), use_docker=use_docker, container=args.container)

        raw = run_psql(COUNTS_SQL, use_docker=use_docker, container=args.container, tuples_only=True)
        rows = parse_counts(raw)
        if not rows:
            log("не удалось разобрать счётчики:\n" + raw)
            return 2
        ok = print_table(rows)
        if not ok:
            log("в приёмнике не хватает строк. public.* не меняли. Повтор с --replace перезапишет только схемы сервисов.")
            return 1
        log("строки источника и приёмника совпадают.")
        return 0
    except RuntimeError as e:
        log(str(e))
        return 1


if __name__ == "__main__":
    sys.exit(main())
