import { useQuery } from '@tanstack/react-query'
import { Shell } from '../components/Shell'
import { PageHead } from '../components/PageHead'
import { Icon } from '../ui/atoms'
import { useApi } from '../api/useApi'
import { useRoles } from '../auth/useRoles'
import type { AuditEntryDto } from '../api/types'

function codeColor(c: number) {
  if (c >= 500) return 'var(--err)'
  if (c >= 400) return 'var(--warn)'
  if (c >= 200 && c < 300) return 'var(--ok)'
  return 'var(--text-2)'
}

function shown(value?: string | null) {
  return value ?? '—'
}

export function Audit() {
  const api = useApi()
  const { canAdmin } = useRoles()

  const log = useQuery({
    queryKey: ['audit'],
    queryFn: () => api.get<AuditEntryDto[]>('/api/audit?limit=200'),
    enabled: canAdmin,
  })

  return (
    <Shell breadcrumb={['Журнал']}>
      <PageHead kicker="система · кто менял" title="Журнал"
        subtitle="Кто, что и когда менял через систему." />

      {!canAdmin && (
        <div style={{ display: 'flex', alignItems: 'center', gap: 10, padding: 16, background: 'var(--warn-bg)', color: 'var(--warn)', borderRadius: 10, fontSize: 13.5 }}>
          <Icon name="users" size={18}/> Журнал доступен только администратору.
        </div>
      )}

      {canAdmin && (
        <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: 12.5 }}>
          <thead>
            <tr>{['Время', 'Пользователь', 'Метод', 'Путь', 'Изменения', 'Код'].map((h, i) => (
              <th key={h} style={{ padding: '9px 12px', textAlign: i === 5 ? 'right' : 'left', fontWeight: 500, fontSize: 11, color: 'var(--text-2)', textTransform: 'uppercase', letterSpacing: 0.5, borderBottom: '1px solid var(--border)' }}>{h}</th>
            ))}</tr>
          </thead>
          <tbody>
            {log.data?.map((a, i) => (
              <tr key={i} style={{ borderBottom: '1px solid var(--border)' }}>
                <td style={{ padding: '9px 12px', color: 'var(--text-2)', whiteSpace: 'nowrap' }}>{new Date(a.occurredAtUtc).toLocaleString('ru-RU')}</td>
                <td style={{ padding: '9px 12px', fontWeight: 500 }}>{a.userName ?? '—'}</td>
                <td style={{ padding: '9px 12px' }}>
                  <span className="mono" style={{ fontSize: 11, padding: '2px 7px', borderRadius: 4, background: 'var(--surface-2)' }}>{a.action}</span>
                </td>
                <td className="mono" style={{ padding: '9px 12px', fontSize: 11.5, color: 'var(--text-2)' }}>{a.path}</td>
                <td style={{ padding: '9px 12px', minWidth: 220 }}>
                  {a.changes?.length ? (
                    <details>
                      <summary style={{ cursor: 'pointer', color: 'var(--accent)', whiteSpace: 'nowrap' }}>
                        {a.changes[0].entity} · {a.changes.length}
                      </summary>
                      <div style={{ display: 'grid', gap: 5, marginTop: 7, maxHeight: 160, overflow: 'auto' }}>
                        {a.changes.map((change, changeIndex) => (
                          <div key={`${change.entityId}-${change.property}-${changeIndex}`} style={{ color: 'var(--text-2)', fontSize: 11.5 }}>
                            <span className="mono" style={{ color: 'var(--text-1)' }}>{change.property}</span>
                            {': '}{shown(change.oldValue)} → {shown(change.newValue)}
                          </div>
                        ))}
                      </div>
                    </details>
                  ) : <span style={{ color: 'var(--text-3)' }}>—</span>}
                </td>
                <td className="tnum" style={{ padding: '9px 12px', textAlign: 'right', fontWeight: 600, color: codeColor(a.statusCode) }}>{a.statusCode}</td>
              </tr>
            ))}
            {log.isLoading && <tr><td colSpan={6} style={{ padding: 16, color: 'var(--text-3)' }}>Загрузка…</td></tr>}
            {log.data && log.data.length === 0 && <tr><td colSpan={6} style={{ padding: 16, color: 'var(--text-3)' }}>Записей пока нет</td></tr>}
          </tbody>
        </table>
      )}
    </Shell>
  )
}
