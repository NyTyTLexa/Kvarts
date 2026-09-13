// C4 architecture diagrams — Level 1, 2, 3, and Deployment view.
// Conventional Simon Brown C4 palette adapted to the warm B-style canvas.

// ─── C4 atoms ─────────────────────────────────────────────────────
const C4 = {
  person:    { bg: '#0b3a6c', fg: '#ffffff', accent: '#8ab1d8' },
  system:    { bg: '#1168bd', fg: '#ffffff', accent: '#a5c4e6' },
  container: { bg: '#438dd5', fg: '#ffffff', accent: '#c6dcef' },
  component: { bg: '#85bbf0', fg: '#0b3a6c', accent: '#d4e5f5' },
  external:  { bg: '#6b6660', fg: '#ffffff', accent: '#b3afa9' },
  database:  { bg: '#438dd5', fg: '#ffffff', accent: '#c6dcef', isDB: true },
  queue:     { bg: '#9a7ad4', fg: '#ffffff', accent: '#d2c5ec' },
};

function C4Box({ kind = 'container', name, tech, desc, w, h, style }) {
  const c = C4[kind];
  const typeLabel = ({
    person: 'Person',
    system: 'Software System',
    container: 'Container',
    component: 'Component',
    external: 'External System',
    database: 'Container · Database',
    queue: 'Container · Queue',
  })[kind];
  return (
    <div style={{
      width: w, height: h,
      background: c.bg, color: c.fg,
      borderRadius: c.isDB ? '12px 12px 18px 18px / 20px 20px 12px 12px' : 8,
      padding: '12px 14px 14px',
      display: 'flex', flexDirection: 'column', gap: 4,
      boxShadow: '0 1px 0 rgba(0,0,0,0.1)',
      position: 'relative',
      ...style,
    }}>
      {c.isDB && (
        <div style={{ position: 'absolute', top: 4, left: 8, right: 8, height: 8, borderRadius: '50%', background: 'rgba(255,255,255,0.18)', border: '1px solid rgba(255,255,255,0.3)' }}/>
      )}
      <div style={{
        fontSize: 9.5, fontFamily: 'JetBrains Mono',
        letterSpacing: 0.5, textTransform: 'uppercase',
        opacity: 0.8, marginTop: c.isDB ? 6 : 0,
      }}>[{typeLabel}{tech ? `: ${tech}` : ''}]</div>
      <div style={{ fontSize: 14, fontWeight: 700, lineHeight: 1.15 }}>{name}</div>
      {desc && <div style={{ fontSize: 11, opacity: 0.85, lineHeight: 1.45 }}>{desc}</div>}
    </div>
  );
}

function C4Boundary({ name, kind = 'system', children, style }) {
  return (
    <div style={{
      border: '2px dashed #438dd5', borderRadius: 12,
      padding: '18px 18px 14px', position: 'relative',
      background: 'rgba(67, 141, 213, 0.04)',
      ...style,
    }}>
      <span style={{
        position: 'absolute', top: -10, left: 14,
        background: 'var(--bg)', padding: '0 8px',
        fontSize: 10.5, fontFamily: 'JetBrains Mono', fontWeight: 500,
        letterSpacing: 0.5, textTransform: 'uppercase',
        color: '#438dd5',
      }}>[{kind === 'system' ? 'System Boundary' : 'Container Boundary'}] {name}</span>
      {children}
    </div>
  );
}

