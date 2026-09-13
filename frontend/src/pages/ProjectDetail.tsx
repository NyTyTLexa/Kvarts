import { useState } from 'react'
import { Link, useNavigate, useParams } from 'react-router-dom'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Shell } from '../components/Shell'
import { PageHead } from '../components/PageHead'
import { Btn, LifecycleBar, StatusPill } from '../ui/atoms'
import { Field } from '../ui/form'
import { inputStyle } from '../ui/styles'
import { PageLoader } from '../ui/feedback'
import { useApi } from '../api/useApi'
import { useRoles } from '../auth/useRoles'
import { fmtMoney, fmtMoneyShort } from '../data/constants'
import { STRATEGY_META, type QuoteStrategy } from '../data/orders'
import type { ProjectDto } from '../api/projects'
import type { OrderDto, Quote, SpecificationDto } from '../api/types'
import type { ApprovalDto, InvoiceDto } from '../api/commercial'

const STATUS_STAGE: Record<string, string> = {
  'Черновик КП': 'draft',
  'Согласование РП': 'rp',
  'В коммерческом блоке': 'comm',
  'КП согласовано': 'invoice',
  'Счёт создан': 'invoice',
  'Ожидание оплаты': 'pay',
  'Оплачено': 'pay',
  'Ожидание поставки': 'ship',
  'Пришёл на склад': 'wh',
  'Отражено в 1С': '1c',
  'Отклонено': 'rp',
}

const ORDER_STRAT: QuoteStrategy[] = ['MinCost', 'MinLeadTime', 'Balanced', 'MlRelevance']

