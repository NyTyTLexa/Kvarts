/** Матрица доступа экранов. API по-прежнему режет мутации политиками. */

export const APP_ROLES = ['admin', 'manager', 'commercial', 'accounting', 'warehouse', 'viewer'] as const
export type AppRole = (typeof APP_ROLES)[number]

const RP: AppRole[] = ['admin', 'manager']
const RP_VIEW: AppRole[] = ['admin', 'manager', 'viewer']
const QUOTE: AppRole[] = ['admin', 'manager', 'commercial', 'viewer']
const ORDERS: AppRole[] = ['admin', 'manager', 'accounting', 'warehouse', 'viewer']
const ALL: AppRole[] = [...APP_ROLES]

export type NavItemDef = {
  id: string
  to: string
  icon: 'folder' | 'document' | 'search' | 'package' | 'tag' | 'truck' | 'layers' | 'scale' | 'bank' | 'chart' | 'clock' | 'users' | 'settings' | 'globe'
  label: string
  hint: string
  roles: AppRole[]
}

export type NavGroupDef = { title: string; items: NavItemDef[] }

export const NAV: NavGroupDef[] = [
  { title: 'Контур КП', items: [
    { id: 'projects', to: '/',               icon: 'folder',   label: 'Проекты',  hint: 'тендеры',        roles: RP_VIEW },
    { id: 'prices',   to: '/prices',         icon: 'tag',      label: 'Прайсы',   hint: 'excel поставщиков', roles: RP },
    { id: 'ecatalog', to: '/ecatalog',       icon: 'search',   label: 'Каталог',  hint: 'цены в витрине', roles: ALL },
    { id: 'specs',    to: '/specifications', icon: 'document', label: 'Заявки',   hint: 'перечень → КП',  roles: QUOTE },
    { id: 'orders',   to: '/orders',         icon: 'package',  label: 'Заказы',   hint: 'снимок КП',      roles: ORDERS },
  ]},
  { title: 'Справочники', items: [
    { id: 'suppliers',     to: '/suppliers',     icon: 'truck',   label: 'Поставщики',    hint: 'контрагенты', roles: RP_VIEW },
    { id: 'manufacturers', to: '/manufacturers', icon: 'layers',  label: 'Производители', hint: 'вендоры',     roles: RP_VIEW },
    { id: 'catalog',       to: '/catalog',       icon: 'package', label: 'Номенклатура',  hint: 'артикулы',    roles: RP_VIEW },
    { id: 'discounts',     to: '/discounts',     icon: 'tag',     label: 'Скидки',        hint: 'к цене КП',  roles: RP },
  ]},
  { title: 'Согласование', items: [
    { id: 'approval',  to: '/approval',  icon: 'scale', label: 'Маржа',  hint: 'комблок',   roles: ['admin', 'manager', 'commercial'] },
    { id: 'invoice',   to: '/invoice',   icon: 'bank',  label: 'Счета',  hint: 'NOC',       roles: ['admin', 'manager', 'accounting'] },
    { id: 'warehouse', to: '/warehouse', icon: 'truck', label: 'Склад',  hint: 'приёмка',   roles: ['admin', 'warehouse'] },
  ]},
  { title: 'Система', items: [
    { id: 'analytics', to: '/analytics', icon: 'chart',    label: 'Сводка',    hint: 'цифры',      roles: ALL },
    { id: 'audit',     to: '/audit',     icon: 'clock',    label: 'Журнал',    hint: 'кто менял',  roles: ['admin'] },
    { id: 'users',     to: '/users',     icon: 'users',    label: 'Пользователи', hint: 'роли',     roles: ['admin'] },
    { id: 'settings', to: '/settings', icon: 'settings', label: 'Настройки', hint: 'контур',     roles: ['admin'] },
    { id: 'retail',   to: '/retail',   icon: 'globe',    label: 'Розница',   hint: 'сверка цен', roles: RP },
  ]},
]

