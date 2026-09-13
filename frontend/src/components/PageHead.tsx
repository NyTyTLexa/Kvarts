import { Icon, type IconName } from '../ui/atoms'

export function PageHead({ icon, kicker, title, subtitle, badge }:
  { icon?: IconName; kicker?: string; title: string; subtitle?: string; badge?: { text: string; kind: 'live' | 'mock' } }) {
  return (
    <header className="page-head">
      {kicker && <div className="page-kicker">{kicker}</div>}
      <div className="page-head-row">
        {icon && (
          <div className="page-head-icon">
            <Icon name={icon} size={18} stroke="var(--accent)" strokeWidth={1.6} />
          </div>
        )}
        <h1 className="page-head-title">{title}</h1>
        {badge?.kind === 'mock' && <span className="status-pill mock">макет</span>}
      </div>
      {subtitle && <p className="page-head-sub">{subtitle}</p>}
    </header>
  )
}