export function ProjectDetail() {
  const { id = '' } = useParams()
  const api = useApi()
  const nav = useNavigate()
  const qc = useQueryClient()
  const { canWrite } = useRoles()
  const [linkId, setLinkId] = useState('')
  const [err, setErr] = useState<string>()

  const project = useQuery({
    queryKey: ['project', id],
    queryFn: () => api.get<ProjectDto>(`/api/projects/${id}`),
  })
  const p = project.data
  const specId = p?.specificationId

  const specs = useQuery({
    queryKey: ['specs-all'],
    queryFn: () => api.get<SpecificationDto[]>('/api/specifications'),
    enabled: canWrite,
  })
  const quotes = useQuery({
    queryKey: ['quote-compare', specId],
    queryFn: () => api.get<Quote[]>(`/api/specifications/${specId}/quote/compare`),
    enabled: !!specId,
  })
  const approvals = useQuery({
    queryKey: ['approvals'],
    queryFn: () => api.get<ApprovalDto[]>('/api/approvals'),
    enabled: !!specId,
  })
  const invoices = useQuery({
    queryKey: ['invoices'],
    queryFn: () => api.get<InvoiceDto[]>('/api/invoices'),
    enabled: !!specId,
  })
  const orders = useQuery({
    queryKey: ['orders'],
    queryFn: () => api.get<OrderDto[]>('/api/orders'),
    enabled: !!specId,
  })

  const link = useMutation({
    mutationFn: (specificationId: string | null) =>
      api.post<void>(`/api/projects/${id}/link`, { specificationId }),
    onSuccess: () => {
      setLinkId('')
      qc.invalidateQueries({ queryKey: ['project', id] })
      qc.invalidateQueries({ queryKey: ['projects'] })
    },
    onError: e => setErr((e as Error).message),
  })

  const mineApprovals = (approvals.data ?? [])
    .filter(a => a.specificationId === specId)
    .sort((a, b) => +new Date(b.createdAtUtc) - +new Date(a.createdAtUtc))
  const lastAppr = mineApprovals[0]
  const mineInvoices = (invoices.data ?? []).filter(i => mineApprovals.some(a => a.id === i.approvalId))
  const mineOrders = (orders.data ?? []).filter(o => o.specificationId === specId)
  const vat = 20
  const cost = lastAppr?.costPrice ?? quotes.data?.find(q => q.strategy === 'Balanced')?.totalCost
  const sell = lastAppr?.sellPrice
  const markup = lastAppr?.markupPercent
  const margin = lastAppr?.marginPercent

  if (project.isLoading) {
    return <Shell breadcrumb={['Проекты', '…']}><PageLoader label="Открываем проект" /></Shell>
  }
  if (project.isError || !p) {
    return (
      <Shell breadcrumb={['Проекты', 'нет']}>
        <PageHead icon="folder" title="Проект не найден" />
        <Btn onClick={() => nav('/')}>К списку</Btn>
      </Shell>
    )
  }

  return (
    <Shell breadcrumb={['Проекты', p.name]} actions={<>
      {specId && <Btn size="sm" kind="ghost" onClick={() => nav(`/specifications/${specId}`)}>Заявка</Btn>}
      {specId && <Btn size="sm" kind="ghost" onClick={() => nav(`/specifications/${specId}/match`)}>Сопоставление</Btn>}
      {specId && <Btn size="sm" kind="primary" onClick={() => nav(`/specifications/${specId}/quote`)}>Четыре КП</Btn>}
    </>}>
      <header className="page-head">
        <div className="page-kicker">карточка проекта</div>
        <div style={{ display: 'flex', alignItems: 'center', gap: 12, flexWrap: 'wrap' }}>
          <h1 className="page-head-title">{p.name}</h1>
          <StatusPill status={p.status} />
        </div>
        <p className="page-head-sub">
          <span className="mono">{p.id.slice(0, 8)}</span>
          {' · '}создан {new Date(p.createdAtUtc).toLocaleDateString('ru-RU')}
          {p.rp ? ` · РП ${p.rp}` : ''}
          {p.specificationTitle ? ` · заявка «${p.specificationTitle}»` : ' · заявка не привязана'}
          {p.itemsCount ? ` · ${p.itemsCount} позиций` : ''}
        </p>
      </header>

      {err && <div style={{ color: 'var(--err)', background: 'var(--err-bg)', padding: '8px 12px', marginBottom: 14 }}>{err}</div>}

      <div style={{ marginBottom: 22 }}>
        <LifecycleBar current={STATUS_STAGE[p.status] ?? 'draft'} />
      </div>

      <div className="project-detail">
        <div>
          {!specId && (
            <div className="panel" style={{ padding: 22, marginBottom: 18, borderStyle: 'dashed' }}>
              <div style={{ fontSize: 16, fontWeight: 700, marginBottom: 8 }}>Нет заявки</div>
              <p style={{ color: 'var(--text-2)', margin: '0 0 14px' }}>Привяжите спецификацию — отсюда откроются сопоставление и четыре листа КП.</p>
              {canWrite && (
                <div style={{ display: 'flex', gap: 8, flexWrap: 'wrap' }}>
                  <select value={linkId} onChange={e => setLinkId(e.target.value)} style={{ ...inputStyle, width: 320, maxWidth: '100%' }}>
                    <option value="">— спецификация —</option>
                    {specs.data?.map(s => <option key={s.id} value={s.id}>{s.title}</option>)}
                  </select>
                  <Btn kind="primary" size="sm" disabled={!linkId || link.isPending}
                    onClick={() => { setErr(undefined); link.mutate(linkId) }}>Привязать</Btn>
                  <Btn size="sm" onClick={() => nav('/specifications')}>К заявкам</Btn>
                </div>
              )}
            </div>
          )}

          {specId && (
            <>
              <div className="field-label" style={{ marginBottom: 10 }}>Листы КП</div>
              {quotes.isLoading && <PageLoader label="Считаем четыре варианта" />}
              {quotes.isError && <div style={{ color: 'var(--err)', background: 'var(--err-bg)', padding: '8px 12px', marginBottom: 12, fontSize: 13 }}>
                Не удалось посчитать КП{(quotes.error as Error)?.message ? `: ${(quotes.error as Error).message}` : '.'}
              </div>}
              <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 12, marginBottom: 22 }}>
                {ORDER_STRAT.map((s, i) => {
                  const q = quotes.data?.find(x => x.strategy === s)
                  const meta = STRATEGY_META[s]
                  return (
                    <button key={s} type="button" disabled={!q}
                      className={'quote-tile quote-tile-' + i}
                      onClick={() => nav(`/specifications/${specId}/quote`)}
                      style={{ minHeight: 140, cursor: q ? 'pointer' : 'default' }}>
                      <div className="field-label">{meta.tag}</div>
                      <div className="tnum" style={{ fontSize: 20, fontWeight: 700, marginTop: 8 }}>
                        {q ? fmtMoneyShort(q.totalCost) : '…'}
                      </div>
                      {q && <div style={{ marginTop: 8, fontSize: 12.5, color: 'var(--text-2)' }}>{q.maxLeadTimeDays} дн · {q.vendorsUsed} пост.</div>}
                    </button>
                  )
                })}
              </div>

              <div className="field-label" style={{ marginBottom: 10 }}>Согласования</div>
              {mineApprovals.length === 0 && (
                <div className="panel" style={{ padding: 16, marginBottom: 18, color: 'var(--text-2)' }}>
                  Ещё нет карточки маржи.{' '}
                  <Link to="/approval" style={{ color: 'var(--accent)' }}>Открыть комблок</Link>
                </div>
              )}
              {mineApprovals.map(a => (
                <div key={a.id} className="panel" style={{
                  padding: '14px 16px', marginBottom: 10,
                  borderLeft: a.status === 'Согласовано' ? '6px solid var(--ok)' : a.status === 'Отклонено' ? '6px solid var(--err)' : '6px solid var(--accent)',
                }}>
                  <div style={{ display: 'flex', gap: 10, alignItems: 'center', flexWrap: 'wrap' }}>
                    <span style={{ fontWeight: 600 }}>{STRATEGY_META[a.strategy]?.tag ?? a.strategy}</span>
                    <StatusPill status={a.status === 'НаСогласованииРП' ? 'Согласование РП' : a.status === 'ВКоммерческомБлоке' ? 'В коммерческом блоке' : a.status} />
                    <span className="tnum" style={{ marginLeft: 'auto', fontWeight: 700 }}>{fmtMoneyShort(a.sellPrice)}</span>
                  </div>
                  <div style={{ fontSize: 12.5, color: 'var(--text-2)', marginTop: 6 }}>
                    наценка {a.markupPercent.toLocaleString('ru-RU')}% · маржа {a.marginPercent.toFixed(1).replace('.', ',')}%
                    {' · '}{new Date(a.createdAtUtc).toLocaleString('ru-RU')}
                  </div>
                </div>
              ))}

              {mineOrders.length > 0 && (
                <>
                  <div className="field-label" style={{ margin: '18px 0 10px' }}>Заказы</div>
                  {mineOrders.map(o => (
                    <Link key={o.id} to={`/orders/${o.id}`} className="panel" style={{
                      display: 'block', padding: '12px 16px', marginBottom: 8,
                    }}>
                      <span className="mono">{o.number}</span>
                      <span style={{ margin: '0 10px' }}>{STRATEGY_META[o.strategy as QuoteStrategy]?.tag ?? o.strategy}</span>
                      <span className="tnum" style={{ float: 'right', fontWeight: 700 }}>{fmtMoney(o.totalCost)}</span>
                    </Link>
                  ))}
                </>
              )}
            </>
          )}
        </div>

        <aside>
          <div className="panel" style={{ padding: 16, marginBottom: 14 }}>
            <div className="field-label" style={{ marginBottom: 12 }}>Финансы</div>
            <Row k="Себестоимость" v={cost == null ? '—' : fmtMoneyShort(cost)} />
            <Row k={markup == null ? 'Наценка' : `Наценка ${markup.toLocaleString('ru-RU')}%`}
              v={cost != null && sell != null ? '+ ' + fmtMoneyShort(sell - cost) : '—'} ok={margin != null && margin >= 20} />
            <div style={{ borderTop: '1px solid var(--border)', margin: '10px 0' }} />
            <Row k="Цена без НДС" v={sell == null ? '—' : fmtMoneyShort(sell)} strong />
            <Row k={`С НДС ${vat}%`} v={sell == null ? '—' : fmtMoneyShort(sell * (1 + vat / 100))} accent />
            {margin != null && <Row k="Маржа" v={margin.toFixed(1).replace('.', ',') + '%'} ok={margin >= 20} />}
          </div>

          {p.rp && (
            <div className="panel" style={{ padding: 16, marginBottom: 14 }}>
              <div className="field-label" style={{ marginBottom: 12 }}>Команда</div>
              <div style={{ display: 'flex', alignItems: 'center', gap: 10 }}>
                <div className="user-plate">{p.rp.slice(0, 2)}</div>
                <div>
                  <div style={{ fontWeight: 600 }}>{p.rp}</div>
                  <div style={{ fontSize: 12, color: 'var(--text-2)' }}>РП</div>
                </div>
              </div>
            </div>
          )}

          {mineInvoices[0] && (
            <Link to="/invoice" className="panel" style={{ display: 'block', padding: 16 }}>
              <div className="field-label">Счёт NOC</div>
              <div className="mono" style={{ fontSize: 16, fontWeight: 700, marginTop: 6 }}>{mineInvoices[0].number}</div>
              <div style={{ marginTop: 6 }}><StatusPill status={mineInvoices[0].status} /></div>
            </Link>
          )}

          {canWrite && specId && (
            <div style={{ marginTop: 14 }}>
              <Field label="Сменить заявку">
                <select defaultValue={specId} onChange={e => { setErr(undefined); link.mutate(e.target.value || null) }} style={inputStyle}>
                  {specs.data?.map(s => <option key={s.id} value={s.id}>{s.title}</option>)
                    ?? <option value={specId}>{p.specificationTitle}</option>}
                </select>
              </Field>
            </div>
          )}
        </aside>
      </div>
    </Shell>
  )
}

function Row({ k, v, strong, accent, ok }: { k: string; v: string; strong?: boolean; accent?: boolean; ok?: boolean }) {
  return (
    <div style={{ display: 'flex', gap: 12, fontSize: 13.5, marginBottom: 8 }}>
      <span style={{ flex: 1, color: 'var(--text-2)' }}>{k}</span>
      <span className="tnum" style={{
        fontWeight: strong ? 700 : 500,
        color: accent ? 'var(--accent)' : ok ? 'var(--ok)' : undefined,
      }}>{v}</span>
    </div>
  )
}
