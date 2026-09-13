import { useMemo, useState, type FormEvent } from 'react'
import { useNavigate, useSearchParams } from 'react-router-dom'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Shell } from '../components/Shell'
import { PageHead } from '../components/PageHead'
import { Btn, StatusPill } from '../ui/atoms'
import { inputStyle } from '../ui/styles'
import { PageLoader } from '../ui/feedback'
import { useApi } from '../api/useApi'
import { useRoles } from '../auth/useRoles'
import { fmtMoney } from '../data/constants'

export interface RetailHitDto {
  shop: string; host: string; name: string; brand?: string; sku?: string
  price: number; currency: string; inStock: boolean; url: string; source: string
}
export interface RetailShopDto {
  host: string; displayName: string; kind: string
  lastSuccessUtc?: string; hitCount: number; lastError?: string
}
export interface RetailSearchResult {
  query: string; hits: RetailHitDto[]; shops: RetailShopDto[]; errors: string[]
}
export interface RetailImportResult {
  vendorsCreated: number; productsCreated: number; offersUpserted: number; errors: string[]
}

export function Retail() {
  const api = useApi()
  const nav = useNavigate()
  const qc = useQueryClient()
  const { canWrite } = useRoles()
  const [params, setParams] = useSearchParams()
  const [input, setInput] = useState(params.get('q') ?? '')
  const [url, setUrl] = useState('')
  const [wb, setWb] = useState(true)
  const [discover, setDiscover] = useState(false)
  const [known, setKnown] = useState(false)
  const [picked, setPicked] = useState<Record<string, boolean>>({})
  const [result, setResult] = useState<RetailSearchResult | null>(null)
  const [importMsg, setImportMsg] = useState<string>()

  const shops = useQuery({
    queryKey: ['retail-shops'],
    queryFn: () => api.get<RetailShopDto[]>('/api/retail/shops'),
  })

  const search = useMutation({
    mutationFn: () => api.post<RetailSearchResult>('/api/retail/search', {
      q: input.trim(),
      url: url.trim() || null,
      wildberries: wb,
      discover,
      knownShops: known,
      limit: 16,
    }),
    onSuccess: (r) => {
      setResult(r)
      setPicked(Object.fromEntries(r.hits.map(h => [hitKey(h), true])))
      qc.invalidateQueries({ queryKey: ['retail-shops'] })
    },
  })

  const importHits = useMutation({
    mutationFn: (hits: RetailHitDto[]) => api.post<RetailImportResult>('/api/retail/import', { hits }),
    onSuccess: (r) => {
      setImportMsg(`Записано: поставщиков +${r.vendorsCreated}, товаров +${r.productsCreated}, офферов ${r.offersUpserted}.`)
      qc.invalidateQueries({ queryKey: ['ecatalog'] })
      qc.invalidateQueries({ queryKey: ['products'] })
    },
    onError: (e) => setImportMsg((e as Error).message),
  })

  const selected = useMemo(
    () => (result?.hits ?? []).filter(h => picked[hitKey(h)]),
    [result, picked],
  )

  function submit(e: FormEvent) {
    e.preventDefault()
    setImportMsg(undefined)
    setParams(input.trim() ? { q: input.trim() } : {})
    search.mutate()
  }

  const shopList = result?.shops ?? shops.data ?? []

  return (
    <Shell breadcrumb={['Система', 'Сверка цен']}>
      <PageHead kicker="система · сверка" title="Розница"
        subtitle="Разовая цена по модели. Чужие КП в каталог не тащим — это не заявка заказчика." />

      <form onSubmit={submit} style={{ display: 'grid', gap: 10, marginBottom: 16 }}>
        <div style={{ display: 'flex', gap: 8, flexWrap: 'wrap' }}>
          <input value={input} onChange={e => setInput(e.target.value)} placeholder="Модель, артикул…"
            style={{ ...inputStyle, flex: 1, minWidth: 240 }} />
          <Btn kind="primary" size="md" type="submit" disabled={search.isPending || (!input.trim() && !url.trim())}>
            Найти
          </Btn>
        </div>
        <input value={url} onChange={e => setUrl(e.target.value)} placeholder="Или вставьте URL карточки — снимем JSON-LD"
          style={inputStyle} />
        <div style={{ display: 'flex', gap: 16, flexWrap: 'wrap', fontSize: 13, color: 'var(--text-2)' }}>
          <label><input type="checkbox" checked={wb} onChange={e => setWb(e.target.checked)} /> Wildberries</label>
          <label><input type="checkbox" checked={discover} onChange={e => setDiscover(e.target.checked)} /> Найти магазины в интернете</label>
          <label><input type="checkbox" checked={known} onChange={e => setKnown(e.target.checked)} /> Известные витрины</label>
        </div>
      </form>

      {search.isError && <div style={{ color: 'var(--err)', background: 'var(--err-bg)', padding: 12, borderRadius: 8, marginBottom: 12 }}>{(search.error as Error).message}</div>}
      {importMsg && <div style={{ color: 'var(--ok)', background: 'var(--ok-bg)', padding: 12, borderRadius: 8, marginBottom: 12, fontSize: 13 }}>{importMsg}</div>}

      <div className="ecatalog-layout">
        <aside>
          <div style={{ fontSize: 11, fontWeight: 600, color: 'var(--text-2)', textTransform: 'uppercase', letterSpacing: 0.6, marginBottom: 8 }}>Магазины</div>
          {shopList.length === 0 && <div style={{ fontSize: 12.5, color: 'var(--text-3)' }}>Появятся после первого поиска.</div>}
          {shopList.map(s => (
            <div key={s.host} style={{ padding: '7px 10px', marginBottom: 2, borderRadius: 7, fontSize: 13 }}>
              <div style={{ display: 'flex', gap: 6, alignItems: 'center' }}>
                <span style={{ flex: 1, fontWeight: 500 }}>{s.displayName}</span>
                <StatusPill status={s.kind === 'Known' ? 'известен' : 'найден'} kind={s.kind === 'Known' ? 'blue' : 'amber'} />
              </div>
              <div className="mono" style={{ fontSize: 11, color: 'var(--text-3)', marginTop: 2 }}>{s.host}{s.hitCount ? ` · ${s.hitCount}` : ''}</div>
              {s.lastError && <div style={{ fontSize: 11, color: 'var(--warn)', marginTop: 2 }}>{s.lastError}</div>}
            </div>
          ))}
        </aside>

        <div>
          {search.isPending && <PageLoader label="Ищем по запросу" />}

          {result && !search.isPending && (
            <>
              <div style={{ display: 'flex', alignItems: 'center', gap: 10, marginBottom: 10, flexWrap: 'wrap' }}>
                <div style={{ fontSize: 12.5, color: 'var(--text-2)' }}>
                  Найдено <b className="tnum" style={{ color: 'var(--text)' }}>{result.hits.length}</b>
                  {selected.length > 0 && <> · выбрано {selected.length}</>}
                </div>
                <div style={{ flex: 1 }} />
                {canWrite
                  ? <Btn kind="primary" size="sm" disabled={selected.length === 0 || importHits.isPending}
                      onClick={() => importHits.mutate(selected)}>
                      Записать в каталог
                    </Btn>
                  : <span style={{ fontSize: 12.5, color: 'var(--text-3)' }}>Импорт — роль РП или admin</span>}
                <Btn size="sm" kind="default" onClick={() => nav('/ecatalog')}>Открыть каталог</Btn>
              </div>

              {result.errors.length > 0 && (
                <div style={{ background: 'var(--warn-bg)', color: 'var(--warn-fg)', padding: 10, borderRadius: 6, marginBottom: 12, fontSize: 12.5 }}>
                  {result.errors.map(e => <div key={e}>{e}</div>)}
                </div>
              )}

              {result.hits.length === 0 && (
                <div style={{ padding: 28, textAlign: 'center', color: 'var(--text-3)', background: 'var(--surface)', border: '1px dashed var(--border-strong)', borderRadius: 12 }}>
                  Ничего не сняли. Попробуйте другой запрос, вставьте URL карточки или снимите «известные витрины» (их часто режет антибот).
                </div>
              )}

              {result.hits.length > 0 && (
                <div style={{ overflowX: 'auto', background: 'var(--surface)', border: '1px solid var(--border)', borderRadius: 10 }}>
                  <table style={{ width: '100%', fontSize: 13 }}>
                    <thead>
                      <tr>
                        {['', 'Магазин', 'Товар', 'Цена', 'Наличие', 'Откуда', ''].map((h, i) => (
                          <th key={h + i} style={{ padding: '8px 12px', textAlign: i === 3 ? 'right' : 'left', fontWeight: 500, fontSize: 11, color: 'var(--text-2)', textTransform: 'uppercase', letterSpacing: 0.5, borderBottom: '1px solid var(--border)' }}>{h}</th>
                        ))}
                      </tr>
                    </thead>
                    <tbody>
                      {result.hits.map(h => (
                        <tr key={hitKey(h)}>
                          <td style={{ padding: '10px 12px' }}>
                            <input type="checkbox" checked={!!picked[hitKey(h)]}
                              onChange={e => setPicked(p => ({ ...p, [hitKey(h)]: e.target.checked }))} />
                          </td>
                          <td style={{ padding: '10px 12px' }}>
                            <div style={{ fontWeight: 600 }}>{h.shop}</div>
                            <div className="mono" style={{ fontSize: 11, color: 'var(--text-3)' }}>{h.host}</div>
                          </td>
                          <td style={{ padding: '10px 12px' }}>
                            <div style={{ fontWeight: 500 }}>{h.name}</div>
                            <div style={{ fontSize: 11.5, color: 'var(--text-3)' }}>{h.brand}{h.sku ? ` · ${h.sku}` : ''}</div>
                          </td>
                          <td className="tnum" style={{ padding: '10px 12px', textAlign: 'right', fontWeight: 600 }}>{fmtMoney(h.price)}</td>
                          <td style={{ padding: '10px 12px' }}>
                            <StatusPill status={h.inStock ? 'в наличии' : 'нет'} kind={h.inStock ? 'green' : 'gray'} />
                          </td>
                          <td style={{ padding: '10px 12px', color: 'var(--text-2)', fontSize: 12 }}>{h.source}</td>
                          <td style={{ padding: '10px 12px', textAlign: 'right' }}>
                            <a href={h.url} target="_blank" rel="noreferrer" style={{ color: 'var(--accent)', fontSize: 12 }}>карточка</a>
                          </td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              )}
            </>
          )}

          {!result && !search.isPending && (
            <div style={{ padding: 28, color: 'var(--text-2)', background: 'var(--surface)', border: '1px solid var(--border)', borderRadius: 12, fontSize: 13.5, lineHeight: 1.55 }}>
              Сверка розничной цены по модели, если нужно. Коммерческое предложение здесь не ищем и не скачиваем:
              КП собирается из заявки и прайса поставщика в разделе «Заявки».
            </div>
          )}
        </div>
      </div>
    </Shell>
  )
}

function hitKey(h: RetailHitDto) {
  return `${h.host}|${h.sku || h.url}`
}
