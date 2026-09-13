import { useState, Fragment } from 'react'
import { useParams, useSearchParams, useNavigate, Link } from 'react-router-dom'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Shell } from '../components/Shell'
import { Btn, Icon } from '../ui/atoms'
import { PulseDots } from '../ui/feedback'
import { useApi } from '../api/useApi'
import { useRoles } from '../auth/useRoles'
import { fmt, fmtMoney } from '../data/constants'
import { STRATEGY_META, type QuoteStrategy } from '../data/orders'
import type { Quote, OfferDto } from '../api/types'
import type { ApprovalDto } from '../api/commercial'

// Выбранный по строке оффер (vendor + цена + срок) — либо из КП, либо ручная замена.
interface Pick { vendorId?: string; vendorName?: string; unitPrice: number; leadTimeDays: number }

function OfferPicker({ productId, current, onPick }:
  { productId: string; current: Pick; onPick: (o: OfferDto) => void }) {
  const api = useApi()
  const offers = useQuery({
    queryKey: ['offers', productId],
    queryFn: () => api.get<OfferDto[]>(`/api/pricelist/by-product/${productId}`),
  })
  return (
    <div style={{ background: 'var(--accent-soft)', padding: '10px 14px', display: 'flex', flexDirection: 'column', gap: 6 }}>
      <div style={{ fontSize: 11, color: 'var(--text-2)', textTransform: 'uppercase', letterSpacing: 0.5, fontWeight: 600 }}>Альтернативные поставщики</div>
      {offers.isLoading && <PulseDots label="Офферы" />}
      {offers.data?.slice().sort((a, b) => a.price - b.price).map(o => {
        const active = o.vendorId === current.vendorId && o.price === current.unitPrice
        return (
          <div key={o.id} className="offer-row" onClick={() => onPick(o)} style={{
            padding: '7px 10px', borderRadius: 7, cursor: 'pointer', fontSize: 12.5,
            background: active ? 'var(--surface)' : 'transparent',
            border: active ? '1.5px solid var(--accent)' : '1.5px solid transparent',
          }}>
            <span style={{ width: 14, height: 14, borderRadius: 999, border: '1.5px solid var(--accent)', display: 'inline-flex', alignItems: 'center', justifyContent: 'center' }}>
              {active && <span style={{ width: 8, height: 8, borderRadius: 999, background: 'var(--accent)' }} />}
            </span>
            <span style={{ fontWeight: 500 }}>{o.vendorName}</span>
            <span className="tnum" style={{ textAlign: 'right' }}>{fmt(o.price)} ₽</span>
            <span className="tnum" style={{ textAlign: 'right', color: 'var(--text-2)' }}>{o.leadTimeDays} дн</span>
            <span className="tnum" style={{ textAlign: 'right', color: 'var(--text-2)' }}>ост. {o.stockQuantity}</span>
          </div>
        )
      })}
    </div>
  )
}

