# Handoff: Кварц — система подбора оборудования и формирования КП

> **Для разработчика:** файлы в этом пакете — **дизайн-прототипы в HTML**, не production-код.
> Состав экранов (B1–B21) и сценарии отсюда. **Живой вид стенда** — в [../UI.md](../UI.md):
> плакат снабжения (сажа / бумага / штамп), не крем и терракота этого макета.

## Fidelity: **High-Fidelity**

Прототипы пиксельно точны: финальные цвета, типографика, отступы, интерактивность (inline-редактирование, dropdown-альтернативы поставщика, lifecycle-бар, fuzzy match cards). Воссоздавать **точь-в-точь** по этим макетам.

---

## Обзор продукта

**Кварц** — корпоративная система автоматизации закупочного процесса:
- Загрузка прайс-листов поставщиков (Excel)
- Загрузка проектного перечня оборудования и нечёткое сопоставление с базой
- Автоматическое формирование 3 вариантов КП (цена / срок / баланс)
- Согласование КП по маршруту (РП → КБ → NOC → БУХ → СК → 1С)
- Справочники: поставщики, производители, номенклатура
- Аналитика, журнал событий, управление пользователями

**Тех. стек:** React 18 (TS) + Vite · .NET 8 ASP.NET Core · PostgreSQL 16 · Elasticsearch 8 · RabbitMQ + MassTransit · Keycloak · Docker Compose

---

## Дизайн-токены

```css
/* Акцент (orange) */
--accent:       #c2410c   /* основной CTA, выбранные элементы */
--accent-2:     #ea580c
--accent-soft:  rgba(194, 65, 12, 0.10)   /* подсветка активных строк */
--accent-border: #fdd9b4

/* Нейтралы (warm stone) */
--bg:           #faf9f7
--surface:      #ffffff
--surface-2:    #f3f1ed
--border:       #ebe8e4
--border-strong:#d8d4cd
--text:         #1f1d1b
--text-2:       #6b6660   /* вторичный текст */
--text-3:       #a8a29e   /* placeholder, метки */

/* Семантика статусов */
--ok:           #15803d   /* зелёный */
--ok-bg:        #f0fdf4
--warn:         #b45309   /* янтарный */
--warn-bg:      #fef3c7
--err:          #b91c1c   /* красный */
--err-bg:       #fef2f2
--info:         #1d4ed8   /* синий */
--info-bg:      #eff6ff
```

### Типографика

| Роль | Шрифт | Размер | Вес |
|---|---|---|---|
| Основной | IBM Plex Sans | 13–13.5px | 400/500/600 |
| Заголовки страниц | IBM Plex Sans | 26px | 600 |
| Mono (артикулы, коды) | JetBrains Mono | 10.5–12px | 400/500 |
| Числа | font-variant-numeric: tabular-nums | — | — |

### Отступы и сетка

```
Page padding:    24px 32px
Card padding:    16–18px
Table cell:      10–13px 12px
Gap (grid):      12–14px
Border radius:   9–12px (карточки), 999px (пилюли статусов), 6–7px (инпуты)
Shadow (cards):  0 1px 2px rgba(0,0,0,0.04)
```

---

## Роли пользователей (RBAC)

| ID | Роль | Короткое | Доступ |
|---|---|---|---|
| `rp` | Руководитель проекта | РП | Прайсы, проекты, КП, скидки |
| `comm` | Коммерческий блок | КБ | Маржа, согласование |
| `acc` | Бухгалтерия | БУХ | Оплата, документы |
| `wh` | Склад | СК | Приёмка, 1С |
| `admin` | Администратор | АДМ | Пользователи, настройки, журнал |

---

## Экраны — полное описание

### B1 · Дашборд проектов (роль: РП)

**Назначение:** точка входа РП, обзор всех проектов.

**Layout:** `V2Shell` (сайдбар 232px + главная область). Сверху — 4 KPI-карточки в сетке `1fr 1fr 1fr 1fr`. Ниже — вкладки (Все / Мои / На согласовании / Архив), под ними таблица.

**Таблица проектов:** колонки — Проект (ID + название), РП, Статус (StatusPill), Маржа (зелёный ≥20%), Срок, Сумма. Строки кликабельны → переход на B20.

**KPI-карточки:**
- Сумма КП в работе / Средняя маржа / КП ждут согласования / Просрочка
- Каждая: `border-radius: 10px`, padding `14px 16px`, белый фон, label `11px uppercase`, число `22–28px font-weight:600`

