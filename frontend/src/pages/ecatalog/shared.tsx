import { useEffect, useState, type ReactNode } from 'react'
import { useNavigate } from 'react-router-dom'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Btn, Icon, StatusPill } from '../../ui/atoms'
import { inputStyle } from '../../ui/styles'
import { PulseDots } from '../../ui/feedback'
import { useApi } from '../../api/useApi'
import { useRoles } from '../../auth/useRoles'
import { fmtMoneyShort } from '../../data/constants'
import type { SpecificationDto, SpecificationItemDto } from '../../api/types'
import {
  type CatalogCardDto, type CartLine,
  addToCart, clearCompare, loadCart, loadCompare, loadFavs, onCatalogStore,
  removeCartLine, saveCart, setCartQty, toggleCompare, toggleFav, exportCartCsv,
} from '../../api/catalog'

const OPEN_EVENT = 'ecatalog-cart-open'

export function openCart() {
  window.dispatchEvent(new Event(OPEN_EVENT))
}

export function useCatalogStore() {
  const [, bump] = useState(0)
  useEffect(() => onCatalogStore(() => bump(n => n + 1)), [])
  return { cart: loadCart(), favs: loadFavs(), compare: loadCompare() }
}

export function useCartOpen() {
  const [open, setOpen] = useState(false)
  useEffect(() => {
    const on = () => setOpen(true)
    window.addEventListener(OPEN_EVENT, on)
    return () => window.removeEventListener(OPEN_EVENT, on)
  }, [])
  return [open, setOpen] as const
}

export function addCardToCart(card: CatalogCardDto, qty = 1, opts?: { price?: number; vendorId?: string; vendorName?: string }) {
  addToCart(card, qty, opts)
  openCart()
}

export function ProductCard({ card }: { card: CatalogCardDto }) {
  const nav = useNavigate()
  const { favs, compare } = useCatalogStore()
  const fav = favs.includes(card.id)
  const cmp = compare.includes(card.id)

  return (
    <article className="ecard" tabIndex={0} role="link"
      onClick={() => nav(`/ecatalog/${card.id}`)}
      onKeyDown={e => { if (e.key === 'Enter') nav(`/ecatalog/${card.id}`) }}>
      <div style={{ display: 'flex', justifyContent: 'space-between', gap: 8, alignItems: 'center' }}>
        <span className="mono" style={{ fontSize: 11, color: 'var(--text-3)' }}>{card.sku}</span>
        <div style={{ display: 'flex', gap: 4 }}>
          <IconBtn title={fav ? 'Убрать из избранного' : 'В избранное'} active={fav}
            onClick={() => toggleFav(card.id)}>
            <Icon name={fav ? 'starFill' : 'star'} size={13} stroke={fav ? 'var(--accent)' : 'currentColor'} />
          </IconBtn>
          <IconBtn title={cmp ? 'Убрать из сравнения' : 'К сравнению'} active={cmp}
            onClick={() => toggleCompare(card.id)}>
            <Icon name="scale" size={13} stroke={cmp ? 'var(--accent)' : 'currentColor'} />
          </IconBtn>
        </div>
      </div>
      {card.manufacturer && (
        <span style={{ fontSize: 11, padding: '1px 7px', background: 'var(--surface-2)', alignSelf: 'flex-start' }}>{card.manufacturer}</span>
      )}
      <div style={{ fontWeight: 600, fontSize: 14, lineHeight: 1.35, minHeight: 38 }}>{card.name}</div>
      <div style={{ fontSize: 11.5, color: 'var(--text-3)' }}>{card.category ?? 'Без категории'}</div>
      <div style={{ marginTop: 'auto' }}>
        {card.minPrice != null
          ? <div className="tnum" style={{ fontSize: 20, fontWeight: 600 }}>{fmtMoneyShort(card.minPrice)}
              {card.maxPrice != null && card.maxPrice !== card.minPrice && (
                <span style={{ fontSize: 12, color: 'var(--text-3)', fontWeight: 400 }}> – {fmtMoneyShort(card.maxPrice)}</span>
              )}
            </div>
          : <div style={{ fontSize: 13, color: 'var(--warn)' }}>нет офферов</div>}
        <div style={{ display: 'flex', gap: 8, marginTop: 6, fontSize: 11.5, color: 'var(--text-2)', flexWrap: 'wrap' }}>
          <span>{card.offerCount} пост.</span>
          <span style={{ color: card.totalStock > 0 ? 'var(--ok)' : 'var(--text-3)' }}>ост. {card.totalStock}</span>
          {card.minLeadTimeDays != null && <span>от {card.minLeadTimeDays} дн</span>}
        </div>
        {card.cheapestVendor && (
          <div style={{ fontSize: 11, color: 'var(--text-3)', marginTop: 4 }}>мин. цена: {card.cheapestVendor}</div>
        )}
      </div>
      <Btn size="sm" kind="primary" onClick={e => { e.stopPropagation(); addCardToCart(card) }}>В подборку</Btn>
    </article>
  )
}

