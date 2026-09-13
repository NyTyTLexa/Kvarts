import { useEffect, useMemo, useState, type FormEvent } from 'react'
import { useNavigate, useSearchParams } from 'react-router-dom'
import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Shell } from '../components/Shell'
import { PageHead } from '../components/PageHead'
import { Btn, Icon } from '../ui/atoms'
import { inputStyle } from '../ui/styles'
import { CatalogGridSkeleton, PulseDots } from '../ui/feedback'
import { useApi } from '../api/useApi'
import { useRoles } from '../auth/useRoles'
import { fmtMoneyShort } from '../data/constants'
import type { CategoryDto, Paged, VendorDto } from '../api/types'
import type { CatalogCardDto, CatalogSort, CatalogSuggestDto } from '../api/catalog'
import type { ManufacturerDto } from '../api/manufacturers'
import { CartDrawer, CompareTray, ProductCard, useCartOpen, useCatalogStore } from './ecatalog/shared'

const PAGE_SIZE = 24
const SORTS: { value: CatalogSort; label: string }[] = [
  { value: 'name', label: 'По имени' },
  { value: 'priceAsc', label: 'Сначала дешевле' },
  { value: 'priceDesc', label: 'Сначала дороже' },
  { value: 'offers', label: 'Больше предложений' },
  { value: 'stock', label: 'По остатку' },
]

