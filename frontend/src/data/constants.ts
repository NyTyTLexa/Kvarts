// Справочные константы из дизайна «Кварц» (жизненный цикл КП и роли по ТЗ).

export interface LifecycleStage {
  id: string
  short: string
  full: string
}

export const LIFECYCLE_STAGES: LifecycleStage[] = [
  { id: 'draft',   short: 'Черновик', full: 'Черновик КП' },
  { id: 'rp',      short: 'РП',       full: 'Согласование РП' },
  { id: 'comm',    short: 'Коммерч.', full: 'Коммерческий блок' },
  { id: 'invoice', short: 'NOC',      full: 'Счёт в NOC' },
  { id: 'pay',     short: 'Оплата',   full: 'Ожидание оплаты' },
  { id: 'ship',    short: 'Поставка', full: 'Ожидание поставки' },
  { id: 'wh',      short: 'Склад',    full: 'Пришёл на склад' },
  { id: '1c',      short: '1С',       full: 'Отражено в 1С' },
]

export interface Role {
  id: string
  label: string
  short: string
}

export const ROLES: Role[] = [
  { id: 'rp',     label: 'Руководитель проекта', short: 'РП' },
  { id: 'comm',   label: 'Коммерческий блок',    short: 'КБ' },
  { id: 'acc',    label: 'Бухгалтерия',          short: 'БУХ' },
  { id: 'wh',     label: 'Склад',                short: 'СК' },
  { id: 'admin',  label: 'Администратор',        short: 'АДМ' },
  { id: 'viewer', label: 'Наблюдатель',          short: 'НАБ' },
]

// Реальные роли бэкенда (Keycloak realm-роли) → отображаемая роль «Кварц».
// Каждая — отдельная учётка с собственными правами (не декорация): admin/manager/viewer —
// исходные три; commercial/accounting/warehouse добавлены под акторов ТЗ UC-06/08/09.
export function mapBackendRole(roles: string[]): Role {
  if (roles.includes('admin'))      return ROLES.find(r => r.id === 'admin')!
  if (roles.includes('manager'))    return ROLES.find(r => r.id === 'rp')!
  if (roles.includes('commercial')) return ROLES.find(r => r.id === 'comm')!
  if (roles.includes('accounting')) return ROLES.find(r => r.id === 'acc')!
  if (roles.includes('warehouse'))  return ROLES.find(r => r.id === 'wh')!
  return ROLES.find(r => r.id === 'viewer')!
}

// Форматтеры чисел/денег (ru-RU), как в макете.
export const fmt = (n: number) => n.toLocaleString('ru-RU').replace(/,/g, ' ')
export const fmtMoney = (n: number) => fmt(Math.round(n)) + ' ₽'
export const fmtMoneyShort = (n: number) => {
  if (n >= 1_000_000) return (n / 1_000_000).toFixed(1).replace('.', ',') + ' млн ₽'
  return fmt(Math.round(n)) + ' ₽'
}
