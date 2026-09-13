import { useNavigate } from 'react-router-dom'
import { useQuery } from '@tanstack/react-query'
import { Shell } from '../components/Shell'
import { PageHead } from '../components/PageHead'
import { Icon, StatusPill } from '../ui/atoms'
import { useApi } from '../api/useApi'
import { fmtMoney } from '../data/constants'
import { ORDER_STATUS_LABEL, ORDER_STATUS_KIND } from '../data/orders'
import { STRATEGY_META, type QuoteStrategy } from '../data/orders'
import type { OrderDto } from '../api/types'

export function OrdersList() {
  const api = useApi()
  const nav = useNavigate()
  const list = useQuery({ queryKey: ['orders'], queryFn: () => api.get<OrderDto[]>('/api/orders') })

  return (
    <Shell breadcrumb={['Заказы']}>
      <PageHead icon="package" kicker="снимок КП" title="Заказы"
        subtitle="Оформлены из выбранного листа КП. Цены на этом шаге уже зафиксированы." />

      <div className="doc-list">
      <table>
        <thead>
          <tr>{['Номер', 'Спецификация', 'Стратегия', 'Статус', 'Срок', 'Сумма', 'Оформил'].map((h, i) => (
            <th key={h} style={{ padding: '10px 12px', textAlign: i >= 4 && i <= 5 ? 'right' : 'left', fontWeight: 500, fontSize: 11, color: 'var(--text-2)', textTransform: 'uppercase', letterSpacing: 0.6, borderBottom: '1px solid var(--border)' }}>{h}</th>
          ))}</tr>
        </thead>
        <tbody>
          {list.data?.map(o => (
            <tr key={o.id} onClick={() => nav(`/orders/${o.id}`)} style={{ borderBottom: '1px solid var(--border)', cursor: 'pointer' }}>
              <td className="mono" style={{ padding: '12px', fontSize: 11.5 }}>
                <span style={{ display: 'inline-flex', alignItems: 'center', gap: 8 }}>
                  <Icon name="package" size={14} stroke="var(--text-2)"/>{o.number}
                </span>
              </td>
              <td style={{ padding: '12px', fontWeight: 500 }}>{o.title}</td>
              <td style={{ padding: '12px' }}>
                <span style={{ padding: '2px 8px', borderRadius: 4, background: 'var(--surface-2)', fontSize: 11.5 }}>{STRATEGY_META[o.strategy as QuoteStrategy]?.tag ?? o.strategy}</span>
              </td>
              <td style={{ padding: '12px' }}><StatusPill status={ORDER_STATUS_LABEL[o.status]} kind={ORDER_STATUS_KIND[o.status]}/></td>
              <td className="tnum" style={{ padding: '12px', textAlign: 'right', color: 'var(--text-2)' }}>{o.maxLeadTimeDays} дн</td>
              <td className="tnum" style={{ padding: '12px', textAlign: 'right', fontWeight: 500 }}>{fmtMoney(o.totalCost)}</td>
              <td style={{ padding: '12px', color: 'var(--text-2)' }}>{o.createdBy ?? '—'}</td>
            </tr>
          ))}
          {list.isLoading && <tr><td colSpan={7} style={{ padding: 16, color: 'var(--text-3)' }}>Загрузка…</td></tr>}
          {list.data && list.data.length === 0 && <tr><td colSpan={7} style={{ padding: 16, color: 'var(--text-3)' }}>Пока нет заказов. Оформите заказ из сравнения КП.</td></tr>}
        </tbody>
      </table>
      </div>
    </Shell>
  )
}
