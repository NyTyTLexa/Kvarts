import type { CSSProperties } from 'react'

export function PulseDots({ label, style, invert }: { label?: string; style?: CSSProperties; invert?: boolean }) {
  return (
    <div style={{ display: 'inline-flex', alignItems: 'center', gap: 10, color: 'var(--text-2)', fontSize: 13, ...style }}>
      <span className={invert ? 'amicro-dots invert' : 'amicro-dots'} aria-hidden><i /><i /><i /></span>
      {label}
    </div>
  )
}

export function PageLoader({ label = 'Загрузка' }: { label?: string }) {
  return (
    <div style={{ display: 'flex', justifyContent: 'center', padding: '36px 12px' }}>
      <PulseDots label={label} />
    </div>
  )
}

export function Skeleton({ w, h, r = 2, style }: { w?: number | string; h: number; r?: number; style?: CSSProperties }) {
  return <div className="amicro-skel" style={{ width: w ?? '100%', height: h, borderRadius: r, ...style }} />
}

export function CatalogGridSkeleton({ n = 8 }: { n?: number }) {
  return (
    <div className="ecatalog-grid">
      {Array.from({ length: n }, (_, i) => (
        <div key={i} className="ecard" style={{ cursor: 'default', pointerEvents: 'none' }}>
          <div style={{ display: 'flex', justifyContent: 'space-between' }}>
            <Skeleton w={72} h={12} r={4} />
            <Skeleton w={56} h={16} r={4} />
          </div>
          <Skeleton h={16} />
          <Skeleton w="70%" h={16} />
          <Skeleton w={90} h={12} r={4} />
          <div style={{ marginTop: 8 }}>
            <Skeleton w={110} h={24} r={6} />
          </div>
          <Skeleton h={26} r={7} />
        </div>
      ))}
    </div>
  )
}

export function TableRowsSkeleton({ rows = 5, cols = 5 }: { rows?: number; cols?: number }) {
  return (
    <>
      {Array.from({ length: rows }, (_, r) => (
        <tr key={r}>
          {Array.from({ length: cols }, (_, c) => (
            <td key={c} style={{ padding: '12px 12px' }}><Skeleton h={12} w={c === 0 ? '80%' : 72} r={4} /></td>
          ))}
        </tr>
      ))}
    </>
  )
}

export function Sparkline({ values, width = 180, height = 44, stroke = 'var(--accent)' }:
  { values: number[]; width?: number; height?: number; stroke?: string }) {
  if (values.length === 0) return null
  const min = Math.min(...values)
  const max = Math.max(...values)
  const span = max - min || 1
  const n = Math.max(values.length, 2)
  const pts = values.map((v, i) => {
    const x = (i / (n - 1)) * width
    const y = height - ((v - min) / span) * (height - 6) - 3
    return `${x.toFixed(1)},${y.toFixed(1)}`
  })
  const line = pts.join(' ')
  const area = `0,${height} ${line} ${width},${height}`
  return (
    <svg width={width} height={height} viewBox={`0 0 ${width} ${height}`} role="img" aria-label="Динамика цены">
      <polygon points={area} fill="var(--accent-soft)" />
      <polyline points={line} fill="none" stroke={stroke} strokeWidth="1.8" strokeLinejoin="round" strokeLinecap="round" />
    </svg>
  )
}

export function BarChart({ items, height = 150 }: { items: { label: string; value: number }[]; height?: number }) {
  const max = Math.max(...items.map(i => i.value), 1)
  if (items.length === 0) return null
  return (
    <div style={{ display: 'flex', alignItems: 'end', gap: 8, height, padding: '8px 0 0' }}>
      {items.map(it => (
        <div key={it.label} title={`${it.label}: ${it.value}`} style={{ flex: 1, minWidth: 34, display: 'flex', flexDirection: 'column', alignItems: 'center', gap: 6, height: '100%', justifyContent: 'end' }}>
          <div style={{
            width: '100%',
            height: `${Math.max(6, (it.value / max) * (height - 28))}px`,
            background: 'var(--accent)',
            borderRadius: 0,
          }} />
          <span style={{ fontSize: 10.5, color: 'var(--text-3)', whiteSpace: 'nowrap' }}>{it.label}</span>
        </div>
      ))}
    </div>
  )
}

export function HBarChart({ items }: { items: { label: string; value: number; hint?: string }[] }) {
  const max = Math.max(...items.map(i => i.value), 1)
  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 10 }}>
      {items.map(it => (
        <div key={it.label}>
          <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: 12.5, marginBottom: 4, gap: 12 }}>
            <span style={{ fontWeight: 500 }}>{it.label}</span>
            <span className="tnum" style={{ color: 'var(--text-2)', whiteSpace: 'nowrap' }}>{it.hint ?? it.value}</span>
          </div>
          <div style={{ height: 8, background: 'var(--surface-2)', overflow: 'hidden', border: '1px solid var(--border)' }}>
            <div style={{ width: `${Math.max(4, it.value / max * 100)}%`, height: '100%', background: 'var(--accent)' }} />
          </div>
        </div>
      ))}
    </div>
  )
}
