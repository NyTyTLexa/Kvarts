// Shared UI atoms used by both variants. The variants distinguish via .v1/.v2
// CSS scope and slightly different inline styles where needed.

const fmt = (n) => n.toLocaleString('ru-RU').replace(/,/g, ' ');
const fmtMoney = (n) => fmt(Math.round(n)) + ' ₽';
const fmtMoneyShort = (n) => {
  if (n >= 1_000_000) return (n / 1_000_000).toFixed(1).replace('.', ',') + ' млн ₽';
  if (n >= 1_000) return (n / 1_000).toFixed(0) + ' тыс ₽';
  return fmt(n) + ' ₽';
};

// Icon set — minimal stroke SVG, sized at parent font size
function Icon({ name, size = 16, stroke = 'currentColor', strokeWidth = 1.6 }) {
  const common = { width: size, height: size, viewBox: '0 0 24 24', fill: 'none', stroke, strokeWidth, strokeLinecap: 'round', strokeLinejoin: 'round' };
  const paths = {
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
    sparkle:   <path d="M12 2 14 9l7 2-7 2-2 7-2-7-7-2 7-2z"/>,
    flame:     <path d="M12 2c2 4 6 6 6 12a6 6 0 1 1-12 0c0-3 1.5-5 3-6 .5 2 1 3 3 4 0-3-1-5 0-10z"/>,
    clock:     <><circle cx="12" cy="12" r="9"/><path d="M12 7v5l3 2"/></>,
    scale:     <><path d="M12 3v18"/><path d="M6 21h12"/><path d="m6 9 6-6 6 6"/><path d="M3 12c0 1.5 1.5 3 3 3s3-1.5 3-3l-3-6z"/><path d="M15 12c0 1.5 1.5 3 3 3s3-1.5 3-3l-3-6z"/></>,
    users:     <><path d="M16 21v-2a4 4 0 0 0-4-4H6a4 4 0 0 0-4 4v2"/><circle cx="9" cy="7" r="4"/><path d="M22 21v-2a4 4 0 0 0-3-3.9"/><path d="M16 3.1a4 4 0 0 1 0 7.8"/></>,
  };
  return <svg {...common}>{paths[name]}</svg>;
}

// Status pill with semantic color
function StatusPill({ status, kind }) {
  // Map every status to a kind
  const map = {
    'Черновик КП':         'gray',
    'Согласование РП':     'amber',
    'В коммерческом блоке':'blue',
    'КП согласовано':      'blue',
    'Счёт создан':         'blue',
    'Ожидание оплаты':     'amber',
    'Оплачено':            'green',
    'Ожидание поставки':   'blue',
    'Пришёл на склад':     'green',
    'Отражено в 1С':       'green',
    'Отклонено':           'red',
  };
  const k = kind || map[status] || 'gray';
  const styles = {
    gray:  { bg: 'var(--stone-100)', fg: 'var(--stone-700)', dot: 'var(--stone-500)' },
    amber: { bg: 'var(--warn-bg)',   fg: '#92400e',           dot: '#d97706' },
    blue:  { bg: 'var(--info-bg)',   fg: '#1e40af',           dot: '#2563eb' },
    green: { bg: 'var(--ok-bg)',     fg: '#166534',           dot: '#16a34a' },
    red:   { bg: 'var(--err-bg)',    fg: '#991b1b',           dot: '#dc2626' },
  }[k];
  return (
    <span style={{
      display: 'inline-flex', alignItems: 'center', gap: 6,
      padding: '3px 8px 3px 7px', borderRadius: 999,
      background: styles.bg, color: styles.fg, fontSize: 12, fontWeight: 500,
      whiteSpace: 'nowrap', lineHeight: 1.4,
    }}>
      <span style={{ width: 6, height: 6, borderRadius: 999, background: styles.dot, flexShrink: 0 }}/>
      {status}
    </span>
  );
}

