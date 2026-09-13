# Commercial — согласования и счета NOC

Вынесенный домен из монолита `ProcurementSystem.Api`. Структура как у `Services.Catalog`.

- **Процесс:** `procurement-commercial`
- **Порт (local):** `5169`
- **Схема PostgreSQL:** `commercial`
- **Соседи:** quoting (пока монолит `Api` на `:5165`) — спецификация и генератор КП

## Что принадлежит сервису

Таблицы в схеме `commercial`:

| Сущность | Назначение |
|---|---|
| `Approvals` | маршрут согласования КП (наценка, маржа, решение КБ) |
| `Invoices` | счёт NOC и линейный ЖЦ стадий |
| `InvoiceLines` | снимок позиций согласованного варианта КП |
| `InvoiceAttachments` | файлы к счёту (bytea) |

Чужие идентификаторы — голый `Guid` без FK и без `Include`:

- `Approval.SpecificationId` — спецификация quoting
- `InvoiceLine.ProductId` — товар каталога

Наименование, артикул, цена, поставщик, производитель уже лежат в снимке строки счёта.
Это тот же паттерн, что у `OrderLine` в монолите, его нельзя «улучшать» джойном на каталог.

## HTTP

JWT-политики как в монолите: `read` (все роли), `write` (admin/manager), `approve`
(admin/commercial), `postPayment` (admin/accounting), `receive` (admin/warehouse),
`admin`. Fallback — аутентифицированный пользователь. `GET /health` — анонимно.

Пути **не менялись**: шлюз режет по префиксу `/api/approvals` и `/api/invoices`.

### Согласования — `/api/approvals`

| Метод | Путь | Политика |
|---|---|---|
| GET | `/api/approvals` | read |
| GET | `/api/approvals/{id}` | read |
| POST | `/api/approvals` | write |
| POST | `/api/approvals/{id}/margin` | write |
| POST | `/api/approvals/{id}/decision` | approve |
| POST | `/api/approvals/{id}/resubmit` | write |

### Счета NOC — `/api/invoices`

| Метод | Путь | Политика |
|---|---|---|
| GET | `/api/invoices` | read |
| GET | `/api/invoices/{id}` | read |
| POST | `/api/invoices` | write |
| POST | `/api/invoices/{id}/status` | postPayment |
| POST | `/api/invoices/{id}/attachments` | postPayment (multipart, лимит 20 МиБ) |
| GET | `/api/invoices/{id}/attachments` | read |
| GET | `/api/invoices/attachments/{attachmentId}/download` | read |

## Фикс относительно монолита

`Approval.Decide` в Domain не проверяет текущий статус, поэтому КБ мог согласовать
документ ещё на стадии «На согласовании РП». Domain не трогаем (чужой файл).
В `ApprovalCommandService.DecideAsync` решение принимается **только** из
`ВКоммерческомБлоке`; иначе — понятная 400, не молчаливый переход.
Уже принятое решение по-прежнему отклоняется фразой «Решение по согласованию уже принято».

Старые фасады `ApprovalService` / `InvoiceService` **не** переносились: контроллеры
работают с CQRS (`*QueryService` / `*CommandService`).

## Что берёт у соседей

Генератор КП и спецификации сюда не копируются. HTTP к quoting
(`Neighbors:QuotingBaseUrl`, по умолчанию `http://localhost:5165`), Bearer входящего
запроса прокидывается как есть:

| Когда | Запрос | Зачем |
|---|---|---|
| POST `/api/approvals` | `GET /api/specifications/{id}` | заказчик, факт существования заявки |
| POST `/api/approvals` | `GET /api/specifications/{id}/quote?strategy=` | `TotalCost`, заголовок КП |
| POST `/api/invoices` | тот же quote | снимок строк (`Matched`, SKU, имя, цена со скидкой) |

Когда quoting вынесут — меняется только URL. Каталог (товар/поставщик) напрямую не читается.

## Что остаётся в монолите и почему

| Остаётся в Api / будущем сервисе | Почему не здесь |
|---|---|
| КП, спецификации, overrides, скидки | quoting: заявка и генерация КП |
| Каталог, прайсы | catalog |
| Заказы | ordering |
| Склад / приёмка | logistics: публикует `GoodsReceiptCompleted`, счёт здесь не пишет |
| Проекты | обёртка над спецификацией |
| Аудит, аналитика | сквозные; аналитика считает маржу по Approval |

## Событие: приёмка проведена

Durable-консьюмер JetStream (не HTTP от склада):

- **Subject:** `procurement.GoodsReceiptCompleted`
- **Durable:** `commercial-goods-receipt`
- **Стрим:** `procurement`
- **Ack:** успех обработки; **Nak:** исключение — сообщение вернётся в стрим
- **Обработчик:** `GoodsReceiptCompletedHandler` (без NATS, покрыт тестом)

По `SpecificationId` события находится счёт в «ОжиданиеПоставки» (согласование той же
спецификации) и системным актором делается «ПришёлНаСклад» → «ОтраженоВ1С».
Счёта нет или он уже в целевой стадии — не ошибка (ack). Пока шлюз не переключил
склад, монолит по-прежнему двигает `public.Invoices` сам — это нормально до cutover.

## Cutover (честно)

- Маршруты commercial на Gateway **ещё не включены**. Включает другой процесс
  (как catalog: кластер есть, route комментируют).
- `compose` / `.slnx` **отсюда не трогаем**.
- Данные монолита лежат в `public.Approvals` / `public.Invoices`. Этот сервис пишет
  в `commercial.*`. Перенос строк — отдельный шаг cutover, не этот PR.

## Зависимости

- `ProjectReference` только на `ProcurementSystem.Domain` и `ProcurementSystem.Contracts`.
- **Нет** ссылки на `ProcurementSystem.Infrastructure`.
- EF Core + Npgsql — схема `commercial`. JWT / Scalar / OTel — те же версии, что у Api.
- NATS.Net 2.8.2 — durable-консьюмер `GoodsReceiptCompleted` (как у Logistics/Worker).

## Миграции

`Persistence/Migrations` — `InitialCreate` под `CommercialDbContext`, схема `commercial`.
При старте `Program.cs` вызывает `Database.Migrate()`.
