// Мок-данные из хендоффа «Кварц» — для экранов, под которые в бэкенде пока нет API
// (дашборд проектов, маршрут согласования, счета NOC, склад и т.п.).

export interface Supplier { id: string; name: string; inn: string; discount: number; slaDays: number; status: string; rating: number; deals: number }
export const SUPPLIERS: Supplier[] = [
  { id: 'sup-01', name: 'ТД Сетевые решения',  inn: '7707654321', discount: 12, slaDays: 14, status: 'Активен',  rating: 4.6, deals: 124 },
  { id: 'sup-02', name: 'НетПром Дистрибуция', inn: '7728100200', discount: 9,  slaDays: 21, status: 'Активен',  rating: 4.4, deals: 87 },
  { id: 'sup-03', name: 'ИТ-Альянс',           inn: '7806221100', discount: 15, slaDays: 28, status: 'Активен',  rating: 4.2, deals: 56 },
  { id: 'sup-04', name: 'СвязьКомплект',       inn: '5018091122', discount: 7,  slaDays: 10, status: 'Активен',  rating: 4.7, deals: 201 },
  { id: 'sup-05', name: 'РТК-Снабжение',       inn: '7704321987', discount: 5,  slaDays: 35, status: 'На паузе', rating: 3.9, deals: 42 },
]

export interface Project { id: string; name: string; rp: string; items: number; status: string; margin: number | null; value: number; due: string }
export const PROJECTS: Project[] = [
  { id: 'P-2026-118', name: 'СКС и сетевое ядро ТЦ «Орбита»',     rp: 'Смирнов А.',   items: 84,  status: 'Согласование РП',     margin: 18.4, value: 14_820_000, due: '2026-06-12' },
  { id: 'P-2026-114', name: 'ЦОД заказчика, расширение фазы 2',   rp: 'Васильева Е.', items: 142, status: 'В коммерческом блоке', margin: 22.1, value: 38_410_000, due: '2026-07-30' },
  { id: 'P-2026-109', name: 'Видеонаблюдение склада №3',          rp: 'Карпов Д.',    items: 41,  status: 'Ожидание оплаты',      margin: 19.8, value: 4_120_000,  due: '2026-05-28' },
  { id: 'P-2026-102', name: 'Wi-Fi инфраструктура офиса',         rp: 'Смирнов А.',   items: 28,  status: 'Пришёл на склад',      margin: 21.3, value: 2_870_000,  due: '2026-05-19' },
  { id: 'P-2026-097', name: 'Замена коммутаторов филиала Казань', rp: 'Лебедев И.',   items: 36,  status: 'Отражено в 1С',        margin: 24.0, value: 6_540_000,  due: '2026-04-15' },
  { id: 'P-2026-088', name: 'Серверная стойка для НИИ',           rp: 'Васильева Е.', items: 19,  status: 'Черновик КП',          margin: null, value: 8_910_000,  due: '2026-08-04' },
]

// Позиции счёта/проекта (для мок-экранов согласования, NOC, склада).
export interface MockLine {
  name: string; vendor: string; supplier: string; qty: number; unitVat: number
  delivery: 'Доставлено' | 'В пути' | 'Ожидается'; warehouse: 'Принято' | 'Не принято'
}
export const MOCK_LINES: MockLine[] = [
  { name: 'Коммутатор Eltex MES2348B, 48×1G + 4×10G SFP+, L3', vendor: 'Eltex',     supplier: 'ТД Сетевые решения', qty: 8,  unitVat: 209_000, delivery: 'Доставлено', warehouse: 'Принято' },
  { name: 'Сервер Supermicro 1U, 2×Xeon Silver 4314, 128GB',    vendor: 'Supermicro',supplier: 'ИТ-Альянс',          qty: 4,  unitVat: 694_000, delivery: 'В пути',     warehouse: 'Не принято' },
  { name: 'IP-камера Hikvision DS-2CD2143G2-IS, 4MP',           vendor: 'Hikvision', supplier: 'СвязьКомплект',      qty: 32, unitVat: 19_700,  delivery: 'Доставлено', warehouse: 'Принято' },
  { name: 'APC Smart-UPS 2200VA, 2U, IEC',                       vendor: 'APC',       supplier: 'НетПром Дистрибуция',qty: 6,  unitVat: 224_000, delivery: 'В пути',     warehouse: 'Не принято' },
  { name: 'Кабель Hyperline UTP cat.6, 305м, бухта',            vendor: 'Hyperline', supplier: 'СвязьКомплект',      qty: 24, unitVat: 25_300,  delivery: 'Ожидается',  warehouse: 'Не принято' },
]

