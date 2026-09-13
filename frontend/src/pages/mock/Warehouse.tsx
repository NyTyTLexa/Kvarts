import { useState } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { Shell } from '../../components/Shell'
import { PageHead } from '../../components/PageHead'
import { Btn, StatusPill, Icon } from '../../ui/atoms'
import { useApi } from '../../api/useApi'
import { useRoles } from '../../auth/useRoles'
import type { OrderDto } from '../../api/types'
import type { ReceiptDto, ReceiptDetailDto } from '../../api/logistics'

export function Warehouse() {
  const api = useApi()
  const qc = useQueryClient()
  const { canReceive } = useRoles()
  const [openId, setOpenId] = useState<string | null>(null)
  const [orderId, setOrderId] = useState('')
  const [err, setErr] = useState<string>()

  const receipts = useQuery({ queryKey: ['receipts'], queryFn: () => api.get<ReceiptDto[]>('/api/receipts') })
  const orders = useQuery({ queryKey: ['orders'], queryFn: () => api.get<OrderDto[]>('/api/orders') })
  const detail = useQuery({
    queryKey: ['receipt', openId],
    queryFn: () => api.get<ReceiptDetailDto>(`/api/receipts/${openId}`),
    enabled: !!openId,
  })

  const create = useMutation({
    mutationFn: (oid: string) => api.post<ReceiptDetailDto>(`/api/receipts/from-order?orderId=${oid}`),
    onSuccess: (r) => { setOrderId(''); qc.invalidateQueries({ queryKey: ['receipts'] }); setOpenId(r.id) },
    onError: (e) => setErr((e as Error).message),
  })
  const setQty = useMutation({
    mutationFn: (v: { lineId: string; qty: number }) => api.post<ReceiptDetailDto>(`/api/receipts/${openId}/lines/${v.lineId}`, { receivedQty: v.qty }),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['receipt', openId] }),
    onError: (e) => setErr((e as Error).message),
  })
  const complete = useMutation({
    mutationFn: () => api.post<ReceiptDetailDto>(`/api/receipts/${openId}/complete`),
    onSuccess: () => { qc.invalidateQueries({ queryKey: ['receipt', openId] }); qc.invalidateQueries({ queryKey: ['receipts'] }) },
    onError: (e) => setErr((e as Error).message),
  })

  const d = detail.data
  const statusKind = (s: string) => (s === 'Проведено' ? 'green' : s === 'Отменена' ? 'red' : 'gray')

  return (
    <Shell breadcrumb={openId ? ['Приёмка склада', d?.orderNumber ?? '…'] : ['Приёмка склада']}
      actions={openId
        ? <Btn size="sm" kind="ghost" onClick={() => { setOpenId(null); setErr(undefined) }}>← К списку</Btn>
        : undefined}>
      <PageHead kicker="согласование · приёмка" title="Склад"
        subtitle="Сверка заказанного и принятого, расхождения, проведение в WMS и 1С." />

      {err && <div style={{ color: 'var(--err)', background: 'var(--err-bg)', padding: '8px 12px', borderRadius: 7, marginBottom: 12, fontSize: 13 }}>{err}</div>}

      {!openId ? (
        <>
          {canReceive && (
            <div style={{ display: 'flex', gap: 8, marginBottom: 16, alignItems: 'center', maxWidth: 620, flexWrap: 'wrap' }}>
              <select value={orderId} onChange={e => setOrderId(e.target.value)} style={{ flex: 1, border: '1px solid var(--border-strong)', borderRadius: 7, padding: '8px 12px', fontSize: 13.5, fontFamily: 'inherit', background: 'var(--surface)' }}>
                <option value="">— выберите заказ для приёмки —</option>
                {orders.data?.map(o => <option key={o.id} value={o.id}>{o.number} · {o.title}</option>)}
              </select>
              <Btn kind="primary" size="md" icon="plus" disabled={!orderId || create.isPending}
                onClick={() => { setErr(undefined); create.mutate(orderId) }}>Создать приёмку</Btn>
            </div>
          )}

          <div className="doc-list">
          <table>
            <thead>
              <tr>{['Заказ', 'Заказчик', 'Позиций', 'Расхождений', 'Статус', 'Внешние ссылки', ''].map((h, i) => (
                <th key={h} style={{ padding: '10px 12px', textAlign: i === 2 || i === 3 ? 'right' : 'left', fontWeight: 500, fontSize: 11, color: 'var(--text-2)', textTransform: 'uppercase', letterSpacing: 0.6, borderBottom: '1px solid var(--border)' }}>{h}</th>
              ))}</tr>
            </thead>
            <tbody>
              {receipts.data?.map(r => (
                <tr key={r.id} style={{ borderBottom: '1px solid var(--border)' }}>
                  <td className="mono" style={{ padding: '11px 12px', fontWeight: 500 }}>{r.orderNumber}</td>
                  <td style={{ padding: '11px 12px', color: 'var(--text-2)' }}>{r.customer ?? '—'}</td>
                  <td className="tnum" style={{ padding: '11px 12px', textAlign: 'right' }}>{r.linesCount}</td>
                  <td className="tnum" style={{ padding: '11px 12px', textAlign: 'right', fontWeight: 600, color: r.discrepancyCount > 0 ? 'var(--warn)' : 'var(--text-3)' }}>{r.discrepancyCount || '—'}</td>
                  <td style={{ padding: '11px 12px' }}><StatusPill status={r.status} kind={statusKind(r.status)} /></td>
                  <td className="mono" style={{ padding: '11px 12px', fontSize: 11, color: 'var(--text-3)' }}>{r.warehouseRef ? `${r.warehouseRef} · ${r.accountingRef}` : '—'}</td>
                  <td style={{ padding: '11px 12px', textAlign: 'right' }}><Btn size="sm" onClick={() => setOpenId(r.id)}>Открыть</Btn></td>
                </tr>
              ))}
              {receipts.isLoading && <tr><td colSpan={7} style={{ padding: 16, color: 'var(--text-3)' }}>Загрузка…</td></tr>}
              {receipts.data && receipts.data.length === 0 && <tr><td colSpan={7} style={{ padding: 16, color: 'var(--text-3)' }}>Приёмок пока нет — создайте из заказа</td></tr>}
            </tbody>
          </table>
          </div>
        </>
      ) : (
        <>
          <div style={{ display: 'flex', alignItems: 'center', gap: 12, marginBottom: 14 }}>
            <span style={{ fontSize: 15, fontWeight: 600 }}>Приёмка заказа {d?.orderNumber}</span>
            {d && <StatusPill status={d.status} kind={statusKind(d.status)} />}
            <div style={{ flex: 1 }} />
            {d?.status === 'Черновик' && canReceive &&
              <Btn size="sm" kind="primary" icon="check" disabled={complete.isPending}
                onClick={() => { setErr(undefined); complete.mutate() }}>Провести приёмку</Btn>}
          </div>

          {d?.status === 'Проведено' && (
            <div style={{ marginBottom: 14, borderLeft: '3px solid var(--ok)', background: 'var(--ok-bg)', padding: '10px 14px', borderRadius: '0 6px 6px 0', fontSize: 12.5, display: 'flex', gap: 18 }}>
              <span><Icon name="check" size={14} stroke="var(--ok)" /> Выгружено во внешние системы:</span>
              <span className="mono">WMS: <b>{d.warehouseRef}</b></span>
              <span className="mono">1С: <b>{d.accountingRef}</b></span>
            </div>
          )}

          <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: 12.5 }}>
            <thead>
              <tr>{['Позиция', 'Заказано', 'Принято', 'Расхождение', 'Состояние'].map((h, i) => (
                <th key={h} style={{ padding: '8px 12px', textAlign: i === 0 ? 'left' : 'right', fontWeight: 500, fontSize: 11, color: 'var(--text-2)', textTransform: 'uppercase', letterSpacing: 0.5, borderBottom: '1px solid var(--border)' }}>{h}</th>
              ))}</tr>
            </thead>
            <tbody>
              {d?.lines.map(l => {
                const state = l.discrepancy === 0 ? 'В норме' : l.discrepancy < 0 ? 'Недостача' : 'Излишек'
                return (
                  <tr key={l.id} style={{ borderBottom: '1px solid var(--border)', background: l.discrepancy !== 0 ? 'var(--warn-bg)' : undefined }}>
                    <td style={{ padding: '11px 12px' }}>
                      <div style={{ fontWeight: 500 }}>{l.name}</div>
                      {l.sku && <div className="mono" style={{ fontSize: 10.5, color: 'var(--text-3)', marginTop: 2 }}>{l.sku}</div>}
                    </td>
                    <td className="tnum" style={{ padding: '11px 12px', textAlign: 'right' }}>{l.orderedQty}</td>
                    <td style={{ padding: '11px 12px', textAlign: 'right' }}>
                      {d.status === 'Черновик' && canReceive ? (
                        <input type="number" min={0} defaultValue={l.receivedQty}
                          onBlur={e => { const v = Math.max(0, +e.target.value || 0); if (v !== l.receivedQty) setQty.mutate({ lineId: l.id, qty: v }) }}
                          style={{ width: 64, textAlign: 'right', border: '1.5px solid var(--accent)', borderRadius: 6, padding: '4px 8px', fontSize: 12.5, fontFamily: 'inherit' }} />
                      ) : <span className="tnum">{l.receivedQty}</span>}
                    </td>
                    <td className="tnum" style={{ padding: '11px 12px', textAlign: 'right', fontWeight: 600, color: l.discrepancy === 0 ? 'var(--text-3)' : l.discrepancy < 0 ? 'var(--err)' : 'var(--warn)' }}>
                      {l.discrepancy > 0 ? '+' : ''}{l.discrepancy || 0}
                    </td>
                    <td style={{ padding: '11px 12px', textAlign: 'right' }}>
                      <StatusPill status={state} kind={l.discrepancy === 0 ? 'green' : l.discrepancy < 0 ? 'red' : 'amber'} />
                    </td>
                  </tr>
                )
              })}
              {detail.isLoading && <tr><td colSpan={5} style={{ padding: 16, color: 'var(--text-3)' }}>Загрузка…</td></tr>}
            </tbody>
          </table>
        </>
      )}
    </Shell>
  )
}
