# Retail — розничные витрины B2C

Вынесенный домен из монолита `ProcurementSystem.Api`. Каркас как у `ProcurementSystem.Services.Catalog`: свой веб-хост, схема PostgreSQL, без ссылки на `ProcurementSystem.Infrastructure`.

- **Процесс:** `procurement-retail`
- **Порт (local / Compose):** `5176`
- **Схема PostgreSQL:** `retail` (история миграций — `retail.__EFMigrationsHistory`)
- **Каталог:** HTTP `Neighbors:Catalog:BaseUrl` (хост `:5168`, Docker `http://catalog:8080`), Bearer пробрасывается

```
dotnet build src\ProcurementSystem.Services.Retail\ProcurementSystem.Services.Retail.csproj
dotnet run --project src\ProcurementSystem.Services.Retail\ProcurementSystem.Services.Retail.csproj
```

## Что принадлежит сервису

| Сущность | Назначение |
|---|---|
| `RetailShops` | известные и найденные витрины (хост, шаблон поиска, hit count) |

Номенклатуру, вендоров и офферы **не копируем** — `POST /api/retail/import` пишет в Catalog (`POST /api/vendors`, `/api/products`, `/api/pricelist`).

## HTTP

JWT-политики как в монолите. `GET /health` — анонимно.

| Метод | Путь | Политика |
|---|---|---|
| GET | `/api/retail/shops` | read |
| POST | `/api/retail/search` | read |
| POST | `/api/retail/import` | write |

Телеметрия — имя сервиса `procurement-retail`, OTLP из `Otlp:Endpoint`.