export const MOCK_INVOICE = {
  number: 'NOC-2026-118-001',
  customer: 'ООО «ТЦ Орбита Девелопмент»',
  contract: '№ 118-СКС от 18.04.2026',
  due: '30.04.2026',
  cost: 9_870_000,      // себестоимость
  markup: 22,           // % наценки по умолчанию
  nmc: 14_820_000,      // НМЦ (начальная макс. цена)
}

export interface MockUser { name: string; email: string; role: string; twoFA: boolean; status: string; last: string }
export const MOCK_USERS: MockUser[] = [
  { name: 'Смирнов А.',   email: 'smirnov@firma.ru',   role: 'РП',  twoFA: true,  status: 'Активен',   last: 'сегодня, 14:32' },
  { name: 'Васильева Е.', email: 'vasileva@firma.ru',  role: 'КБ',  twoFA: true,  status: 'Активен',   last: 'сегодня, 16:48' },
  { name: 'Карпов Д.',    email: 'karpov@firma.ru',    role: 'РП',  twoFA: false, status: 'Активен',   last: 'вчера, 11:09' },
  { name: 'Алексеев П.',  email: 'alekseev@firma.ru',  role: 'БУХ', twoFA: true,  status: 'Активен',   last: 'сегодня, 09:14' },
  { name: 'Орлова М.',    email: 'orlova@firma.ru',    role: 'СК',  twoFA: false, status: 'Активен',   last: '21.04, 17:50' },
  { name: 'Денисов В.',   email: 'denisov@firma.ru',   role: 'АДМ', twoFA: true,  status: 'Активен',   last: 'сегодня, 08:02' },
  { name: 'Гончарова Л.', email: 'goncharova@firma.ru',role: 'КБ',  twoFA: false, status: 'Заблокир.', last: '02.04, 13:21' },
]

export const ROLE_BADGE: Record<string, { bg: string; fg: string }> = {
  'РП':  { bg: '#fed7aa', fg: '#9a3412' },
  'КБ':  { bg: '#bae6fd', fg: '#075985' },
  'БУХ': { bg: '#bbf7d0', fg: '#14532d' },
  'СК':  { bg: '#e9d5ff', fg: '#6b21a8' },
  'АДМ': { bg: '#fecaca', fg: '#991b1b' },
}

export interface Manufacturer { name: string; country: string; products: number; share: number }
export const MANUFACTURERS: Manufacturer[] = [
  { name: 'Eltex',      country: 'Россия',   products: 312, share: 23 },
  { name: 'QTECH',      country: 'Россия',   products: 188, share: 14 },
  { name: 'Hikvision',  country: 'Китай',    products: 421, share: 31 },
  { name: 'Supermicro', country: 'США',      products: 96,  share: 7 },
  { name: 'Hyperline',  country: 'Россия',   products: 274, share: 20 },
  { name: 'APC',        country: 'Ирландия', products: 64,  share: 5 },
]

export interface ApprovalStep { role: string; person: string; state: 'done' | 'active' | 'wait' }
export const APPROVAL_ROUTE: ApprovalStep[] = [
  { role: 'РП',        person: 'Смирнов А.',   state: 'done' },
  { role: 'КБ',        person: 'Васильева Е.', state: 'active' },
  { role: 'Директор',  person: 'Громов С.',    state: 'wait' },
  { role: 'Бухгалтерия', person: 'Алексеев П.', state: 'wait' },
]

export interface EventRow { ts: string; user: string; action: string; obj: string; tag: string }
export const EVENT_LOG: EventRow[] = [
  { ts: '24.04 14:32', user: 'Смирнов А.',   action: 'Загрузил прайс',             obj: 'ТД Сетевые решения · 1284 поз.',  tag: 'upload' },
  { ts: '24.04 14:45', user: 'Смирнов А.',   action: 'Создал проект',              obj: 'P-2026-118 «СКС ТЦ Орбита»',      tag: 'create' },
  { ts: '24.04 14:47', user: 'Система',      action: 'Сформировала 3 варианта КП', obj: 'P-2026-118 · 84 позиции',         tag: 'system' },
  { ts: '24.04 15:18', user: 'Смирнов А.',   action: 'Утвердил вариант КП',        obj: 'Балансированный · 14 820 000 ₽',  tag: 'approve' },
  { ts: '24.04 16:48', user: 'Васильева Е.', action: 'Рассчитала маржинальность',  obj: '18.4% · согласовано',             tag: 'approve' },
  { ts: '24.04 16:51', user: 'Система',      action: 'Создала счёт в NOC',         obj: 'NOC-2026-118-001',                tag: 'system' },
]
