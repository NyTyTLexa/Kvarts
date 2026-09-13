# Notifications — колокольчик и email

Вынесенный домен из монолита `ProcurementSystem.Api`. Каркас как у `ProcurementSystem.Services.Logistics`: свой веб-хост, схема PostgreSQL, без ссылки на `ProcurementSystem.Infrastructure`.

- **Процесс:** `procurement-notifications`
- **Порт (local):** `5175`
- **Схема PostgreSQL:** `notifications` (история миграций — `notifications.__EFMigrationsHistory`)
- **Источник событий:** HTTP-опрос соседей (не NATS/Outbox)

В `ProcurementSystem.slnx` не добавлен — его правит другой процесс.

```
dotnet build src\ProcurementSystem.Services.Notifications\ProcurementSystem.Services.Notifications.csproj
dotnet run --project src\ProcurementSystem.Services.Notifications\ProcurementSystem.Services.Notifications.csproj
```

## Что принадлежит сервису

Таблица в схеме `notifications`:

| Сущность | Назначение |
|---|---|
| `Notifications` | in-app уведомления (дедуп по `DedupeKey`, прочтение, `EmailedAtUtc`) |

Чужие агрегаты не храним: `RelatedEntityId` — голый `Guid` без FK.

## HTTP

JWT-политики как в монолите: `read` (все роли), `write` (admin/manager), `admin`, `approve`, `postPayment`, `receive`. Fallback — аутентифицированный пользователь. `GET /health` — анонимно.

Контроллер тот же, что в Api: класс `NotificationsController`, маршрут `api/notifications`. Пути менять нельзя — шлюз режет по префиксу.

| Метод | Путь | Политика |
|---|---|---|
| GET | `/api/notifications` | read |
| GET | `/api/notifications/count` | read |
| POST | `/api/notifications/{id}/read` | read |
| POST | `/api/notifications/read-all` | read |

Телеметрия — имя сервиса `procurement-notifications`, OTLP из `Otlp:Endpoint`.

DTO (camelCase, как `Shell.tsx`): `NotificationDto`, `NotificationCountDto` (`{ unread }`).

## Sync: HTTP-опрос открытых документов

Монолитный `SyncAsync` читал чужие таблицы. Здесь `IOpenWorkSource` тянет существующие GET соседей. Outbox у Commercial/Ordering нет; Logistics публикует только `GoodsReceiptCompleted` (проведённая приёмка), а колокольчик смотрит **черновики**. Поэтому JetStream-консьюмера и процессора Outbox нет. `NatsConnection` всё же регистрируется по `Nats:Url` — конфиг/compose требуют `Nats__Url`.

| Сосед | База по умолчанию | GET | Фильтр |
|---|---|---|---|
| Commercial | `http://localhost:5169` | `api/approvals`, `api/invoices` | Approval: НаСогласованииРП / ВКоммерческомБлоке; Invoice: не ОтраженоВ1С и не Отменён |
| Ordering | `http://localhost:5171` | `api/orders` | не Completed и не Cancelled |
| Logistics | `http://localhost:5172` | `api/receipts` | ReceiptStatus.Черновик |

По каждому типу — `OrderByDescending(CreatedAtUtc).Take(20)`. Dedupe: `{type}:{entityId}`. Недоступный сосед логируется warning, остальные источники не падают.

Исходящая авторизация: проброс `Authorization` из текущего запроса; в фоне (email dispatcher) — password grant `client_id=procurement-api`, пользователь `Neighbors:ServiceUser` / `Neighbors:ServicePassword` (дефолт `admin`/`admin`). Токен кэшируется до `expires_in − 30с`. URL: `Neighbors:TokenUrl` либо `{Keycloak:Authority}/protocol/openid-connect/token`.

## Email

`EmailNotificationDispatcher` (hosted) каждые `Email:PollIntervalSeconds` (мин. 5 с) вызывает `EmailNotificationProcessor.DispatchOnceAsync`: сначала `SyncAsync`, затем SMTP по строкам с пустым `EmailedAtUtc`. `Email:Enabled=false` (по умолчанию) — отправитель `NoOpEmailSender`, процесс не падает. SMTP — MailHog (`Host=localhost`, `Port=1025`).

## Зависимости

- `ProjectReference` только на `ProcurementSystem.Domain` и `ProcurementSystem.Contracts`.
- **Нет** ссылки на `ProcurementSystem.Infrastructure`.
- Сущность — `ProcurementSystem.Domain.Notifications.Notification`, не копия класса.
- EF Core + Npgsql — схема `notifications`; NATS.Net — соединение без консьюмера; OTel/Serilog — как у Logistics.

## Миграции и запуск

Рукописная `Persistence/Migrations/20260912120000_InitialCreate.cs` (колонка `EmailedAtUtc` сразу в Initial). `Program.cs` вызывает `Database.Migrate()`: схема `notifications`, таблица, история в `notifications.__EFMigrationsHistory`. `PendingModelChangesWarning` игнорируется.

Нужен живой Postgres (`ConnectionStrings:Postgres`). Без него процесс не поднимется дальше `Migrate()`. Соседи и Keycloak для Sync/email — по желанию: недоступный источник пропускается.

Локально: `http://localhost:5175/health` → `{ "status": "ok" }`.