export function KPDetail() {
  const { id = '' } = useParams()
  const [params] = useSearchParams()
  const strategy = (params.get('strategy') as QuoteStrategy) || 'Balanced'
  const api = useApi()
  const qc = useQueryClient()
  const nav = useNavigate()
  const { canWrite } = useRoles()

  const [expanded, setExpanded] = useState<string | null>(null)
  const [err, setErr] = useState<string>()

  const sendToKb = useMutation({
    mutationFn: () => api.post<ApprovalDto>('/api/approvals', {
      specificationId: id, strategy, markupPercent: 10,
    }),
    onSuccess: (a) => nav(`/approval?id=${a.id}`),
    onError: (e) => setErr((e as Error).message),
  })

  const quote = useQuery({
    queryKey: ['quote', id, strategy],
    queryFn: () => api.get<Quote>(`/api/specifications/${id}/quote?strategy=${strategy}`),
  })

  const lines = quote.data?.lines ?? []
  const matched = lines.filter(l => l.matched)
  const total = matched.reduce((a, l) => a + l.unitPriceDiscounted * l.quantity, 0)
  const maxLead = matched.reduce((a, l) => Math.max(a, l.leadTimeDays), 0)
  const changed = matched.filter(l => l.selectionReason === 'Выбрано вручную').length

  return (
    <Shell breadcrumb={['Заявки', quote.data?.specificationTitle ?? '…', 'Детальный КП']} actions={
      canWrite ? <Btn size="sm" kind="primary" icon="check" disabled={sendToKb.isPending || !quote.data}
        onClick={() => { setErr(undefined); sendToKb.mutate() }}
        title="Создать карточку маржи по этому листу КП">Передать в КБ</Btn> : undefined
    }>
      <header className="page-head">
        <div style={{ display: 'flex', alignItems: 'center', gap: 10, marginBottom: 2 }}>
          <h1 className="page-head-title">Детальный КП · {STRATEGY_META[strategy].tag}</h1>
          <Link to={`/specifications/${id}/quote`} style={{ marginLeft: 'auto', color: 'var(--text-3)', fontSize: 12.5 }}>к сравнению вариантов</Link>
        </div>
        <p className="page-head-sub" style={{ marginLeft: 0 }}>Разверните позицию, чтобы заменить поставщика. {changed > 0 && <span style={{ color: 'var(--accent)', background: 'var(--accent-soft)', padding: '1px 8px', borderRadius: 999, marginLeft: 6 }}>Изменено строк: {changed}</span>}</p>
      </header>
      {err && <div style={{ color: 'var(--err)', background: 'var(--err-bg)', padding: '8px 12px', marginBottom: 14, fontSize: 13 }}>{err}</div>}

      {/* summary strip */}
      <div className="stat-grid-4" style={{ marginBottom: 18, maxWidth: 760 }}>
        <Metric label="Итого" value={fmtMoney(total)} accent />
        <Metric label="Макс. срок" value={`${maxLead} дн`} />
        <Metric label="Позиций" value={`${matched.length} / ${lines.length}`} />
        <Metric label="Замен" value={`${changed}`} />
      </div>

      <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: 12.5 }}>
        <thead>
          <tr>{['', 'Позиция', 'Поставщик', 'Кол-во', 'Цена', 'Срок', 'Итог'].map((h, i) => (
            <th key={i} style={{ padding: '8px 12px', textAlign: i >= 3 ? 'right' : 'left', fontWeight: 500, fontSize: 11, color: 'var(--text-2)', textTransform: 'uppercase', letterSpacing: 0.5, borderBottom: '1px solid var(--border)' }}>{h}</th>
          ))}</tr>
        </thead>
        <tbody>
          {lines.map(l => {
            const open = expanded === l.specificationItemId
            const replaced = l.selectionReason === 'Выбрано вручную'
            return (
              <Fragment key={l.specificationItemId}>
                <tr onClick={() => l.matched && setExpanded(open ? null : l.specificationItemId)}
                  style={{ borderBottom: open ? 'none' : '1px solid var(--border)', cursor: l.matched ? 'pointer' : 'default', background: open ? 'var(--accent-soft)' : undefined }}>
                  <td style={{ padding: '10px 12px', color: 'var(--text-3)', width: 24 }}>{l.matched && <span style={{ display: 'inline-flex', transform: open ? 'rotate(90deg)' : 'none' }}><Icon name="chevR" size={13} /></span>}</td>
                  <td style={{ padding: '10px 12px' }}>
                    <div style={{ fontWeight: 500 }}>{l.name}</div>
                    {l.sku && <div className="mono" style={{ fontSize: 10.5, color: 'var(--text-3)', marginTop: 2 }}>{l.sku}</div>}
                    {l.manufacturer && <div style={{ fontSize: 10.5, color: 'var(--text-2)', marginTop: 2 }}>{l.manufacturer}</div>}
                  </td>
                  <td style={{ padding: '10px 12px' }}>
                    {l.matched
                      ? <>
                        <div style={{ display: 'inline-flex', alignItems: 'center', gap: 8 }}>
                          <span className="chip">{l.vendorName}{replaced ? ' · вручную' : ''}</span>
                          {replaced && (
                            <Btn size="sm" kind="ghost" icon="x"
                              onClick={async (e) => {
                                e.stopPropagation()
                                await api.del(`/api/specifications/${id}/items/${l.specificationItemId}/override`)
                                qc.invalidateQueries({ queryKey: ['quote', id, strategy] })
                              }}
                              style={{ height: 20, fontSize: 11, padding: '2px 8px' }}>Сбросить выбор</Btn>
                          )}
                        </div>
                        <div style={{ fontSize: 10.5, color: 'var(--text-3)', marginTop: 3 }}>{l.selectionReason}</div>
                      </>
                      : <span style={{ fontSize: 11.5, color: 'var(--warn)' }}>нет предложений</span>}
                  </td>
                  <td className="tnum" style={{ padding: '10px 12px', textAlign: 'right' }}>{l.quantity}</td>
                  <td className="tnum" style={{ padding: '10px 12px', textAlign: 'right' }}>{l.matched ? fmt(l.unitPriceDiscounted) + ' ₽' : '—'}</td>
                  <td className="tnum" style={{ padding: '10px 12px', textAlign: 'right', color: 'var(--text-2)' }}>{l.matched ? l.leadTimeDays + ' дн' : '—'}</td>
                  <td className="tnum" style={{ padding: '10px 12px', textAlign: 'right', fontWeight: 600 }}>{l.matched ? fmtMoney(l.unitPriceDiscounted * l.quantity) : '—'}</td>
                </tr>
                {open && l.productId && (
                  <tr style={{ borderBottom: '1px solid var(--border)' }}>
                    <td colSpan={7} style={{ padding: 0 }}>
                      <OfferPicker productId={l.productId} current={{ vendorId: l.vendorId, vendorName: l.vendorName, unitPrice: l.unitPriceDiscounted, leadTimeDays: l.leadTimeDays }}
                        onPick={async (o) => {
                          await api.post(`/api/specifications/${id}/items/${l.specificationItemId}/override`, { vendorId: o.vendorId })
                          qc.invalidateQueries({ queryKey: ['quote', id, strategy] })
                          setExpanded(null)
                        }} />
                    </td>
                  </tr>
                )}
              </Fragment>
            )
          })}
        </tbody>
      </table>
    </Shell>
  )
}

function Metric({ label, value, accent }: { label: string; value: string; accent?: boolean }) {
  return (
    <div style={{ background: accent ? 'var(--accent-soft)' : 'var(--surface)', border: '1px solid ' + (accent ? 'var(--accent-border)' : 'var(--border)'), borderRadius: 10, padding: '12px 14px' }}>
      <div className="field-label" style={{ color: accent ? 'var(--accent)' : undefined }}>{label}</div>
      <div className="tnum" style={{ fontSize: 20, fontWeight: 600, marginTop: 4 }}>{value}</div>
    </div>
  )
}

