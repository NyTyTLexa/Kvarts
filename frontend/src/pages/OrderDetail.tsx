import { useState, Fragment } from 'react'
import { useParams, Link } from 'react-router-dom'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { Shell } from '../components/Shell'
import { Btn, Icon, StatusPill } from '../ui/atoms'
import { useApi } from '../api/useApi'
import { useRoles } from '../auth/useRoles'
import { fmt, fmtMoney } from '../data/constants'
import {
  ORDER_FLOW, ORDER_STATUS_LABEL, ORDER_STATUS_KIND, ORDER_TRANSITIONS,
  STRATEGY_META, type OrderStatus, type QuoteStrategy,
} from '../data/orders'
import type { OrderDetailDto } from '../api/types'

function Lifecycle({ status }: { status: OrderStatus }) {
  const cancelled = status === 'Cancelled'
  const idx = ORDER_FLOW.indexOf(status)
  return (
    <div>
      <div style={{ fontSize: 11, color: 'var(--text-2)', textTransform: 'uppercase', letterSpacing: 0.5, fontWeight: 500, marginBottom: 10 }}>Жизненный цикл</div>
      <div className="flow-strip">
        {ORDER_FLOW.map((s, i, arr) => {
          const done = !cancelled && i < idx, active = !cancelled && i === idx
          return (
            <Fragment key={s}>
              <div style={{
                flex: 1, padding: '10px 12px', borderRadius: 6, fontSize: 12,
                display: 'flex', flexDirection: 'column', gap: 4, border: '1px solid',
                background: active ? 'var(--accent-soft)' : done ? 'var(--surface)' : 'var(--surface-2)',
                borderColor: active ? 'var(--accent)' : 'var(--border)',
                color: active ? 'var(--accent)' : done ? 'var(--text)' : 'var(--text-3)',
              }}>
                <div style={{ fontSize: 10, fontWeight: 600, textTransform: 'uppercase', letterSpacing: 0.4, color: 'var(--text-3)' }}>Этап {i + 1}</div>
                <div style={{ fontWeight: active || done ? 500 : 400 }}>{ORDER_STATUS_LABEL[s]}</div>
                <div style={{ fontSize: 10.5, color: 'var(--text-3)' }}>{done ? 'выполнено' : active ? 'текущий' : '—'}</div>
              </div>
              {i < arr.length - 1 && <div className="flow-chevron">›</div>}
            </Fragment>
          )
        })}
      </div>
      {cancelled && <div style={{ marginTop: 10, fontSize: 13, color: 'var(--err)', background: 'var(--err-bg)', padding: '8px 12px', borderRadius: 7, display: 'inline-flex', alignItems: 'center', gap: 8 }}><Icon name="x" size={14}/> Заказ отменён</div>}
    </div>
  )
}

