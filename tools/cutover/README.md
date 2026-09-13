# Cutover данных `public.*` → схемы сервисов (12.6)

Копирует рабочие строки монолита в схемы вынесенных сервисов. **Источник не удаляет.**

| Схема | Таблицы |
|---|---|
| `quoting` | Specifications, SpecificationItems, QuoteOverrides |
| `commercial` | Approvals, Invoices, InvoiceLines, InvoiceAttachments |
| `ordering` | Orders, OrderLines |
| `logistics` | GoodsReceipts, GoodsReceiptLines |
| `notifications` | Notifications |
| `retail` | RetailShops |

`logistics.OutboxMessages` не копируется — иначе повторно улетит `GoodsReceiptCompleted`.

Идентификаторы (Guid) сохраняются: соседние агрегаты ссылаются на них без FK.
Повторный запуск идемпотентен (`ON CONFLICT DO NOTHING` — без указания колонки, чтобы покрывать
не только первичный ключ: у `Notifications` есть уникальный индекс `DedupeKey`, и сервис
генерирует свои уведомления с теми же ключами, но другими `Id`).

**Про сверку счётчиков.** Строк в схеме сервиса может быть **больше**, чем в `public`: после
переключения маршрутов сервис пишет свои новые данные, а копия в `public` остаётся на момент
снятия. Ошибка переноса — это когда в приёмнике **меньше** исходных строк; расхождение в плюс
нормально. Перед переключением префикса копию имеет смысл снять повторно (дрейф).

## Запуск

Схемы должны уже существовать (каждый сервис делает `Migrate()` при старте).
Postgres стенда — контейнер `ps-postgres`, с хоста порт **5433**.

```text
python tools/cutover/migrate.py
```

Хостовый `psql` вместо `docker exec`:

```text
python tools/cutover/migrate.py --psql
```

Или напрямую:

```text
docker exec -i ps-postgres psql -U procurement -d procurement -v ON_ERROR_STOP=1 < tools/cutover/migrate_public_to_services.sql
```

Только сверка счётчиков: `python tools/cutover/migrate.py --counts-only`.

Полный пересъём приёмника (если в схемах сервисов уже был мусор): `--replace`.
`public.*` всё равно не трогается.

## Откат

Данные монолита на месте. Чтобы SPA снова читала `public.*` — уберите маршруты
шлюза на quoting/commercial/ordering/logistics (catch-all вернёт трафик в `Api`).
Сами строки в схемах сервисов можно оставить или очистить:

```sql
TRUNCATE quoting."QuoteOverrides", quoting."SpecificationItems", quoting."Specifications",
         commercial."InvoiceAttachments", commercial."InvoiceLines", commercial."Invoices", commercial."Approvals",
         ordering."OrderLines", ordering."Orders",
         logistics."GoodsReceiptLines", logistics."GoodsReceipts",
         notifications."Notifications"
RESTART IDENTITY CASCADE;
```
