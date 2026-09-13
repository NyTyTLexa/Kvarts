import React from 'react'
import { LIFECYCLE_STAGES } from '../data/constants'

// ─── Icon ─────────────────────────────────────────────────────────
// Минимальный stroke-набор из хендоффа (strokeWidth 1.6), размер = font-size родителя.
const ICON_PATHS = {
  dashboard: <><rect x="3" y="3" width="7" height="9"/><rect x="14" y="3" width="7" height="5"/><rect x="14" y="12" width="7" height="9"/><rect x="3" y="16" width="7" height="5"/></>,
  folder:    <path d="M3 7a2 2 0 0 1 2-2h4l2 2h8a2 2 0 0 1 2 2v9a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2z"/>,
  upload:    <><path d="M12 16V4"/><path d="m7 9 5-5 5 5"/><path d="M5 20h14"/></>,
  document:  <><path d="M14 3H7a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h10a2 2 0 0 0 2-2V8z"/><path d="M14 3v5h5"/></>,
  tag:       <><path d="m20.59 13.41-7.17 7.17a2 2 0 0 1-2.83 0L2 12V2h10l8.59 8.59a2 2 0 0 1 0 2.82z"/><circle cx="7" cy="7" r="1.5"/></>,
  chart:     <><path d="M3 3v18h18"/><path d="m7 15 4-4 4 4 5-7"/></>,
  layers:    <><path d="m12 2 10 5-10 5L2 7z"/><path d="m2 12 10 5 10-5"/><path d="m2 17 10 5 10-5"/></>,
  settings:  <><circle cx="12" cy="12" r="3"/><path d="M19.4 15a1.7 1.7 0 0 0 .3 1.8l.1.1a2 2 0 0 1-2.8 2.8l-.1-.1a1.7 1.7 0 0 0-1.8-.3 1.7 1.7 0 0 0-1 1.5V21a2 2 0 0 1-4 0v-.1a1.7 1.7 0 0 0-1.1-1.5 1.7 1.7 0 0 0-1.8.3l-.1.1a2 2 0 1 1-2.8-2.8l.1-.1a1.7 1.7 0 0 0 .3-1.8 1.7 1.7 0 0 0-1.5-1H3a2 2 0 0 1 0-4h.1a1.7 1.7 0 0 0 1.5-1.1 1.7 1.7 0 0 0-.3-1.8l-.1-.1a2 2 0 1 1 2.8-2.8l.1.1a1.7 1.7 0 0 0 1.8.3H9a1.7 1.7 0 0 0 1-1.5V3a2 2 0 0 1 4 0v.1a1.7 1.7 0 0 0 1 1.5 1.7 1.7 0 0 0 1.8-.3l.1-.1a2 2 0 1 1 2.8 2.8l-.1.1a1.7 1.7 0 0 0-.3 1.8V9a1.7 1.7 0 0 0 1.5 1H21a2 2 0 0 1 0 4h-.1a1.7 1.7 0 0 0-1.5 1z"/></>,
  bell:      <><path d="M6 8a6 6 0 0 1 12 0c0 7 3 9 3 9H3s3-2 3-9"/><path d="M10.3 21a1.94 1.94 0 0 0 3.4 0"/></>,
  search:    <><circle cx="11" cy="11" r="7"/><path d="m21 21-4.3-4.3"/></>,
  plus:      <><path d="M12 5v14"/><path d="M5 12h14"/></>,
  check:     <path d="m5 12 5 5L20 7"/>,
  x:         <><path d="M18 6 6 18"/><path d="m6 6 12 12"/></>,
  arrowR:    <><path d="M5 12h14"/><path d="m12 5 7 7-7 7"/></>,
  arrowDown: <><path d="M12 5v14"/><path d="m5 12 7 7 7-7"/></>,
  arrowUp:   <><path d="M12 19V5"/><path d="m5 12 7-7 7 7"/></>,
  chevR:     <path d="m9 6 6 6-6 6"/>,
  chevD:     <path d="m6 9 6 6 6-6"/>,
  filter:    <path d="M3 5h18l-7 9v6l-4-2v-4z"/>,
  download:  <><path d="M12 4v12"/><path d="m7 11 5 5 5-5"/><path d="M5 20h14"/></>,
  excel:     <><rect x="3" y="3" width="18" height="18" rx="2"/><path d="M9 9l6 6m0-6l-6 6"/></>,
  dots:      <><circle cx="5" cy="12" r="1"/><circle cx="12" cy="12" r="1"/><circle cx="19" cy="12" r="1"/></>,
  edit:      <><path d="M11 4H5a2 2 0 0 0-2 2v13a2 2 0 0 0 2 2h13a2 2 0 0 0 2-2v-6"/><path d="M18.5 2.5a2.12 2.12 0 0 1 3 3L12 15l-4 1 1-4z"/></>,
  package:   <><path d="m21 8-9 4-9-4 9-4 9 4z"/><path d="M3 8v8l9 4 9-4V8"/><path d="m12 12 9-4M12 12v9M12 12 3 8"/></>,
  truck:     <><path d="M10 17h4V5H2v12h3"/><path d="M20 17h2v-3.4a2 2 0 0 0-.6-1.4l-2-2A2 2 0 0 0 18 9.6H14V17h3"/><circle cx="7.5" cy="17.5" r="2"/><circle cx="17.5" cy="17.5" r="2"/></>,
  bank:      <><path d="m3 21 18 0"/><path d="m5 21 0-10"/><path d="m9 21 0-10"/><path d="m15 21 0-10"/><path d="m19 21 0-10"/><path d="M3 11 12 3l9 8"/></>,
  clock:     <><circle cx="12" cy="12" r="9"/><path d="M12 7v5l3 2"/></>,
  scale:     <><path d="M12 3v18"/><path d="M6 21h12"/><path d="m6 9 6-6 6 6"/><path d="M3 12c0 1.5 1.5 3 3 3s3-1.5 3-3l-3-6z"/><path d="M15 12c0 1.5 1.5 3 3 3s3-1.5 3-3l-3-6z"/></>,
  users:     <><path d="M16 21v-2a4 4 0 0 0-4-4H6a4 4 0 0 0-4 4v2"/><circle cx="9" cy="7" r="4"/><path d="M22 21v-2a4 4 0 0 0-3-3.9"/><path d="M16 3.1a4 4 0 0 1 0 7.8"/></>,
  logout:    <><path d="M9 21H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h4"/><path d="m16 17 5-5-5-5"/><path d="M21 12H9"/></>,
  star:      <path d="M12 3.2 14.5 9l6.3.7-4.7 4.3 1.3 6.2L12 17.8 6.6 20.2 7.9 14 3.2 9.7 9.5 9z"/>,
  starFill:  <path d="M12 3.2 14.5 9l6.3.7-4.7 4.3 1.3 6.2L12 17.8 6.6 20.2 7.9 14 3.2 9.7 9.5 9z" fill="currentColor" stroke="none"/>,
  copy:      <><rect x="9" y="9" width="13" height="13" rx="2"/><path d="M5 15H4a2 2 0 0 1-2-2V4a2 2 0 0 1 2-2h9a2 2 0 0 1 2 2v1"/></>,
  globe:     <><circle cx="12" cy="12" r="9"/><path d="M3 12h18"/><path d="M12 3a15 15 0 0 1 0 18"/><path d="M12 3a15 15 0 0 0 0 18"/></>,
  menu:      <><path d="M4 7h16"/><path d="M4 12h16"/><path d="M4 17h16"/></>,
} as const

