import { useQuery } from '@tanstack/react-query'
import { Shell } from '../../components/Shell'
import { PageHead } from '../../components/PageHead'
import { Icon, StatusPill } from '../../ui/atoms'
import { useApi } from '../../api/useApi'
import { useRoles } from '../../auth/useRoles'

type UserDto = {
  userName: string
  email?: string
  displayName: string
  roles: string[]
  enabled: boolean
  lastActivityUtc?: string
}

const ROLE_LABELS: Record<string, string> = {
  admin: 'Администратор',
  manager: 'РП',
  commercial: 'КБ',
  accounting: 'Бухгалтерия',
  warehouse: 'Склад',
  viewer: 'Наблюдатель',
}

function initials(user: UserDto) {
  return (user.displayName || user.userName)
    .replace(/[._-]/g, ' ')
    .split(' ')
    .filter(Boolean)
    .map(p => p[0])
    .join('')
    .slice(0, 2)
    .toUpperCase()
}

export function Users() {
  const api = useApi()
  const { canAdmin } = useRoles()
  const users = useQuery({
    queryKey: ['users'],
    queryFn: () => api.get<UserDto[]>('/api/users'),
    enabled: canAdmin,
  })

  return (
    <Shell breadcrumb={['Люди']}>
      <PageHead kicker="система · учётки" title="Люди"
        subtitle="Новых регистрирует форма входа, роль по умолчанию — просмотр." />

      {!canAdmin && (
        <div style={{ display: 'flex', alignItems: 'center', gap: 10, padding: 16, background: 'var(--warn-bg)', color: 'var(--warn)', borderRadius: 10, fontSize: 13.5 }}>
          <Icon name="users" size={18}/> Раздел доступен только администратору.
        </div>
      )}

      {canAdmin && users.isLoading && <div style={{ color: 'var(--text-3)', fontSize: 13 }}>Загрузка пользователей…</div>}
      {canAdmin && users.isError && <div style={{ color: 'var(--err)', background: 'var(--err-bg)', padding: 14, borderRadius: 8, fontSize: 13 }}>
        Не удалось загрузить пользователей{(users.error as Error)?.message ? `: ${(users.error as Error).message}` : '.'}
      </div>}

      {canAdmin && users.data && (
        <div className="doc-list">
        <table>
          <thead>
            <tr>{['Пользователь', 'Роли', 'Источник', 'Последняя активность', 'Статус'].map((h) => (
              <th key={h} style={{ padding: '10px 12px', textAlign: 'left', fontWeight: 500, fontSize: 11, color: 'var(--text-2)', textTransform: 'uppercase', letterSpacing: 0.6, borderBottom: '1px solid var(--border)' }}>{h}</th>
            ))}</tr>
          </thead>
          <tbody>
            {users.data.map((u) => {
              return (
                <tr key={u.userName} style={{ borderBottom: '1px solid var(--border)' }}>
                  <td style={{ padding: '11px 12px' }}>
                    <div style={{ display: 'flex', alignItems: 'center', gap: 10 }}>
                      <div style={{ width: 28, height: 28, borderRadius: 4, background: 'var(--accent-soft)', color: 'var(--accent-2)', display: 'flex', alignItems: 'center', justifyContent: 'center', fontSize: 11, fontWeight: 600 }}>{initials(u)}</div>
                      <div>
                        <div style={{ fontWeight: 500 }}>{u.displayName}</div>
                        <div className="mono" style={{ fontSize: 11, color: 'var(--text-3)' }}>{u.email ?? u.userName}</div>
                      </div>
                    </div>
                  </td>
                  <td style={{ padding: '11px 12px' }}>
                    <div style={{ display: 'flex', flexWrap: 'wrap', gap: 6 }}>
                      {(u.roles.length ? u.roles : ['viewer']).map(r => (
                        <StatusPill key={r} status={ROLE_LABELS[r] ?? r} />
                      ))}
                    </div>
                  </td>
                  <td style={{ padding: '11px 12px', color: 'var(--text-2)' }}>организация</td>
                  <td style={{ padding: '11px 12px', color: 'var(--text-2)' }}>{u.lastActivityUtc ? new Date(u.lastActivityUtc).toLocaleString('ru-RU') : '—'}</td>
                  <td style={{ padding: '11px 12px' }}>
                    <StatusPill status={u.enabled ? 'Активен' : 'Неактивен'} kind={u.enabled ? 'green' : 'red'} />
                  </td>
                </tr>
              )
            })}
            {users.data.length === 0 && <tr><td colSpan={5} style={{ padding: 16, color: 'var(--text-3)' }}>Пользователей пока нет</td></tr>}
          </tbody>
        </table>
        </div>
      )}
    </Shell>
  )
}
