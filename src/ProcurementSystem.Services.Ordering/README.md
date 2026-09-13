# Ordering — вынос сервиса заказов

Второй вынесенный домен из монолита `ProcurementSystem.Api`. Структура как у `ProcurementSystem.Services.Catalog`.

- **Процесс:** `procurement-ordering`
- **Порт (local):** `5171` (commercial занял `5169`, quoting — `5170`, catalog — `5168`, api — `5165`, auth — `5167`)
- **Схема PostgreSQL:** `ordering`
- **Агрегат:** `Order` + `OrderLine` (снимок выбранного варианта КП)

## Что принадлежит сервису

Таблицы в схеме `ordering`:

| Сущность | Назначение |
|---|---|
| `Orders` | заказ: номер, статус, итог, стратегия, `SpecificationId` как голый Guid |
| `OrderLines` | позиции-снимки: имя, артикул, цена, поставщик, срок; `ProductId`/`VendorId` — голые Guid без FK |

Отображаемые поля (наименование, артикул, цена, имя поставщика) **уже лежат в строке**. Каталог при чтении заказа не дёргается — это осознанный паттерн монолита, его не ломаем.

Матрица переходов статусов **одна**: `Order.Transitions` / `Order.TryTransitionTo` в Domain. Сервис больше не держит приватную копию (дефект монолита при переносе закрыт).

## HTTP

JWT-политики как в монолите: `read` (все роли), `write` (admin/manager). Fallback — аутентифицированный пользователь. `GET /health` — анонимно. Пути **не менялись**: шлюз режет по `/api/orders`.

| Метод | Путь | Политика | Назначение |
|---|---|---|---|
| GET | `/api/orders` | read | список |
| GET | `/api/orders/{id}` | read | заказ с позициями |
| POST | `/api/orders/from-quote` | write | оформить из КП (`specId`, `strategy`, `wPrice`, `wLead`, `onlyInStock`) |
| POST | `/api/orders/{id}/status` | write | смена статуса; недопустимый переход → 409 |
| GET | `/health` | anonymous | `{ "status": "ok" }` |

## Что остаётся в монолите и почему

| Остаётся в Api / будущем сервисе | Почему не здесь |
|---|---|
| Генератор КП, спецификации, overrides, скидки | quoting: заявка и расчёт варианта. Заказ только забирает снимок |
| Товары, офферы, `StockQuantity` | catalog: витринный остаток чужой. Резерв — HTTP к соседу, не `Include` оффера |
| Согласования, счета NOC | commercial |
| Приёмка / склад | logistics |
| `AuditMiddleware`, журнал аудита | сквозной контур монолита |
| Outbox / NATS / Meilisearch | заказ не публикует `ProductUpserted` |
| `OrdersController` в Api | cutover шлюза делает другой процесс; этот сервис чужие файлы не трогает |

## Данные у соседей

| Сосед | Зачем | Как | Конфиг |
|---|---|---|---|
| Quoting (пока монолит `:5165`) | снимок КП + `customer` спецификации | `GET /api/specifications/{id}/quote?...`, `GET /api/specifications/{id}`; JWT прокидывается | `Neighbors:Quoting:BaseUrl` |
| Catalog (`:5168`, пока часто тот же монолит) | резерв/возврат остатка при оформлении и отмене | `POST /api/catalog/stock/adjust` `{ items: [{ productId, vendorId, quantity, sign }] }` | `Neighbors:Catalog:BaseUrl` |

Каталог отдаёт `POST /api/catalog/stock/adjust`. 404/405 (старый монолит без эндпоинта) по-прежнему логируются, заказ сохраняется.

## Cutover (честно)

- Маршрут `/api/orders` на Gateway **ещё не включён**. Пока шлюз шлёт заказы в монолит.
- `compose` / `.slnx` **отсюда не трогаем**.
- Схема `ordering` живёт рядом с `public.Orders` монолита. Это не cutover данных: два комплекта таблиц, пока другой процесс не решит, откуда читать.

## Зависимости

- `ProjectReference` только на `ProcurementSystem.Domain` и `ProcurementSystem.Contracts`.
- **Нет** ссылки на `ProcurementSystem.Infrastructure`.
- JWT / Scalar / EF Core + Npgsql / OpenTelemetry + Serilog — пакеты тех же версий, что у Api/Catalog.

## Сборка

Проект в `ProcurementSystem.slnx` не добавляется. Собирать csproj напрямую:

```
dotnet build src\ProcurementSystem.Services.Ordering\ProcurementSystem.Services.Ordering.csproj
```

## Миграции

Ручная `InitialOrdering` создаёт `ordering.Orders` / `ordering.OrderLines`. История EF — таблица `ordering.__EFMigrationsHistory` (не общая с монолитом). При старте — `Database.Migrate()`, как в Api/Catalog.