function IconBtn({ children, title, active, onClick }: { children: ReactNode; title: string; active?: boolean; onClick: () => void }) {
  return (
    <button type="button" title={title} className="icon-btn" onClick={e => { e.stopPropagation(); onClick() }}
      style={{
        width: 26, height: 26, borderRadius: 6, border: '1px solid ' + (active ? 'var(--accent)' : 'transparent'),
        background: active ? 'var(--accent-soft)' : 'transparent', color: active ? 'var(--accent)' : 'var(--text-3)',
        display: 'flex', alignItems: 'center', justifyContent: 'center', cursor: 'pointer', padding: 0,
      }}>
      {children}
    </button>
  )
}

export function CartDrawer({ open, onClose }: { open: boolean; onClose: () => void }) {
  const api = useApi()
  const nav = useNavigate()
  const qc = useQueryClient()
  const { canWrite } = useRoles()
  const { cart } = useCatalogStore()
  const [specId, setSpecId] = useState('')
  const [err, setErr] = useState<string>()

  const specs = useQuery({
    queryKey: ['specifications'],
    queryFn: () => api.get<SpecificationDto[]>('/api/specifications'),
    enabled: open && canWrite,
  })

  const cartCount = cart.reduce((s, l) => s + l.qty, 0)
  const cartSum = cart.reduce((s, l) => s + (l.unitPrice ?? 0) * l.qty, 0)

  async function pushItems(id: string) {
    for (const line of cart) {
      await api.post<SpecificationItemDto>(`/api/specifications/${id}/items`, {
        productId: line.productId, sku: line.sku, name: line.name, quantity: line.qty,
      })
    }
  }

  const checkout = useMutation({
    mutationFn: async () => {
      if (specId) {
        await pushItems(specId)
        return specId
      }
      const spec = await api.post<SpecificationDto>('/api/specifications', {
        title: `Подборка из каталога · ${new Date().toLocaleDateString('ru-RU')}`,
        customer: null,
      })
      await pushItems(spec.id)
      return spec.id
    },
    onSuccess: (id) => {
      saveCart([])
      qc.invalidateQueries({ queryKey: ['specifications'] })
      onClose()
      nav(`/specifications/${id}`)
    },
    onError: (e) => setErr((e as Error).message),
  })

  if (!open) return null

  return (
    <div className="cart-drawer pop-enter">
      <div style={{ padding: '12px 14px', borderBottom: '1px solid var(--border)', display: 'flex', alignItems: 'center' }}>
        <div style={{ fontWeight: 600 }}>Подборка{cartCount > 0 ? ` · ${cartCount}` : ''}</div>
        <div style={{ flex: 1 }} />
        <Btn size="sm" kind="ghost" icon="x" onClick={onClose}>{''}</Btn>
      </div>
      <div style={{ padding: 12, overflowY: 'auto', flex: 1 }}>
        {cart.length === 0 && <div style={{ fontSize: 13, color: 'var(--text-3)' }}>Пусто. Добавьте позиции с витрины — из них получится спецификация для КП.</div>}
        {cart.map(l => (
          <CartRow key={l.productId} line={l} />
        ))}
      </div>
      <div style={{ padding: 12, borderTop: '1px solid var(--border)' }}>
        {err && <div style={{ color: 'var(--err)', background: 'var(--err-bg)', padding: '8px 10px', borderRadius: 7, marginBottom: 10, fontSize: 12.5 }}>{err}</div>}
        <div style={{ display: 'flex', justifyContent: 'space-between', marginBottom: 10, fontSize: 13 }}>
          <span style={{ color: 'var(--text-2)' }}>Ориентир по ценам</span>
          <b className="tnum">{fmtMoneyShort(cartSum)}</b>
        </div>
        <div style={{ display: 'flex', gap: 8, marginBottom: 10 }}>
          <Btn size="sm" kind="default" icon="download" disabled={cart.length === 0} onClick={() => exportCartCsv(cart)}>CSV</Btn>
          {cart.length > 0 && <Btn size="sm" kind="ghost" onClick={() => saveCart([])}>Очистить</Btn>}
        </div>
        {canWrite ? (
          <>
            <select value={specId} onChange={e => setSpecId(e.target.value)} style={{ ...inputStyle, marginBottom: 8 }}>
              <option value="">Новая спецификация</option>
              {(specs.data ?? []).map(s => (
                <option key={s.id} value={s.id}>{s.title}{s.customer ? ` · ${s.customer}` : ''}</option>
              ))}
            </select>
            <Btn kind="primary" size="md" disabled={cart.length === 0 || checkout.isPending}
              style={{ width: '100%', justifyContent: 'center' }}
              onClick={() => { setErr(undefined); checkout.mutate() }}>
              {checkout.isPending
                ? <PulseDots invert label={specId ? 'Добавляем…' : 'Собираем спецификацию…'} />
                : specId ? 'Добавить в выбранную спецификацию' : 'Собрать спецификацию и перейти к КП'}
            </Btn>
          </>
        ) : (
          <div style={{ fontSize: 12.5, color: 'var(--text-3)' }}>Создать спецификацию может РП или администратор. CSV можно скачать без этого.</div>
        )}
      </div>
    </div>
  )
}