---

### B2 · Загрузка прайса (роль: РП)

**Назначение:** UC-01, загрузка Excel-файла поставщика.

**Layout:** 2-колонная сетка `380px 1fr`. Левая: dropzone (accent-soft bg, dashed border), поля метаданных, предупреждение о дублях. Правая: маппинг колонок (grid 3 колонки) + таблица предпросмотра.

**Dropzone состояние «файл получен»:** bg `var(--accent-soft)`, border `1.5px dashed var(--accent)`, иконка Excel, filename в mono, прогресс.

**Маппинг:** каждая строка — `[label источника] → [badge]`. Нераспознанные колонки: фон `#fef3e8`, цвет `#9a3412`, border `1px dashed`.

**Валидация:** badge «✓ Структура валидна» зелёный (`#dcfce7 / #166534`).

---

### B3 · Сравнение 3 вариантов КП ★ (роль: РП)

**Назначение:** UC-04, КЛЮЧЕВОЙ экран. РП выбирает один из трёх вариантов.

**Layout:** метаданные проекта (row из свойств) → 3 карточки варианта (grid `1fr 1fr 1fr`) → таблица diff по позициям.

**3 карточки вариантов:**
- Активная (выбранная): `border: 1.5px solid var(--accent)`, `box-shadow: 0 0 0 4px var(--accent-soft)`, бейдж «выбран» `bg: var(--accent), color: white`
- Неактивная: `border: 1px solid var(--border)`
- Внутри каждой: тег (СЕБЕСТОИМОСТЬ / СКОРОСТЬ / БАЛАНС), большое число итога, 2×2 метрики, плюсы/минусы

**Diff-таблица:** 3 пары колонок (Цена + Срок) для каждого варианта. Ячейки выбранного варианта подсвечены `var(--accent-soft)`. Если поставщик отличается — подсветка.

---

### B4 · Детальный КП с inline-заменой поставщика (роль: РП)

**Назначение:** просмотр позиций, ручная замена поставщика по строке.

**Layout:** summary strip (4 метрики) → split `1fr 300px`. Слева — таблица позиций. Справа — lifecycle (5 этапов), скидки по вендорам, комментарии.

**Inline dropdown строки i=1:**
- Строка подсвечена `var(--accent-soft)`
- Под ней expandable блок с альтернативами
- Каждая альтернатива: radio circle + название + score SLA + цена + скидка + срок
- Активная: `border: 1.5px solid var(--accent)`, `background: var(--surface)`

**Lifecycle sidebar:** вертикальная цепочка; выполненные — оранжевый кружок с ✓, активный — оранжевый, будущие — серые.

**Комментарии:** аватар (инициалы в кружке с цветным фоном) + имя + время + текст. Поле ввода внизу.

---

### B5 · Скидки по поставщикам (роль: РП)

**Назначение:** UC-03, ручной ввод % скидок.

**Layout:** вкладки (По поставщикам / По производителям / Спец. условия) → таблица.

**Поле скидки:** `border: 1.5px solid var(--accent)` у редактируемой строки, mono-шрифт, `%` суффикс.

**Предупреждение о пересчёте:** `accent-soft` callout внизу.

---

### B14 · Новый проект (роль: РП)

**Назначение:** создание карточки проекта перед загрузкой перечня.

**Layout:** одна колонка 920px max-width. Секции с label `11px uppercase`, инпуты.

**Секции:** Основное / Заказчик / Тендер и сроки / Команда / Тэги.

**Инпут:** `border: 1px solid var(--border-strong)`, `border-radius: 7px`, padding `8px 12px`, fontSize 13.5px. Select/combo показывают `<chevron>` справа.

**Цветные тэги:** pill-форма, каждый уникального цвета `(background/foreground)` — СКС `#fed7aa/#9a3412`, Q2 `#bbf7d0/#14532d`.

---

### B15 · Загрузка перечня (роль: РП)

**Назначение:** UC-02 шаг 2, загрузка Excel-перечня оборудования с прогрессом обработки.

**Layout:** `420px 1fr`. Слева: файл + прогресс-бар + timeline этапов. Справа: 4 KPI + таблица превью.

**Timeline этапов (6):** ромб-иконка (квадрат 17×17 `rotate: 45deg`). Выполненные — зелёные `#15803d`, активный — `var(--accent)`, будущие — `var(--surface-2)` с серой рамкой.

