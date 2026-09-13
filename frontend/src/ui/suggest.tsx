import { useEffect, useRef, useState, type CSSProperties, type KeyboardEvent, type ReactNode } from 'react'
import { inputStyle } from './styles'

export function SuggestBox<T>({
  value, onChange, items, loading, onPick, placeholder, render, empty, disabled, autoFocus, boxed,
}: {
  value: string
  onChange: (v: string) => void
  items: T[]
  loading?: boolean
  onPick: (item: T) => void
  placeholder?: string
  render: (item: T) => ReactNode
  empty?: string
  disabled?: boolean
  autoFocus?: boolean
  boxed?: boolean
}) {
  const [open, setOpen] = useState(false)
  const [hi, setHi] = useState(0)
  const box = useRef<HTMLDivElement>(null)
  const show = open && value.trim().length >= 2 && !disabled

  useEffect(() => { setHi(0) }, [items])
  useEffect(() => {
    const onDoc = (e: MouseEvent) => {
      if (!box.current?.contains(e.target as Node)) setOpen(false)
    }
    document.addEventListener('mousedown', onDoc)
    return () => document.removeEventListener('mousedown', onDoc)
  }, [])

  function key(e: KeyboardEvent<HTMLInputElement>) {
    if (!show) return
    if (e.key === 'ArrowDown') { e.preventDefault(); setHi(i => Math.min(items.length - 1, i + 1)) }
    if (e.key === 'ArrowUp') { e.preventDefault(); setHi(i => Math.max(0, i - 1)) }
    if (e.key === 'Escape') { setOpen(false); return }
    if (e.key === 'Enter' && items[hi]) { e.preventDefault(); onPick(items[hi]); setOpen(false) }
  }

  return (
    <div ref={box} style={{ position: 'relative', flex: 1, minWidth: 0 }}>
      <input value={value} disabled={disabled} autoFocus={autoFocus} placeholder={placeholder}
        onChange={e => { onChange(e.target.value); setOpen(true) }}
        onFocus={() => setOpen(true)}
        onKeyDown={key}
        style={(boxed ? { ...inputStyle, margin: 0 } : {
          width: '100%', border: 'none', background: 'transparent', outline: 'none',
          fontSize: 13.5, color: 'var(--text)', fontFamily: 'inherit',
        }) as CSSProperties} />
      {show && (
        <div className="pop-enter" style={{
          position: 'absolute', left: 0, right: 0, top: 'calc(100% + 6px)', zIndex: 30,
          background: 'var(--surface)', border: '1px solid var(--border-strong)',
          boxShadow: 'var(--shadow-md)', maxHeight: 280, overflowY: 'auto',
        }}>
          {loading && <div style={{ padding: '8px 12px', fontSize: 13, color: 'var(--text-3)' }}>Ищем…</div>}
          {!loading && items.map((it, i) => (
            <button type="button" key={i} className="suggest-item"
              onMouseDown={e => { e.preventDefault(); onPick(it); setOpen(false) }}
              onMouseEnter={() => setHi(i)}
              style={{
                display: 'flex', width: '100%', textAlign: 'left', padding: '8px 12px',
                border: 'none', cursor: 'pointer', fontSize: 13, fontFamily: 'inherit',
                background: i === hi ? 'var(--amber-50)' : 'transparent', color: 'var(--text)',
              }}>
              {render(it)}
            </button>
          ))}
          {!loading && items.length === 0 && (
            <div style={{ padding: '8px 12px', fontSize: 13, color: 'var(--text-3)' }}>{empty ?? 'Нет совпадений — можно ввести своё'}</div>
          )}
        </div>
      )}
    </div>
  )
}

export function filterOptions(options: string[], q: string, limit = 8) {
  const n = q.trim().toLowerCase()
  if (!n) return options.slice(0, limit)
  return options.filter(o => o.toLowerCase().includes(n)).slice(0, limit)
}