function CartRow({ line: l }: { line: CartLine }) {
  const nav = useNavigate()
  return (
    <div style={{ padding: '8px 0', borderBottom: '1px solid var(--border)', fontSize: 13 }}>
      <div style={{ fontWeight: 500, cursor: 'pointer' }} onClick={() => nav(`/ecatalog/${l.productId}`)}>{l.name}</div>
      <div className="mono" style={{ fontSize: 11, color: 'var(--text-3)', marginTop: 2 }}>{l.sku}{l.vendorName ? ` · ${l.vendorName}` : ''}</div>
      <div style={{ display: 'flex', alignItems: 'center', gap: 8, marginTop: 6 }}>
        <input type="number" min={1} value={l.qty} onChange={e => setCartQty(l.productId, Number(e.target.value))}
          style={{ ...inputStyle, width: 64, padding: '4px 8px' }} />
        <span className="tnum" style={{ color: 'var(--text-2)' }}>{l.unitPrice != null ? fmtMoneyShort(l.unitPrice * l.qty) : '—'}</span>
        <div style={{ flex: 1 }} />
        <Btn size="sm" kind="ghost" onClick={() => removeCartLine(l.productId)}>убрать</Btn>
      </div>
    </div>
  )
}

export function CompareTray() {
  const nav = useNavigate()
  const { compare } = useCatalogStore()
  if (compare.length === 0) return null
  return (
    <div className="pop-enter" style={{
      position: 'sticky', bottom: 12, zIndex: 20, marginTop: 16,
      background: 'var(--surface)', border: '1px solid var(--accent-border)', borderRadius: 12,
      boxShadow: 'var(--shadow-md)', padding: '10px 14px', display: 'flex', alignItems: 'center', gap: 10, flexWrap: 'wrap',
    }}>
      <StatusPill status={`к сравнению · ${compare.length}`} kind="amber" />
      <span style={{ fontSize: 12.5, color: 'var(--text-2)' }}>до 4 позиций, цены и сроки рядом</span>
      <div style={{ flex: 1 }} />
      <Btn size="sm" kind="ghost" onClick={() => clearCompare()}>Сбросить</Btn>
      <Btn size="sm" kind="primary" disabled={compare.length < 2}
        onClick={() => nav(`/ecatalog/compare?ids=${compare.join(',')}`)}>
        Сравнить
      </Btn>
    </div>
  )
}


