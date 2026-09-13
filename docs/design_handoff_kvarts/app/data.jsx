// Realistic dataset: network/IT equipment for a corporate procurement system.
// All names are real industry vendors & SKUs (used as data, not as branding).

const SUPPLIERS = [
  { id: 'sup-01', name: 'ТД Сетевые решения',    inn: '7707654321', discount: 12, slaDays: 14, status: 'Активен',  rating: 4.6, deals: 124 },
  { id: 'sup-02', name: 'НетПром Дистрибуция',   inn: '7728100200', discount: 9,  slaDays: 21, status: 'Активен',  rating: 4.4, deals: 87 },
  { id: 'sup-03', name: 'ИТ-Альянс',             inn: '7806221100', discount: 15, slaDays: 28, status: 'Активен',  rating: 4.2, deals: 56 },
  { id: 'sup-04', name: 'СвязьКомплект',         inn: '5018091122', discount: 7,  slaDays: 10, status: 'Активен',  rating: 4.7, deals: 201 },
  { id: 'sup-05', name: 'РТК-Снабжение',         inn: '7704321987', discount: 5,  slaDays: 35, status: 'На паузе', rating: 3.9, deals: 42 },
];

const VENDORS = [
  { id: 'v-01', name: 'Eltex',     country: 'Россия' },
  { id: 'v-02', name: 'QTECH',     country: 'Россия' },
  { id: 'v-03', name: 'Hikvision', country: 'Китай' },
  { id: 'v-04', name: 'Supermicro',country: 'США' },
  { id: 'v-05', name: 'Hyperline', country: 'Россия' },
  { id: 'v-06', name: 'APC',       country: 'Ирландия' },
];

// Project line items + quotes from multiple suppliers.
// quote: [supplierId, priceWithoutVAT, leadDays]
const PROJECT_ITEMS = [
  {
    id: 'L01', artikul: 'MES2348B',  name: 'Коммутатор Eltex MES2348B, 48×1G + 4×10G SFP+, L3',
    vendor: 'v-01', unit: 'шт', qty: 8, nmc: 187_000,
    quotes: [
      ['sup-01', 142_300, 14],
      ['sup-02', 138_900, 21],
      ['sup-04', 145_200, 10],
    ],
  },
  {
    id: 'L02', artikul: 'QSW-3450-28T-AC', name: 'QTECH QSW-3450-28T-AC, 24×1G + 4×10G, управляемый',
    vendor: 'v-02', unit: 'шт', qty: 12, nmc: 96_500,
    quotes: [
      ['sup-02', 74_900, 18],
      ['sup-03', 71_200, 28],
      ['sup-04', 78_400, 9],
    ],
  },
  {
    id: 'L03', artikul: 'SYS-1029P-WTRT', name: 'Сервер Supermicro 1U, 2×Xeon Silver 4314, 128GB DDR4',
    vendor: 'v-04', unit: 'шт', qty: 4, nmc: 612_000,
    quotes: [
      ['sup-01', 498_400, 30],
      ['sup-03', 472_800, 42],
      ['sup-04', 511_900, 21],
    ],
  },
  {
    id: 'L04', artikul: 'DS-2CD2143G2-IS', name: 'IP-камера Hikvision DS-2CD2143G2-IS, 4MP, купольная',
    vendor: 'v-03', unit: 'шт', qty: 32, nmc: 18_400,
    quotes: [
      ['sup-01', 13_900, 7],
      ['sup-02', 14_200, 14],
      ['sup-04', 13_400, 5],
    ],
  },
  {
    id: 'L05', artikul: 'PDU-19-16A-IEC', name: 'Hyperline PDU 19" 16A, 8×C13, амперметр',
    vendor: 'v-05', unit: 'шт', qty: 16, nmc: 14_200,
    quotes: [
      ['sup-01', 10_800, 10],
      ['sup-04', 11_300, 7],
    ],
  },
  {
    id: 'L06', artikul: 'SMT2200RMI2U',   name: 'APC Smart-UPS 2200VA, 2U, IEC',
    vendor: 'v-06', unit: 'шт', qty: 6, nmc: 198_000,
    quotes: [
      ['sup-02', 156_700, 25],
      ['sup-03', 149_900, 35],
    ],
  },
  {
    id: 'L07', artikul: 'PC6-UTP-305',    name: 'Кабель Hyperline UTP cat.6, 305м, бухта',
    vendor: 'v-05', unit: 'бухта', qty: 24, nmc: 22_500,
    quotes: [
      ['sup-01', 17_800, 7],
      ['sup-02', 18_400, 12],
      ['sup-04', 17_200, 5],
    ],
  },
  {
    id: 'L08', artikul: 'WS5500-AP-RU',   name: 'Wi-Fi точка доступа Eltex WB-2P-LR2, indoor',
    vendor: 'v-01', unit: 'шт', qty: 20, nmc: 32_400,
    quotes: [
      ['sup-01', 24_700, 14],
      ['sup-02', 25_300, 21],
    ],
  },
];