export type IconName = keyof typeof ICON_PATHS

export function Icon({ name, size = 16, stroke = 'currentColor', strokeWidth = 1.6 }:
  { name: IconName; size?: number; stroke?: string; strokeWidth?: number }) {
  return (
    <svg width={size} height={size} viewBox="0 0 24 24" fill="none" aria-hidden="true"
      stroke={stroke} strokeWidth={strokeWidth} strokeLinecap="round" strokeLinejoin="round">
      {ICON_PATHS[name]}
    </svg>
  )
}

// Подложка знака — акцентный синий, а НЕ --cover: он совпадает с цветом сайдбара,
// и на нём знак исчезал целиком, оставляя висеть в воздухе один белый лист.
// Синий читается и на графитовом сайдбаре, и на белой карточке входа.
// Смысл знака: спецификация, в которой позиция подобрана — отсюда галочка.
export function AppMark({ size = 28 }: { size?: number }) {
  return (
    <svg className="app-mark" width={size} height={size} viewBox="0 0 32 32" aria-hidden="true">
      <rect width="32" height="32" rx="7" fill="var(--accent)" />
      {/* лист с загнутым углом */}
      <path
        d="M8.5 5h11l5 5v16a1.5 1.5 0 0 1-1.5 1.5H8.5A1.5 1.5 0 0 1 7 26V6.5A1.5 1.5 0 0 1 8.5 5z"
        fill="var(--surface)"
      />
      <path d="M19.5 5l5 5h-5z" fill="var(--accent-border)" />
      {/* одна строка спецификации: на 16 px мелкие штрихи всё равно схлопываются */}
      <rect x="10.5" y="12.5" width="8" height="1.8" rx="0.9" fill="var(--accent-border)" />
      {/* галочка — позиция подобрана */}
      <path
        d="M11 20.5l3 3 6.5-6.5"
        fill="none"
        stroke="var(--accent)"
        strokeWidth="2.6"
        strokeLinecap="round"
        strokeLinejoin="round"
      />
    </svg>
  )
}

// ─── StatusPill ───────────────────────────────────────────────────
export type PillKind = 'gray' | 'amber' | 'blue' | 'green' | 'red'