const ROUTES: { prefix: string; exact?: boolean; roles: AppRole[] }[] = [
  { prefix: '/', exact: true, roles: RP_VIEW },
  { prefix: '/projects', roles: RP_VIEW },
  { prefix: '/specifications', roles: QUOTE },
  { prefix: '/ecatalog', roles: ALL },
  { prefix: '/orders', roles: ORDERS },
  { prefix: '/prices', roles: RP },
  { prefix: '/suppliers', roles: RP_VIEW },
  { prefix: '/manufacturers', roles: RP_VIEW },
  { prefix: '/catalog', roles: RP_VIEW },
  { prefix: '/discounts', roles: RP },
  { prefix: '/approval', roles: ['admin', 'manager', 'commercial'] },
  { prefix: '/invoice', roles: ['admin', 'manager', 'accounting'] },
  { prefix: '/warehouse', roles: ['admin', 'warehouse'] },
  { prefix: '/analytics', roles: ALL },
  { prefix: '/audit', roles: ['admin'] },
  { prefix: '/users', roles: ['admin'] },
  { prefix: '/settings', roles: ['admin'] },
  { prefix: '/retail', roles: RP },
]

export function appRoles(raw: string[]): AppRole[] {
  const found = APP_ROLES.filter(r => raw.includes(r))
  return found.length ? found : ['viewer']
}

export function primaryRole(roles: string[]): AppRole {
  const mine = appRoles(roles)
  for (const r of APP_ROLES) if (mine.includes(r)) return r
  return 'viewer'
}

export function homePath(roles: string[]): string {
  switch (primaryRole(roles)) {
    case 'commercial': return '/approval'
    case 'accounting': return '/invoice'
    case 'warehouse': return '/warehouse'
    default: return '/'
  }
}

export function canAccess(pathname: string, roles: string[]): boolean {
  const mine = appRoles(roles)
  if (mine.includes('admin')) return true
  const rule = [...ROUTES]
    .sort((a, b) => b.prefix.length - a.prefix.length)
    .find(r => r.exact ? pathname === r.prefix : pathname === r.prefix || pathname.startsWith(r.prefix + '/'))
  if (!rule) return true
  return rule.roles.some(r => mine.includes(r))
}

export function navFor(roles: string[]): NavGroupDef[] {
  const mine = appRoles(roles)
  const allow = (item: NavItemDef) => mine.includes('admin') || item.roles.some(r => mine.includes(r))
  return NAV
    .map(g => ({ ...g, items: g.items.filter(allow) }))
    .filter(g => g.items.length > 0)
}

export function isNavActive(pathname: string, to: string) {
  if (to === '/') return pathname === '/' || pathname.startsWith('/projects/')
  if (to === '/catalog') return pathname === '/catalog' || pathname.startsWith('/catalog/')
  return pathname === to || pathname.startsWith(to + '/')
}

/** Нижние вкладки телефона: рабочий контур роли, не всё меню. */
const PHONE_TAB_IDS: Record<AppRole, readonly string[]> = {
  admin: ['projects', 'specs', 'ecatalog', 'approval'],
  manager: ['projects', 'specs', 'ecatalog', 'approval'],
  commercial: ['approval', 'specs', 'ecatalog', 'analytics'],
  accounting: ['invoice', 'orders', 'ecatalog', 'analytics'],
  warehouse: ['warehouse', 'orders', 'ecatalog', 'analytics'],
  viewer: ['projects', 'specs', 'ecatalog', 'analytics'],
}

export function phoneTabsFor(roles: string[]): NavItemDef[] {
  const allowed = navFor(roles).flatMap(g => g.items)
  const out: NavItemDef[] = []
  for (const id of PHONE_TAB_IDS[primaryRole(roles)]) {
    const it = allowed.find(x => x.id === id)
    if (it) out.push(it)
  }
  return out
}
