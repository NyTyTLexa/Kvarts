import { useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import { useQuery } from '@tanstack/react-query'
import { Shell } from '../components/Shell'
import { Btn, Icon, StatusPill } from '../ui/atoms'
import { inputStyle } from '../ui/styles'
import { PageLoader, Sparkline } from '../ui/feedback'
import { useApi } from '../api/useApi'
import { fmtMoney, fmtMoneyShort } from '../data/constants'
import type { CatalogProductDetailDto } from '../api/catalog'
import { copyText, toggleCompare, toggleFav } from '../api/catalog'
import { CartDrawer, CompareTray, ProductCard, addCardToCart, useCartOpen, useCatalogStore } from './ecatalog/shared'

export function ECatalogProduct() {
  const { id = '' } = useParams()
  const api = useApi()
  const [qty, setQty] = useState(1)
  const [copied, setCopied] = useState(false)
  const [cartOpen, setCartOpen] = useCartOpen()
  const { cart, favs, compare } = useCatalogStore()
  const fav = favs.includes(id)
  const cmp = compare.includes(id)

  const q = useQuery({
    queryKey: ['ecatalog-product', id],
    queryFn: () => api.get<CatalogProductDetailDto>(`/api/catalog/${id}`),
    enabled: !!id,
  })

  const cartCount = cart.reduce((s, l) => s + l.qty, 0)
  const detail = q.data
  const card = detail?.card
  const offers = [...(detail?.offers ?? [])].sort((a, b) => a.price - b.price)
  const history = detail?.history ?? []
  const prices = history.map(h => h.price)
  const delta = prices.length >= 2 ? prices[prices.length - 1] - prices[0] : 0
  const deltaPct = prices.length >= 2 && prices[0] !== 0 ? (delta / prices[0]) * 100 : 0

  function copySku() {
    if (!card) return
    copyText(card.sku)
    setCopied(true)
    setTimeout(() => setCopied(false), 1200)
  }

  return (
    <Shell breadcrumb={['Каталог', card?.sku ?? '…']} actions={
      <Btn size="sm" kind="primary" icon="package" onClick={() => setCartOpen(v => !v)}>
        Подборка{cartCount > 0 ? ` · ${cartCount}` : ''}
      </Btn>
    }>
      <Link to="/ecatalog" style={{ fontSize: 12.5, color: 'var(--text-2)', display: 'inline-flex', alignItems: 'center', gap: 5, marginBottom: 10 }}>
        <span style={{ display: 'inline-flex', transform: 'scaleX(-1)' }}><Icon name="chevR" size={12} stroke="var(--text-3)" /></span>
        к витрине
      </Link>

      {q.isLoading && <PageLoader label="Открываем карточку" />}
      {q.isError && <div style={{ color: 'var(--err)', background: 'var(--err-bg)', padding: 14, borderRadius: 8 }}>Позиция не найдена или каталог недоступен.</div>}

      {card && (
        <div className="page-enter">
          <div style={{ display: 'flex', gap: 16, alignItems: 'flex-start', flexWrap: 'wrap', marginBottom: 18 }}>
            <div style={{ flex: 1, minWidth: 280 }}>
              <div style={{ display: 'flex', alignItems: 'center', gap: 8, marginBottom: 6 }}>
                <span className="mono" style={{ fontSize: 12.5, color: 'var(--text-3)' }}>{card.sku}</span>
                <Btn size="sm" kind="ghost" icon="copy" onClick={copySku}>{copied ? 'скопировано' : 'копировать'}</Btn>
              </div>
              <h1 className="page-head-title" style={{ margin: '0 0 8px', lineHeight: 1.25 }}>{card.name}</h1>
              <div style={{ display: 'flex', gap: 8, flexWrap: 'wrap', fontSize: 12.5, color: 'var(--text-2)' }}>
                {card.manufacturer && <span style={{ padding: '2px 8px', background: 'var(--surface-2)' }}>{card.manufacturer}</span>}
                {card.category && <span>{card.category}</span>}
              </div>
            </div>
            <div style={{ background: 'var(--surface)', border: '1px solid var(--border)', borderRadius: 12, padding: 16, minWidth: 260 }}>
              {card.minPrice != null
                ? <div className="tnum" style={{ fontSize: 28, fontWeight: 600 }}>{fmtMoneyShort(card.minPrice)}
                    {card.maxPrice != null && card.maxPrice !== card.minPrice && (
                      <span style={{ fontSize: 13, color: 'var(--text-3)', fontWeight: 400 }}> – {fmtMoneyShort(card.maxPrice)}</span>
                    )}
                  </div>
                : <div style={{ color: 'var(--warn)' }}>Нет предложений</div>}
              <div style={{ fontSize: 12, color: 'var(--text-2)', margin: '6px 0 12px' }}>
                {card.offerCount} пост. · ост. {card.totalStock}
                {card.minLeadTimeDays != null ? ` · от ${card.minLeadTimeDays} дн` : ''}
              </div>
              <div style={{ display: 'flex', gap: 8, alignItems: 'center', marginBottom: 10 }}>
                <input type="number" min={1} value={qty} onChange={e => setQty(Math.max(1, Number(e.target.value) || 1))}
                  style={{ ...inputStyle, width: 72, padding: '6px 8px' }} />
                <Btn kind="primary" size="md" disabled={card.offerCount === 0} style={{ flex: 1, justifyContent: 'center' }}
                  onClick={() => addCardToCart(card, qty)}>В подборку</Btn>
              </div>
              <div style={{ display: 'flex', gap: 8 }}>
                <Btn size="sm" kind={fav ? 'primary' : 'default'} icon={fav ? 'starFill' : 'star'} onClick={() => toggleFav(card.id)}>
                  {fav ? 'В избранном' : 'В избранное'}
                </Btn>
                <Btn size="sm" kind={cmp ? 'primary' : 'default'} icon="scale" onClick={() => toggleCompare(card.id)}>
                  {cmp ? 'В сравнении' : 'Сравнить'}
                </Btn>
              </div>
            </div>
          </div>

          <div className="ecatalog-detail">
            <div>
              <div style={{ fontSize: 14, fontWeight: 600, marginBottom: 10 }}>Предложения поставщиков</div>
              {offers.length === 0 && <div style={{ fontSize: 13, color: 'var(--text-3)' }}>Офферов нет — загрузите прайс.</div>}
              {offers.length > 0 && (
                <div style={{ overflowX: 'auto', background: 'var(--surface)', border: '1px solid var(--border)', borderRadius: 10 }}>
                  <table style={{ width: '100%', fontSize: 13 }}>
                    <thead>
                      <tr>{['Поставщик', 'Цена', 'Срок', 'Остаток', 'Сайт', ''].map((h, i) => (
                        <th key={h} style={{ padding: '8px 12px', textAlign: i === 0 ? 'left' : 'right', fontWeight: 500, fontSize: 11, color: 'var(--text-2)', textTransform: 'uppercase', letterSpacing: 0.5, borderBottom: '1px solid var(--border)' }}>{h}</th>
                      ))}</tr>
                    </thead>
                    <tbody>
                      {offers.map((o, i) => (
                        <tr key={o.id}>
                          <td style={{ padding: '10px 12px' }}>
                            <span style={{ fontWeight: 600 }}>{o.vendorName}</span>
                            {i === 0 && <span style={{ marginLeft: 8 }}><StatusPill status="мин. цена" kind="green" /></span>}
                          </td>
                          <td className="tnum" style={{ padding: '10px 12px', textAlign: 'right', fontWeight: 600 }}>{fmtMoney(o.price)}</td>
                          <td className="tnum" style={{ padding: '10px 12px', textAlign: 'right' }}>{o.leadTimeDays} дн</td>
                          <td className="tnum" style={{ padding: '10px 12px', textAlign: 'right', color: o.stockQuantity > 0 ? 'var(--ok)' : 'var(--text-3)' }}>{o.stockQuantity}</td>
                          <td style={{ padding: '10px 12px', textAlign: 'right' }}>
                            {o.sourceUrl
                              ? <a href={o.sourceUrl} target="_blank" rel="noreferrer" style={{ color: 'var(--accent)', fontSize: 12 }}>сайт</a>
                              : <span style={{ color: 'var(--text-3)' }}>—</span>}
                          </td>
                          <td style={{ padding: '10px 12px', textAlign: 'right' }}>
                            <Btn size="sm" kind="ghost" onClick={() => addCardToCart(card, qty, { price: o.price, vendorId: o.vendorId, vendorName: o.vendorName })}>взять</Btn>
                          </td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              )}
            </div>

            <div>
              <div style={{ fontSize: 14, fontWeight: 600, marginBottom: 10 }}>История цены</div>
              <div style={{ background: 'var(--surface)', border: '1px solid var(--border)', borderRadius: 10, padding: 14 }}>
                {prices.length < 2 && <div style={{ fontSize: 12.5, color: 'var(--text-3)' }}>Мало точек — история появится после обновлений прайса.</div>}
                {prices.length >= 2 && (
                  <>
                    <Sparkline values={prices} width={240} height={56} />
                    <div style={{ marginTop: 8, fontSize: 12.5 }}>
                      <span style={{ color: delta < 0 ? 'var(--ok)' : delta > 0 ? 'var(--err)' : 'var(--text-2)', fontWeight: 600 }}>
                        {delta === 0 ? 'без изменения' : `${delta > 0 ? '+' : ''}${deltaPct.toFixed(1)}%`}
                      </span>
                      <span style={{ color: 'var(--text-3)' }}> за {prices.length} точек</span>
                    </div>
                  </>
                )}
              </div>
            </div>
          </div>

          {detail && detail.analogs.length > 0 && (
            <div style={{ marginTop: 28 }}>
              <div style={{ fontSize: 14, fontWeight: 600, marginBottom: 12 }}>Аналоги в той же категории</div>
              <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fill, minmax(240px, 1fr))', gap: 12 }}>
                {detail.analogs.map(a => <ProductCard key={a.id} card={a} />)}
              </div>
            </div>
          )}

          <CompareTray />
        </div>
      )}

      <CartDrawer open={cartOpen} onClose={() => setCartOpen(false)} />
    </Shell>
  )
}