**KPI-бейджи:** Точное `#15803d`, Нечёткое `#b45309`, Не найдено `#b91c1c`.

**Превью-таблица:** колонка «Сопоставление» с StatusPill: «Точное» (green), «Нечёткое 87%» (amber), «Не найдено» (red).

---

### B16 · Сопоставление позиций + fuzzy ★ (роль: РП)

**Назначение:** UC-02 шаг 3, ключевой экран подбора — просмотр нечётких совпадений и действия по несопоставленным.

**Filter pills:** горизонтальный ряд; активный — `accent-soft bg + accent border`; фильтр порога fuzzy `≥ 75%`.

**Match card (fuzzy):** верхняя секция `bg: var(--surface)` — позиция из перечня. Нижняя секция `bg: var(--accent-soft)` — список альтернатив в grid `60px 1fr 130px 100px 100px 26px`:
- SCORE: `fontSize: 16, fontWeight: 700`; ≥80% зелёный, 70–79% янтарный, <70% красный
- Активная строка: `border: 1.5px solid var(--accent)`
- Иконка `check` (accent) / `plus` (gray) справа

**Match card (не найдено):** нижняя секция `bg: #fef2f2`, 3 карточки действий (grid 1fr 1fr 1fr): «Добавить в номенклатуру» (accent border), «Запросить аналог», «Пропустить».

---

### B6 · Согласование КБ (роль: КБ)

**Назначение:** UC-06, расчёт маржинальности и согласование КП.

**Калькулятор маржи:** 3 блока в grid `1fr 28px 1fr 28px 1fr`:
- Себестоимость: `bg: var(--surface-2)`, число `22px`
- Наценка: `bg: var(--accent-soft), border: 1.5px solid var(--accent)`, число `accent`
- Цена клиенту: `bg: var(--surface-2)`
- Разделители `→`

**Слайдер:** 8px высота, `bg: var(--surface-2)`, заполнение `linear-gradient(90deg, accent-soft, accent)`. Thumb: белый кружок `16×16px, border: 2px solid accent`. Метка НМЦ: вертикальная линия `#dc2626` на 80%.

**Маршрут согласования:** вертикальная цепочка; линия `1.5px solid var(--border)`; кружок `22×22px`:
- done: `bg #15803d`
- active: `bg var(--accent)` + `box-shadow: 0 0 0 4px accent-soft`
- wait: white bg + dashed border

---

### B7 · Счёт NOC (роль: БУХ)

**Назначение:** UC-08, просмотр счёта и управление статусами жизненного цикла.

**Lifecycle bar:** горизонтальный, 8 этапов в `grid repeat(8, 1fr)`. Активный этап: `bg: accent-soft, border-top: 2px solid accent, color: accent`. Завершённые: нормальный фон, `color: text-2`.

**Таблица позиций:** колонки — №, Позиция, Кол-во, Цена с НДС, Сумма, Поставка (StatusPill), Склад (StatusPill).

**Контрагент sidebar:** key-value grid `auto 1fr`. Документы: список с цветной точкой (зелёная = приложен).

---

### B11 · Склад: приёмка (роль: СК)

**Назначение:** UC-09, фиксация прихода товара.

**Таблица приёмки:** колонки — Позиция, По счёту, Прибыло (редактируемое поле), Расхождение, Состояние, Статус. Строки с расхождением: `bg: #fef3e8`.

**Callout расхождения:** `border-left: 3px solid var(--accent)`, текст с описанием автоматического акта.

**Партии sidebar:** список карточек с `border: 1px solid`. Активная партия: `accent border + accent-soft bg`.

---

### B12 · Бухгалтерия: очередь к оплате (роль: БУХ)

**Назначение:** UC-08, очередь счетов.

**Срочность:** pill `daysLeft ≤ 7` → `bg: #fef3e8, color: #9a3412`. Остальные — `var(--surface-2)`.

**Действие:** кнопка «Отметить оплату» `kind=primary` только для статуса «Согласован».

**Без договора:** иконка `x` красная, color `#b91c1c`.

---

### B13 · Администратор: пользователи (роль: АДМ)

**Назначение:** управление учётными записями и ролями.

**Роль-бейджи:** `border-radius: 4px`, каждая роль — уникальный цвет:
- РП: `#fed7aa / #9a3412`
- КБ: `#bae6fd / #075985`
- БУХ: `#bbf7d0 / #14532d`
- СК: `#e9d5ff / #6b21a8`
- АДМ: `#fecaca / #991b1b`