// Pre-computed variants
function computeVariants(items, suppliers) {
  const supById = Object.fromEntries(suppliers.map(s => [s.id, s]));
  function pick(items, scoreFn) {
    return items.map(it => {
      let best = it.quotes[0];
      let bestScore = scoreFn(best);
      for (const q of it.quotes) {
        const s = scoreFn(q);
        if (s < bestScore) { best = q; bestScore = s; }
      }
      const [supId, price, lead] = best;
      const sup = supById[supId];
      const discounted = price * (1 - sup.discount / 100);
      return {
        ...it,
        supplierId: supId,
        supplierName: sup.name,
        unitPrice: price,
        unitPriceDisc: Math.round(discounted),
        discount: sup.discount,
        leadDays: lead,
        total: Math.round(discounted * it.qty),
      };
    });
  }
  const cheap = pick(items, q => q[1]);
  const fast  = pick(items, q => q[2]);
  const bal   = pick(items, q => q[1] * 0.6 + q[2] * 8000 * 0.4);
  const sum = (rows) => rows.reduce((a, r) => a + r.total, 0);
  return {
    cheap: { id: 'cheap', label: 'По минимальной цене', tag: 'Себестоимость', rows: cheap, total: sum(cheap), maxLead: Math.max(...cheap.map(r => r.leadDays)) },
    fast:  { id: 'fast',  label: 'По минимальному сроку', tag: 'Скорость',   rows: fast,  total: sum(fast),  maxLead: Math.max(...fast.map(r => r.leadDays)) },
    bal:   { id: 'bal',   label: 'Балансированный',        tag: 'Баланс',    rows: bal,   total: sum(bal),   maxLead: Math.max(...bal.map(r => r.leadDays)) },
  };
}

const VARIANTS = computeVariants(PROJECT_ITEMS, SUPPLIERS);

const PROJECTS = [
  { id: 'P-2026-118', name: 'СКС и сетевое ядро ТЦ "Орбита"',     rp: 'Смирнов А.', items: 84, status: 'Согласование РП',    margin: 18.4, value: 14_820_000, due: '2026-06-12' },
  { id: 'P-2026-114', name: 'ЦОД заказчика, расширение фазы 2',   rp: 'Васильева Е.', items: 142, status: 'В коммерческом блоке', margin: 22.1, value: 38_410_000, due: '2026-07-30' },
  { id: 'P-2026-109', name: 'Видеонаблюдение склада №3',          rp: 'Карпов Д.',  items: 41, status: 'Ожидание оплаты',     margin: 19.8, value: 4_120_000,  due: '2026-05-28' },
  { id: 'P-2026-102', name: 'Wi-Fi инфраструктура офиса',         rp: 'Смирнов А.', items: 28, status: 'Пришёл на склад',    margin: 21.3, value: 2_870_000,  due: '2026-05-19' },
  { id: 'P-2026-097', name: 'Замена коммутаторов филиала Казань', rp: 'Лебедев И.', items: 36, status: 'Отражено в 1С',      margin: 24.0, value: 6_540_000,  due: '2026-04-15' },
  { id: 'P-2026-088', name: 'Серверная стойка для НИИ',           rp: 'Васильева Е.', items: 19, status: 'Черновик КП',        margin: null,   value: 8_910_000,  due: '2026-08-04' },
];

const LIFECYCLE_STAGES = [
  { id: 'draft',   short: 'Черновик',     full: 'Черновик КП' },
  { id: 'rp',      short: 'РП',            full: 'Согласование РП' },
  { id: 'comm',    short: 'Коммерч.',      full: 'Коммерческий блок' },
  { id: 'invoice', short: 'NOC',           full: 'Счёт в NOC' },
  { id: 'pay',     short: 'Оплата',        full: 'Ожидание оплаты' },
  { id: 'ship',    short: 'Поставка',      full: 'Ожидание поставки' },
  { id: 'wh',      short: 'Склад',         full: 'Пришёл на склад' },
  { id: '1c',      short: '1С',            full: 'Отражено в 1С' },
];

const ROLES = [
  { id: 'rp',    label: 'Руководитель проекта', short: 'РП' },
  { id: 'comm',  label: 'Коммерческий блок',    short: 'КБ' },
  { id: 'acc',   label: 'Бухгалтерия',          short: 'БУХ' },
  { id: 'wh',    label: 'Склад',                short: 'СК' },
  { id: 'admin', label: 'Администратор',        short: 'АДМ' },
];

const EVENT_LOG = [
  { ts: '24.04 14:32:08', user: 'Смирнов А.',    action: 'Загрузил прайс',            obj: 'ТД Сетевые решения · 1284 поз.',          tag: 'upload' },
  { ts: '24.04 14:45:12', user: 'Смирнов А.',    action: 'Создал проект',             obj: 'P-2026-118 «СКС ТЦ Орбита»',              tag: 'create' },
  { ts: '24.04 14:47:01', user: 'Система',       action: 'Сформировала 3 варианта КП',obj: 'P-2026-118 · 84 позиции',                 tag: 'system' },
  { ts: '24.04 15:02:44', user: 'Смирнов А.',    action: 'Изменил скидку вендора',    obj: 'Eltex · 8% → 12%',                         tag: 'edit' },
  { ts: '24.04 15:04:09', user: 'Смирнов А.',    action: 'Заменил поставщика по поз.', obj: 'L02 · НетПром → СвязьКомплект',            tag: 'edit' },
  { ts: '24.04 15:18:55', user: 'Смирнов А.',    action: 'Утвердил вариант КП',       obj: 'Балансированный · 14 820 000 ₽',          tag: 'approve' },
  { ts: '24.04 15:19:02', user: 'Система',       action: 'Передала КП',               obj: '→ Коммерческий блок',                      tag: 'system' },
  { ts: '24.04 16:48:30', user: 'Васильева Е.',  action: 'Рассчитала маржинальность',  obj: '18.4% · согласовано',                      tag: 'approve' },
  { ts: '24.04 16:51:14', user: 'Система',       action: 'Создала счёт в NOC',         obj: 'NOC-2026-118-001',                         tag: 'system' },
  { ts: '24.04 17:23:09', user: 'Алексеев П.',   action: 'Согласовал счёт',           obj: 'Этап 1/3',                                 tag: 'approve' },
];

Object.assign(window, {
  SUPPLIERS, VENDORS, PROJECT_ITEMS, VARIANTS, PROJECTS, LIFECYCLE_STAGES, ROLES, EVENT_LOG,
});
