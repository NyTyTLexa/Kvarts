# Logistics — склад и приёмка

Вынесенный домен из монолита `ProcurementSystem.Api`. Каркас как у `ProcurementSystem.Services.Catalog`: свой веб-хост, схема PostgreSQL, Outbox → NATS, без ссылки на `ProcurementSystem.Infrastructure`.

- **Процесс:** `procurement-logistics`
- **Порт (local):** `5172`
- **Схема PostgreSQL:** `logistics` (история миграций — `logistics.__EFMigrationsHistory`)
- **Событие:** Outbox → NATS JetStream `procurement.GoodsReceiptCompleted`

В `ProcurementSystem.slnx` не добавлен — его правит другой процесс.

```
dotnet build src\ProcurementSystem.Services.Logistics\ProcurementSystem.Services.Logistics.csproj
dotnet run --project src\ProcurementSystem.Services.Logistics\ProcurementSystem.Services.Logistics.csproj
```

## Что принадлежит сервису

Таблицы в схеме `logistics`:

| Сущность | Назначение |
|---|---|
| `GoodsReceipts` | шапка приёмки, снимок номера заказа и клиента |
| `GoodsReceiptLines` | сверка заказано/принято; sku/имя/цена — снимок |
| `OutboxMessages` | транзакционная публикация событий |

Ссылки на чужие агрегаты — голый `Guid` без FK и без `Include`:

- `GoodsReceipt.OrderId` — заказ соседнего сервиса
- `GoodsReceiptLine.ProductId` — товар каталога; наименование, артикул и цена уже в снимке строки

## HTTP

JWT-политики как в монолите: `read` (все роли), `write` (admin/manager), `admin`, `approve`, `postPayment`, `receive` (admin/warehouse). Fallback — аутентифицированный пользователь. `GET /health` — анонимно.

Контроллер тот же, что в Api: класс `WarehouseController`, маршрут `api/receipts` (файла `ReceiptsController.cs` в монолите нет). Пути менять нельзя — шлюз режет по префиксу.

### Приёмка — `/api/receipts`

| Метод | Путь | Политика |
|---|---|---|
| GET | `/api/receipts` | read |
| GET | `/api/receipts/{id}` | read |
| POST | `/api/receipts/from-order?orderId=` | receive |
| POST | `/api/receipts/{id}/lines/{lineId}` | receive |
| POST | `/api/receipts/{id}/complete` | receive |

Телеметрия — имя сервиса `procurement-logistics`, OTLP из `Otlp:Endpoint`.

## Событие: приёмка проведена

Раньше `WarehouseService.CompleteAsync` сам двигал счёт: `Invoice.ОжиданиеПоставки` → `ПришёлНаСклад` → `ОтраженоВ1С` через `IInvoiceCommandService`. Счета теперь в чужом сервисе — прямого UPDATE их таблицы нет.

При проведении в ту же транзакцию, что и `Status = Проведено`, в Outbox кладётся событие. Фоновый `OutboxProcessor` публикует его в JetStream.

- **Тип / subject:** `GoodsReceiptCompleted` → `procurement.GoodsReceiptCompleted` (`Contracts.Messaging.Subject`)
- **Стрим:** `procurement` (`procurement.>`)
- **Запись:** `ProcurementSystem.Contracts/Events/GoodsReceiptCompleted.cs`
- **Тело (JSON, System.Text.Json по умолчанию — PascalCase):**

```json
{
  "ReceiptId": "...",
  "OrderId": "...",
  "SpecificationId": "...",
  "OrderNumber": "ORD-...",
  "Customer": "...",
  "CompletedAtUtc": "2026-09-05T12:00:00Z",
  "OccurredAtUtc": "2026-09-05T12:00:00Z",
  "ReceivedAmount": 1234.56,
  "WarehouseRef": "WMS-...",
  "AccountingRef": "1С-...",
  "Lines": [
    {
      "LineId": "...",
      "ProductId": "...",
      "Sku": "...",
      "Name": "...",
      "OrderedQty": 10,
      "ReceivedQty": 9,
      "Discrepancy": -1,
      "UnitPrice": 100.00
    }
  ]
}
```

`SpecificationId` берётся из `GET /api/orders/{id}` (`OrderDetailDto.SpecificationId`)
в момент проведения и кладётся в событие: у Commercial нет таблицы заказов.

Слушатель (Commercial) по `SpecificationId` находит связанный счёт в статусе
«ОжиданиеПоставки» и делает два перехода ЖЦ. Если счёта нет — пропускает, как
раньше делал монолит. Этот сервис счёт не читает и не пишет.

## Данные у соседей

| Сосед | Зачем | Как |
|---|---|---|
| Заказы (пока монолит `:5165`) | создать приёмку из заказа | HTTP `GET /api/orders/{id}`, `Authorization` пробрасывается. База — `Services:Ordering`. Снимок: id, number, customer, lines (productId/sku/name/quantity/unitPrice). 404 → «Заказ не найден». |
| Каталог | не вызываем | `ProductId` в строке — Guid; sku/имя/цена уже в снимке приёмки. |
| Счета / согласования | не вызываем | только событие выше. |
| WMS / 1С | проведение | `IWarehouseGateway` / `IAccountingGateway` (мок или `ExternalAccounting:Mode=ErpNext`). Контракты в Domain, адаптеры скопированы внутрь сервиса. |

## Что остаётся в монолите и почему

| Остаётся в Api / будущем сервисе | Почему не здесь |
|---|---|
| `GET /api/integration/status` | экран «Настройки», оба шлюза сразу; здесь шлюзы нужны только приёмке |
| Уведомления | `NotificationService` смотрит черновики `GoodsReceipt` в `public` — модуль уведомлений не наш |
| Аналитика | `AnalyticsController` считает `GoodsReceipt` — чужой экран |
| Счета, согласования | commercial |
| Заказы | ordering |
| Каталог, спецификации | catalog / quoting |
| Аудит HTTP | сквозной журнал, не bounded context склада |
| Маршрут шлюза `/api/receipts`, compose, slnx, фронт | правит оркестратор |

## Cutover (честно)

- Данные в `public.GoodsReceipts` **не копируем**. Схема `logistics` стартует пустой. Перенос строк — отдельный шаг.
- Пока шлюз шлёт `api/*` в монолит, этот хост снаружи не виден. Route на logistics включает другой процесс.
- `compose` / `.slnx` **отсюда не трогаем**.
- Колокол и аналитика монолита продолжают читать `public.*`, пока их не перенацелят.

## Зависимости

- `ProjectReference` только на `ProcurementSystem.Domain` и `ProcurementSystem.Contracts`.
- **Нет** ссылки на `ProcurementSystem.Infrastructure`.
- EF Core + Npgsql — схема `logistics`; NATS.Net — Outbox; OTel/Serilog — как у Catalog.

## Миграции и запуск

В отличие от Catalog, здесь есть рукописная `Persistence/Migrations/20260905120000_InitialLogistics.cs` (без Designer/snapshot — `PendingModelChangesWarning` игнорируется, как в монолите). `Program.cs` вызывает `Database.Migrate()`: схема `logistics`, таблицы приёмок и outbox, история в `logistics.__EFMigrationsHistory`.

Нужны живые Postgres (`ConnectionStrings:Postgres`) и по желанию NATS (`Nats:Url`). Без Postgres процесс не поднимется дальше `Migrate()`. Outbox при недоступном NATS ретраит создание стрима 15 раз и затем крутит выборку.

Локально: `http://localhost:5172/health` → `{ "status": "ok" }`.