**2FA статус:** icon check `color: #15803d` / x `color: #b45309`.

**Безопасность sidebar:** список с семантическими цветами `ok: #15803d / warn: #b45309`.

---

### B16–B19 · Справочники

Все справочники используют единый паттерн:
- Поиск вверху: `bg: var(--surface-2), border-radius: 7px, icon search`
- Chips фильтров: `border-radius: 999px`
- Таблица со строками `borderBottom: 1px solid var(--border)`

**B18 (Номенклатура):** дерево категорий слева `260px`, список справа. Активная категория: `bg: var(--accent-soft), color: var(--accent)`. Indent: `14 + depth * 16 px`.

**B19 (Профиль поставщика):** аватар 56×56 `border-radius: 10px`, мини-гистограмма активности (12 столбиков).

---

### B20 · Карточка проекта (роль: РП)

**Lifecycle:** горизонтальный бар на 8 этапов в `surface, border-radius: 10px`. Внутри flex с разделителями `1px solid border`. Активный этап: `bg: accent-soft`.

**Версии КП:** список версий v3/v2/v1. Текущая: `border-left: 3px solid var(--accent), bg: accent-soft`. Откатанные: нормальный фон.

**Финансы sidebar:** мини-таблица с `Себестоимость / Наценка / Цена / С НДС / vs НМЦ`.

---

### B21 · Настройки и интеграции (роль: АДМ)

**Layout:** side-nav 220px + контент. Side-nav item active: `bg: accent-soft, color: accent`.

**Интеграции:** grid `1fr 1fr`, карточки с аватаром, статусом и аптайм-метрикой. Не настроена: `border-color: #fecaca`.

**KPI таблица:** колонки «цель» + «факт». Факт соответствует: `#15803d`. Требует уточнения: `#b45309`.

---

### B22 · Архитектура · слои системы

7 пронумерованных слоёв с разделителями-коннекторами (стрелки с подписями). Список событий RabbitMQ в mono. Docker Compose footer.

---

### C4 L1–L4 · Диаграммы архитектуры

C4-палитра (цвета боксов):
```
Person:    bg #0b3a6c  fg #ffffff
System:    bg #1168bd  fg #ffffff
Container: bg #438dd5  fg #ffffff
Component: bg #85bbf0  fg #0b3a6c
External:  bg #6b6660  fg #ffffff
Database:  bg #438dd5  fg #ffffff  border-radius: 12px 12px 18px 18px / 20px 20px 12px 12px
Queue:     bg #9a7ad4  fg #ffffff
```

Boundary: `border: 2px dashed #438dd5, border-radius: 12px`. Label: абсолютный, `top: -10px`, `bg: var(--bg)`.

Стрелки: `1.5px solid #6b6660` + треугольный наконечник `border` трюк.

---

## Компоненты общего использования

### StatusPill
```tsx
// kind: 'green' | 'amber' | 'blue' | 'gray' | 'red'
<StatusPill status="Согласование РП" kind="amber"/>
// Рендер: inline-flex, dot 6×6px, padding: 3px 8px 3px 7px, border-radius: 999px
```
Маппинг статусов → kind (файл `app/ui.jsx`, fn `StatusPill`).

### Sidebar (V2Shell)
- Ширина: 232px
- Фон: `var(--sidebar-bg)` = `#faf9f7`
- Логотип: 28×28 `border-radius: 6px bg: var(--accent)`
- Nav item active: `bg: var(--sidebar-active-bg)` = `#efece6`
- Правая граница: `1px solid var(--border)`

### Кнопки (Btn)
```
kind=primary:  bg: var(--accent), color: white, border: 1px solid accent
kind=default:  bg: surface, color: text, border: 1px solid border-strong
kind=ghost:    bg: transparent, color: text-2, border: none
kind=danger:   bg: surface, color: var(--err), border: 1px solid border
size=sm:       h: 26px, padding: 4px 10px, fontSize: 12px
size=md:       h: 34px, padding: 7px 14px, fontSize: 13px
border-radius: 7px
```

---

## Навигация и переходы

