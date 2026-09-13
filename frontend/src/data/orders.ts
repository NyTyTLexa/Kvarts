import type { PillKind } from '../ui/atoms'

// Статусы заказа (бэкенд OrderStatus) — метки, цвета и допустимые переходы.
export type OrderStatus = 'Draft' | 'Placed' | 'Confirmed' | 'Shipped' | 'Completed' | 'Cancelled'

export const ORDER_STATUS_LABEL: Record<OrderStatus, string> = {
  Draft: 'Черновик',
  Placed: 'Размещён',
  Confirmed: 'Подтверждён',
  Shipped: 'Отгружен',
  Completed: 'Завершён',
  Cancelled: 'Отменён',
}

export const ORDER_STATUS_KIND: Record<OrderStatus, PillKind> = {
  Draft: 'gray',
  Placed: 'blue',
  Confirmed: 'blue',
  Shipped: 'amber',
  Completed: 'green',
  Cancelled: 'red',
}

// Должно совпадать с OrderService.Transitions на бэкенде.
export const ORDER_TRANSITIONS: Record<OrderStatus, OrderStatus[]> = {
  Draft: ['Placed', 'Cancelled'],
  Placed: ['Confirmed', 'Cancelled'],
  Confirmed: ['Shipped', 'Cancelled'],
  Shipped: ['Completed'],
  Completed: [],
  Cancelled: [],
}

// Основная цепочка жизненного цикла (без Cancelled) — для горизонтального бара.
export const ORDER_FLOW: OrderStatus[] = ['Draft', 'Placed', 'Confirmed', 'Shipped', 'Completed']

// Стратегии подбора КП.
export type QuoteStrategy = 'MinCost' | 'MinLeadTime' | 'Balanced' | 'MlRelevance'

export const STRATEGY_META: Record<QuoteStrategy, { tag: string; label: string; desc: string }> = {
  MinCost:      { tag: 'Себестоимость', label: 'По минимальной цене',  desc: 'Минимальная закупка по каждой позиции' },
  MinLeadTime:  { tag: 'Скорость',      label: 'По минимальному сроку', desc: 'Минимальный срок поставки' },
  Balanced:     { tag: 'Баланс',        label: 'Балансированный',       desc: 'Взвешенный баланс цена / срок' },
  MlRelevance:  { tag: 'Актуальность',  label: 'По свежести прайса',    desc: 'Свежая цена, наличие и сходство с заявкой' },
}
