import { useState } from 'react'
import { useParams, useNavigate, Link } from 'react-router-dom'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { Shell } from '../components/Shell'
import { Btn, Icon, StatusPill } from '../ui/atoms'
import { SuggestBox } from '../ui/suggest'
import { useApi } from '../api/useApi'
import { useRoles } from '../auth/useRoles'
import { fmtMoneyShort } from '../data/constants'
import type { SpecificationDetailDto, SpecificationItemDto } from '../api/types'
import type { CatalogSuggestDto } from '../api/catalog'

export function SpecificationDetail() {
  const { id = '' } = useParams()
  const api = useApi()
  const qc = useQueryClient()
  const nav = useNavigate()
  const { canWrite } = useRoles()

  const [search, setSearch] = useState('')
  const [qty, setQty] = useState(1)

  const spec = useQuery({
    queryKey: ['specification', id],
    queryFn: () => api.get<SpecificationDetailDto>(`/api/specifications/${id}`),
  })

  const q = search.trim()
  const suggest = useQuery({
    queryKey: ['catalog-suggest', q],
    queryFn: () => api.get<CatalogSuggestDto[]>(`/api/catalog/suggest?q=${encodeURIComponent(q)}&limit=8`),
    enabled: canWrite && q.length >= 2,
  })

  const invalidate = () => {
    qc.invalidateQueries({ queryKey: ['specification', id] })
    qc.invalidateQueries({ queryKey: ['specifications'] })
  }

  const addItem = useMutation({
    mutationFn: (body: { productId?: string; sku?: string; name: string; quantity: number }) =>
      api.post<SpecificationItemDto>(`/api/specifications/${id}/items`, body),
    onSuccess: () => { setSearch(''); setQty(1); invalidate() },
  })
  const delItem = useMutation({
    mutationFn: (itemId: string) => api.del<void>(`/api/specifications/${id}/items/${itemId}`),
    onSuccess: invalidate,
  })

  const data = spec.data
  const matched = data?.items.filter(i => i.matched).length ?? 0

  return (
    <Shell breadcrumb={['Заявки', data?.title ?? '…']} actions={<>
      <Btn size="sm" kind="default" icon="search" disabled={!data || data.items.length === 0}
        onClick={() => nav(`/specifications/${id}/match`)}>Сопоставление</Btn>
      <Btn size="sm" kind="primary" icon="scale" disabled={!data || data.items.length === 0}
        onClick={() => nav(`/specifications/${id}/quote`)}>Сформировать КП</Btn>
    </>}>
      <header className="page-head">
        <div style={{ display: 'flex', alignItems: 'center', gap: 10, marginBottom: 2 }}>
          <h1 className="page-head-title">{data?.title ?? 'Спецификация'}</h1>
          <Link to="/specifications" style={{ marginLeft: 'auto', color: 'var(--text-3)', fontSize: 12.5 }}>к списку</Link>
        </div>
        <p className="page-head-sub" style={{ marginLeft: 0 }}>
          {data?.customer ? <>Заказчик: <b style={{ color: 'var(--text)' }}>{data.customer}</b> · </> : null}
          позиций <b className="tnum" style={{ color: 'var(--text)' }}>{data?.items.length ?? 0}</b> ·
          сопоставлено <b className="tnum" style={{ color: matched === (data?.items.length ?? 0) ? 'var(--ok)' : 'var(--warn)' }}> {matched}</b>
        </p>
      </header>

      <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: 13 }}>
        <thead>
          <tr>{['Наименование', 'Артикул', 'Кол-во', 'Сопоставление', ''].map((h, i) => (
            <th key={h} style={{ padding: '10px 12px', textAlign: i === 2 ? 'right' : 'left', fontWeight: 500, fontSize: 11, color: 'var(--text-2)', textTransform: 'uppercase', letterSpacing: 0.6, borderBottom: '1px solid var(--border)' }}>{h}</th>
          ))}</tr>
        </thead>
        <tbody>
          {data?.items.map(it => (
            <tr key={it.id} style={{ borderBottom: '1px solid var(--border)' }}>
              <td style={{ padding: '11px 12px', fontWeight: 500 }}>{it.name}</td>
              <td className="mono" style={{ padding: '11px 12px', fontSize: 11.5, color: 'var(--text-2)' }}>{it.sku ?? '—'}</td>
              <td className="tnum" style={{ padding: '11px 12px', textAlign: 'right' }}>{it.quantity}</td>
              <td style={{ padding: '11px 12px' }}>
                {it.matched
                  ? <StatusPill status="Сопоставлено" kind="green"/>
                  : <StatusPill status="Не сопоставлено" kind="amber"/>}
              </td>
              <td style={{ padding: '11px 12px', textAlign: 'right' }}>
                {canWrite && <button onClick={() => delItem.mutate(it.id)} title="Удалить" style={{ background: 'transparent', border: 'none', color: 'var(--text-3)', cursor: 'pointer', padding: 4 }}><Icon name="x" size={14}/></button>}
              </td>
            </tr>
          ))}
          {data && data.items.length === 0 && <tr><td colSpan={5} style={{ padding: 16, color: 'var(--text-3)' }}>Нет позиций. Начните печатать артикул или имя — подсказка из каталога.</td></tr>}
        </tbody>
      </table>

      {canWrite && (
        <div style={{ marginTop: 18, background: 'var(--surface)', border: '1px solid var(--border)', padding: 16, maxWidth: 720 }}>
          <div className="field-label">Добавить позицию</div>
          <div style={{ display: 'flex', gap: 8, alignItems: 'center' }}>
            <div style={{ flex: 1, display: 'flex', alignItems: 'center', gap: 8, background: 'var(--surface)', border: '1px solid var(--border-strong)', padding: '7px 12px' }}>
              <Icon name="search" size={15} stroke="var(--text-3)"/>
              <SuggestBox
                value={search}
                onChange={setSearch}
                items={suggest.data ?? []}
                loading={suggest.isFetching}
                placeholder="Артикул или название — подсказка из каталога"
                onPick={p => addItem.mutate({ productId: p.id, sku: p.sku, name: p.name, quantity: qty })}
                render={p => (
                  <>
                    <span className="mono" style={{ width: 92, color: 'var(--text-3)', flexShrink: 0 }}>{p.sku}</span>
                    <span style={{ flex: 1, fontWeight: 500 }}>{p.name}</span>
                    {p.minPrice != null && <span className="tnum" style={{ color: 'var(--text-2)' }}>{fmtMoneyShort(p.minPrice)}</span>}
                  </>
                )}
              />
            </div>
            <input type="number" min={1} value={qty} onChange={e => setQty(Math.max(1, +e.target.value || 1))}
              title="Количество" style={{ width: 72, border: '1px solid var(--border-strong)', padding: '8px 10px', fontSize: 13.5, fontFamily: 'inherit' }}/>
            <Btn size="sm" kind="default" disabled={q.length < 2 || addItem.isPending}
              onClick={() => addItem.mutate({ name: q, sku: q, quantity: qty })}>
              Как есть
            </Btn>
          </div>
          <div style={{ marginTop: 8, fontSize: 12, color: 'var(--text-3)' }}>Enter — взять подсказку. «Как есть» — строка без сопоставления, его доберёт экран матчинга.</div>
        </div>
      )}
    </Shell>
  )
}