export function OrderDetail() {
  const { id = '' } = useParams()
  const api = useApi()
  const qc = useQueryClient()
  const { canWrite } = useRoles()
  const [err, setErr] = useState<string>()

  const order = useQuery({ queryKey: ['order', id], queryFn: () => api.get<OrderDetailDto>(`/api/orders/${id}`) })

  const changeStatus = useMutation({
    mutationFn: (status: OrderStatus) => api.post<void>(`/api/orders/${id}/status`, { status }),
    onSuccess: () => { setErr(undefined); qc.invalidateQueries({ queryKey: ['order', id] }); qc.invalidateQueries({ queryKey: ['orders'] }) },
    onError: (e) => setErr((e as Error).message),
  })

  const o = order.data
  const transitions = o ? ORDER_TRANSITIONS[o.status] : []

  return (
    <Shell breadcrumb={['Заказы NOC', o?.number ?? '…']} actions={canWrite && o && transitions.map(t => (
      <Btn key={t} size="sm" kind={t === 'Cancelled' ? 'danger' : 'primary'}
        disabled={changeStatus.isPending} onClick={() => changeStatus.mutate(t)}>
        {t === 'Cancelled' ? 'Отменить' : `→ ${ORDER_STATUS_LABEL[t]}`}
      </Btn>
    ))}>
      <header className="page-head">
        <div style={{ display: 'flex', alignItems: 'center', gap: 12, flexWrap: 'wrap' }}>
          <h1 className="page-head-title">{o?.number ?? 'Заказ'}</h1>
          {o && <StatusPill status={ORDER_STATUS_LABEL[o.status]} kind={ORDER_STATUS_KIND[o.status]}/>}
          <Link to="/orders" style={{ marginLeft: 'auto', color: 'var(--text-3)', fontSize: 12.5 }}>к заказам</Link>
        </div>
        {o && <p className="page-head-sub" style={{ marginLeft: 0 }}>{o.title}{o.customer ? ` · ${o.customer}` : ''}</p>}
      </header>

      {err && <div style={{ color: 'var(--err)', background: 'var(--err-bg)', padding: '8px 12px', borderRadius: 7, marginBottom: 14, fontSize: 13 }}>{err}</div>}

      {o && (
        <>
          {/* Свойства */}
          <div style={{ background: 'var(--surface-2)', borderRadius: 9, padding: '12px 16px', marginBottom: 22 }}>
            <div className="kv-grid" style={{ fontSize: 13 }}>
              <span style={{ color: 'var(--text-2)' }}>Стратегия</span>
              <span><span style={{ padding: '2px 8px', borderRadius: 4, background: 'var(--surface)', fontSize: 11.5, border: '1px solid var(--border)' }}>{STRATEGY_META[o.strategy as QuoteStrategy]?.label ?? o.strategy}</span></span>
              <span style={{ color: 'var(--text-2)' }}>Сумма</span>
              <span className="tnum" style={{ fontWeight: 600 }}>{fmtMoney(o.totalCost)}</span>
              <span style={{ color: 'var(--text-2)' }}>Макс. срок</span>
              <span className="tnum">{o.maxLeadTimeDays} дн</span>
              <span style={{ color: 'var(--text-2)' }}>Оформил</span>
              <span>{o.createdBy ?? '—'} · {new Date(o.createdAtUtc).toLocaleString('ru-RU')}</span>
            </div>
          </div>

          <div style={{ marginBottom: 22 }}><Lifecycle status={o.status}/></div>

          <div style={{ display: 'flex', alignItems: 'center', gap: 10, marginBottom: 10 }}>
            <span style={{ fontSize: 14, fontWeight: 600 }}>Позиции заказа</span>
            <span style={{ fontSize: 12, color: 'var(--text-2)' }}>{o.lines.length} поз.</span>
          </div>
          <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: 12.5 }}>
            <thead>
              <tr>{['Позиция', 'Поставщик', 'Кол-во', 'Цена', 'Срок', 'Итог'].map((h, i) => (
                <th key={h} style={{ padding: '8px 12px', textAlign: i >= 2 ? 'right' : 'left', fontWeight: 500, fontSize: 11, color: 'var(--text-2)', textTransform: 'uppercase', letterSpacing: 0.5, borderBottom: '1px solid var(--border)' }}>{h}</th>
              ))}</tr>
            </thead>
            <tbody>
              {o.lines.map(l => (
                <tr key={l.id} style={{ borderBottom: '1px solid var(--border)' }}>
                  <td style={{ padding: '10px 12px' }}>
                    <div style={{ fontWeight: 500 }}>{l.name}</div>
                    {l.sku && <div className="mono" style={{ fontSize: 10.5, color: 'var(--text-3)', marginTop: 2 }}>{l.sku}</div>}
                  </td>
                  <td style={{ padding: '10px 12px' }}>
                    {l.vendorName && <span className="chip">{l.vendorName}</span>}
                  </td>
                  <td className="tnum" style={{ padding: '10px 12px', textAlign: 'right' }}>{l.quantity}</td>
                  <td className="tnum" style={{ padding: '10px 12px', textAlign: 'right' }}>{fmt(l.unitPrice)} ₽</td>
                  <td className="tnum" style={{ padding: '10px 12px', textAlign: 'right', color: 'var(--text-2)' }}>{l.leadTimeDays} дн</td>
                  <td className="tnum" style={{ padding: '10px 12px', textAlign: 'right', fontWeight: 600 }}>{fmtMoney(l.lineTotal)}</td>
                </tr>
              ))}
            </tbody>
          </table>

          <div style={{ marginTop: 20, display: 'flex', alignItems: 'center', gap: 14, fontSize: 12.5, color: 'var(--text-2)', borderTop: '1px solid var(--border)', paddingTop: 14 }}>
            <span style={{ color: 'var(--text-3)' }}>Смежные журналы</span>
            <Link to="/invoice" style={{ color: 'var(--accent)', display: 'inline-flex', alignItems: 'center', gap: 5, fontWeight: 500 }}>Счета <Icon name="arrowR" size={13} /></Link>
            <Link to="/warehouse" style={{ color: 'var(--accent)', display: 'inline-flex', alignItems: 'center', gap: 5, fontWeight: 500 }}>Приёмка <Icon name="arrowR" size={13} /></Link>
          </div>
        </>
      )}
    </Shell>
  )
}
