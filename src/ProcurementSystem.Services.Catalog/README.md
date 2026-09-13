# Catalog — шаблон выноса сервиса

Первый вынесенный домен из монолита `ProcurementSystem.Api`. Этот файл — образец README для следующих пяти сервисов (quoting, commercial, ordering, logistics, matching/search): те же разделы, та же честность про cutover.

- **Процесс:** `procurement-catalog`
- **Порт (local):** `5168`
- **Схема PostgreSQL:** `catalog`
- **Событие:** Outbox → NATS JetStream `procurement.ProductUpserted`

## Что принадлежит сервису

Таблицы в схеме `catalog`:

| Сущность | Назначение |
|---|---|
| `Products` | номенклатура (SKU, имя, производитель-строка, категория-путь) |
| `Vendors` | поставщики |
| `Manufacturers` | справочник брендов (ТЗ п.5.2) |
| `PriceListItems` | офферы (цена, срок, остаток) |
| `PriceHistory` | точки истории цены при каждом upsert оффера |
| `PriceImportUploads` | архив оригиналов Excel (ТЗ п.5.1) |
| `Discounts` | скидки по поставщику **или** производителю (ТЗ п.5.4) |

`Product.Category` — `HasMaxLength(1000)`: путь «N уровень иерархии» с реальных прайсов (EKF) не влезает в монолитные 300 символов.

## HTTP

JWT-политики как в монолите: `read` (все роли), `write` (admin/manager), `admin` (admin). Fallback — аутентифицированный пользователь. `GET /health` — анонимно.

### Товары — `/api/products`

| Метод | Путь | Политика |
|---|---|---|
| GET | `/api/products` | read |
| GET | `/api/products/categories` | read |
| GET | `/api/products/{id}` | read |
| GET | `/api/products/{id}/analogs` | read |
| POST | `/api/products` | write |
| PUT | `/api/products/{id}` | write |
| DELETE | `/api/products/{id}` | write |

### Витрина — `/api/catalog`

| Метод | Путь | Политика |
|---|---|---|
| GET | `/api/catalog` | read |
| GET | `/api/catalog/suggest` | read |
| GET | `/api/catalog/compare` | read |
| GET | `/api/catalog/{id}` | read |

### Поставщики — `/api/vendors`

| Метод | Путь | Политика |
|---|---|---|
| GET | `/api/vendors` | read |
| GET | `/api/vendors/{id}` | read |
| POST | `/api/vendors` | write |
| PUT | `/api/vendors/{id}` | write |
| DELETE | `/api/vendors/{id}` | write |

### Производители — `/api/manufacturers`

| Метод | Путь | Политика |
|---|---|---|
| GET | `/api/manufacturers` | read |
| GET | `/api/manufacturers/{id}` | read |
| POST | `/api/manufacturers` | write |
| PUT | `/api/manufacturers/{id}` | write |
| DELETE | `/api/manufacturers/{id}` | write |

### Прайс-листы — `/api/pricelist`

| Метод | Путь | Политика |
|---|---|---|
| GET | `/api/pricelist/by-product/{productId}` | read |
| GET | `/api/pricelist/history/{productId}` | read |
| POST | `/api/pricelist` | write |
| POST | `/api/pricelist/import` | write (multipart, `vendorId` query+form, `file`, лимит 64 МиБ) |
| POST | `/api/pricelist/seed-corpus` | admin |
| GET | `/api/pricelist/uploads` | read |
| GET | `/api/pricelist/uploads/{id}/download` | read |

### Скидки — `/api/discounts`

| Метод | Путь | Политика |
|---|---|---|
| GET | `/api/discounts` | read |
| GET | `/api/discounts/{id}` | read |
| POST | `/api/discounts` | write |
| PUT | `/api/discounts/{id}` | write |
| DELETE | `/api/discounts/{id}` | write |

## Импорт: два фикса относительно монолита

1. **Лист с таблицей цен.** Не `Worksheets.First()`. Обходим все листы, `DetectColumns` на каждом, берём **первый**, где нашлась строка заголовков («Артикул» + имя + «Цена»). Иерархию (`CollectHierarchy`) читаем со **всех остальных** листов — вспомогательные листы EKF работают, даже если прайс не на листе 1. Нет заголовков нигде — та же ошибка «Структура файла не распознана…».
2. **OLE2 `.xls`.** NPOI копирует **все** листы в in-memory ClosedXML (не только `GetSheetAt(0)`). Имена листов: ≤ 31 символ, без `: \ / ? * [ ]`, уникальные. Пустая книга → лист «Лист1».

Дальше как в монолите: архив исходных байт, пакеты `SaveBatchSize = 500`, `EnqueueEvent(ProductUpserted)`, `RecordPriceChange`.

## Что остаётся в монолите и почему

Каталог — справочник. Остальное — другие агрегаты и другие сервисы:

| Остаётся в Api / будущем сервисе | Почему не здесь |
|---|---|
| КП, спецификации, overrides | quoting: заявка и генерация КП |
| Согласования | commercial |
| Счета NOC | commercial |
| Заказы | ordering |
| Склад / приёмка | logistics |
| Проекты | обёртка над спецификацией |
| Аудит, аналитика | сквозные, не bounded context каталога |
| `/api/search`, Meilisearch, Worker | поиск читает индекс, не схему `catalog` |
| Retail-импорт | съём витрин, отдельный контур |
| Matching (ML) | живёт на спецификации + каталоге, владелец — quoting/matching |

`POST /api/pricelist/seed-corpus` здесь наполняет **только** таблицы каталога (вендоры, товары, офферы, архив xlsx, история, Outbox). Кривая заявка для ML-стенда — сущность quoting, поэтому в JSON `specificationId = 000…0`, `specItems = 0`, `specUnmatched = 0`, пока quoting не вынесен. Форма ответа та же (`CorpusResult` на фронте не ломается). Повторный вызов всё равно кладёт `ProductUpserted` на каждый товар корпуса.

`IDiscountService.GetActiveForAsync` перенесён как in-process API для будущего сервиса КП. Отдельного HTTP-маршрута в монолите не было — и здесь нет.

## Cutover (честно)

- Worker по-прежнему читает `public` через `AppDbContext`. Пока индексатор не перенацелят на `catalog.*`, Meilisearch **не увидит** строки, записанные только этим сервисом.
- Маршруты каталога на Gateway **ещё не включены** (кластер `catalog` есть, route закомментирован). Включает другой процесс.
- `compose` / `.slnx` / миграции **отсюда не трогаем**.

## Зависимости

- `ProjectReference` только на `ProcurementSystem.Domain` и `ProcurementSystem.Contracts`.
- **Нет** ссылки на `ProcurementSystem.Infrastructure`.
- ClosedXML + NPOI — импорт Excel; EF Core + Npgsql — схема `catalog`.

## Миграции и запуск

`dotnet` в этом процессе недоступен, миграции **не сгенерированы**. `Program.cs` вызывает `Database.Migrate()` как монолит: без миграций это no-op, таблицы `catalog.*` не появятся. Другой процесс: добавить проект в sln/compose, `dotnet ef migrations add` под `CatalogDbContext` (хост Web SDK, `IDesignTimeDbContextFactory` не нужен), схема `catalog`.

Локально после включения в sln: `http://localhost:5168/health` → `{ "status": "ok" }`. Meilisearch в `appsettings.json` — как у Api; этот процесс в индекс не пишет.