export function ECatalog() {
  const api = useApi()
  const nav = useNavigate()
  const qc = useQueryClient()
  const { canAdmin } = useRoles()
  const [params, setParams] = useSearchParams()
  const [cartOpen, setCartOpen] = useCartOpen()
  const { cart, favs } = useCatalogStore()

  const page = Math.max(1, Number(params.get('page') || 1) || 1)
  const search = params.get('q') ?? ''
  const category = params.get('category') ?? ''
  const manufacturer = params.get('manufacturer') ?? ''
  const vendorId = params.get('vendor') ?? ''
  const inStockOnly = params.get('stock') === '1'
  const sort = (SORTS.some(s => s.value === params.get('sort')) ? params.get('sort') : 'name') as CatalogSort
  const favOnly = params.get('fav') === '1'

  const [input, setInput] = useState(search)
  const [suggestOpen, setSuggestOpen] = useState(false)
  const [suggestQ, setSuggestQ] = useState('')


  useEffect(() => { setInput(search) }, [search])
  useEffect(() => {
    const t = setTimeout(() => {
      const next = input.trim()
      if (next === search) return
      patch({ q: next || null, page: null })
    }, 400)
    return () => clearTimeout(t)
  }, [input, search])
  useEffect(() => {
    const t = setTimeout(() => setSuggestQ(input.trim()), 200)
    return () => clearTimeout(t)
  }, [input])

  function patch(next: Record<string, string | null>) {
    setParams(prev => {
      const p = new URLSearchParams(prev)
      for (const [k, v] of Object.entries(next)) {
        if (!v || (k === 'page' && v === '1')) p.delete(k)
        else p.set(k, v)
      }
      return p
    })
  }

  const qs = new URLSearchParams({ page: String(page), pageSize: String(PAGE_SIZE), sort })
  if (search) qs.set('search', search)
  if (category) qs.set('category', category)
  if (manufacturer) qs.set('manufacturer', manufacturer)
  if (vendorId) qs.set('vendorId', vendorId)
  if (inStockOnly) qs.set('inStockOnly', 'true')

  const list = useQuery({
    queryKey: ['ecatalog', page, search, category, manufacturer, vendorId, inStockOnly, sort],
    queryFn: () => api.get<Paged<CatalogCardDto>>(`/api/catalog?${qs.toString()}`),
    placeholderData: keepPreviousData,
    enabled: !favOnly,
  })
  const favQuery = useQuery({
    queryKey: ['ecatalog-fav', favs.join(',')],
    queryFn: () => api.get<CatalogCardDto[]>(`/api/catalog/compare?ids=${favs.slice(0, 48).join(',')}`),
    enabled: favOnly && favs.length > 0,
  })
  const cats = useQuery({
    queryKey: ['product-categories'],
    queryFn: () => api.get<CategoryDto[]>('/api/products/categories'),
  })
  const mfrs = useQuery({
    queryKey: ['manufacturers-all'],
    queryFn: () => api.get<Paged<ManufacturerDto>>('/api/manufacturers?pageSize=100'),
  })
  const vendors = useQuery({
    queryKey: ['vendors-all'],
    queryFn: () => api.get<Paged<VendorDto>>('/api/vendors?pageSize=100'),
  })
  const suggest = useQuery({
    queryKey: ['ecatalog-suggest', suggestQ],
    queryFn: () => api.get<CatalogSuggestDto[]>(`/api/catalog/suggest?q=${encodeURIComponent(suggestQ)}&limit=8`),
    enabled: suggestOpen && suggestQ.length >= 2,
  })

  const seed = useMutation({
    mutationFn: () => api.post<{ vendors: number; products: number; offers: number }>('/api/admin/seed?vendors=20&products=800&maxOffersPerProduct=3'),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['ecatalog'] })
      qc.invalidateQueries({ queryKey: ['product-categories'] })
      qc.invalidateQueries({ queryKey: ['manufacturers-all'] })
      qc.invalidateQueries({ queryKey: ['vendors-all'] })
    },
  })

  const tree = useMemo(() => buildTree(cats.data ?? []), [cats.data])
  const items = favOnly ? (favQuery.data ?? []) : (list.data?.items ?? [])
  const total = favOnly ? favs.length : (list.data?.total ?? 0)
  const pages = Math.max(1, Math.ceil((list.data?.total ?? 0) / PAGE_SIZE))
  const loading = favOnly ? favQuery.isLoading : list.isLoading
  const errored = favOnly ? favQuery.isError : list.isError
  const emptyCatalog = !loading && total === 0 && !search && !category && !manufacturer && !vendorId && !inStockOnly && !favOnly
  const cartCount = cart.reduce((s, l) => s + l.qty, 0)

  function submit(e: FormEvent) {
    e.preventDefault()
    patch({ q: input.trim() || null, page: null })
    setSuggestOpen(false)
  }

  return (
    <Shell breadcrumb={['Каталог']} actions={
      <div style={{ display: 'flex', gap: 8 }}>
        <Btn size="sm" kind={favOnly ? 'primary' : 'default'} icon={favOnly ? 'starFill' : 'star'}
          onClick={() => patch({ fav: favOnly ? null : '1', page: null })}>
          Избранное{favs.length ? ` · ${favs.length}` : ''}
        </Btn>
        <Btn size="sm" kind="primary" icon="package" onClick={() => setCartOpen(v => !v)}>
          Подборка{cartCount > 0 ? ` · ${cartCount}` : ''}
        </Btn>
      </div>
    }>
      <PageHead icon="search" kicker="витрина" title="Каталог"
        subtitle="Цены из прайсов поставщиков. Соберите подборку и превратите её в заявку." />

      <div className="ecatalog-layout">
        <aside>
          <div className="field-label">Категории</div>
          <NavItem active={!category} label="Все" count={favOnly ? favs.length : (list.data?.total ?? 0)}
            onClick={() => patch({ category: null, page: null })} />
          {tree.map(node => (
            <div key={node.name}>
              <NavItem active={category === node.name || category.startsWith(node.name + ' /')} label={node.name} count={node.count}
                onClick={() => patch({ category: category === node.name ? null : node.name, page: null })} />
              {(category === node.name || category.startsWith(node.name + ' /')) && node.children.map(ch => (
                <NavItem key={ch.path} active={category === ch.path} label={ch.name} count={ch.count} indent
                  onClick={() => patch({ category: ch.path, page: null })} />
              ))}
            </div>
          ))}
        </aside>

        <div>
          <form onSubmit={submit} style={{ display: 'flex', gap: 8, marginBottom: 12, flexWrap: 'wrap', position: 'relative' }}>
            <div style={{ flex: 1, minWidth: 0, display: 'flex', alignItems: 'center', gap: 8, background: 'var(--surface)', border: '1px solid var(--border)', borderRadius: 8, padding: '7px 12px', position: 'relative' }}>
              <Icon name="search" size={15} stroke="var(--text-3)" />
              <input value={input} onChange={e => setInput(e.target.value)}
                onFocus={() => setSuggestOpen(true)}
                onBlur={() => setTimeout(() => setSuggestOpen(false), 180)}
                placeholder="Модель, артикул, бренд…"
                style={{ flex: 1, border: 'none', background: 'transparent', outline: 'none', fontSize: 13.5, color: 'var(--text)' }} />
              {suggestOpen && (suggest.data?.length ?? 0) > 0 && (
                <div className="pop-enter" style={{
                  position: 'absolute', left: 0, right: 0, top: 'calc(100% + 6px)', zIndex: 30,
                  background: 'var(--surface)', border: '1px solid var(--border)', borderRadius: 10, boxShadow: 'var(--shadow-md)', overflow: 'hidden',
                }}>
                  {suggest.data!.map(s => (
                    <button type="button" key={s.id} className="suggest-item" onMouseDown={() => nav(`/ecatalog/${s.id}`)}
                      style={{ display: 'flex', width: '100%', textAlign: 'left', gap: 10, padding: '8px 12px', border: 'none', background: 'transparent', cursor: 'pointer', fontSize: 13 }}>
                      <span className="mono" style={{ width: 88, color: 'var(--text-3)', flexShrink: 0 }}>{s.sku}</span>
                      <span style={{ flex: 1, fontWeight: 500 }}>{s.name}</span>
                      <span className="tnum" style={{ color: 'var(--text-2)' }}>{s.minPrice != null ? fmtMoneyShort(s.minPrice) : '—'}</span>
                    </button>
                  ))}
                </div>
              )}
            </div>
            <select value={manufacturer} onChange={e => patch({ manufacturer: e.target.value || null, page: null })} style={{ ...inputStyle, width: 170 }}>
              <option value="">Все бренды</option>
              {(mfrs.data?.items ?? []).filter(m => m.isActive).map(m => (
                <option key={m.id} value={m.name}>{m.name}</option>
              ))}
            </select>
            <select value={vendorId} onChange={e => patch({ vendor: e.target.value || null, page: null })} style={{ ...inputStyle, width: 180 }}>
              <option value="">Все поставщики</option>
              {(vendors.data?.items ?? []).map(v => (
                <option key={v.id} value={v.id}>{v.name}</option>
              ))}
            </select>
            <select value={sort} onChange={e => patch({ sort: e.target.value === 'name' ? null : e.target.value, page: null })} style={{ ...inputStyle, width: 180 }}>
              {SORTS.map(s => <option key={s.value} value={s.value}>{s.label}</option>)}
            </select>
            <label style={{ display: 'flex', alignItems: 'center', gap: 6, fontSize: 13, color: 'var(--text-2)', padding: '0 6px' }}>
              <input type="checkbox" checked={inStockOnly} onChange={e => patch({ stock: e.target.checked ? '1' : null, page: null })} />
              в наличии
            </label>
            <Btn kind="default" size="md" type="submit">Найти</Btn>
          </form>

          <div style={{ fontSize: 12.5, color: 'var(--text-2)', marginBottom: 12 }}>
            {favOnly ? <>Избранное · <b className="tnum" style={{ color: 'var(--text)' }}>{favs.length}</b></> : (
              <>Найдено <b className="tnum" style={{ color: 'var(--text)' }}>{total.toLocaleString('ru-RU')}</b></>
            )}
            {category && <> · {category}</>}
            {manufacturer && <> · {manufacturer}</>}
            {inStockOnly && <> · в наличии</>}
          </div>

          {errored && <div style={{ color: 'var(--err)', background: 'var(--err-bg)', padding: 12, borderRadius: 8, marginBottom: 12 }}>Не удалось загрузить каталог.</div>}
          {loading && items.length === 0 && <CatalogGridSkeleton n={PAGE_SIZE} />}

          {items.length > 0 && (
            <div className="ecatalog-grid">
              {items.map(card => <ProductCard key={card.id} card={card} />)}
            </div>
          )}

          {!loading && items.length === 0 && !emptyCatalog && (
            <div style={{ padding: 28, color: 'var(--text-3)', textAlign: 'center' }}>
              {favOnly ? 'В избранном пусто — отметьте позиции звездой на карточке.' : 'В этой выборке пусто. Снимите фильтр или измените запрос.'}
            </div>
          )}

          {emptyCatalog && (
            <div style={{ padding: '36px 20px', textAlign: 'center', background: 'var(--surface)', border: '1px dashed var(--border-strong)', borderRadius: 12 }}>
              <div style={{ fontWeight: 600, fontSize: 15, marginBottom: 6 }}>Каталог пока пустой</div>
              <div style={{ fontSize: 13, color: 'var(--text-2)', maxWidth: 420, margin: '0 auto 16px' }}>
                Загрузите прайс поставщика — позиции появятся в каталоге.
              </div>
              <div style={{ display: 'flex', gap: 8, justifyContent: 'center', flexWrap: 'wrap' }}>
                <Btn kind="primary" size="md" onClick={() => nav('/prices')}>Загрузить прайс</Btn>
                {canAdmin && (
                  <Btn kind="default" size="md" disabled={seed.isPending} onClick={() => seed.mutate()}>
                    {seed.isPending ? <PulseDots label="Наполняем…" /> : 'Наполнить демо-каталогом'}
                  </Btn>
                )}
              </div>
              {seed.isError && <div style={{ color: 'var(--err)', marginTop: 10, fontSize: 12.5 }}>{(seed.error as Error).message}</div>}
            </div>
          )}

          {!favOnly && list.data && list.data.total > PAGE_SIZE && (
            <div style={{ display: 'flex', alignItems: 'center', gap: 12, marginTop: 16, fontSize: 12.5, color: 'var(--text-2)' }}>
              <div style={{ flex: 1 }} />
              <Btn size="sm" kind="default" disabled={page <= 1} onClick={() => patch({ page: String(page - 1) })}>Назад</Btn>
              <span className="tnum">Стр. {page} / {pages}</span>
              <Btn size="sm" kind="default" disabled={page >= pages} onClick={() => patch({ page: String(page + 1) })}>Вперёд</Btn>
            </div>
          )}

          <CompareTray />
        </div>
      </div>

      <CartDrawer open={cartOpen} onClose={() => setCartOpen(false)} />
    </Shell>
  )
}

