import { useState } from 'react'
import { useParams, useNavigate, Link } from 'react-router-dom'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Shell } from '../components/Shell'
import { Btn, Icon, StatusPill } from '../ui/atoms'
import { PageLoader } from '../ui/feedback'
import { useApi } from '../api/useApi'
import { useRoles } from '../auth/useRoles'
import type { ItemMatchDto, MatchKind, MatchSuggestion, SpecMatchDto } from '../api/matching'

type Filter = 'all' | 'matched' | 'unmatched' | 'exact' | 'fuzzy' | 'analog' | 'none'

const KIND_LABEL: Record<MatchKind, string> = {
  None: 'нет',
  ExactSku: 'артикул',
  FuzzyName: 'ML · имя',
  Analog: 'аналог',
}

export function MatchResults() {
  const { id = '' } = useParams()
  const api = useApi()
  const nav = useNavigate()
  const qc = useQueryClient()
  const { canWrite } = useRoles()
  const [filter, setFilter] = useState<Filter>('all')
  const [err, setErr] = useState<string>()

  const match = useQuery({
    queryKey: ['spec-match', id],
    queryFn: () => api.get<SpecMatchDto>(`/api/specifications/${id}/match`),
  })

  const applyBest = useMutation({
    mutationFn: () => api.post<SpecMatchDto>(`/api/specifications/${id}/match/apply?minP=0.5`),
    onSuccess: (dto) => {
      qc.setQueryData(['spec-match', id], dto)
      qc.invalidateQueries({ queryKey: ['specification', id] })
      qc.invalidateQueries({ queryKey: ['specifications'] })
    },
    onError: (e) => setErr((e as Error).message),
  })

  const applyOne = useMutation({
    mutationFn: (p: { itemId: string; productId: string }) =>
      api.post<void>(`/api/specifications/${id}/match/apply-one`, p),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['spec-match', id] })
      qc.invalidateQueries({ queryKey: ['specification', id] })
    },
    onError: (e) => setErr((e as Error).message),
  })

  const data = match.data
  const items = data?.itemsDetail ?? []
  const topKind = (it: ItemMatchDto): MatchKind => it.suggestions[0]?.kind ?? 'None'
  const shown = items.filter(i => {
    if (filter === 'all') return true
    if (filter === 'matched') return i.matched
    if (filter === 'unmatched') return !i.matched
    if (filter === 'exact') return topKind(i) === 'ExactSku'
    if (filter === 'fuzzy') return topKind(i) === 'FuzzyName'
    if (filter === 'analog') return topKind(i) === 'Analog'
    return topKind(i) === 'None'
  })
  const pills: { id: Filter; label: string; count: number }[] = [
    { id: 'all', label: 'Все строки', count: items.length },
    { id: 'matched', label: 'Уже в заявке', count: data?.matched ?? 0 },
    { id: 'unmatched', label: 'Ещё без товара', count: data?.unmatched ?? 0 },
    { id: 'exact', label: 'Точный SKU', count: data?.exactHits ?? 0 },
    { id: 'fuzzy', label: 'По имени', count: data?.fuzzyHits ?? 0 },
    { id: 'analog', label: 'Аналог', count: data?.analogHits ?? 0 },
    { id: 'none', label: 'Нет гипотезы', count: items.filter(i => topKind(i) === 'None').length },
  ]

  return (
    <Shell breadcrumb={['Заявки', data?.title ?? '…', 'Сопоставление']} actions={<>
      {canWrite && <Btn size="sm" kind="default" disabled={applyBest.isPending || !data}
        onClick={() => { setErr(undefined); applyBest.mutate() }}>Применить лучшие (P≥0.5)</Btn>}
      <Btn size="sm" kind="primary" icon="scale" disabled={!data || data.items === 0}
        onClick={() => nav(`/specifications/${id}/quote`)}>Перейти к КП</Btn>
    </>}>
      <header className="page-head">
        <div style={{ display: 'flex', alignItems: 'center', gap: 10 }}>
          <div className="page-head-icon"><Icon name="search" size={18} stroke="var(--accent)" /></div>
          <h1 className="page-head-title">Сопоставление</h1>
          <Link to={`/specifications/${id}`} style={{ marginLeft: 'auto', color: 'var(--text-3)', fontSize: 13 }}>к заявке</Link>
        </div>
        <p className="page-head-sub">
          Точный артикул → TF-IDF + логрегрессия по имени → аналоги категории.
          {data && <> Каталог <b className="tnum">{data.catalogSize}</b>
            · модель на <b className="tnum">{data.trainedOn}</b>
            · {data.items} строк за <b className="tnum">{data.elapsedMs < 1000 ? `${data.elapsedMs} мс` : `${(data.elapsedMs / 1000).toFixed(1)} с`}</b>
            · артикул {data.exactHits} · ML {data.fuzzyHits} · аналог {data.analogHits}
          </>}
          {!data && 'Считаем по всему каталогу…'}
        </p>
      </header>

      <div style={{ display: 'flex', gap: 8, marginBottom: 16, flexWrap: 'wrap' }}>
        {pills.map(p => (
          <button key={p.id} type="button" onClick={() => setFilter(p.id)} className={'filter-chip' + (filter === p.id ? ' is-on' : '')}>
            {p.label}<span className="tnum" style={{ fontSize: 11 }}>{p.count}</span>
          </button>
        ))}
      </div>

      {match.isLoading && <PageLoader label="Считаем TF-IDF и вероятности" />}
      {err && <div style={{ color: 'var(--err)', background: 'var(--err-bg)', padding: '8px 12px', borderRadius: 7, marginBottom: 12, fontSize: 13 }}>{err}</div>}

      <div style={{ display: 'flex', flexDirection: 'column', gap: 10 }}>
        {shown.map(it => (
          <MatchCard key={it.itemId} item={it} canWrite={canWrite}
            applying={applyOne.isPending}
            onPick={s => { setErr(undefined); applyOne.mutate({ itemId: it.itemId, productId: s.productId }) }} />
        ))}
        {data && items.length === 0 && <div style={{ color: 'var(--text-3)', padding: 16 }}>В спецификации нет позиций.</div>}
      </div>
    </Shell>
  )
}