function C4Arrow({ label, tech, vertical, dashed }) {
  return (
    <div style={{
      display: 'flex',
      flexDirection: vertical ? 'column' : 'row',
      alignItems: 'center', gap: 6,
      justifyContent: 'center',
      padding: vertical ? '4px 0' : '0 4px',
      minWidth: vertical ? 0 : 100,
      minHeight: vertical ? 28 : 0,
    }}>
      <div style={{
        flex: 1,
        [vertical ? 'width' : 'height']: 1.5,
        background: '#6b6660',
        borderRight: vertical && dashed ? '2px dashed #6b6660' : 'none',
        borderTop: !vertical && dashed ? '2px dashed #6b6660' : 'none',
        ...(vertical
          ? { minHeight: 14, width: dashed ? 0 : 1.5 }
          : { minWidth: 30, height: dashed ? 0 : 1.5 }),
      }}/>
      <div style={{ textAlign: 'center' }}>
        <div style={{ fontSize: 10.5, color: 'var(--text)', fontWeight: 500 }}>{label}</div>
        {tech && <div className="mono" style={{ fontSize: 9.5, color: 'var(--text-3)', marginTop: 1 }}>[{tech}]</div>}
      </div>
      <div style={{
        flex: 1,
        [vertical ? 'width' : 'height']: 1.5,
        background: '#6b6660',
        position: 'relative',
        ...(vertical
          ? { minHeight: 14, width: 1.5 }
          : { minWidth: 30, height: 1.5 }),
      }}>
        <div style={{
          position: 'absolute',
          ...(vertical
            ? { bottom: -4, left: -3, width: 0, height: 0, borderLeft: '4px solid transparent', borderRight: '4px solid transparent', borderTop: '6px solid #6b6660' }
            : { right: -4, top: -3, width: 0, height: 0, borderTop: '4px solid transparent', borderBottom: '4px solid transparent', borderLeft: '6px solid #6b6660' }),
        }}/>
      </div>
    </div>
  );
}

// Legend used at the bottom of every diagram
function C4Legend() {
  return (
    <div style={{ display: 'flex', gap: 14, flexWrap: 'wrap', padding: '12px 14px', background: 'var(--surface-2)', borderRadius: 7, marginTop: 18, fontSize: 11 }}>
      <span className="mono" style={{ color: 'var(--text-2)', textTransform: 'uppercase', letterSpacing: 0.5 }}>Условные обозначения:</span>
      {[
        ['Person',          C4.person.bg],
        ['Software System (ours)', C4.system.bg],
        ['Container',       C4.container.bg],
        ['Database',        C4.database.bg, 'db'],
        ['Message Bus',     C4.queue.bg],
        ['External',        C4.external.bg],
      ].map(([l, c, kind], i) => (
        <span key={i} style={{ display: 'inline-flex', alignItems: 'center', gap: 6 }}>
          <span style={{ width: 14, height: 10, background: c, borderRadius: kind === 'db' ? '3px 3px 6px 6px / 4px 4px 3px 3px' : 2 }}/>
          {l}
        </span>
      ))}
    </div>
  );
}

// ─── L1 — System Context ──────────────────────────────────────────
function V2C4Context() {
  return (
    <V2Shell active="settings" breadcrumb={['Архитектура', 'C4 · Level 1 — System Context']} role="admin"
      actions={<>
        <Btn size="sm" kind="ghost" icon="download">PNG</Btn>
        <Btn size="sm" kind="ghost">Структурайзер DSL</Btn>
      </>}
    >
      <div style={{ marginBottom: 18 }}>
        <div className="mono" style={{ fontSize: 11, color: 'var(--text-3)', letterSpacing: 0.6 }}>C4 · LEVEL 1</div>
        <h1 style={{ margin: '4px 0 0', fontSize: 26, fontWeight: 600, letterSpacing: -0.4 }}>System Context</h1>
        <p style={{ margin: '6px 0 0', color: 'var(--text-2)', fontSize: 13.5, maxWidth: 720 }}>
          Кто использует систему «Кварц» и с какими внешними системами она интегрируется.
        </p>
      </div>

      {/* Top row — Personas */}
      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(5, 1fr)', gap: 14, marginBottom: 12 }}>
        {[
          { name: 'Руководитель проекта', desc: 'Загружает прайсы и перечни, выбирает вариант КП' },
          { name: 'Коммерческий блок',    desc: 'Расчёт маржи и согласование цены клиента' },
          { name: 'Бухгалтерия',          desc: 'Оплата счетов, договоры' },
          { name: 'Склад',                 desc: 'Приёмка, отметка в 1С' },
          { name: 'Администратор',         desc: 'Пользователи, настройки, аудит' },
        ].map((p, i) => (
          <C4Box key={i} kind="person" name={p.name} desc={p.desc} h={110}/>
        ))}
      </div>

      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(5, 1fr)', marginBottom: 12 }}>
        {Array.from({ length: 5 }).map((_, i) => (
          <C4Arrow key={i} vertical label="HTTPS" tech="OAuth 2.0"/>
        ))}
      </div>

      {/* Centre — Our system */}
      <div style={{ display: 'flex', justifyContent: 'center', marginBottom: 20 }}>
        <C4Box kind="system" name="Кварц" tech="Корпоративная информационная система"
          desc="Подбор оборудования, формирование КП, ведение закупочного процесса от Excel до 1С"
          w={520} h={130}/>
      </div>

      {/* Connections to external systems */}
      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(5, 1fr)', gap: 14, marginBottom: 12 }}>
        {Array.from({ length: 5 }).map((_, i) => (
          <C4Arrow key={i} vertical
            label={['Создаёт счета', 'Передаёт приходы', 'SSO учётки', 'Уведомления', 'Push в чат'][i]}
            tech={['REST API', 'HTTP / файлы', 'LDAP / SAML', 'SMTP', 'Webhook'][i]}
          />
        ))}
      </div>

      {/* External systems */}
      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(5, 1fr)', gap: 14 }}>
        {[
          { name: 'NOC',         desc: 'Внутренний инструмент ведения заказов и согласований' },
          { name: '1С Бухгалтерия 8.3', desc: 'Отражение приходов и счетов-фактур' },
          { name: 'AD / LDAP',   desc: 'Корпоративные учётные записи (требует уточнения)' },
          { name: 'SMTP',         desc: 'Email-уведомления о согласованиях и поставках' },
          { name: 'Мессенджер',   desc: 'Корпоративный чат (способ уточняется)' },
        ].map((s, i) => (
          <C4Box key={i} kind="external" name={s.name} desc={s.desc} h={110}/>
        ))}
      </div>

      <C4Legend/>
    </V2Shell>
  );
}

