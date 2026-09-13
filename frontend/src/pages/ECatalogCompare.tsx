import { useMemo } from 'react'
import { Link, useNavigate, useSearchParams } from 'react-router-dom'
import { useQueries, useQuery } from '@tanstack/react-query'
import { Shell } from '../components/Shell'
import { PageHead } from '../components/PageHead'
import { Btn, Icon, StatusPill } from '../ui/atoms'
import { PageLoader } from '../ui/feedback'
import { useApi } from '../api/useApi'
import { fmtMoneyShort } from '../data/constants'
import type { CatalogCardDto, CatalogProductDetailDto } from '../api/catalog'
import { clearCompare, loadCompare } from '../api/catalog'
import { CartDrawer, addCardToCart, useCartOpen, useCatalogStore } from './ecatalog/shared'

export function ECatalogCompare() {
  const api = useApi()
  const nav = useNavigate()
  const [params] = useSearchParams()
  const [cartOpen, setCartOpen] = useCartOpen()
  const { cart } = useCatalogStore()
  const fromUrl = (params.get('ids') ?? '').split(',').map(s => s.trim()).filter(Boolean)
  const ids = fromUrl.length > 0 ? fromUrl.slice(0, 4) : loadCompare().slice(0, 4)

  const cardsQ = useQuery({
    queryKey: ['ecatalog-compare', ids.join(',')],
    queryFn: () => api.get<CatalogCardDto[]>(`/api/catalog/compare?ids=${ids.join(',')}`),
    enabled: ids.length > 0,
  })

  const details = useQueries({
    queries: ids.map(id => ({
      queryKey: ['ecatalog-product', id],
      queryFn: () => api.get<CatalogProductDetailDto>(`/api/catalog/${id}`),
      enabled: ids.length > 0,
    })),
  })

  const cards = cardsQ.data ?? []
  const byId = useMemo(() => Object.fromEntries(cards.map(c => [c.id, c])), [cards])
  const ordered = ids.map(id => byId[id]).filter(Boolean)
  const minPrice = Math.min(...ordered.map(c => c.minPrice ?? Infinity))
  const minLead = Math.min(...ordered.map(c => c.minLeadTimeDays ?? Infinity))
  const cartCount = cart.reduce((s, l) => s + l.qty, 0)

  const rows: { label: string; pick?: 'min-price' | 'min-lead' | 'max-stock'; render: (c: CatalogCardDto) => string }[] = [
    { label: 'Артикул', render: c => c.sku },
    { label: 'Производитель', render: c => c.manufacturer ?? '—' },
    { label: 'Категория', render: c => c.category ?? '—' },
    { label: 'Мин. цена', pick: 'min-price', render: c => c.minPrice != null ? fmtMoneyShort(c.minPrice) : 'нет офферов' },
    { label: 'Макс. цена', render: c => c.maxPrice != null ? fmtMoneyShort(c.maxPrice) : '—' },
    { label: 'Поставщиков', render: c => String(c.offerCount) },
    { label: 'Остаток', pick: 'max-stock', render: c => String(c.totalStock) },
    { label: 'Срок от', pick: 'min-lead', render: c => c.minLeadTimeDays != null ? `${c.minLeadTimeDays} дн` : '—' },
    { label: 'Лучшая цена у', render: c => c.cheapestVendor ?? '—' },
  ]
  const maxStock = Math.max(...ordered.map(c => c.totalStock), 0)

  return (
    <Shell breadcrumb={['Каталог', 'Сравнение']} actions={
      <div style={{ display: 'flex', gap: 8 }}>
        <Btn size="sm" kind="ghost" onClick={() => { clearCompare(); nav('/ecatalog') }}>Сбросить</Btn>
        <Btn size="sm" kind="primary" icon="package" onClick={() => setCartOpen(v => !v)}>
          Подборка{cartCount > 0 ? ` · ${cartCount}` : ''}
        </Btn>
      </div>
    }>
      <Link to="/ecatalog" style={{ fontSize: 12.5, color: 'var(--text-2)', display: 'inline-flex', alignItems: 'center', gap: 5, marginBottom: 8 }}>
        <span style={{ display: 'inline-flex', transform: 'scaleX(-1)' }}><Icon name="chevR" size={12} stroke="var(--text-3)" /></span>
        к каталогу
      </Link>
      <PageHead icon="scale" title="Сравнение позиций"
        subtitle="До четырёх артикулов рядом: цена, срок, остаток и кто даёт лучшее предложение." />

      {ids.length < 2 && (
        <div style={{ padding: 28, textAlign: 'center', color: 'var(--text-2)', background: 'var(--surface)', border: '1px dashed var(--border-strong)', borderRadius: 12 }}>
          Отметьте в каталоге минимум две позиции иконкой весов.
        </div>
      )}

      {ids.length >= 2 && cardsQ.isLoading && <PageLoader label="Сводим позиции" />}
      {cardsQ.isError && <div style={{ color: 'var(--err)', background: 'var(--err-bg)', padding: 12, borderRadius: 8 }}>Не удалось загрузить сравнение.</div>}

      {ordered.length >= 2 && (
        <div className="page-enter" style={{ overflowX: 'auto' }}>
          <table style={{ width: '100%', fontSize: 13, background: 'var(--surface)', border: '1px solid var(--border)', borderRadius: 12, borderCollapse: 'separate', borderSpacing: 0 }}>
            <thead>
              <tr>
                <th style={{ width: 160, padding: '12px 14px', textAlign: 'left', fontWeight: 500, fontSize: 11, color: 'var(--text-2)', textTransform: 'uppercase', letterSpacing: 0.5, borderBottom: '1px solid var(--border)' }}>Параметр</th>
                {ordered.map(c => (
                  <th key={c.id} style={{ padding: '12px 14px', textAlign: 'left', fontWeight: 600, borderBottom: '1px solid var(--border)', minWidth: 180 }}>
                    <div style={{ cursor: 'pointer' }} onClick={() => nav(`/ecatalog/${c.id}`)}>{c.name}</div>
                    <div className="mono" style={{ fontSize: 11, color: 'var(--text-3)', fontWeight: 400, marginTop: 4 }}>{c.sku}</div>
                  </th>
                ))}
              </tr>
            </thead>
            <tbody>
              {rows.map(row => (
                <tr key={row.label}>
                  <td style={{ padding: '10px 14px', color: 'var(--text-2)', borderBottom: '1px solid var(--border)' }}>{row.label}</td>
                  {ordered.map(c => {
                    const win =
                      (row.pick === 'min-price' && c.minPrice === minPrice && minPrice !== Infinity) ||
                      (row.pick === 'min-lead' && c.minLeadTimeDays === minLead && minLead !== Infinity) ||
                      (row.pick === 'max-stock' && c.totalStock === maxStock && maxStock > 0)
                    return (
                      <td key={c.id} style={{
                        padding: '10px 14px', borderBottom: '1px solid var(--border)',
                        background: win ? 'var(--ok-bg)' : undefined, fontWeight: win ? 600 : 400,
                      }} className="tnum">
                        {row.render(c)}
                        {win && <span style={{ marginLeft: 8 }}><StatusPill status="лучше" kind="green" /></span>}
                      </td>
                    )
                  })}
                </tr>
              ))}
              <tr>
                <td style={{ padding: '12px 14px', color: 'var(--text-2)' }}>Офферы</td>
                {ordered.map(c => {
                  const idx = ids.indexOf(c.id)
                  const d = details[idx]?.data
                  return (
                    <td key={c.id} style={{ padding: '12px 14px', verticalAlign: 'top', fontSize: 12.5 }}>
                      {details[idx]?.isLoading && <span style={{ color: 'var(--text-3)' }}>…</span>}
                      {(d?.offers ?? []).slice(0, 5).map(o => (
                        <div key={o.id} style={{ display: 'flex', justifyContent: 'space-between', gap: 8, padding: '3px 0' }}>
                          <span style={{ color: 'var(--text-2)' }}>{o.vendorName}</span>
                          <span className="tnum">{fmtMoneyShort(o.price)}</span>
                        </div>
                      ))}
                      <Btn size="sm" kind="primary" style={{ marginTop: 8 }} onClick={() => addCardToCart(c)}>В подборку</Btn>
                    </td>
                  )
                })}
              </tr>
            </tbody>
          </table>
        </div>
      )}

      <CartDrawer open={cartOpen} onClose={() => setCartOpen(false)} />
    </Shell>
  )
}