const STATUS_KIND: Record<string, PillKind> = {
  'Черновик КП': 'gray',
  'Согласование РП': 'amber',
  'В коммерческом блоке': 'blue',
  'КП согласовано': 'blue',
  'Счёт создан': 'blue',
  'Ожидание оплаты': 'amber',
  'Оплачено': 'green',
  'Ожидание поставки': 'blue',
  'Пришёл на склад': 'green',
  'Отражено в 1С': 'green',
  'Отклонено': 'red',
}

const PILL_STYLE: Record<PillKind, { bg: string; fg: string }> = {
  gray:  { bg: 'var(--surface-2)', fg: 'var(--text-2)' },
  amber: { bg: 'var(--warn-bg)',   fg: 'var(--warn-fg)' },
  blue:  { bg: 'var(--info-bg)',   fg: 'var(--info-fg)' },
  green: { bg: 'var(--ok-bg)',     fg: 'var(--ok-fg)' },
  red:   { bg: 'var(--err-bg)',    fg: 'var(--err-fg)' },
}

export function StatusPill({ status, kind }: { status: string; kind?: PillKind }) {
  const s = PILL_STYLE[kind || STATUS_KIND[status] || 'gray']
  return (
    <span className="status-pill" style={{ background: s.bg, color: s.fg }}>{status}</span>
  )
}

// ─── LifecycleBar ─────────────────────────────────────────────────
export function LifecycleBar({ current, compact }: { current: string; compact?: boolean }) {
  const stages = LIFECYCLE_STAGES
  const idx = stages.findIndex(s => s.id === current)
  return (
    <div className="lifecycle-bar" style={{
      display: 'flex', alignItems: 'stretch', background: 'var(--surface)', color: 'var(--text)',
      border: '1px solid var(--border)', borderRadius: 8, padding: compact ? 4 : 6, overflow: 'auto',
    }}>
      {stages.map((s, i) => {
        const done = i < idx, active = i === idx, future = i > idx
        return (
          <div key={s.id} style={{
            flex: 1, padding: compact ? '4px 8px' : '8px 10px',
            display: 'flex', alignItems: 'center', gap: 8, fontSize: compact ? 11 : 12,
            color: future ? 'var(--text-3)' : active ? 'var(--accent)' : 'var(--text-2)',
            fontWeight: active ? 600 : 500,
            background: active ? 'var(--accent-soft)' : 'transparent',
            borderRadius: 6, whiteSpace: 'nowrap',
            borderRight: i < stages.length - 1 ? '1px solid var(--border)' : undefined,
          }}>
            <span style={{
              width: 16, height: 16, borderRadius: 999, display: 'inline-flex',
              alignItems: 'center', justifyContent: 'center',
              background: active || done ? 'var(--accent)' : 'var(--surface-2)',
              color: done || active ? 'var(--on-accent)' : 'var(--text-3)',
              fontSize: 10, fontWeight: 700,
            }}>{done ? <Icon name="check" size={10} stroke="var(--on-accent)" /> : i + 1}</span>
            {s.short}
          </div>
        )
      })}
    </div>
  )
}

// ─── Btn ──────────────────────────────────────────────────────────
type BtnKind = 'default' | 'primary' | 'ghost' | 'danger'

export function Btn({ children, kind = 'default', size = 'md', icon, iconRight, style, className, ...rest }:
  React.ButtonHTMLAttributes<HTMLButtonElement> & { kind?: BtnKind; size?: 'sm' | 'md'; icon?: IconName; iconRight?: IconName }) {
  const kinds: Record<BtnKind, React.CSSProperties> = {
    default: { background: 'var(--surface)', color: 'var(--text)', border: '1px solid var(--border-strong)' },
    primary: { background: 'var(--accent)', color: 'var(--on-accent)', border: '1px solid var(--accent)' },
    ghost:   { background: 'transparent', color: 'var(--text-2)', border: '1px solid transparent' },
    danger:  { background: 'var(--surface)', color: 'var(--err)', border: '1px solid var(--border)' },
  }
  const sz = size === 'sm'
    ? { padding: '4px 10px', fontSize: 12, height: 28, gap: 6 }
    : { padding: '7px 14px', fontSize: 13, height: 34, gap: 7 }
  const kindClass = kind === 'primary' ? 'btn-primary' : kind === 'ghost' ? 'btn-ghost' : kind === 'danger' ? 'btn-danger' : ''
  return (
    <button {...rest} className={['btn', kindClass, className].filter(Boolean).join(' ')} style={{
      ...kinds[kind], ...sz, display: 'inline-flex', alignItems: 'center',
      fontWeight: 500, cursor: 'pointer', ...style,
    }}>
      {icon && <Icon name={icon} size={size === 'sm' ? 12 : 14}/>}
      {children}
      {iconRight && <Icon name={iconRight} size={size === 'sm' ? 12 : 14}/>}
    </button>
  )
}
