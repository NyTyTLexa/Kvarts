import { useQuery } from '@tanstack/react-query'
import { Shell } from '../../components/Shell'
import { PageHead } from '../../components/PageHead'
import { Icon, StatusPill, type IconName } from '../../ui/atoms'
import { useApi } from '../../api/useApi'
import { THEMES, useTheme } from '../../ui/theme'
import type { IntegrationStatusDto } from '../../api/logistics'

// Служебные интерфейсы стенда. Раньше здесь лежали четыре неподписанные карточки
// без ссылок — выглядели как кнопки, но не нажимались. Теперь ведут в те системы,
// которые реально подняты рядом с приложением.
// Адреса хостовые: браузер открывает их снаружи сети контейнеров.
const SERVICES: { name: string; icon: IconName; note: string; url: string }[] = [
  { name: 'Keycloak',     icon: 'users',    note: 'учётки, роли, вход организации', url: 'http://localhost:8088' },
  { name: 'Grafana',      icon: 'chart',    note: 'трассы, логи и метрики сервисов', url: 'http://localhost:3000' },
  { name: 'Meilisearch',  icon: 'search',   note: 'поисковый индекс каталога',       url: 'http://localhost:7700' },
  { name: 'NATS',         icon: 'package',  note: 'очередь событий, JetStream',      url: 'http://localhost:8222' },
  { name: 'Описание API', icon: 'document', note: 'интерактивная спецификация',      url: 'http://localhost:5165/scalar' },
]

export function Settings() {
  const api = useApi()
  const live = useQuery({ queryKey: ['integration-status'], queryFn: () => api.get<IntegrationStatusDto[]>('/api/integration/status') })
  const [theme, setTheme] = useTheme()

  return (
    <Shell breadcrumb={['Настройки']}>
      <PageHead icon="settings" kicker="контур" title="Настройки"
        subtitle="Тема оформления и состояние учёта со склада." />

      <div className="field-label" style={{ marginBottom: 10 }}>Цвет темы</div>
      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fill, minmax(180px, 1fr))', gap: 10, marginBottom: 22 }}>
        {THEMES.map(t => (
          <button key={t.id} type="button" className={'theme-swatch' + (theme === t.id ? ' is-on' : '')}
            onClick={() => setTheme(t.id)}>
            <span style={{ display: 'block', height: 28, borderRadius: 5, background: t.swatch, marginBottom: 8 }} />
            <div style={{ fontWeight: 600, fontSize: 13.5 }}>{t.label}</div>
            <div style={{ fontSize: 12, color: 'var(--text-3)' }}>{t.hint}</div>
          </button>
        ))}
      </div>

      {live.data && live.data.length > 0 && (
        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fill, minmax(260px, 1fr))', gap: 12, marginBottom: 16 }}>
          {live.data.map(s => (
            <div key={s.kind} style={{ background: 'var(--surface)', borderRadius: 8, padding: '14px 16px', border: '1px solid var(--border)' }}>
              <div style={{ display: 'flex', alignItems: 'center', gap: 10 }}>
                <Icon name={s.kind === 'accounting' ? 'document' : 'truck'} size={16} stroke="var(--text-2)" />
                <div style={{ flex: 1, minWidth: 0 }}>
                  <div style={{ fontWeight: 600, fontSize: 13.5 }}>{s.system}</div>
                  <div style={{ fontSize: 12, color: 'var(--text-3)' }}>{s.kind === 'accounting' ? 'учёт' : 'склад'}</div>
                </div>
                <StatusPill status={s.available ? 'доступна' : 'нет связи'} kind={s.available ? 'green' : 'red'} />
              </div>
            </div>
          ))}
        </div>
      )}

      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fill, minmax(260px, 1fr))', gap: 12 }}>
        {SERVICES.map(it => (
          <a key={it.name} className="svc-card" href={it.url} target="_blank" rel="noreferrer"
            title={`Открыть ${it.name} в новой вкладке`}>
            <Icon name={it.icon} size={16} stroke="var(--accent)" />
            <div style={{ flex: 1, minWidth: 0 }}>
              <div style={{ fontWeight: 600, fontSize: 13.5 }}>{it.name}</div>
              <div style={{ fontSize: 12, color: 'var(--text-3)' }}>{it.note}</div>
            </div>
            <Icon name="globe" size={14} stroke="var(--text-3)" />
          </a>
        ))}
      </div>
    </Shell>
  )
}