function MatchCard({ item, canWrite, applying, onPick }: {
  item: ItemMatchDto; canWrite: boolean; applying: boolean; onPick: (s: MatchSuggestion) => void
}) {
  const top = item.suggestions[0]
  return (
    <div className="panel" style={{ overflow: 'hidden' }}>
      <div style={{ background: 'var(--surface)', padding: '12px 16px', display: 'flex', alignItems: 'center', gap: 12 }}>
        <div style={{ flex: 1 }}>
          <div style={{ fontWeight: 500 }}>{item.name}</div>
          <div style={{ fontSize: 11.5, color: 'var(--text-3)', marginTop: 2 }}>
            {item.sku && <span className="mono">{item.sku} · </span>}кол-во {item.quantity}
          </div>
        </div>
        {item.matched
          ? <span style={{ display: 'inline-flex', alignItems: 'center', gap: 6, color: 'var(--ok)', fontSize: 12.5, fontWeight: 500 }}><Icon name="check" size={15} /> В заявке</span>
          : top
            ? <span className="tnum" style={{ color: top.probability >= 0.5 ? 'var(--ok)' : 'var(--warn)', fontSize: 12.5, fontWeight: 600 }}>P {top.probability.toFixed(2)}</span>
            : <span style={{ display: 'inline-flex', alignItems: 'center', gap: 6, color: 'var(--err)', fontSize: 12.5, fontWeight: 500 }}><Icon name="x" size={15} /> Пусто</span>}
      </div>

      {item.suggestions.length > 0 ? (
        <div style={{ background: item.matched ? 'var(--accent-soft)' : 'var(--surface-2)', padding: '10px 16px 12px' }}>
          {item.suggestions.map((s, i) => (
            <div key={s.productId} className="match-suggest" style={{
              padding: '8px 0', borderTop: i === 0 ? 'none' : '1px solid var(--border)', fontSize: 12.5,
            }}>
              <div>
                <div style={{ fontWeight: 500 }}>{s.name}</div>
                <div style={{ fontSize: 11.5, color: 'var(--text-3)', marginTop: 2 }}>
                  <span className="mono">{s.sku}</span>
                  {s.manufacturer && <> · {s.manufacturer}</>}
                  {s.category && <> · {s.category}</>}
                  <span style={{ marginLeft: 8 }}>{s.reason}</span>
                </div>
              </div>
              <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
                <StatusPill status={KIND_LABEL[s.kind]} kind={s.kind === 'ExactSku' ? 'green' : s.kind === 'Analog' ? 'amber' : 'blue'} />
                <span className="tnum" style={{ fontWeight: 600, color: s.probability >= 0.5 ? 'var(--ok)' : 'var(--warn)' }}>
                  P {s.probability.toFixed(2)}
                </span>
              </div>
              {canWrite && (
                <Btn size="sm" kind={i === 0 ? 'primary' : 'ghost'} disabled={applying || item.currentProductId === s.productId}
                  onClick={() => onPick(s)}>{item.currentProductId === s.productId ? 'выбрано' : 'взять'}</Btn>
              )}
            </div>
          ))}
        </div>
      ) : (
        <div style={{ background: 'var(--err-bg)', padding: '12px 16px', fontSize: 12.5, color: 'var(--text-2)' }}>
          Модель не нашла кандидата в каталоге — позиция не попадёт в итог КП, пока не добавите товар вручную.
        </div>
      )}
      {top && !item.matched && top.probability < 0.5 && (
        <div style={{ padding: '8px 16px', fontSize: 12, color: 'var(--warn)', background: 'var(--warn-bg)' }}>
          Лучшая гипотеза ниже порога 0.5 — подтвердите вручную, если это тот товар.
        </div>
      )}
    </div>
  )
}
