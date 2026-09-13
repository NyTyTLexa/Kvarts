import { Link } from 'react-router-dom'
import { Shell } from '../components/Shell'
import { Btn } from '../ui/atoms'
import { useRoles } from '../auth/useRoles'

export function Forbidden({ path }: { path?: string }) {
  const { role, home } = useRoles()
  return (
    <Shell breadcrumb={['Нет доступа']}>
      <header className="page-head">
        <div style={{ display: 'flex', alignItems: 'center', gap: 10 }}>
          <h1 className="page-head-title">Раздел закрыт для вашей роли</h1>
          <span className="status-pill mock">{role.short}</span>
        </div>
        <p className="page-head-sub" style={{ marginLeft: 0 }}>
          {role.label} не работает в {path || 'этом разделе'}. Контур роли — другой набор экранов, это разделение обязанностей из ТЗ, не ошибка входа.
        </p>
      </header>
      <Link to={home}><Btn kind="primary">К своему разделу</Btn></Link>
    </Shell>
  )
}
