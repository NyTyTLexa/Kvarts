import { useQuery } from '@tanstack/react-query'
import { Shell } from '../../components/Shell'
import { PageHead } from '../../components/PageHead'
import { BarChart, HBarChart, PageLoader } from '../../ui/feedback'
import { fmtMoneyShort } from '../../data/constants'
import { useApi } from '../../api/useApi'

type AnalyticsSummary = {
  projects: number
  specifications: number
  specificationItems: number
  matchedItems: number
  unmatchedItems: number
  approvals: number
  invoices: number
  orders: number
  goodsReceipts: number
  costTotal: number
  sellTotal: number
  averageMarginPercent: number
  topVendors: { vendorName: string; lines: number; totalAmount: number }[]
  monthly: { year: number; month: number; orders: number; totalAmount: number }[]
  recentEvents: { occurredAtUtc: string; userName?: string; action: string; path: string; statusCode: number }[]
}

const TONE: Record<string, string> = { text: 'var(--text)', ok: 'var(--ok)', warn: 'var(--warn)', err: 'var(--err)' }

function statusColor(code: number) {
  if (code >= 500) return 'var(--err)'
  if (code >= 400) return 'var(--warn)'
  if (code >= 200 && code < 300) return 'var(--ok)'
  return 'var(--text-2)'
}

function monthLabel(year: number, month: number) {
  return new Date(year, month - 1, 1).toLocaleDateString('ru-RU', { month: 'short', year: '2-digit' })
}

export function Analytics() {
  const api = useApi()
  const summary = useQuery({
    queryKey: ['analytics-summary'],
    queryFn: () => api.get<AnalyticsSummary>('/api/analytics/summary'),
  })

  const data = summary.data
  const kpis = data ? [
    { label: 'Сумма КП в работе', value: fmtMoneyShort(data.sellTotal ?? 0), tone: 'text' },
    { label: 'Средняя маржа', value: `${(data.averageMarginPercent ?? 0).toLocaleString('ru-RU')}%`, tone: (data.averageMarginPercent ?? 0) > 0 ? 'ok' : 'warn' },
    { label: 'КП / согласования', value: (data.approvals ?? 0).toLocaleString('ru-RU'), tone: 'text' },
    { label: 'Несопоставлено', value: (data.unmatchedItems ?? 0).toLocaleString('ru-RU'), tone: (data.unmatchedItems ?? 0) > 0 ? 'warn' : 'ok' },
  ] : []

  return (
    <Shell breadcrumb={['Сводка']}>
      <PageHead kicker="система · цифры" title="Сводка"
        subtitle="Проекты, КП, счета и поставщики по текущим данным." />

      {summary.isLoading && <PageLoader label="Собираем сводку" />}
      {summary.isError && <div style={{ color: 'var(--err)', background: 'var(--err-bg)', padding: 14, borderRadius: 8, fontSize: 13 }}>
        Не удалось загрузить сводку{(summary.error as Error)?.message ? `: ${(summary.error as Error).message}` : '.'}
      </div>}

      {data && (
        <>
          <div className="stat-grid-4" style={{ marginBottom: 18 }}>
            {kpis.map((k, i) => (
              <div key={k.label} className="panel" style={{ padding: '12px 14px', background: i === 0 ? 'var(--accent-soft)' : 'var(--surface)' }}>
                <div className="field-label">{k.label}</div>
                <div className="tnum" style={{ fontSize: 20, fontWeight: 700, marginTop: 4, color: k.tone === 'text' ? undefined : TONE[k.tone] }}>{k.value}</div>
              </div>
            ))}
          </div>

          <div className="stat-grid-5" style={{ marginBottom: 26 }}>
            {[
              ['Проекты', data.projects],
              ['Спецификации', data.specifications],
              ['Позиций', data.specificationItems],
              ['Счета NOC', data.invoices],
              ['Приемки', data.goodsReceipts],
            ].map(([label, value]) => (
              <div key={label} style={{ padding: '10px 12px', background: 'var(--surface)', border: '1px solid var(--border)', borderRadius: 10 }}>
                <div className="field-label">{label}</div>
                <div className="tnum" style={{ fontSize: 18, fontWeight: 600 }}>{Number(value).toLocaleString('ru-RU')}</div>
              </div>
            ))}
          </div>

          <div className="split-2">
            <div>
              <div style={{ fontSize: 14, fontWeight: 600, marginBottom: 12 }}>Топ поставщиков по заказам</div>
              {(data.topVendors?.length ?? 0) > 0
                ? <HBarChart items={(data.topVendors ?? []).map(v => ({ label: v.vendorName, value: v.totalAmount, hint: `${fmtMoneyShort(v.totalAmount)} · ${v.lines}` }))} />
                : <div style={{ color: 'var(--text-3)', fontSize: 13 }}>Заказов с поставщиками пока нет.</div>}
            </div>

            <div>
              <div style={{ fontSize: 14, fontWeight: 600, marginBottom: 12 }}>Динамика заказов по месяцам</div>
              {(data.monthly?.length ?? 0) > 0
                ? <BarChart items={(data.monthly ?? []).map(m => ({ label: monthLabel(m.year, m.month), value: m.totalAmount }))} />
                : <div style={{ color: 'var(--text-3)', fontSize: 13 }}>Заказов пока нет.</div>}
            </div>
          </div>

          <div style={{ marginTop: 28 }}>
            <div style={{ fontSize: 14, fontWeight: 600, marginBottom: 12 }}>Последние события</div>
            <div style={{ display: 'flex', flexDirection: 'column' }}>
              {(data.recentEvents ?? []).map((e, i) => (
                <div key={`${e.occurredAtUtc}-${i}`} style={{ display: 'flex', gap: 10, padding: '8px 0', borderBottom: '1px solid var(--border)', fontSize: 12.5 }}>
                  <span style={{ width: 138, flexShrink: 0, color: 'var(--text-3)', fontVariantNumeric: 'tabular-nums' }}>{new Date(e.occurredAtUtc).toLocaleString('ru-RU')}</span>
                  <span style={{ width: 90, color: statusColor(e.statusCode), fontWeight: 600 }}>{e.statusCode}</span>
                  <span style={{ flex: 1 }}><b style={{ fontWeight: 500 }}>{e.userName ?? 'system'}</b> · {e.action} <span style={{ color: 'var(--text-2)' }}>{e.path}</span></span>
                </div>
              ))}
              {(data.recentEvents?.length ?? 0) === 0 && <div style={{ color: 'var(--text-3)', fontSize: 13 }}>Событий пока нет.</div>}
            </div>
          </div>
        </>
      )}
    </Shell>
  )
}
