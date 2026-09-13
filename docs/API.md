# Справочник REST API

Перечень эндпоинтов по контроллерам монолита `ProcurementSystem.Api`, сервиса
`ProcurementSystem.Auth` и вынесенных `Services.{Catalog,Quoting,Commercial,Ordering,Logistics,Matching,Notifications}`.
Интерактивная версия со схемами моделей — Scalar монолита: http://localhost:5165/scalar;
Auth: http://localhost:5167/scalar; Catalog (Docker): http://localhost:5161/scalar;
Matching (хост): http://localhost:5174/scalar; Notifications (хост): http://localhost:5175/scalar.

## Общее

- **Base URL (Docker):** `http://localhost:5160` — Gateway (YARP). SPA (nginx) ходит
  same-origin `/api/...`, nginx проксирует на шлюз. Шлюз JWT не проверяет.
- **Base URL (Vite, хост):** фронт ходит на `/api/...`; Vite проксирует `/api/auth` и
  `/api/users` на Auth `:5167`, **остальной** `/api` — на монолит `:5165` (шлюз и Catalog
  не участвуют). Каталог в этом режиме читается из схемы `public`, не из `catalog`.
- **Кто обслуживает префикс** (таблица шлюза, `src/ProcurementSystem.Gateway/appsettings.json`):

| Префикс | Процесс | Примечание |
|---|---|---|
| `/api/auth`, `/api/users` | Auth `:5167` | в монолите контроллеры заглушены |
| `/api/catalog`, `/api/products`, `/api/vendors`, `/api/manufacturers`, `/api/pricelist`, `/api/discounts` | Catalog `:5161` | те же пути есть и в монолите; Docker-SPA идёт в Catalog |
| `/api/specifications` (кроме `…/match`) | Quoting `:5162` / хост `:5170` | КП, импорт, CRUD заявок |
| `/api/matching`, `/api/specifications/{id}/match` | Matching `:5174` | ML-подбор; своей схемы нет |
| `/api/approvals`, `/api/invoices` | Commercial `:5163` / хост `:5169` | |
| `/api/orders` | Ordering `:5164` / хост `:5171` | |
| `/api/receipts` | Logistics `:5166` / хост `:5172` | |
| `/api/notifications` | Notifications `:5175` | колокольчик + SMTP; в монолите контроллер сохранён |
| прочее `/api/**` | монолит `:5165` | поиск, проекты, аналитика, аудит, retail |
| `GET /health` | тот хост, к которому обратились | шлюз: `{ "status": "ok", "service": "gateway" }`; Auth добавляет `"service": "auth"` |

- **Формат:** JSON (`application/json`), кроме загрузки/скачивания файлов.
- **enum** сериализуются **строками** (`"Balanced"`, `"Согласовано"`) и принимаются по имени.
- **Аутентификация:** JWT Bearer (Keycloak). Заголовок `Authorization: Bearer <access-token>`.
  Все эндпоинты требуют аутентификации, кроме `GET /health`, `POST /api/auth/login` и
  `POST /api/auth/register`.
