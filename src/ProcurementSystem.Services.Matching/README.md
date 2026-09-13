# Matching — ML-подбор позиций заявки к каталогу

Вынесенный процесс из монолита. Своей схемы нет: каталог читается по HTTP, спецификации живут у Quoting.

- **Процесс:** `procurement-matching`
- **Порт (local):** `5174` (catalog — `5168`, quoting — `5170`, api — `5165`, auth — `5167`)
- **Схема PostgreSQL:** нет

## HTTP

JWT-политики как в монолите: `read` (все роли), `write` (admin/manager). Fallback — аутентифицированный пользователь. `GET /health` — анонимно.

Два префикса на одни и те же методы (фронт и шлюз):

### Сопоставление спецификации

| Метод | Путь | Политика |
|---|---|---|
| GET | `/api/specifications/{specId}/match` | read |
| POST | `/api/specifications/{specId}/match/apply?minP=0.5` | write |
| POST | `/api/specifications/{specId}/match/apply-one` | write |
| GET | `/api/matching/{specId}` | read |
| POST | `/api/matching/{specId}/apply?minP=0.5` | write |
| POST | `/api/matching/{specId}/apply-one` | write |

### Подсказки без заявки

| Метод | Путь | Политика |
|---|---|---|
| POST | `/api/matching/suggest` | read |
| GET | `/api/matching/suggest?sku=&name=&take=5` | read |

`GET /health` → `{ "status": "ok" }`.

## Данные у соседей

| Сосед | Зачем | Как | Конфиг |
|---|---|---|---|
| Catalog (`http://catalog:8080` в compose, локально `:5168`) | номенклатура для TF-IDF; при большом каталоге — SELECT из схемы `catalog` | JWT / read-only SQL | `Neighbors:Catalog:BaseUrl`, `ConnectionStrings:Postgres` |
| Quoting (`http://quoting:8080` в compose, локально `:5170`) | спецификация и запись `ProductId` | JWT прокидывается `AuthForwardHandler` | `Neighbors:Quoting:BaseUrl` |

Конкретные пути:

- Catalog `GET /api/catalog?page=&pageSize=48` — запасной снимок (витрина режет pageSize до 48)
- `SELECT` из `catalog."Products"` — основной снимок на стенде (десятки–сотни тысяч SKU; своей схемы нет, только чтение)
- Catalog `GET /api/catalog/{id}` — проверка существования товара
- Quoting `GET /api/specifications/{id}` — заявка с позициями
- Quoting `POST /api/specifications/{id}/match/apply-one` — привязать позицию (прямой вызов процесса, не шлюз)

## Зависимости

- `ProjectReference` только на `ProcurementSystem.Domain` и `ProcurementSystem.Contracts`.
- **Нет** ссылки на `ProcurementSystem.Infrastructure`.
- **Нет** EF / Npgsql: своей БД нет.
- JWT / Scalar / OpenTelemetry + Serilog — пакеты тех же версий, что у Quoting, без EF-инструментации.

## Сборка

Проект в `ProcurementSystem.slnx`. Локально: `http://localhost:5174/health` → `{ "status": "ok" }`.
