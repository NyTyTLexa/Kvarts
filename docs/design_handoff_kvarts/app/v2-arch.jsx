// B22 — Architecture diagram of the distributed system.

function V2Arch() {
  return (
    <V2Shell active="settings" breadcrumb={['Документация', 'Архитектура']} role="admin"
      actions={<>
        <Btn size="sm" kind="ghost" icon="download">PDF</Btn>
        <Btn size="sm" kind="ghost">Поделиться</Btn>
      </>}
    >
      <div style={{ marginBottom: 22 }}>
        <h1 style={{ margin: 0, fontSize: 26, fontWeight: 600, letterSpacing: -0.4 }}>Архитектура системы</h1>
        <p style={{ margin: '6px 0 0', color: 'var(--text-2)', fontSize: 13.5, maxWidth: 880 }}>
          Распределённая информационная система автоматизации закупок и интеллектуального подбора оборудования.
          Микросервисы на .NET 8, событийная синхронизация через RabbitMQ, поисковый индекс Elasticsearch, IAM через Keycloak.
        </p>
      </div>

      {/* Tech stack badges */}
      <div style={{ display: 'flex', flexWrap: 'wrap', gap: 6, marginBottom: 26 }}>
        {[
          ['.NET 8 / C#',      '#dbeafe', '#1e40af'],
          ['ASP.NET Core',     '#dbeafe', '#1e40af'],
          ['React + TS',       '#bae6fd', '#075985'],
          ['PostgreSQL',       '#dcfce7', '#166534'],
          ['Elasticsearch',    '#fef3c7', '#92400e'],
          ['RabbitMQ',         '#fed7aa', '#9a3412'],
          ['MassTransit',      '#fed7aa', '#9a3412'],
          ['Keycloak',         '#fecaca', '#991b1b'],
          ['OpenTelemetry',    '#e9d5ff', '#6b21a8'],
          ['Docker Compose',   '#bae6fd', '#075985'],
        ].map(([l, bg, fg], i) => (
          <span key={i} style={{ padding: '4px 10px', borderRadius: 999, background: bg, color: fg, fontSize: 11.5, fontWeight: 500, fontFamily: 'JetBrains Mono', letterSpacing: 0.2 }}>{l}</span>
        ))}
      </div>

      {/* Layer 1: Clients */}
      <ArchLayer num="01" title="Клиентский уровень" tag="USERS · BROWSERS">
        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(5, 1fr)', gap: 10 }}>
          {[
            { role: 'РП',  desc: 'Загрузка прайсов, КП, проекты', bg: '#fed7aa', fg: '#9a3412' },
            { role: 'КБ',  desc: 'Маржинальность, согласование',   bg: '#bae6fd', fg: '#075985' },
            { role: 'БУХ', desc: 'Оплата, договоры',                bg: '#bbf7d0', fg: '#14532d' },
            { role: 'СК',  desc: 'Приёмка, отметка в 1С',          bg: '#e9d5ff', fg: '#6b21a8' },
            { role: 'АДМ', desc: 'Пользователи, настройки',         bg: '#fecaca', fg: '#991b1b' },
          ].map((r, i) => (
            <ArchNode key={i} icon={r.role} iconBg={r.bg} iconFg={r.fg} title={`Роль · ${r.role}`} sub={r.desc}/>
          ))}
        </div>
      </ArchLayer>

      <ArchConnector label="HTTPS · OAuth 2.0 token"/>

      {/* Layer 2: Frontend */}
      <ArchLayer num="02" title="Фронтенд" tag="REACT · TYPESCRIPT">
        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(3, 1fr)', gap: 10 }}>
          <ArchNode title="React SPA" sub="TypeScript · Vite · React Query" badges={['react', 'ts', 'vite']}/>
          <ArchNode title="UI-кит «Кварц»" sub="22 экрана · 5 ролей · оранжевый акцент" badges={['css-modules', 'radix']}/>
          <ArchNode title="Auth client" sub="OIDC через Keycloak Adapter · refresh token rotation" badges={['oidc-client-ts']}/>
        </div>
      </ArchLayer>

      <ArchConnector label="REST · /api/v1 · JWT в заголовках"/>

      {/* Layer 3: API Gateway + Auth */}
      <ArchLayer num="03" title="API Gateway и аутентификация" tag="EDGE LAYER">
        <div style={{ display: 'grid', gridTemplateColumns: '2fr 1fr', gap: 10 }}>
          <ArchNode title="API Gateway / YARP" sub="Маршрутизация, rate limit, CORS, OpenAPI агрегация" badges={['YARP', 'ASP.NET']}/>
          <ArchNode warn title="Keycloak (Docker)" sub="IAM · RBAC · OAuth 2.0 / OIDC · realm «kvarts»" badges={['keycloak']} icon="K" iconBg="#fecaca" iconFg="#991b1b"/>
        </div>
      </ArchLayer>

      <ArchConnector label="gRPC · события через шину"/>

      {/* Layer 4: Microservices */}
      <ArchLayer num="04" title="Микросервисы (.NET 8)" tag="DOMAIN LAYER">
        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(4, 1fr)', gap: 10 }}>
          {[
            { t: 'Pricing Service',     sub: 'Прайсы, скидки, нормализация',                badges: ['CRUD', 'CQRS'] },
            { t: 'Catalog Service',     sub: 'Номенклатура, производители, иерархия',      badges: ['DDD'] },
            { t: 'Project Service',     sub: 'Проекты, перечни, состояния',                badges: ['EF Core'] },
            { t: 'Quote Service',       sub: 'Генерация 3 вариантов КП · веса',            badges: ['алгоритмы', 'pricing'] },
            { t: 'Matching Service',    sub: 'Fuzzy matching · аналоги · ML',              badges: ['ML.NET', 'TF-IDF'] },
            { t: 'Approval Service',    sub: 'Маршруты согласования · BPM',                badges: ['workflow'] },
            { t: 'NOC Integration',     sub: 'Создание счетов в NOC · статусы',            badges: ['adapter'] },
            { t: 'Audit Service',       sub: 'Журнал событий · 3 года ретенция',           badges: ['append-only'] },
          ].map((s, i) => (
            <ArchNode key={i} small title={s.t} sub={s.sub} badges={s.badges}/>
          ))}
        </div>
      </ArchLayer>

      <ArchConnector label="События доменов · Outbox pattern"/>

      {/* Layer 5: Message bus */}
      <ArchLayer num="05" title="Шина сообщений" tag="EVENT-DRIVEN">
        <div style={{ display: 'grid', gridTemplateColumns: '1fr 2fr', gap: 10 }}>
          <ArchNode title="RabbitMQ" sub="3 узла кластер · durable очереди · DLX" badges={['AMQP']} icon="R" iconBg="#fed7aa" iconFg="#9a3412"/>
          <ArchNode title="MassTransit" sub="Saga для жизненного цикла КП → склад → 1С · retry с экспонентой · идемпотентность" badges={['saga', 'idempotency', 'retry']}/>
        </div>
        {/* Event topology */}
        <div style={{ marginTop: 10, padding: '12px 14px', background: 'var(--surface-2)', borderRadius: 8, fontSize: 12 }}>
          <div style={{ fontSize: 11, color: 'var(--text-2)', textTransform: 'uppercase', letterSpacing: 0.5, fontWeight: 500, marginBottom: 8 }}>Ключевые события</div>
          <div style={{ display: 'grid', gridTemplateColumns: 'repeat(3, 1fr)', gap: '4px 18px', fontFamily: 'JetBrains Mono', fontSize: 11.5 }}>
            {[
              'PriceListUploaded',
              'ProjectCreated',
              'PositionMatched',
              'QuoteGenerated',
              'QuoteApproved',
              'NocInvoiceCreated',
              'PaymentRegistered',
              'ShipmentArrived',
              'PostedTo1C',
            ].map(e => <span key={e} style={{ color: 'var(--text-2)' }}>· <span style={{ color: 'var(--accent)' }}>{e}</span></span>)}
          </div>
        </div>
      </ArchLayer>

      <ArchConnector label="Транзакционная запись · потребление событий"/>

      {/* Layer 6: Data layer */}
      <ArchLayer num="06" title="Хранилища" tag="DATA LAYER">
        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(3, 1fr)', gap: 10 }}>
          <ArchNode title="PostgreSQL 16" sub="Основное транзакционное хранилище · 12 схем · WAL архивация" badges={['ACID', 'EF Core', 'WAL-G']} icon="Pg" iconBg="#dcfce7" iconFg="#166534"/>
          <ArchNode title="Elasticsearch 8" sub="Поисковый индекс · номенклатура + прайсы · fuzzy · 7M документов" badges={['fuzzy', 'aggregations']} icon="Es" iconBg="#fef3c7" iconFg="#92400e"/>
          <ArchNode title="MinIO (S3)" sub="Файлы прайсов, документов, версий КП · 5 лет ретенция" badges={['S3', 'lifecycle']} icon="S3" iconBg="#bae6fd" iconFg="#075985"/>
        </div>
      </ArchLayer>

      <ArchConnector label="HTTP / API клиенты · файловый обмен"/>

      {/* Layer 7: External integrations */}
      <ArchLayer num="07" title="Внешние системы" tag="INTEGRATIONS">
        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(5, 1fr)', gap: 10 }}>
          <ArchNode small title="NOC" sub="Счета и согласование" badges={['API']}/>
          <ArchNode small title="1С Бухгалтерия 8.3" sub="Приходы, СФ" badges={['1C HTTP']}/>
          <ArchNode small title="AD / LDAP" sub="Учётки сотрудников" badges={['SSO']} warn/>
          <ArchNode small title="SMTP / Email" sub="Уведомления" badges={['SMTP']}/>
          <ArchNode small title="Корп. мессенджер" sub="Push согласований" badges={['webhook']} warn/>
        </div>
      </ArchLayer>

      {/* Cross-cutting: Observability + Resilience */}
      <div style={{ marginTop: 28, display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 18 }}>
        <div style={{ background: 'var(--surface)', border: '1px solid var(--border)', borderRadius: 10, padding: '16px 18px' }}>
          <div style={{ fontSize: 11, color: 'var(--text-2)', fontWeight: 600, textTransform: 'uppercase', letterSpacing: 0.5, marginBottom: 12 }}>Наблюдаемость · cross-cutting</div>
          {[
            ['OpenTelemetry SDK',      'Трейсы из API + шины + БД', '.NET Otel'],
            ['Jaeger / Tempo',         'Распределённая трассировка', 'Tempo'],
            ['Prometheus + Grafana',   'Метрики RED/USE',            'Prometheus'],
            ['Seq / Elastic + Kibana', 'Структурированные логи',     'Serilog'],
          ].map(([l, d, b], i) => (
            <div key={i} style={{ display: 'flex', alignItems: 'center', gap: 10, padding: '8px 0', borderTop: i === 0 ? 'none' : '1px solid var(--border)' }}>
              <span style={{ width: 6, height: 6, borderRadius: 999, background: 'var(--accent)' }}/>
              <div style={{ flex: 1 }}>
                <div style={{ fontSize: 12.5, fontWeight: 500 }}>{l}</div>
                <div style={{ fontSize: 11, color: 'var(--text-2)' }}>{d}</div>
              </div>
              <span style={{ padding: '2px 7px', borderRadius: 4, background: 'var(--surface-2)', fontSize: 10.5, fontFamily: 'JetBrains Mono' }}>{b}</span>
            </div>
          ))}
        </div>

        <div style={{ background: 'var(--surface)', border: '1px solid var(--border)', borderRadius: 10, padding: '16px 18px' }}>
          <div style={{ fontSize: 11, color: 'var(--text-2)', fontWeight: 600, textTransform: 'uppercase', letterSpacing: 0.5, marginBottom: 12 }}>Отказоустойчивость · паттерны</div>
          {[
            ['Outbox',         'Гарантированная доставка событий в шину',  'PostgreSQL'],
            ['Saga (MassTransit)', 'Управление жизненным циклом КП',         'in-process'],
            ['Idempotent consumers', 'Дедупликация по MessageId',             'Redis'],
            ['Circuit Breaker', 'Защита от каскадных отказов NOC/1C',      'Polly'],
            ['Read replicas',  'Реплика PG для аналитики',                    'PG streaming'],
          ].map(([l, d, b], i) => (
            <div key={i} style={{ display: 'flex', alignItems: 'center', gap: 10, padding: '8px 0', borderTop: i === 0 ? 'none' : '1px solid var(--border)' }}>
              <span style={{ width: 6, height: 6, borderRadius: 999, background: '#15803d' }}/>
              <div style={{ flex: 1 }}>
                <div style={{ fontSize: 12.5, fontWeight: 500 }}>{l}</div>
                <div style={{ fontSize: 11, color: 'var(--text-2)' }}>{d}</div>
              </div>
              <span style={{ padding: '2px 7px', borderRadius: 4, background: 'var(--surface-2)', fontSize: 10.5, fontFamily: 'JetBrains Mono' }}>{b}</span>
            </div>
          ))}
        </div>
      </div>

      {/* Docker Compose footer */}
      <div style={{ marginTop: 22, padding: '14px 18px', background: 'var(--surface-2)', borderRadius: 10, display: 'flex', alignItems: 'center', gap: 14 }}>
        <div style={{ width: 36, height: 36, borderRadius: 7, background: '#bae6fd', color: '#075985', display: 'flex', alignItems: 'center', justifyContent: 'center', fontFamily: 'JetBrains Mono', fontWeight: 700, fontSize: 15 }}>{'<>'}</div>
        <div style={{ flex: 1 }}>
          <div style={{ fontSize: 13, fontWeight: 600 }}>Docker Compose</div>
          <div style={{ fontSize: 12, color: 'var(--text-2)', marginTop: 2 }}>
            Полная контейнеризация: <span className="mono">api-gateway · 8 services · keycloak · postgres · elasticsearch · rabbitmq · minio · prometheus · jaeger</span> — всего 16 контейнеров.
          </div>
        </div>
        <span className="mono" style={{ fontSize: 11, color: 'var(--text-3)' }}>docker compose up -d</span>
      </div>
    </V2Shell>
  );
}