- **Авторизация:** политика указана в колонке «Доступ». Матрица ролей — в
  [README](../README.md#аутентификация-и-роли).

| Политика | Роли |
|---|---|
| `read` | admin, manager, viewer, commercial, accounting, warehouse |
| `write` | admin, manager |
| `approve` | admin, commercial |
| `postPayment` | admin, accounting |
| `setInvoiceStatus` | admin, commercial, accounting |
| `receive` | admin, warehouse |
| `admin` | admin |

Типовые коды ответов: `200/201/204` — успех; `400` — ошибка валидации/бизнес-правила;
`401` — нет/невалидный токен; `403` — роль не разрешена политикой; `404` — не найдено;
`409` — недопустимый переход статуса (заказ, счёт, приёмка). Для счёта отказ по роли
на `POST /api/invoices/{id}/status` — `403` (`Invoice.ActorMaySet`), а не `409`.

---

## Аутентификация — `/api/auth`

Сервис `ProcurementSystem.Auth`. Шлюз (и Vite) режет сюда. Keycloak Admin API /
Resource Owner Password; своей БД нет. SPA также входит по OIDC (редирект на Keycloak),
эти эндпоинты — форма логина/регистрации (`frontend/src/pages/AuthScreen.tsx`).

| Метод | Путь | Доступ | Описание |
|---|---|---|---|
| POST | `/api/auth/register` | **анонимно** | Создать учётку в realm `procurement`. Роль по умолчанию — `viewer`. `409` если логин/email заняты; `502`/`503` если Keycloak не принял / недоступен |
| POST | `/api/auth/login` | **анонимно** | Парольный вход, в ответе токены Keycloak. `401` если отклонено |

```jsonc
// POST /api/auth/register — RegisterRequest
{ "username": "ivanov", "email": "ivanov@example.com", "firstName": "Иван", "lastName": "Иванов", "password": "secretsecret", "company": "ООО Ромашка" }
// ответ RegisterResult: { "ok": true, "message": "Учётка создана. Можно входить." }

// POST /api/auth/login — LoginRequest
{ "username": "manager", "password": "manager" }
// ответ LoginResult: { "ok": true, "message": "Ок", "tokens": { "accessToken", "refreshToken", "idToken", "expiresIn", "tokenType", "scope" } }
```

---

## Каталог

### Товары — `/api/products`

| Метод | Путь | Доступ | Описание |
|---|---|---|---|
| GET | `/api/products` | read | Список с пагинацией. Query: `page=1`, `pageSize=20`, `search`, `category` (префиксный фильтр по пути категории) |
| GET | `/api/products/categories` | read | Иерархические категории (пути) с числом позиций — для фильтра |
| GET | `/api/products/{id}` | read | Карточка товара |
| GET | `/api/products/{id}/analogs` | read | Аналоги (та же нижняя категория иерархии, ТЗ п.5.3) |
| POST | `/api/products` | write | Создать товар |
| PUT | `/api/products/{id}` | write | Обновить товар |
| DELETE | `/api/products/{id}` | write | Удалить товар |

```jsonc
// POST / PUT — CreateProductRequest / UpdateProductRequest
{ "sku": "R740-16SFF", "name": "Dell PowerEdge R740", "manufacturer": "Dell", "category": "Серверы / Стоечные" }
```

### Электронный каталог — `/api/catalog`

Витрина номенклатуры с агрегатами по офферам (мин/макс цена, число поставщиков, остаток).

| Метод | Путь | Доступ | Описание |
|---|---|---|---|
| GET | `/api/catalog` | read | Карточки. Query: `page`, `pageSize` (до 48), `search`, `category`, `manufacturer`, `vendorId`, `inStockOnly`, `sort` (`name`/`priceAsc`/`priceDesc`/`offers`/`stock`) |
| GET | `/api/catalog/suggest` | read | Подсказки. Query: `q` (≥2 символа), `limit` (до 16) |
| GET | `/api/catalog/compare` | read | Карточки пачкой. Query: `ids=guid,guid` (до 48, порядок запроса) |
| GET | `/api/catalog/{id}` | read | Карточка + офферы + аналоги + история цены |
| POST | `/api/catalog/stock/adjust` | write | Резерв/возврат остатка пакетом. Тело: `{ "items": [{ "productId", "vendorId", "quantity", "sign" }] }`. `sign` — только направление (−1 резерв, +1 возврат), количество по модулю. Неизвестные офферы пропускаются, ниже нуля не уходим. Ответ `204`. Клиент — вынесенный Ordering; в монолите резерв пишет `OrderService` напрямую |

Экраны: `/ecatalog` витрина, `/ecatalog/{id}` позиция, `/ecatalog/compare?ids=` сравнение. Подборка в localStorage → новая или существующая спецификация, выгрузка CSV.

### Розничные витрины — `/api/retail`

Живой путь — сервис `ProcurementSystem.Services.Retail` (`:5176`, схема `retail`).
Шлюз: `/api/retail/{**rest}`. Контроллер монолита сохранён (прямой `:5165` → `public`).

Поиск магазинов и съём публичных карточек (JSON-LD / OpenGraph / Wildberries JSON). Не B2B-прайс, не обход капчи.
Импорт пишет вендора/товар/оффер в **Catalog** по HTTP (`Neighbors:Catalog:BaseUrl`), не в свою БД.

| Метод | Путь | Доступ | Описание |
|---|---|---|---|
| GET | `/api/retail/shops` | read | Известные + найденные хосты |
| POST | `/api/retail/search` | read | `{ q, wildberries, discover, knownShops, url, limit }` — превью офферов |
| POST | `/api/retail/import` | write | `{ hits }` — upsert Vendor / Product / PriceListItem в Catalog |

Экран: `/retail`.

### Поиск — `/api/search`

| Метод | Путь | Доступ | Описание |
|---|---|---|---|
| GET | `/api/search` | read | Fuzzy-поиск по индексу Meilisearch. Query: `q`, `limit=20` |

---

## Поставщики и производители

### Поставщики — `/api/vendors`

| Метод | Путь | Доступ | Описание |
|---|---|---|---|
| GET | `/api/vendors` | read | Список (поиск/пагинация) |
| GET | `/api/vendors/{id}` | read | Карточка поставщика |
| POST | `/api/vendors` | write | Создать |
| PUT | `/api/vendors/{id}` | write | Обновить |
| DELETE | `/api/vendors/{id}` | write | Удалить |

```jsonc
// POST / PUT — CreateVendorRequest / UpdateVendorRequest
{ "name": "ЭТМ", "inn": "7714010893", "defaultLeadTimeDays": 7 }
```

### Производители — `/api/manufacturers`

| Метод | Путь | Доступ | Описание |
|---|---|---|---|
| GET | `/api/manufacturers` | read | Список со счётчиком позиций каталога |
| GET | `/api/manufacturers/{id}` | read | Карточка |
| POST | `/api/manufacturers` | write | Создать |
| PUT | `/api/manufacturers/{id}` | write | Обновить |
| DELETE | `/api/manufacturers/{id}` | write | Удалить |

```jsonc
// POST — CreateManufacturerRequest
{ "name": "Cisco", "country": "США" }
// PUT — UpdateManufacturerRequest
{ "name": "Cisco", "country": "США", "isActive": true }
```

---

## Прайс-листы — `/api/pricelist`

| Метод | Путь | Доступ | Описание |
|---|---|---|---|
| GET | `/api/pricelist/by-product/{productId}` | read | Офферы (предложения поставщиков) по товару |
| GET | `/api/pricelist/history/{productId}` | read | История цен товара |
| POST | `/api/pricelist` | write | Добавить оффер вручную |
| POST | `/api/pricelist/import?vendorId=` | write | Импорт прайса из Excel (`multipart/form-data`, поле `file`; `.xlsx`/`.xls`, до 64 МБ) |
| POST | `/api/pricelist/seed-corpus?lists=120&catalogSize=4000` | write | Стенд ML: 120 прайсов × тысячи SKU + кривая заявка. Идемпотентно, не парсит интернет |
| GET | `/api/pricelist/uploads?vendorId=&limit=50` | read | Архив загрузок (ТЗ п.5.1) |
| GET | `/api/pricelist/uploads/{id}/download` | read | Скачать оригинал загруженного файла |

```jsonc
// POST /api/pricelist — CreateOfferRequest
{ "productId": "…", "vendorId": "…", "price": 145000, "leadTimeDays": 5, "stockQuantity": 12 }
```

Импорт распознаёт колонки по заголовкам (гибкий маппинг): «Артикул», «Наименование»/«Номенклатура»,
«Цена» (приоритет «без НДС»); опционально «Производитель», «Категория», «Срок», «Остаток».
Лист с товарами ищется по заголовкам (не только первый); из `.xls` переливаются все листы;
путь иерархии длиннее 300 символов сжимается (корень и нижние уровни).
Ответ — сводка (обработано строк, создано товаров, обновлено офферов, ошибки).

---

## Скидки — `/api/discounts`

Скидка нацелена **либо** на поставщика (`vendorId`), **либо** на производителя (`manufacturer`).
Применяется генератором КП автоматически (ТЗ п.5.4).

| Метод | Путь | Доступ | Описание |
|---|---|---|---|
| GET | `/api/discounts` | read | Список скидок |
| GET | `/api/discounts/{id}` | read | Карточка |
| POST | `/api/discounts` | write | Создать |
| PUT | `/api/discounts/{id}` | write | Изменить % и срок |
| DELETE | `/api/discounts/{id}` | write | Удалить |

```jsonc
// POST — CreateDiscountRequest (заполнить vendorId ИЛИ manufacturer)
{ "vendorId": "…", "manufacturer": null, "percent": 7.5, "validFromUtc": null, "validToUtc": null }
// PUT — UpdateDiscountRequest
{ "percent": 10, "validFromUtc": "2026-01-01T00:00:00Z", "validToUtc": "2026-12-31T00:00:00Z" }
```

---

## Спецификации — `/api/specifications`

| Метод | Путь | Доступ | Описание |
|---|---|---|---|
| GET | `/api/specifications` | read | Список спецификаций |
| GET | `/api/specifications/{id}` | read | Спецификация с позициями |
| POST | `/api/specifications` | write | Создать |
| POST | `/api/specifications/{id}/items` | write | Добавить позицию |
| DELETE | `/api/specifications/{id}/items/{itemId}` | write | Удалить позицию |
| DELETE | `/api/specifications/{id}` | write | Удалить спецификацию |
| POST | `/api/specifications/{id}/items/{itemId}/override` | write | Ручной выбор поставщика для позиции (ТЗ п.5.5) |
| DELETE | `/api/specifications/{id}/items/{itemId}/override` | write | Снять ручной выбор |
| POST | `/api/specifications/import` | write | Импорт спецификации из Excel (`multipart/form-data`) |

```jsonc
// POST /api/specifications — CreateSpecificationRequest
{ "title": "Оснащение ЦОД, 2 очередь", "customer": "ООО Ромашка" }
// POST …/items — AddSpecificationItemRequest (productId необязателен — можно по тексту)
{ "productId": null, "sku": "R740", "name": "Сервер стоечный", "quantity": 4 }
// POST …/items/{itemId}/override — SetVendorOverrideRequest
{ "vendorId": "…" }
```

При импорте позиции сопоставляются с каталогом: точный артикул, затем ML (TF-IDF + логрегрессия,
аналоги категории). Битый файл не роняет запрос — спецификация создаётся с ошибкой в сводке.

### ML-сопоставление — `/api/matching` и `/api/specifications/{specId}/match`

Живой путь Docker-SPA — сервис `ProcurementSystem.Services.Matching` (`:5174`).
Контроллер монолита с теми же путями сохранён (прямой заход на `:5165`).
Копия матчера в Quoting остаётся для генератора КП.

| Метод | Путь | Доступ | Описание |
|---|---|---|---|
| GET | `/api/matching/{id}` | read | Превью гипотез по каждой позиции: Kind (`ExactSku`/`FuzzyName`/`Analog`), `probability`, косинус, причина |
| GET | `/api/specifications/{id}/match` | read | Тот же контракт (фронт `frontend/src/pages/MatchResults.tsx`) |
| POST | `/api/matching/{id}/apply?minP=0.5` | write | Проставить `ProductId` по лучшей гипотезе с P ≥ порога |
| POST | `/api/specifications/{id}/match/apply?minP=0.5` | write | Алиас |
| POST | `/api/matching/{id}/apply-one` | write | Ручной выбор: `{ "itemId", "productId" }` |
| POST | `/api/specifications/{id}/match/apply-one` | write | Алиас |
| POST | `/api/matching/suggest` | read | Скоринг одной строки `{ "sku", "name", "take" }` без спецификации |
| GET | `/api/matching/suggest?sku=&name=&take=5` | read | То же, для curl |

Алгоритм — в [ML_MATCHING.md](ML_MATCHING.md). Каталог: витрина `GET /api/catalog`
(pageSize ≤ 48) как запасной путь; на большом стенде — read-only `SELECT` из схемы
`catalog` (`ConnectionStrings:Postgres`). Спецификации — `GET /api/specifications/{id}` у Quoting.
Запись `ProductId` — `POST .../match/apply-one` на процесс Quoting (не через шлюз).

---

## Коммерческое предложение — `/api/specifications/{specId}/quote`

Генерация «на лету», ничего не сохраняет (кроме overrides). Общие query-параметры:
`strategy` (`MinCost`/`MinLeadTime`/`Balanced`/`MlRelevance`, по умолчанию `Balanced`), `wPrice=0.5`,
`wLead=0.5` (веса для баланса), `onlyInStock=false`.

| Метод | Путь | Доступ | Описание |
|---|---|---|---|
| GET | `.../quote` | read | КП по одной стратегии |
| GET | `.../quote/compare` | read | Все четыре стратегии сразу (цена / срок / баланс / ML-актуальность) |
| GET | `.../quote/export` | read | Выгрузка КП в Excel (`.xlsx`) |
| GET | `.../quote/export/pdf` | read | Выгрузка КП в PDF |

Ответ содержит строки (выбранный поставщик, цена без/со скидкой, без/с НДС, срок, «причина выбора»)
и итоги (стоимость, макс. срок, число поставщиков, несопоставленные позиции).

---

## Согласование КП — `/api/approvals`

Маршрут: `НаСогласованииРП → ВКоммерческомБлоке → Согласовано / Отклонено` (ТЗ п.B6).

| Метод | Путь | Доступ | Описание |
|---|---|---|---|
| GET | `/api/approvals` | read | Список согласований |
| GET | `/api/approvals/{id}` | read | Карточка |
| POST | `/api/approvals` | write | Создать (себестоимость берётся из генератора КП) |
| POST | `/api/approvals/{id}/margin` | write | Задать наценку → передать в КБ |
| POST | `/api/approvals/{id}/decision` | approve | Решение КБ: согласовать/отклонить. При `approved: true` автоматически создаётся счёт NOC (автор «Система (авто)»). Согласование и счёт — два сохранения |
| POST | `/api/approvals/{id}/resubmit` | write | Вернуть отклонённое на доработку (ТЗ п.5.6) |

```jsonc
// POST /api/approvals — CreateApprovalRequest
{ "specificationId": "…", "strategy": "Balanced", "markupPercent": 25 }
// POST …/margin — SetMarginRequest
{ "markupPercent": 30 }
// POST …/decision — DecisionRequest
{ "approved": true, "comment": "Согласовано с условием предоплаты 50%" }
```

---

## Счета NOC — `/api/invoices`

Создаётся только из `Approval` в статусе «Согласовано» (обычно автоматически при решении КБ).
8-стадийный ЖЦ, переход только на следующую стадию (ТЗ п.B7).

| Метод | Путь | Доступ | Описание |
|---|---|---|---|
| GET | `/api/invoices` | read | Список счетов |
| GET | `/api/invoices/{id}` | read | Счёт с позициями |
| POST | `/api/invoices` | write | Создать из согласованного КП (фиксирует позиции). Повтор по тому же `approvalId` идемпотентен: возвращает существующий счёт, второго не создаёт |
| POST | `/api/invoices/{id}/status` | setInvoiceStatus | Продвинуть статус. «Согласован» — только КБ; последующие стадии и «Отменён» — бухгалтерия (`Invoice.ActorMaySet`). Отказ по роли — `403`; недопустимый переход — `409`. Повтор текущей стадии идемпотентен |
| POST | `/api/invoices/{id}/attachments` | postPayment | Загрузить документ (`multipart/form-data`) |
| GET | `/api/invoices/{id}/attachments` | read | Список документов счёта |
| GET | `/api/invoices/attachments/{attachmentId}/download` | read | Скачать документ |

```jsonc
// POST /api/invoices — CreateInvoiceRequest
{ "approvalId": "…", "contract": "Д-2026/14", "dueDateUtc": "2026-08-01T00:00:00Z" }
// POST …/status — SetInvoiceStatusRequest
{ "status": "ОжиданиеОплаты" }
```

---

## Заказы — `/api/orders`

Заказ — снимок выбранного варианта КП; при оформлении резервируется остаток (ТЗ UC-04).

| Метод | Путь | Доступ | Описание |
|---|---|---|---|
| GET | `/api/orders` | read | Список заказов (в т.ч. `specificationId`) |
| GET | `/api/orders/{id}` | read | Заказ с позициями |
| POST | `/api/orders/from-quote` | write | Оформить из КП. Query: `specId`, `strategy`, `wPrice`, `wLead`, `onlyInStock` |
| POST | `/api/orders/{id}/status` | write | Сменить статус (`409` при недопустимом переходе) |

```jsonc
// POST …/status — ChangeOrderStatusRequest
{ "status": "Placed" }
```

Переходы: `Draft→Placed→Confirmed→Shipped→Completed`; `Cancelled` — из любого нефинального
(возвращает зарезервированный остаток).

---

## Приёмка на склад — `/api/receipts`

Актор — Склад (UC-09). Проведение выгружает документ в WMS и 1С и автоматически продвигает
связанный счёт NOC.

| Метод | Путь | Доступ | Описание |
|---|---|---|---|
| GET | `/api/receipts` | read | Список приёмок |
| GET | `/api/receipts/{id}` | read | Приёмка с позициями (заказано/принято/расхождение) |
| POST | `/api/receipts/from-order?orderId=` | receive | Создать приёмку из заказа |
| POST | `/api/receipts/{id}/lines/{lineId}` | receive | Указать принятое количество по строке |
| POST | `/api/receipts/{id}/complete` | receive | Провести (выгрузка в WMS/1С) |

```jsonc
// POST …/lines/{lineId} — SetReceivedRequest
{ "receivedQty": 4 }
```

---

## Проекты — `/api/projects`

Карточка проекта (`/projects/{id}` на фронте). Статус/сумма/маржа подтягиваются из связанных Approval/Invoice.

| Метод | Путь | Доступ | Описание |
|---|---|---|---|
| GET | `/api/projects` | read | Список карточек |
| GET | `/api/projects/{id}` | read | Карточка |
| POST | `/api/projects` | write | Создать |
| POST | `/api/projects/{id}/link` | write | Привязать/отвязать спецификацию |
| DELETE | `/api/projects/{id}` | write | Удалить |

```jsonc
// POST — CreateProjectRequest
{ "name": "ЦОД, 2 очередь", "rp": "Иванов И.И.", "dueDateUtc": null, "specificationId": null }
// POST …/link — LinkSpecificationRequest
{ "specificationId": "…" }   // null — отвязать
```

---

## Аналитика — `/api/analytics`

Сводка по закупочному процессу (ТЗ UC-10, упрощённо: без периода и выгрузки отчёта).

| Метод | Путь | Доступ | Описание |
|---|---|---|---|
| GET | `/api/analytics/summary` | read | KPI: проекты, спецификации, сопоставление, согласования, счета, заказы, приёмки, суммы/маржа, топ поставщиков, помесячные заказы, последние события аудита |

---

## Уведомления — `/api/notifications`

In-app центр (колокольчик в шапке). Живой путь — `ProcurementSystem.Services.Notifications`
(шлюз `/api/notifications`). Список фильтруется по текущему пользователю и его ролям.
При чтении сервис подтягивает открытые согласования/счета/заказы/приёмки HTTP-опросом
соседей. Те же события рассылает SMTP-диспетчер сервиса (`Email:Recipients:{role}`),
если `Email:Enabled=true`. Контроллер в монолите сохранён (прямой заход на `:5165`).

| Метод | Путь | Доступ | Описание |
|---|---|---|---|
| GET | `/api/notifications` | read | Список (до 50). Query: `unreadOnly` |
| GET | `/api/notifications/count` | read | `{ "unread": N }` |
| POST | `/api/notifications/{id}/read` | read | Пометить прочитанным (`204` / `404`) |
| POST | `/api/notifications/read-all` | read | Прочитать все видимые |

---

## Пользователи — `/api/users`

Сервис Auth. Список из Keycloak Admin API (не JWT и не журнал аудита монолита).
CRUD учёток в SPA нет — создание через `POST /api/auth/register` или админку Keycloak.

| Метод | Путь | Доступ | Описание |
|---|---|---|---|
| GET | `/api/users` | **admin** | Список: userName, email, displayName, roles, enabled; `lastActivityUtc` всегда `null`. `503` если Keycloak недоступен |

---

## Служебные

### Интеграции — `/api/integration`

| Метод | Путь | Доступ | Описание |
|---|---|---|---|
| GET | `/api/integration/status` | read | Статус шлюзов 1С/WMS (сейчас мок-адаптеры) |

### Аудит — `/api/audit`

| Метод | Путь | Доступ | Описание |
|---|---|---|---|
| GET | `/api/audit` | **admin** | Журнал мутаций. Query: `limit` (1…500, по умолчанию 100). Поля: `userName`, `action`, `path`, `statusCode`, `occurredAtUtc`, `changes` — массив `{ entity, entityId, property, oldValue, newValue }` или `null`, если перехватчик ничего не зафиксировал |

### Администрирование — `/api/admin`

| Метод | Путь | Доступ | Описание |
|---|---|---|---|
| POST | `/api/admin/seed` | **admin** | Наполнить каталог демо-данными |
| POST | `/api/admin/reset` | **admin** | Очистить всё и наполнить заново (чистит и поисковый индекс) |
| POST | `/api/admin/reindex` | **admin** | Полная переиндексация поиска |

### Проверка живости

| Метод | Путь | Доступ | Описание |
|---|---|---|---|
| GET | `/health` | **анонимно** | `{ "status": "ok" }` |