| Откуда | Куда | Триггер |
|---|---|---|
| B1 Дашборд | B20 Карточка проекта | клик по строке |
| B1 Дашборд | B14 Новый проект | кнопка «Новый» |
| B14 Новый проект | B15 Загрузка перечня | кнопка «Создать и загрузить перечень» |
| B15 Загрузка | B16 Сопоставление | прогресс 100% → авто или кнопка |
| B16 Сопоставление | B3 Сравнение КП | кнопка «Перейти к КП» |
| B3 Сравнение | B4 Детальный КП | кнопка «Открыть детали» / «Выбрать вариант» |
| B4 Детальный КП | B6 Согласование КБ | кнопка «Передать в КБ» |
| B6 Согласование | B7 Счёт NOC | после нажатия «Согласовать» |
| B7 NOC | B11 Приёмка склада | уведомление об отгрузке |
| B8 Поставщики | B19 Профиль поставщика | клик по строке / карточке |

---

## Состояния и State Management

### Жизненный цикл КП (Lifecycle stages)
```ts
type LifecycleStage =
  | 'draft'    // Черновик КП
  | 'rp'       // Согласование РП
  | 'comm'     // Коммерческий блок
  | 'invoice'  // Счёт в NOC
  | 'pay'      // Ожидание оплаты
  | 'ship'     // Ожидание поставки
  | 'wh'       // Пришёл на склад
  | '1c'       // Отражено в 1С
```

### KP Variants
```ts
type QuoteVariant = 'cheap' | 'fast' | 'balanced'
// cheap:    min(price) per position
// fast:     min(leadDays) per position
// balanced: 0.6 * price + 0.4 * leadDays * weight (конфигурируется)
```

### Match result
```ts
type MatchStatus = 'exact' | 'fuzzy' | 'none'
// exact:  точное по артикулу
// fuzzy:  score >= threshold (default 75%)
// none:   не найдено
```

---

## Файлы дизайна

| Файл | Содержимое |
|---|---|
| `Prototype.html` | Design canvas с 25+ артбордами |
| `Prototype-print.html` | Печатная версия (33 страницы A4 landscape) |
| `tokens.css` | CSS-переменные (цвета, светлая/тёмная темы) |
| `app/data.jsx` | Реалистичные данные (поставщики, позиции, проекты, KP-расчёт) |
| `app/ui.jsx` | Атомы: Icon, StatusPill, LifecycleBar, Btn, NavItem |
| `app/v2.jsx` | V2Shell, V2Dashboard, V2KPCompare, V2NOC |
| `app/v2-more.jsx` | V2Upload, V2KPDetail, V2Discounts, V2Approval |
| `app/v2-data.jsx` | V2Suppliers, V2Analytics, V2Log |
| `app/v2-roles.jsx` | V2WarehouseReceive, V2AccountingQueue, V2AdminUsers |
| `app/v2-uc02.jsx` | V2NewProject, V2UploadProjectList, V2MatchResults |
| `app/v2-cat.jsx` | V2Vendors, V2Catalog, V2SupplierProfile |
| `app/v2-detail.jsx` | V2ProjectDetail, V2Settings |
| `app/v2-arch.jsx` | V2Arch (7-слойная схема) |
| `app/v2-c4.jsx` | V2C4Context, V2C4Containers, V2C4Components, V2C4Deployment |

---

## Тех. требования к реализации

- **Lingua franca UI:** React 18 + TypeScript + Vite
- **Стейт:** TanStack Query для серверного состояния, Zustand/Jotai для UI-стейта
- **Таблицы:** TanStack Table v8 (виртуализация для прайсов 7M+)
- **Формы:** React Hook Form + Zod
- **Иконки:** Lucide React (stroke-only, strokeWidth 1.6)
- **Excel upload:** `react-dropzone` + `xlsx` (SheetJS)
- **Fuzzy search:** клиентская — `fuse.js`; серверная — Elasticsearch fuzzy query
- **Inline editing:** кастомный `<SelectSupplier>` dropdown с portal
- **Routing:** TanStack Router или React Router v6
- **i18n:** русский язык, `Intl.NumberFormat('ru-RU')` для чисел и валюты

---

## Открытые вопросы (из ТЗ)

1. Алгоритм балансированного варианта КП — веса 60/40 или конфигурируются пользователем?
2. Состав и порядок круга согласователей в NOC
3. Способ интеграции с NOC: REST API / прямая запись в БД?
4. SSO через AD/LDAP: SAML или OIDC federation в Keycloak?
5. Способ доставки уведомлений (Email / мессенджер / оба)?
6. Сроки действия скидок: бессрочно или с датой окончания?
