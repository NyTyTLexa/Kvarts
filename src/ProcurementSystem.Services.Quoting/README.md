# Quoting — вынос спецификаций, подбора и генерации КП

Один из шести сервисов, вынесенных из монолита `ProcurementSystem.Api`. Структура как у `ProcurementSystem.Services.Catalog`.

- **Процесс:** `procurement-quoting`
- **Порт (local):** `5170` (catalog — `5168`, ordering — `5171`, api — `5165`, auth — `5167`)
- **Схема PostgreSQL:** `quoting`
- **КП не таблица:** коммерческое предложение считается генератором на лету, сущности `Quote` в БД нет

## Что принадлежит сервису

Таблицы в схеме `quoting`:

| Сущность | Назначение |
|---|---|
| `Specifications` | заявка заказчика: название, заказчик |
| `SpecificationItems` | позиции заявки: сырой артикул/имя, количество, `ProductId` как голый Guid |
| `QuoteOverrides` | ручной выбор поставщика на позицию (ТЗ п.5.5); `VendorId` — голый Guid |

Отображаемые поля товара (наименование, артикул, производитель, цена) **не джойнятся** из каталога: при генерации КП они приходят снимком по HTTP и кладутся в строки ответа. Это осознанный паттерн монолита.

`ProductId` / `VendorId` — Guid без внешнего ключа и без `Include`. Навигация `SpecificationItem.Product` в Domain игнорируется в `QuotingDbContext`.

## HTTP

JWT-политики как в монолите: `read` (все роли), `write` (admin/manager). Fallback — аутентифицированный пользователь. `GET /health` — анонимно. Пути **не менялись**: шлюз режет по `/api/specifications`.

### Спецификации — `/api/specifications`

| Метод | Путь | Политика |
|---|---|---|
| GET | `/api/specifications` | read |
| GET | `/api/specifications/{id}` | read |
| POST | `/api/specifications` | write |
| POST | `/api/specifications/{id}/items` | write |
| DELETE | `/api/specifications/{id}/items/{itemId}` | write |
| DELETE | `/api/specifications/{id}` | write |
| POST | `/api/specifications/{id}/items/{itemId}/override` | write |
| DELETE | `/api/specifications/{id}/items/{itemId}/override` | write |
| POST | `/api/specifications/import` | write (multipart, xlsx, лимит 50 МиБ) |

### Сопоставление — `/api/specifications/{specId}/match`

| Метод | Путь | Политика |
|---|---|---|
| GET | `/api/specifications/{id}/match` | read |
| POST | `/api/specifications/{id}/match/apply?minP=0.5` | write |
| POST | `/api/specifications/{id}/match/apply-one` | write |

### КП — `/api/specifications/{specId}/quote`

| Метод | Путь | Политика |
|---|---|---|
| GET | `/api/specifications/{id}/quote` | read |
| GET | `/api/specifications/{id}/quote/compare` | read |
| GET | `/api/specifications/{id}/quote/export` | read (xlsx) |
| GET | `/api/specifications/{id}/quote/export/pdf` | read (pdf) |

`GET /health` → `{ "status": "ok" }`.

## Данные у соседей

| Сосед | Зачем | Как | Конфиг |
|---|---|---|---|
| Catalog (`http://catalog:8080` в compose, локально `:5168`) | номенклатура, офферы, история цены, скидки | JWT прокидывается `AuthForwardHandler` | `Neighbors:Catalog:BaseUrl` |

Конкретные пути Catalog:

- `GET /api/catalog?page=&pageSize=48` — полный снимок номенклатуры для TF-IDF (pageSize у витрины clamped до 48)
- `GET /api/catalog/{id}` — карточка + офферы + история цены
- `GET /api/catalog/suggest?q=` — поиск по артикулу при добавлении позиции
- `GET /api/products?search=` — запасной точный поиск SKU (тот же хост Catalog)
- `GET /api/discounts` — активные скидки; отдельного `GetActiveFor` HTTP в Catalog нет, фильтруем на своей стороне

Каталог при чтении спецификации не дёргается: в позициях лежат `RawSku` / `RawName` / `ProductId`.

## Что остаётся в монолите и почему

| Остаётся в Api / другом сервисе | Почему не здесь |
|---|---|
| Товары, офферы, скидки, прайс-импорт | catalog: справочник, мы только читаем |
| Заказы | ordering: забирает снимок КП по тем же путям `/api/specifications/{id}/quote` |
| Согласования, счета NOC | commercial |
| Приёмка / склад | logistics |
| Проекты | обёртка над спецификацией, другой агрегат |
| `AuditMiddleware`, журнал аудита | сквозной контур монолита |
| Meilisearch / Worker / `ISearchEngine` | индекс пишет catalog+worker. В монолите matcher ходил в Meilisearch **опционально** (try/catch); здесь кандидаты — только TF-IDF + артикул + категория. Качество fuzzy на коротких именах может чуть просесть, пока шлюз не даст quoting тот же индекс |
| `seed-corpus` с кривой заявкой | Catalog наполняет витрину; спецификацию корпуса больше не создаёт (в ответе `specificationId = 000…0`). Отдельного seed заявок в quoting нет |
| Контроллеры quoting в Api | cutover шлюза делает другой процесс; этот сервис чужие файлы не трогает |

## Cutover (честно)

- Маршруты `/api/specifications` на Gateway **ещё не включены**. Пока шлюз шлёт заявки и КП в монолит.
- `compose` / `.slnx` **отсюда не трогаем**.
- Схема `quoting` живёт рядом с `public.Specifications` монолита. Это не cutover данных: два комплекта таблиц, пока другой процесс не решит, откуда читать.

## Зависимости

- `ProjectReference` только на `ProcurementSystem.Domain` и `ProcurementSystem.Contracts`.
- **Нет** ссылки на `ProcurementSystem.Infrastructure`.
- ClosedXML — импорт заявки и Excel-КП; QuestPDF — PDF; EF Core + Npgsql — схема `quoting`.
- JWT / Scalar / OpenTelemetry + Serilog — пакеты тех же версий, что у Api/Catalog.

## Сборка

Проект в `ProcurementSystem.slnx` не добавляется. Собирать csproj напрямую:

```
dotnet build src\ProcurementSystem.Services.Quoting\ProcurementSystem.Services.Quoting.csproj
```

Локально: `http://localhost:5170/health` → `{ "status": "ok" }`. В Docker Catalog — `http://catalog:8080`.

## Миграции

Ручная `InitialQuoting` создаёт `quoting.Specifications` / `quoting.SpecificationItems` / `quoting.QuoteOverrides`. История EF — таблица `quoting.__EFMigrationsHistory`. При старте — `Database.Migrate()`, как в Api/Catalog/Ordering.
