import type { OfferDto } from './types'

export interface CatalogCardDto {
  id: string
  sku: string
  name: string
  manufacturer?: string
  category?: string
  minPrice?: number
  maxPrice?: number
  offerCount: number
  totalStock: number
  cheapestVendor?: string
  minLeadTimeDays?: number
}

export interface CatalogSuggestDto {
  id: string
  sku: string
  name: string
  manufacturer?: string
  minPrice?: number
  category?: string
}

export interface CatalogPricePointDto {
  recordedAtUtc: string
  price: number
  vendorId: string
}

export interface CatalogProductDetailDto {
  card: CatalogCardDto
  offers: OfferDto[]
  analogs: CatalogCardDto[]
  history: CatalogPricePointDto[]
}

export type CatalogSort = 'name' | 'priceAsc' | 'priceDesc' | 'offers' | 'stock'

export interface CartLine {
  productId: string
  sku: string
  name: string
  manufacturer?: string
  qty: number
  unitPrice?: number
  vendorId?: string
  vendorName?: string
}

const CART_KEY = 'ecatalog-cart-v1'
const FAV_KEY = 'ecatalog-fav-v1'
const CMP_KEY = 'ecatalog-compare-v1'
const STORE_EVENT = 'ecatalog-store'
const COMPARE_MAX = 4

function readJson<T>(key: string, fallback: T): T {
  try {
    const raw = localStorage.getItem(key)
    return raw ? JSON.parse(raw) as T : fallback
  } catch {
    return fallback
  }
}

function emitStore() {
  window.dispatchEvent(new Event(STORE_EVENT))
}

export function onCatalogStore(cb: () => void) {
  window.addEventListener(STORE_EVENT, cb)
  window.addEventListener('storage', cb)
  return () => {
    window.removeEventListener(STORE_EVENT, cb)
    window.removeEventListener('storage', cb)
  }
}

export function loadCart(): CartLine[] { return readJson<CartLine[]>(CART_KEY, []) }
export function saveCart(lines: CartLine[]) {
  localStorage.setItem(CART_KEY, JSON.stringify(lines))
  emitStore()
}

export function addToCart(card: CatalogCardDto, qty = 1, opts?: { price?: number; vendorId?: string; vendorName?: string }) {
  const unit = opts?.price ?? card.minPrice
  const cart = loadCart()
  const i = cart.findIndex(l => l.productId === card.id)
  if (i >= 0) {
    cart[i] = {
      ...cart[i],
      qty: cart[i].qty + qty,
      unitPrice: unit ?? cart[i].unitPrice,
      vendorId: opts?.vendorId ?? cart[i].vendorId,
      vendorName: opts?.vendorName ?? cart[i].vendorName,
    }
  } else {
    cart.push({
      productId: card.id, sku: card.sku, name: card.name, manufacturer: card.manufacturer,
      qty, unitPrice: unit, vendorId: opts?.vendorId, vendorName: opts?.vendorName,
    })
  }
  saveCart(cart)
}

export function setCartQty(id: string, qty: number) {
  saveCart(loadCart().map(l => l.productId === id ? { ...l, qty: Math.max(1, qty) } : l))
}

export function removeCartLine(id: string) {
  saveCart(loadCart().filter(l => l.productId !== id))
}

export function loadFavs(): string[] { return readJson<string[]>(FAV_KEY, []) }
export function isFav(id: string) { return loadFavs().includes(id) }
export function toggleFav(id: string): string[] {
  const cur = loadFavs()
  const next = cur.includes(id) ? cur.filter(x => x !== id) : [...cur, id]
  localStorage.setItem(FAV_KEY, JSON.stringify(next))
  emitStore()
  return next
}

export function loadCompare(): string[] { return readJson<string[]>(CMP_KEY, []) }
export function inCompare(id: string) { return loadCompare().includes(id) }
export function toggleCompare(id: string): { ids: string[]; error?: string } {
  const cur = loadCompare()
  if (cur.includes(id)) {
    const ids = cur.filter(x => x !== id)
    localStorage.setItem(CMP_KEY, JSON.stringify(ids))
    emitStore()
    return { ids }
  }
  const ids = cur.length >= COMPARE_MAX ? [...cur.slice(1), id] : [...cur, id]
  localStorage.setItem(CMP_KEY, JSON.stringify(ids))
  emitStore()
  return { ids }
}
export function clearCompare() {
  localStorage.setItem(CMP_KEY, JSON.stringify([]))
  emitStore()
}

export function exportCartCsv(lines: CartLine[]) {
  const header = 'Артикул;Наименование;Производитель;Кол-во;Цена;Поставщик;Сумма'
  const rows = lines.map(l => {
    const sum = l.unitPrice != null ? l.unitPrice * l.qty : ''
    return [l.sku, l.name, l.manufacturer ?? '', l.qty, l.unitPrice ?? '', l.vendorName ?? '', sum]
      .map(v => `"${String(v).replaceAll('"', '""')}"`).join(';')
  })
  const blob = new Blob(['\uFEFF' + [header, ...rows].join('\n')], { type: 'text/csv;charset=utf-8' })
  const url = URL.createObjectURL(blob)
  const a = document.createElement('a')
  a.href = url
  a.download = `podborka-${new Date().toISOString().slice(0, 10)}.csv`
  document.body.appendChild(a)
  a.click()
  a.remove()
  URL.revokeObjectURL(url)
}

export async function copyText(text: string) {
  try { await navigator.clipboard.writeText(text) } catch { /* ignore */ }
}