// ─── L2 — Containers ──────────────────────────────────────────────
function V2C4Containers() {
  return (
    <V2Shell active="settings" breadcrumb={['Архитектура', 'C4 · Level 2 — Containers']} role="admin"
      actions={<>
        <Btn size="sm" kind="ghost" icon="download">PNG</Btn>
        <Btn size="sm" kind="ghost">Структурайзер DSL</Btn>
      </>}
    >
      <div style={{ marginBottom: 18 }}>
        <div className="mono" style={{ fontSize: 11, color: 'var(--text-3)', letterSpacing: 0.6 }}>C4 · LEVEL 2</div>
        <h1 style={{ margin: '4px 0 0', fontSize: 26, fontWeight: 600, letterSpacing: -0.4 }}>Containers</h1>
        <p style={{ margin: '6px 0 0', color: 'var(--text-2)', fontSize: 13.5, maxWidth: 760 }}>
          Что внутри «Кварца»: фронтенд, API Gateway, 8 микросервисов на .NET 8, шина сообщений, три хранилища и IAM.
        </p>
      </div>

      {/* User strip */}
      <div style={{ display: 'flex', gap: 14, marginBottom: 8 }}>
        <C4Box kind="person" name="Пользователи" desc="РП · КБ · БУХ · СК · АДМ" w={220} h={64} style={{ padding: '8px 14px' }}/>
        <div style={{ flex: 1 }}/>
      </div>
      <div style={{ display: 'flex', alignItems: 'center', gap: 0, paddingLeft: 100, marginBottom: 8 }}>
        <C4Arrow vertical label="HTTPS" tech="React app"/>
      </div>

      {/* System Boundary */}
      <C4Boundary name="Кварц">
        {/* Tier 1 — Edge */}
        <div style={{ display: 'grid', gridTemplateColumns: '1.6fr 1fr', gap: 14, marginBottom: 10 }}>
          <C4Box kind="container" name="React SPA" tech="React 18 · TS · Vite"
            desc="22 экрана UI · OIDC-клиент Keycloak · React Query для серверного состояния" h={92}/>
          <C4Box kind="container" name="Keycloak" tech="Docker · OIDC"
            desc="IAM-сервис: пользователи, роли, токены, политика паролей" h={92}/>
        </div>

        <div style={{ display: 'grid', gridTemplateColumns: '1.6fr 1fr', gap: 14, alignItems: 'center', marginBottom: 10 }}>
          <C4Arrow vertical label="REST · JWT" tech="/api/v1"/>
          <C4Arrow vertical label="Валидация токена" tech="JWKS" dashed/>
        </div>

        {/* Tier 2 — Gateway */}
        <div style={{ marginBottom: 10 }}>
          <C4Box kind="container" name="API Gateway" tech="ASP.NET Core · YARP"
            desc="Маршрутизация, rate limit, CORS, агрегация OpenAPI" h={70}/>
        </div>
        <C4Arrow vertical label="gRPC внутри кластера" tech="Protobuf"/>

        {/* Tier 3 — Microservices */}
        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(4, 1fr)', gap: 12, marginTop: 8, marginBottom: 14 }}>
          {[
            { n: 'Pricing Service',    d: 'Прайс-листы, скидки' },
            { n: 'Catalog Service',    d: 'Номенклатура, вендоры' },
            { n: 'Project Service',    d: 'Проекты и перечни' },
            { n: 'Quote Service',      d: 'Алгоритмы 3 вариантов КП' },
            { n: 'Matching Service',   d: 'Fuzzy + аналоги' },
            { n: 'Approval Service',   d: 'Маршруты согласования' },
            { n: 'NOC Integration',    d: 'Счета и статусы NOC' },
            { n: 'Audit Service',      d: 'Журнал событий 3 года' },
          ].map((s, i) => (
            <C4Box key={i} kind="container" name={s.n} tech=".NET 8" desc={s.d} h={90}/>
          ))}
        </div>

        {/* Arrows down to bus + data */}
        <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 14, alignItems: 'center', marginBottom: 10 }}>
          <C4Arrow vertical label="EF Core · ACID" tech="Outbox"/>
          <C4Arrow vertical label="Publish events" tech="MassTransit"/>
        </div>

        {/* Tier 4 — Bus + databases */}
        <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 14, marginBottom: 14 }}>
          <C4Box kind="database" name="PostgreSQL 16" tech="EF Core · WAL-G"
            desc="Транзакционное хранилище · 12 схем · WAL-архивация · реплика для аналитики" h={104}/>
          <C4Box kind="queue" name="RabbitMQ + MassTransit" tech="AMQP · Saga"
            desc="События доменов · idempotent consumers · DLX · saga жизненного цикла КП" h={104}/>
        </div>

        <C4Arrow vertical label="Reindex on event" tech="Pricing/Catalog → Search"/>

        <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 14, marginTop: 8 }}>
          <C4Box kind="database" name="Elasticsearch 8" tech="Fuzzy · aggregations"
            desc="Поисковый индекс · 7M документов номенклатуры + позиций прайсов" h={92}/>
          <C4Box kind="database" name="MinIO (S3)" tech="Object storage"
            desc="Файлы прайсов, документов, версии КП · lifecycle 5 лет" h={92}/>
        </div>
      </C4Boundary>

      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(3, 1fr)', gap: 14, marginTop: 10 }}>
        <C4Arrow vertical label="API · счета" tech="REST · webhook"/>
        <C4Arrow vertical label="HTTP / файлы" tech="1С Web HTTP"/>
        <C4Arrow vertical label="SSO / SMTP / Chat" tech="LDAP · SMTP · webhook"/>
      </div>

      {/* External row */}
      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(3, 1fr)', gap: 14 }}>
        <C4Box kind="external" name="NOC" desc="Внутренний инструмент ведения заказов" h={70}/>
        <C4Box kind="external" name="1С Бухгалтерия 8.3" desc="Учётная система" h={70}/>
        <C4Box kind="external" name="AD / SMTP / Мессенджер" desc="Корпоративные сервисы" h={70}/>
      </div>

      <C4Legend/>
    </V2Shell>
  );
}