function NavItem({ active, label, count, onClick, indent }: { active: boolean; label: string; count: number; onClick: () => void; indent?: boolean }) {
  return (
    <button type="button" onClick={onClick} style={{
      display: 'flex', width: '100%', textAlign: 'left', gap: 8, padding: indent ? '6px 10px 6px 22px' : '7px 10px', marginBottom: 1,
      border: '1px solid transparent', borderRadius: 6, cursor: 'pointer', fontSize: indent ? 12.5 : 13,
      background: active ? 'var(--accent-soft)' : 'transparent',
      color: active ? 'var(--accent)' : 'var(--text-2)', fontWeight: active ? 500 : 400,
    }}>
      <span style={{ flex: 1 }}>{label}</span>
      <span className="tnum" style={{ fontSize: 11, color: active ? 'var(--accent)' : 'var(--text-3)' }}>{count}</span>
    </button>
  )
}

function buildTree(cats: CategoryDto[]) {
  const tops = new Map<string, { name: string; count: number; children: { path: string; name: string; count: number }[] }>()
  for (const c of cats) {
    const parts = c.path.split(' / ')
    const name = parts[0]
    let node = tops.get(name)
    if (!node) { node = { name, count: 0, children: [] }; tops.set(name, node) }
    node.count += c.productsCount
    if (parts.length > 1) node.children.push({ path: c.path, name: parts.slice(1).join(' / '), count: c.productsCount })
  }
  return [...tops.values()].sort((a, b) => b.count - a.count)
}
