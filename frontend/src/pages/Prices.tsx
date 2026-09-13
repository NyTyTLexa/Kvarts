import { useState, useRef } from 'react'
import { useNavigate } from 'react-router-dom'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { Shell } from '../components/Shell'
import { PageHead } from '../components/PageHead'
import { Btn, Icon } from '../ui/atoms'
import { inputStyle } from '../ui/styles'
import { SuggestBox } from '../ui/suggest'
import { useApi } from '../api/useApi'
import { useRoles } from '../auth/useRoles'
import { fmt } from '../data/constants'
import type { Paged, ProductDto, VendorDto, OfferDto, PriceHistoryDto, PriceImportResult } from '../api/types'
import type { CatalogSuggestDto } from '../api/catalog'
import type { PriceImportUploadDto } from '../api/priceImports'

type CorpusResult = {
  vendors: number; products: number; offers: number; uploads: number
  specificationId: string; specItems: number; specUnmatched: number
  elapsedMs: number
}

export function Prices() {
  const api = useApi()
  const qc = useQueryClient()
  const nav = useNavigate()
  const { canWrite, canAdmin } = useRoles()
  const fileRef = useRef<HTMLInputElement>(null)

  const [search, setSearch] = useState('')
  const [product, setProduct] = useState<ProductDto | null>(null)
  const [importVendor, setImportVendor] = useState('')
  const [newVendorName, setNewVendorName] = useState('')
  const [importResult, setImportResult] = useState<PriceImportResult | null>(null)
  const [corpus, setCorpus] = useState<CorpusResult | null>(null)
  const [err, setErr] = useState<string>()

  // справочник поставщиков для выпадающих списков
  const vendors = useQuery({ queryKey: ['vendors', 'all'], queryFn: () => api.get<Paged<VendorDto>>('/api/vendors?pageSize=200') })

  const products = useQuery({
    queryKey: ['catalog-suggest', search.trim()],
    queryFn: () => api.get<CatalogSuggestDto[]>(`/api/catalog/suggest?q=${encodeURIComponent(search.trim())}&limit=8`),
    enabled: search.trim().length >= 2 && !product,
  })

  const offers = useQuery({
    queryKey: ['offers', product?.id],
    queryFn: () => api.get<OfferDto[]>(`/api/pricelist/by-product/${product!.id}`),
    enabled: !!product,
  })
  const history = useQuery({
    queryKey: ['price-history', product?.id],
    queryFn: () => api.get<PriceHistoryDto[]>(`/api/pricelist/history/${product!.id}`),
    enabled: !!product,
  })

  const importXlsx = useMutation({
    mutationFn: async (file: File) => {
      let vendorId = importVendor
      if (!vendorId) {
        const name = newVendorName.trim() || file.name.replace(/\.(xlsx|xls)$/i, '') || 'Поставщик'
        const created = await api.post<VendorDto>('/api/vendors', { name, inn: null, defaultLeadTimeDays: 14 })
        vendorId = created.id
        setImportVendor(created.id)
        qc.invalidateQueries({ queryKey: ['vendors'] })
      }
      const fd = new FormData()
      fd.append('file', file)
      fd.append('vendorId', vendorId)
      return api.post<PriceImportResult>(`/api/pricelist/import?vendorId=${vendorId}`, fd)
    },
    onSuccess: (r) => {
      setImportResult(r)
      qc.invalidateQueries({ queryKey: ['offers'] })
      qc.invalidateQueries({ queryKey: ['price-history'] })
      qc.invalidateQueries({ queryKey: ['price-uploads'] })
      qc.invalidateQueries({ queryKey: ['product-search'] })
    },
    onError: (e) => setErr((e as Error).message),
  })

  // Архив загрузок (ТЗ п.5.1): оригиналы файлов + история версий.
  const uploads = useQuery({ queryKey: ['price-uploads'], queryFn: () => api.get<PriceImportUploadDto[]>('/api/pricelist/uploads?limit=200') })

  const seedCorpus = useMutation({
    mutationFn: () => api.post<CorpusResult>('/api/pricelist/seed-corpus?lists=120&catalogSize=4000'),
    onSuccess: r => {
      setCorpus(r)
      qc.invalidateQueries({ queryKey: ['vendors'] })
      qc.invalidateQueries({ queryKey: ['price-uploads'] })
      qc.invalidateQueries({ queryKey: ['ecatalog'] })
      qc.invalidateQueries({ queryKey: ['specifications'] })
      qc.invalidateQueries({ queryKey: ['product-categories'] })
    },
    onError: e => setErr((e as Error).message),
  })

  // добавление одного оффера (фиксирует точку истории цены)
  const [offVendor, setOffVendor] = useState('')
  const [offPrice, setOffPrice] = useState('')
  const [offLead, setOffLead] = useState(14)
  const [offStock, setOffStock] = useState(0)
  const addOffer = useMutation({
    mutationFn: () => api.post<OfferDto>('/api/pricelist', {
      productId: product!.id, vendorId: offVendor, price: +offPrice, leadTimeDays: offLead, stockQuantity: offStock,
    }),
    onSuccess: () => { setOffPrice(''); qc.invalidateQueries({ queryKey: ['offers', product?.id] }); qc.invalidateQueries({ queryKey: ['price-history', product?.id] }) },
    onError: (e) => setErr((e as Error).message),
  })

  const vList = vendors.data?.items ?? []
  const vName = (id: string) => vList.find(v => v.id === id)?.name ?? id.slice(0, 8)

  return (
    <Shell breadcrumb={['Прайсы']} actions={canAdmin && (
      <Btn size="sm" kind="ghost" disabled={seedCorpus.isPending} onClick={() => { setErr(undefined); seedCorpus.mutate() }}>
        {seedCorpus.isPending ? 'Собираем корпус…' : 'Корпус для сопоставления'}
      </Btn>
    )}>
      <PageHead icon="tag" kicker="excel поставщиков" title="Прайсы"
        subtitle="Загрузка прайс-листов поставщиков в общий каталог." />

      {err && <div style={{ color: 'var(--err)', background: 'var(--err-bg)', padding: '8px 12px', borderRadius: 7, marginBottom: 14, fontSize: 13 }}>{err}</div>}
      {corpus && (
        <div style={{ color: 'var(--ok)', background: 'var(--ok-bg)', padding: '10px 12px', marginBottom: 14, fontSize: 13 }}>
          Залито прайсов: <b>{corpus.uploads}</b> · поставщиков {corpus.vendors} · позиций {corpus.products} · офферов {corpus.offers}
          {corpus.elapsedMs > 0 && <> · {Math.round(corpus.elapsedMs / 1000)} с</>}.
          Кривая заявка: {corpus.specItems} строк, из них {corpus.specUnmatched} ещё без привязки к каталогу — это вход для ML.{' '}
          <button type="button" onClick={() => nav(`/specifications/${corpus.specificationId}/match`)}
            style={{ background: 'none', border: 'none', color: 'var(--accent)', cursor: 'pointer', textDecoration: 'underline', padding: 0, fontSize: 13 }}>
            Открыть ML-сопоставление
          </button>
        </div>
      )}

      {/* Импорт прайса */}
      {canWrite && (
        <div style={{ background: 'var(--surface)', border: '1px solid var(--border)', borderRadius: 10, padding: 16, marginBottom: 22, maxWidth: 720, boxShadow: 'var(--shadow-sm)' }}>
          <div className="field-label">Импорт прайс-листа (Excel)</div>
          {vendors.isError && <div style={{ color: 'var(--err)', fontSize: 13, marginBottom: 10 }}>Не удалось загрузить поставщиков: {(vendors.error as Error).message}</div>}
          {vList.length === 0 && !vendors.isLoading && (
            <p style={{ margin: '0 0 10px', fontSize: 13, color: 'var(--text-2)' }}>
              Поставщиков ещё нет — укажите имя, и он создастся вместе с загрузкой файла.
            </p>
          )}
          <div style={{ display: 'flex', gap: 10, alignItems: 'center', flexWrap: 'wrap' }}>
            {vList.length > 0 && (
              <select value={importVendor} onChange={e => setImportVendor(e.target.value)} style={{ ...inputStyle, width: 260 }}>
                <option value="">— новый поставщик —</option>
                {vList.map(v => <option key={v.id} value={v.id}>{v.name}</option>)}
              </select>
            )}
            {!importVendor && (
              <input value={newVendorName} onChange={e => setNewVendorName(e.target.value)}
                placeholder="Имя поставщика" style={{ ...inputStyle, width: 240 }} />
            )}
            <Btn kind="primary" icon="upload" disabled={importXlsx.isPending} onClick={() => fileRef.current?.click()}>
              {importXlsx.isPending ? 'Загрузка…' : 'Выбрать .xlsx / .xls'}
            </Btn>
            <input ref={fileRef} type="file" accept=".xlsx,.xls,application/vnd.ms-excel,application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" hidden
              onChange={e => { const f = e.target.files?.[0]; if (f) { setErr(undefined); importXlsx.mutate(f) } e.target.value = '' }}/>
          </div>
          <div style={{ marginTop: 8, fontSize: 12, color: 'var(--text-3)' }}>
            Колонки по заголовкам: Артикул, Наименование/Номенклатура, Цена (без НДС). Опционально: Производитель, Категория, Срок, Остаток.
            Стенд — кнопка сверху: 120 поставщиков ПЛ-NNN, ~4 тыс. SKU в каталоге, кривая заявка на ~180 строк. Первый прогон 1–2 минуты, повтор ничего не плодит.
          </div>
          {importResult && (
            <div style={{ marginTop: 12, fontSize: 13, color: 'var(--ok)', background: 'var(--ok-bg)', padding: '8px 12px' }}>
              Обработано строк: <b>{importResult.rowsProcessed}</b> · новых товаров: <b>{importResult.productsCreated}</b> · офферов: <b>{importResult.offersUpserted}</b>
              {importResult.errors.length > 0 && <span style={{ color: 'var(--warn)' }}> · ошибок: {importResult.errors.length}</span>}
              {importResult.errors.length > 0 && (
                <ul style={{ margin: '8px 0 0', paddingLeft: 18, color: 'var(--text-2)', maxHeight: 140, overflow: 'auto' }}>
                  {importResult.errors.slice(0, 12).map((x, i) => <li key={i}>{x}</li>)}
                </ul>
              )}
            </div>
          )}
        </div>
      )}
      {!canWrite && (
        <div style={{ color: 'var(--warn)', background: 'var(--warn-bg)', padding: '10px 12px', marginBottom: 16, fontSize: 13 }}>
          Загрузка прайса доступна руководителю проекта (<span className="mono">manager</span>) и администратору.
        </div>
      )}

      {/* Архив загрузок (ТЗ п.5.1: оригиналы файлов + история версий) */}
      {uploads.data && uploads.data.length > 0 && (
        <div style={{ marginBottom: 22 }}>
          <div style={{ fontSize: 12, fontWeight: 600, marginBottom: 8, color: 'var(--text-2)', textTransform: 'uppercase', letterSpacing: 0.5 }}>История загрузок</div>
          <div className="doc-list" style={{ maxWidth: 900 }}>
          <table>
            <thead><tr>{['Файл', 'Поставщик', 'Загружено', 'Кто', 'Строк', 'Товаров', 'Офферов', 'Ошибок', ''].map((h, i) => (
              <th key={h} style={{ padding: '7px 10px', textAlign: i >= 4 && i <= 7 ? 'right' : 'left', fontWeight: 500, fontSize: 11, color: 'var(--text-2)', textTransform: 'uppercase', borderBottom: '1px solid var(--border)' }}>{h}</th>
            ))}</tr></thead>
            <tbody>
              {uploads.data.map(u => (
                <tr key={u.id} style={{ borderBottom: '1px solid var(--border)' }}>
                  <td className="mono" style={{ padding: '7px 10px', fontSize: 11.5 }}>{u.fileName}</td>
                  <td style={{ padding: '7px 10px' }}>{u.vendorName}</td>
                  <td style={{ padding: '7px 10px', color: 'var(--text-2)' }}>{new Date(u.uploadedAtUtc).toLocaleString('ru-RU')}</td>
                  <td style={{ padding: '7px 10px', color: 'var(--text-2)' }}>{u.uploadedBy ?? '—'}</td>
                  <td className="tnum" style={{ padding: '7px 10px', textAlign: 'right' }}>{u.rowsProcessed}</td>
                  <td className="tnum" style={{ padding: '7px 10px', textAlign: 'right' }}>{u.productsCreated}</td>
                  <td className="tnum" style={{ padding: '7px 10px', textAlign: 'right' }}>{u.offersUpserted}</td>
                  <td className="tnum" style={{ padding: '7px 10px', textAlign: 'right', color: u.errorsCount > 0 ? 'var(--warn)' : 'var(--text-3)' }}>{u.errorsCount || '—'}</td>
                  <td style={{ padding: '7px 10px', textAlign: 'right' }}>
                    <button onClick={() => api.download(`/api/pricelist/uploads/${u.id}/download`, u.fileName)}
                      title="Скачать оригинал" style={{ background: 'transparent', border: 'none', color: 'var(--text-3)', cursor: 'pointer', padding: 4 }}>
                      <Icon name="download" size={14}/>
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
          </div>
        </div>
      )}

      {/* Поиск товара */}
      <div style={{ fontSize: 12, fontWeight: 600, marginBottom: 8, color: 'var(--text-2)', textTransform: 'uppercase', letterSpacing: 0.5 }}>Цены по товару</div>
      <div style={{ display: 'flex', alignItems: 'center', gap: 8, background: 'var(--surface)', border: '1px solid var(--border-strong)', padding: '7px 12px', maxWidth: 520, marginBottom: 8 }}>
        <Icon name="search" size={15} stroke="var(--text-3)"/>
        <SuggestBox
          value={search}
          onChange={v => { setSearch(v); setProduct(null) }}
          items={products.data ?? []}
          loading={products.isFetching}
          placeholder="Артикул или название — подсказка из каталога"
          onPick={p => { setProduct({ id: p.id, sku: p.sku, name: p.name, manufacturer: p.manufacturer }); setSearch(p.name) }}
          render={p => (
            <>
              <span className="mono" style={{ width: 88, color: 'var(--text-3)', flexShrink: 0 }}>{p.sku}</span>
              <span style={{ flex: 1, fontWeight: 500 }}>{p.name}</span>
            </>
          )}
        />
      </div>

      {product && (
        <div style={{ marginTop: 8 }}>
          <div style={{ display: 'flex', alignItems: 'center', gap: 10, marginBottom: 14 }}>
            <span style={{ fontWeight: 600, fontSize: 15 }}>{product.name}</span>
            <span className="mono" style={{ fontSize: 11.5, color: 'var(--text-3)' }}>{product.sku}</span>
            <Btn size="sm" kind="ghost" onClick={() => setProduct(null)}>сбросить</Btn>
          </div>

          <div className="form-cols" style={{ gridTemplateColumns: '1fr 1fr', gap: 20 }}>
            {/* Офферы */}
            <div>
              <div style={{ fontSize: 13, fontWeight: 600, marginBottom: 8 }}>Предложения поставщиков</div>
              <div style={{ overflowX: 'auto' }}>
              <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: 12.5 }}>
                <thead><tr>{['Поставщик', 'Цена', 'Срок', 'Остаток'].map((h, i) => (
                  <th key={h} style={{ padding: '8px 10px', textAlign: i === 0 ? 'left' : 'right', fontWeight: 500, fontSize: 11, color: 'var(--text-2)', textTransform: 'uppercase', borderBottom: '1px solid var(--border)' }}>{h}</th>
                ))}</tr></thead>
                <tbody>
                  {offers.data?.slice().sort((a, b) => a.price - b.price).map(o => (
                    <tr key={o.id} style={{ borderBottom: '1px solid var(--border)' }}>
                      <td style={{ padding: '8px 10px', fontWeight: 500 }}>{o.vendorName}</td>
                      <td className="tnum" style={{ padding: '8px 10px', textAlign: 'right' }}>{fmt(o.price)} ₽</td>
                      <td className="tnum" style={{ padding: '8px 10px', textAlign: 'right', color: 'var(--text-2)' }}>{o.leadTimeDays} дн</td>
                      <td className="tnum" style={{ padding: '8px 10px', textAlign: 'right', color: 'var(--text-2)' }}>{o.stockQuantity}</td>
                    </tr>
                  ))}
                  {offers.data && offers.data.length === 0 && <tr><td colSpan={4} style={{ padding: 12, color: 'var(--text-3)' }}>Нет предложений</td></tr>}
                </tbody>
              </table>
              </div>

              {canWrite && (
                <div style={{ marginTop: 12, display: 'flex', gap: 6, flexWrap: 'wrap', alignItems: 'center' }}>
                  <select value={offVendor} onChange={e => setOffVendor(e.target.value)} style={{ ...inputStyle, width: 160, padding: '6px 8px' }}>
                    <option value="">поставщик…</option>
                    {vList.map(v => <option key={v.id} value={v.id}>{v.name}</option>)}
                  </select>
                  <input value={offPrice} onChange={e => setOffPrice(e.target.value)} placeholder="цена" inputMode="numeric" style={{ ...inputStyle, width: 90, padding: '6px 8px' }}/>
                  <input type="number" min={1} value={offLead} onChange={e => setOffLead(+e.target.value || 1)} title="срок" style={{ ...inputStyle, width: 64, padding: '6px 8px' }}/>
                  <input type="number" min={0} value={offStock} onChange={e => setOffStock(+e.target.value || 0)} title="остаток" style={{ ...inputStyle, width: 72, padding: '6px 8px' }}/>
                  <Btn size="sm" kind="primary" disabled={!offVendor || !offPrice || addOffer.isPending} onClick={() => { setErr(undefined); addOffer.mutate() }}>Добавить оффер</Btn>
                </div>
              )}
            </div>

            {/* История цены */}
            <div>
              <div style={{ fontSize: 13, fontWeight: 600, marginBottom: 8 }}>История цены</div>
              <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: 12.5 }}>
                <thead><tr>{['Дата', 'Поставщик', 'Цена', 'Срок'].map((h, i) => (
                  <th key={h} style={{ padding: '8px 10px', textAlign: i >= 2 ? 'right' : 'left', fontWeight: 500, fontSize: 11, color: 'var(--text-2)', textTransform: 'uppercase', borderBottom: '1px solid var(--border)' }}>{h}</th>
                ))}</tr></thead>
                <tbody>
                  {history.data?.map((h, i) => (
                    <tr key={i} style={{ borderBottom: '1px solid var(--border)' }}>
                      <td style={{ padding: '8px 10px', color: 'var(--text-2)' }}>{new Date(h.recordedAtUtc).toLocaleString('ru-RU')}</td>
                      <td style={{ padding: '8px 10px' }}>{vName(h.vendorId)}</td>
                      <td className="tnum" style={{ padding: '8px 10px', textAlign: 'right', fontWeight: 500 }}>{fmt(h.price)} ₽</td>
                      <td className="tnum" style={{ padding: '8px 10px', textAlign: 'right', color: 'var(--text-2)' }}>{h.leadTimeDays} дн</td>
                    </tr>
                  ))}
                  {history.data && history.data.length === 0 && <tr><td colSpan={4} style={{ padding: 12, color: 'var(--text-3)' }}>История пуста. Точки фиксируются при добавлении/импорте офферов.</td></tr>}
                </tbody>
              </table>
            </div>
          </div>
        </div>
      )}
    </Shell>
  )
}