// ─── L3 — Components (Quote Service zoom) ─────────────────────────
function V2C4Components() {
  return (
    <V2Shell active="settings" breadcrumb={['Архитектура', 'C4 · Level 3 — Components', 'Quote Service']} role="admin"
      actions={<>
        <Btn size="sm" kind="ghost" icon="download">PNG</Btn>
        <Btn size="sm" kind="ghost">Структурайзер DSL</Btn>
      </>}
    >
      <div style={{ marginBottom: 18 }}>
        <div className="mono" style={{ fontSize: 11, color: 'var(--text-3)', letterSpacing: 0.6 }}>C4 · LEVEL 3 — QUOTE SERVICE</div>
        <h1 style={{ margin: '4px 0 0', fontSize: 26, fontWeight: 600, letterSpacing: -0.4 }}>Components</h1>
        <p style={{ margin: '6px 0 0', color: 'var(--text-2)', fontSize: 13.5, maxWidth: 760 }}>
          Декомпозиция Quote Service — сердце алгоритмической логики системы. Здесь рассчитываются три варианта КП.
        </p>
      </div>

      {/* External callers row */}
      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(3, 1fr)', gap: 14, marginBottom: 8 }}>
        <C4Box kind="container" name="API Gateway" tech="YARP" desc="Запросы от React" h={60}/>
        <C4Box kind="queue" name="RabbitMQ" tech="MassTransit" desc="События PriceListUploaded, ProjectCreated" h={60}/>
        <C4Box kind="container" name="Matching Service" tech=".NET 8" desc="Запрос альтернатив для позиции" h={60}/>
      </div>

      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(3, 1fr)', gap: 14, marginBottom: 8 }}>
        {['HTTP /api/v1/quotes', 'subscribe', 'gRPC GetAlternatives'].map((l, i) => (
          <C4Arrow key={i} vertical label={l} tech={['JSON', 'AMQP', 'Protobuf'][i]}/>
        ))}
      </div>

      {/* Quote Service boundary */}
      <C4Boundary kind="container" name="Quote Service · .NET 8">
        {/* Inbound */}
        <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr 1fr', gap: 12, marginBottom: 12 }}>
          <C4Box kind="component" name="QuoteController" tech="ASP.NET Controller"
            desc="POST /quotes/generate · GET /quotes/{id}" h={84}/>
          <C4Box kind="component" name="EventConsumer" tech="MassTransit consumer"
            desc="Подписан на ProjectCreated, PriceListUpdated" h={84}/>
          <C4Box kind="component" name="MatchingClient" tech="gRPC client"
            desc="Получает альтернативы из Matching Service" h={84}/>
        </div>

        <C4Arrow vertical label="Mediator command / event"/>

        {/* Orchestrator */}
        <div style={{ display: 'flex', justifyContent: 'center', marginBottom: 12, marginTop: 6 }}>
          <C4Box kind="component" name="QuoteOrchestrator" tech="MediatR handler"
            desc="Координирует расчёт трёх вариантов: cheap · fast · balanced. Применяет скидки, считает итоги." w={560} h={86}/>
        </div>

        {/* Strategy components */}
        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(3, 1fr)', gap: 10, marginBottom: 12 }}>
          <C4Box kind="component" name="CheapStrategy" tech="IQuoteStrategy"
            desc="Min(price) по каждой позиции с учётом скидки" h={84}/>
          <C4Box kind="component" name="FastStrategy" tech="IQuoteStrategy"
            desc="Min(lead) среди поставщиков с подтверждённым SLA" h={84}/>
          <C4Box kind="component" name="BalancedStrategy" tech="IQuoteStrategy"
            desc="0.6·price + 0.4·lead — веса конфигурируются" h={84}/>
        </div>

        <C4Arrow vertical label="Запрос цен и скидок"/>

        {/* Calculators */}
        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(3, 1fr)', gap: 10, marginBottom: 12 }}>
          <C4Box kind="component" name="PricingCalculator" tech="domain service"
            desc="Базовая цена → скидка → НДС → итог" h={80}/>
          <C4Box kind="component" name="DiscountResolver" tech="domain service"
            desc="Скидки по вендору и производителю на дату" h={80}/>
          <C4Box kind="component" name="LeadTimeEstimator" tech="domain service"
            desc="SLA поставщика · резервы · логистика" h={80}/>
        </div>

        <C4Arrow vertical label="Persist · publish"/>

        {/* Output */}
        <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 10, marginTop: 6 }}>
          <C4Box kind="component" name="QuoteRepository" tech="EF Core · PostgreSQL"
            desc="Сохраняет версии КП и позиции" h={70}/>
          <C4Box kind="component" name="EventPublisher" tech="MassTransit producer"
            desc="QuoteGenerated · QuoteVersioned · через Outbox" h={70}/>
        </div>
      </C4Boundary>

      {/* Outbound */}
      <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 14, marginTop: 12, marginBottom: 8 }}>
        <C4Arrow vertical label="EF Core" tech="ACID · Outbox"/>
        <C4Arrow vertical label="Publish QuoteGenerated" tech="MassTransit → RabbitMQ"/>
      </div>
      <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 14 }}>
        <C4Box kind="database" name="PostgreSQL · quotes schema" tech="EF Core" desc="Версии КП, позиции, история" h={70}/>
        <C4Box kind="queue" name="RabbitMQ · domain events" tech="MassTransit" desc="Quote* события для Approval / Audit / Search" h={70}/>
      </div>

      <C4Legend/>
    </V2Shell>
  );
}

