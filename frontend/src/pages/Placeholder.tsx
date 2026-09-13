import { Shell } from '../components/Shell'

export function Placeholder({ title, breadcrumb }: { title: string; breadcrumb: string[] }) {
  return (
    <Shell breadcrumb={breadcrumb}>
      <div style={{ height: '100%', display: 'flex', flexDirection: 'column', alignItems: 'center', justifyContent: 'center', gap: 14, color: 'var(--text-2)' }}>
        <div style={{ width: 40, height: 40, borderRadius: 8, background: 'var(--accent-soft)' }} />
        <div className="page-head-title" style={{ fontSize: 22 }}>{title}</div>
        <div className="field-label">Экран в разработке</div>
      </div>
    </Shell>
  )
}
