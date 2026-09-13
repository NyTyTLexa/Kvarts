import { useState } from 'react'
import { useParams, useNavigate, Link } from 'react-router-dom'
import { useQuery, useMutation } from '@tanstack/react-query'
import { Shell } from '../components/Shell'
import { Btn, Icon, StatusPill } from '../ui/atoms'
import { PageLoader } from '../ui/feedback'
import { useApi } from '../api/useApi'
import { useRoles } from '../auth/useRoles'
import { fmt, fmtMoney, fmtMoneyShort } from '../data/constants'
import { STRATEGY_META, type QuoteStrategy } from '../data/orders'
import type { Quote, OrderDetailDto } from '../api/types'
import type { ApprovalDto } from '../api/commercial'

const ORDER: QuoteStrategy[] = ['MinCost', 'MinLeadTime', 'Balanced', 'MlRelevance']

export function QuoteCompare() {
  const { id = '' } = useParams()
  const api = useApi()
  const nav = useNavigate()
  const { canWrite } = useRoles()
  const [sel, setSel] = useState<QuoteStrategy>('Balanced')
  const [err, setErr] = useState<string>()

  const compare = useQuery({
    queryKey: ['quote-compare', id],
    queryFn: () => api.get<Quote[]>(`/api/specifications/${id}/quote/compare`),
  })

  const createOrder = useMutation({
    mutationFn: () => api.post<OrderDetailDto>(`/api/orders/from-quote?specId=${id}&strategy=${sel}`),
    onSuccess: (o) => nav(`/orders/${o.id}`),
    onError: (e) => setErr((e as Error).message),
  })
  const sendToKb = useMutation({
    mutationFn: () => api.post<ApprovalDto>('/api/approvals', {
      specificationId: id, strategy: sel, markupPercent: 10,
    }),
    onSuccess: (a) => nav(`/approval?id=${a.id}`),
    onError: (e) => setErr((e as Error).message),
  })

  const byStrategy = (s: QuoteStrategy) => compare.data?.find(q => q.strategy === s)
  const current = byStrategy(sel)
  const title = compare.data?.[0]?.specificationTitle

  function exportXlsx() {
    api.download(`/api/specifications/${id}/quote/export?strategy=${sel}`, `КП_${title ?? id}_${sel}.xlsx`)
  }
  function exportPdf() {
    api.download(`/api/specifications/${id}/quote/export/pdf?strategy=${sel}`, `КП_${title ?? id}_${sel}.pdf`)
  }

  return (
    <Shell breadcrumb={['Заявки', title ?? '…', 'Сравнение КП']} actions={<>
      {canWrite && <Btn size="sm" kind="ghost" iconRight="arrowR" disabled={!current || sendToKb.isPending}
        onClick={() => { setErr(undefined); sendToKb.mutate() }}>Передать в КБ</Btn>}
      <Btn size="sm" kind="default" icon="edit" disabled={!current} onClick={() => nav(`/specifications/${id}/quote/detail?strategy=${sel}`)}>Детальный КП</Btn>
      <Btn size="sm" kind="default" icon="download" disabled={!current} onClick={exportXlsx}>Выгрузить в Excel</Btn>
      <Btn size="sm" kind="default" icon="download" disabled={!current} onClick={exportPdf}>PDF</Btn>
      {canWrite && <Btn size="sm" kind="primary" icon="check" disabled={!current || createOrder.isPending}
        onClick={() => { setErr(undefined); createOrder.mutate() }}>Оформить заказ</Btn>}
    </>}>
      <header className="page-head">
        <div style={{ display: 'flex', alignItems: 'center', gap: 10 }}>
          <div className="page-head-icon"><Icon name="scale" size={18} stroke="var(--accent)" /></div>
          <h1 className="page-head-title">Варианты КП</h1>
          <Link to={`/specifications/${id}`} style={{ marginLeft: 'auto', color: 'var(--text-3)', fontSize: 13 }}>к заявке</Link>
        </div>
        {current && (
          <p className="page-head-sub">
            {title}. Сопоставлено {current.matchedPositions}
            {current.unmatchedPositions > 0 && <> · без предложений {current.unmatchedPositions}</>}
          </p>
        )}
      </header>

      {compare.isLoading && <PageLoader label="Считаем варианты КП" />}
      {err && <div style={{ color: 'var(--err)', background: 'var(--err-bg)', padding: '8px 12px', marginBottom: 14, fontSize: 13 }}>{err}</div>}

      <div className="quote-board" style={{ marginBottom: 28 }}>
        {ORDER.map((s, i) => {
          const q = byStrategy(s); if (!q) return null
          const meta = STRATEGY_META[s]
          const selected = s === sel
          return (
            <button key={s} type="button" onClick={() => setSel(s)}
              className={'quote-tile quote-tile-' + i + (selected ? ' is-on' : '')}>
              <div className="field-label">{meta.tag}</div>
              <div style={{ fontSize: 14, fontWeight: 600, margin: '4px 0 12px' }}>{meta.label}</div>
              <div className="tnum" style={{ fontSize: 22, fontWeight: 700 }}>{fmtMoneyShort(q.totalCost)}</div>
              <div className="tnum" style={{ fontSize: 12, color: 'var(--text-3)', marginTop: 4 }}>с НДС {fmtMoneyShort(q.totalCostWithVat)}</div>
              <div style={{ marginTop: 'auto', paddingTop: 12, display: 'flex', justifyContent: 'space-between', fontSize: 12.5, color: 'var(--text-2)', borderTop: '1px solid var(--border)' }}>
                <span>{q.maxLeadTimeDays} дн</span>
                <span>{q.vendorsUsed} пост.</span>
                {selected && <span style={{ color: 'var(--accent)', fontWeight: 700 }}>выбран</span>}
              </div>
            </button>
          )
        })}
      </div>

      {/* Позиции выбранного варианта */}
      {current && (
        <>
          <div className="quote-sum">
            <span style={{ fontSize: 14, fontWeight: 600 }}>Позиции варианта «{STRATEGY_META[sel].tag}»</span>
            <span style={{ fontSize: 12, color: 'var(--text-2)', padding: '2px 8px', borderRadius: 999, background: 'var(--surface-2)' }}>{current.lines.length} позиций</span>
            <div style={{ flex: 1 }}/>
            <span style={{ fontSize: 13, color: 'var(--text-2)' }}>
              Итого без НДС: <b className="tnum" style={{ color: 'var(--text)', fontSize: 15 }}>{fmtMoney(current.totalCost)}</b>
              <span style={{ margin: '0 8px', color: 'var(--text-3)' }}>·</span>
              с НДС {fmt(current.vatRate)}%: <b className="tnum" style={{ color: 'var(--text)', fontSize: 15 }}>{fmtMoney(current.totalCostWithVat)}</b>
            </span>
          </div>
          <div className="doc-list">
          <table>
            <thead>
              <tr>{['Позиция', 'Поставщик', 'Кол-во', 'Цена', 'Скидка', 'Срок', 'Итог'].map((h, i) => (
                <th key={h} style={{ padding: '8px 12px', textAlign: i >= 2 ? 'right' : 'left', fontWeight: 500, fontSize: 11, color: 'var(--text-2)', textTransform: 'uppercase', letterSpacing: 0.5, borderBottom: '1px solid var(--border)' }}>{h}</th>
              ))}</tr>
            </thead>
            <tbody>
              {current.lines.map(l => (
                <tr key={l.specificationItemId} style={{ borderBottom: '1px solid var(--border)', background: l.matched ? undefined : 'var(--warn-bg)' }}>
                  <td style={{ padding: '10px 12px' }}>
                    <div style={{ fontWeight: 500 }}>{l.name}</div>
                    {l.sku && <div className="mono" style={{ fontSize: 10.5, color: 'var(--text-3)', marginTop: 2 }}>{l.sku}</div>}
                    {l.manufacturer && <div style={{ fontSize: 10.5, color: 'var(--text-2)', marginTop: 2 }}>{l.manufacturer}</div>}
                    {l.matchKind && l.matchKind !== 'None' && (
                      <div style={{ fontSize: 10.5, color: 'var(--text-3)', marginTop: 2 }}>
                        {l.matchKind}{typeof l.matchScore === 'number' ? ` · P ${l.matchScore.toFixed(2)}` : ''}
                      </div>
                    )}
                  </td>
                  <td style={{ padding: '10px 12px' }}>
                    {l.matched
                      ? <><span className="chip">{l.vendorName}</span>{l.selectionReason && <div style={{ fontSize: 10.5, color: 'var(--text-3)', marginTop: 3 }}>{l.selectionReason}</div>}</>
                      : <StatusPill status="нет предложений" kind="amber"/>}
                  </td>
                  <td className="tnum" style={{ padding: '10px 12px', textAlign: 'right' }}>{l.quantity}</td>
                  <td className="tnum" style={{ padding: '10px 12px', textAlign: 'right' }}>
                    {l.matched
                      ? (l.discountPercent > 0
                          ? <><span style={{ textDecoration: 'line-through', color: 'var(--text-3)', marginRight: 5 }}>{fmt(l.unitPrice)}</span>{fmt(l.unitPriceDiscounted)} ₽</>
                          : fmt(l.unitPrice) + ' ₽')
                      : '—'}
                  </td>
                  <td className="tnum" style={{ padding: '10px 12px', textAlign: 'right', fontWeight: 500, color: l.matched && l.discountPercent > 0 ? 'var(--ok)' : 'var(--text-3)' }}>
                    {l.matched && l.discountPercent > 0 ? '−' + fmt(l.discountPercent) + '%' : '—'}
                  </td>
                  <td className="tnum" style={{ padding: '10px 12px', textAlign: 'right', color: 'var(--text-2)' }}>{l.matched ? l.leadTimeDays + ' дн' : '—'}</td>
                  <td className="tnum" style={{ padding: '10px 12px', textAlign: 'right', fontWeight: 600 }}>{l.matched ? fmtMoneyShort(l.lineTotal) : '—'}</td>
                </tr>
              ))}
            </tbody>
          </table>
          </div>
        </>
      )}
    </Shell>
  )
}