// ─── Deployment view ──────────────────────────────────────────────
function V2C4Deployment() {
  return (
    <V2Shell active="settings" breadcrumb={['Архитектура', 'C4 · Deployment']} role="admin"
      actions={<>
        <Btn size="sm" kind="ghost" icon="download">PNG</Btn>
        <Btn size="sm" kind="ghost">docker-compose.yml</Btn>
      </>}
    >
      <div style={{ marginBottom: 18 }}>
        <div className="mono" style={{ fontSize: 11, color: 'var(--text-3)', letterSpacing: 0.6 }}>C4 · DEPLOYMENT</div>
        <h1 style={{ margin: '4px 0 0', fontSize: 26, fontWeight: 600, letterSpacing: -0.4 }}>Deployment</h1>
        <p style={{ margin: '6px 0 0', color: 'var(--text-2)', fontSize: 13.5, maxWidth: 760 }}>
          Развёртывание через Docker Compose. На проде каждый узел кластеризуется (PG/Patroni · RabbitMQ кластер · ES 3 ноды), для дев-среды достаточно одного хоста.
        </p>
      </div>

      {/* Deployment node — Host */}
      <DepNode label="Deployment Node · Production Host" tech="Linux · 32 vCPU · 128 GB RAM">
        <DepNode label="Docker Compose" tech="orchestration" inner>
          <div style={{ display: 'grid', gridTemplateColumns: 'repeat(4, 1fr)', gap: 10, marginBottom: 14 }}>
            {[
              ['kvarts-spa',        'nginx · React build', 'container'],
              ['kvarts-gateway',    'YARP · :8080',         'container'],
              ['keycloak',          'Keycloak 24 · :8180',  'container'],
              ['pricing-svc',       '.NET 8 · :5001',       'container'],
              ['catalog-svc',       '.NET 8 · :5002',       'container'],
              ['project-svc',       '.NET 8 · :5003',       'container'],
              ['quote-svc',         '.NET 8 · :5004',       'container'],
              ['matching-svc',      '.NET 8 · :5005',       'container'],
              ['approval-svc',      '.NET 8 · :5006',       'container'],
              ['noc-svc',           '.NET 8 · :5007',       'container'],
              ['audit-svc',         '.NET 8 · :5008',       'container'],
              ['outbox-dispatcher', '.NET 8 worker',        'container'],
            ].map(([n, t, k], i) => (
              <C4Box key={i} kind="container" name={n} tech={t} h={64} style={{ padding: '8px 12px' }}/>
            ))}
          </div>

          <div style={{ display: 'grid', gridTemplateColumns: 'repeat(4, 1fr)', gap: 10 }}>
            <C4Box kind="database" name="postgres" tech="PG 16 · :5432" desc="WAL-G в S3" h={84}/>
            <C4Box kind="database" name="elasticsearch" tech=":9200" desc="single node (dev) · 3 ноды (prod)" h={84}/>
            <C4Box kind="queue" name="rabbitmq" tech=":5672 · :15672" desc="management UI · DLX" h={84}/>
            <C4Box kind="database" name="minio" tech=":9000 · :9001" desc="S3 совместимое · lifecycle" h={84}/>
          </div>
        </DepNode>

        <DepNode label="Observability sidecar" tech="наблюдаемость" inner style={{ marginTop: 14 }}>
          <div style={{ display: 'grid', gridTemplateColumns: 'repeat(4, 1fr)', gap: 10 }}>
            {[
              ['otel-collector', 'OTLP receiver',           'OpenTelemetry'],
              ['jaeger',         'Distributed tracing',     ':16686'],
              ['prometheus',     'Metrics scrape',          ':9090'],
              ['grafana',        'Дашборды RED/USE',         ':3000'],
            ].map(([n, d, t], i) => (
              <C4Box key={i} kind="container" name={n} tech={t} desc={d} h={84}/>
            ))}
          </div>
        </DepNode>
      </DepNode>

      <div style={{ marginTop: 18, display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 18 }}>
        <div style={{ background: 'var(--surface)', border: '1px solid var(--border)', borderRadius: 10, padding: '14px 16px' }}>
          <div style={{ fontSize: 11, color: 'var(--text-2)', fontWeight: 600, textTransform: 'uppercase', letterSpacing: 0.5, marginBottom: 10 }}>Сети Docker</div>
          {[
            ['kvarts-public',  'gateway, spa, keycloak — bridge',          'external'],
            ['kvarts-app',     'все .NET сервисы — internal',                'internal'],
            ['kvarts-data',    'PG, ES, RabbitMQ, MinIO — internal',         'internal'],
            ['kvarts-obs',     'OpenTelemetry, Prometheus, Grafana, Jaeger', 'internal'],
          ].map(([n, d, k], i) => (
            <div key={i} style={{ display: 'flex', alignItems: 'center', gap: 10, padding: '8px 0', borderTop: i === 0 ? 'none' : '1px solid var(--border)', fontSize: 12 }}>
              <span style={{ width: 6, height: 6, borderRadius: 999, background: k === 'external' ? 'var(--accent)' : 'var(--text-2)' }}/>
              <span className="mono" style={{ minWidth: 130 }}>{n}</span>
              <span style={{ flex: 1, color: 'var(--text-2)' }}>{d}</span>
              <span style={{ fontSize: 10.5, padding: '1px 7px', borderRadius: 4, background: 'var(--surface-2)', color: 'var(--text-2)' }}>{k}</span>
            </div>
          ))}
        </div>

        <div style={{ background: 'var(--surface)', border: '1px solid var(--border)', borderRadius: 10, padding: '14px 16px' }}>
          <div style={{ fontSize: 11, color: 'var(--text-2)', fontWeight: 600, textTransform: 'uppercase', letterSpacing: 0.5, marginBottom: 10 }}>Тома и persistence</div>
          {[
            ['pg-data',     '/var/lib/postgresql · WAL-G в MinIO'],
            ['es-data',     '/usr/share/elasticsearch/data'],
            ['rabbit-data', '/var/lib/rabbitmq · durable очереди'],
            ['minio-data',  '/data · lifecycle 5 лет'],
            ['keycloak-data', '/opt/keycloak/data — экспорт realm'],
          ].map(([n, d], i) => (
            <div key={i} style={{ display: 'flex', alignItems: 'center', gap: 10, padding: '8px 0', borderTop: i === 0 ? 'none' : '1px solid var(--border)', fontSize: 12 }}>
              <span className="mono" style={{ minWidth: 110 }}>{n}</span>
              <span style={{ flex: 1, color: 'var(--text-2)' }}>{d}</span>
            </div>
          ))}
        </div>
      </div>

      <C4Legend/>
    </V2Shell>
  );
}

function DepNode({ label, tech, children, inner, style }) {
  return (
    <div style={{
      border: '2px solid #438dd5', borderRadius: 12,
      padding: inner ? '18px 14px 14px' : '22px 16px 16px',
      position: 'relative',
      background: inner ? 'rgba(67, 141, 213, 0.04)' : 'rgba(67, 141, 213, 0.06)',
      ...style,
    }}>
      <span style={{
        position: 'absolute', top: -10, left: 14,
        background: 'var(--bg)', padding: '0 8px',
        fontSize: 10.5, fontFamily: 'JetBrains Mono', fontWeight: 500,
        letterSpacing: 0.5, textTransform: 'uppercase',
        color: '#438dd5',
      }}>[Deployment Node] {label} <span style={{ color: 'var(--text-3)' }}>{tech && `· ${tech}`}</span></span>
      {children}
    </div>
  );
}

Object.assign(window, { V2C4Context, V2C4Containers, V2C4Components, V2C4Deployment });