// Helper components ───────────────────────────────────────────────
function ArchLayer({ num, title, tag, children }) {
  return (
    <div style={{ background: 'var(--surface)', border: '1px solid var(--border)', borderRadius: 11, overflow: 'hidden' }}>
      <div style={{ display: 'flex', alignItems: 'stretch', borderBottom: '1px solid var(--border)' }}>
        <span className="mono" style={{ padding: '11px 18px', fontSize: 11, letterSpacing: '0.12em', color: 'var(--text-3)', borderRight: '1px solid var(--border)', display: 'flex', alignItems: 'center' }}>{num}</span>
        <div style={{ padding: '11px 18px', display: 'flex', alignItems: 'center', flex: 1, fontSize: 14, fontWeight: 600 }}>{title}</div>
        <span className="mono" style={{ padding: '11px 18px', fontSize: 10.5, letterSpacing: '0.12em', color: 'var(--accent)', borderLeft: '1px solid var(--border)', display: 'flex', alignItems: 'center' }}>{tag}</span>
      </div>
      <div style={{ padding: 12 }}>{children}</div>
    </div>
  );
}

function ArchNode({ title, sub, badges, small, warn, icon, iconBg, iconFg }) {
  return (
    <div style={{
      background: warn ? '#fef9f3' : 'var(--surface)',
      border: '1px solid', borderColor: warn ? '#fdd9b4' : 'var(--border-strong)',
      borderRadius: 8, padding: small ? '10px 12px' : '12px 14px',
    }}>
      <div style={{ display: 'flex', alignItems: 'flex-start', gap: 10, marginBottom: badges ? 8 : 0 }}>
        {icon && (
          <div style={{ width: 28, height: 28, borderRadius: 6, background: iconBg || 'var(--surface-2)', color: iconFg || 'var(--text)', display: 'flex', alignItems: 'center', justifyContent: 'center', fontWeight: 700, fontSize: 12, flexShrink: 0 }}>{icon}</div>
        )}
        <div style={{ flex: 1, minWidth: 0 }}>
          <div style={{ fontSize: small ? 12.5 : 13, fontWeight: 600 }}>{title}</div>
          <div style={{ fontSize: 11.5, color: 'var(--text-2)', marginTop: 2, lineHeight: 1.45 }}>{sub}</div>
        </div>
      </div>
      {badges && (
        <div style={{ display: 'flex', flexWrap: 'wrap', gap: 4 }}>
          {badges.map((b, i) => (
            <span key={i} style={{ padding: '1px 6px', borderRadius: 3, background: 'var(--surface-2)', fontSize: 10, fontFamily: 'JetBrains Mono', color: 'var(--text-2)', letterSpacing: 0.2 }}>{b}</span>
          ))}
        </div>
      )}
    </div>
  );
}

function ArchConnector({ label }) {
  return (
    <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'center', gap: 12, padding: '10px 0' }}>
      <div style={{ flex: 1, height: 1, background: 'var(--border)' }}/>
      <span className="mono" style={{ fontSize: 10.5, color: 'var(--text-3)', letterSpacing: '0.1em' }}>↓ {label}</span>
      <div style={{ flex: 1, height: 1, background: 'var(--border)' }}/>
    </div>
  );
}

Object.assign(window, { V2Arch });