// Lifecycle progress bar — central reusable component
function LifecycleBar({ current, compact }) {
  const stages = window.LIFECYCLE_STAGES;
  const idx = stages.findIndex(s => s.id === current);
  return (
    <div style={{ display: 'flex', alignItems: 'stretch', gap: 0, background: 'var(--surface)', border: '1px solid var(--border)', borderRadius: 8, padding: compact ? 4 : 6, overflow: 'hidden' }}>
      {stages.map((s, i) => {
        const done = i < idx, active = i === idx, future = i > idx;
        return (
          <React.Fragment key={s.id}>
            <div style={{
              flex: 1, padding: compact ? '4px 8px' : '8px 10px',
              display: 'flex', alignItems: 'center', gap: 8,
              fontSize: compact ? 11 : 12,
              color: future ? 'var(--text-3)' : active ? 'var(--accent)' : 'var(--text-2)',
              fontWeight: active ? 600 : 500,
              background: active ? 'var(--accent-soft)' : 'transparent',
              borderRadius: 6, whiteSpace: 'nowrap',
            }}>
              <span style={{
                width: 16, height: 16, borderRadius: 999,
                display: 'inline-flex', alignItems: 'center', justifyContent: 'center',
                background: done ? 'var(--accent)' : active ? 'var(--accent)' : 'var(--stone-200)',
                color: done || active ? 'white' : 'var(--text-3)',
                fontSize: 10, fontWeight: 600,
              }}>
                {done ? '✓' : i + 1}
              </span>
              {s.short}
            </div>
            {i < stages.length - 1 && (
              <div style={{ width: 1, alignSelf: 'center', height: compact ? 16 : 20, background: 'var(--border)' }}/>
            )}
          </React.Fragment>
        );
      })}
    </div>
  );
}

// Reusable button
function Btn({ children, kind = 'default', size = 'md', icon, iconRight, style, ...rest }) {
  const styles = {
    default: { background: 'var(--surface)', color: 'var(--text)', border: '1px solid var(--border-strong)' },
    primary: { background: 'var(--accent)', color: 'white', border: '1px solid var(--accent)' },
    ghost:   { background: 'transparent', color: 'var(--text-2)', border: '1px solid transparent' },
    danger:  { background: 'var(--surface)', color: 'var(--err)', border: '1px solid var(--border)' },
  }[kind];
  const sz = size === 'sm'
    ? { padding: '4px 10px', fontSize: 12, height: 26, gap: 6 }
    : { padding: '7px 14px', fontSize: 13, height: 34, gap: 7 };
  return (
    <button {...rest} style={{
      ...styles, ...sz, borderRadius: 7, display: 'inline-flex', alignItems: 'center',
      fontWeight: 500, cursor: 'pointer', fontFamily: 'inherit',
      ...style,
    }}>
      {icon && <Icon name={icon} size={size === 'sm' ? 12 : 14}/>}
      {children}
      {iconRight && <Icon name={iconRight} size={size === 'sm' ? 12 : 14}/>}
    </button>
  );
}

// Sidebar nav item
function NavItem({ icon, label, active, count, children }) {
  return (
    <div style={{
      display: 'flex', alignItems: 'center', gap: 10,
      padding: '6px 10px', margin: '1px 8px', borderRadius: 6,
      color: active ? 'var(--sidebar-text-strong)' : 'var(--sidebar-text)',
      background: active ? 'var(--sidebar-active-bg)' : 'transparent',
      fontSize: 13, fontWeight: active ? 500 : 400,
      cursor: 'pointer', lineHeight: 1.2,
    }}>
      <Icon name={icon} size={15}/>
      <span style={{ flex: 1 }}>{label}</span>
      {count != null && (
        <span style={{ fontSize: 11, color: 'var(--text-3)', fontVariantNumeric: 'tabular-nums' }}>{count}</span>
      )}
    </div>
  );
}

Object.assign(window, {
  fmt, fmtMoney, fmtMoneyShort, Icon, StatusPill, LifecycleBar, Btn, NavItem,
});
