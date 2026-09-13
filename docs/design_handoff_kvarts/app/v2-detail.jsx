// Variant B — project detail page + settings / integrations.

// ─── B20 — Project detail card ────────────────────────────────────
function V2ProjectDetail() {
  const total = window.VARIANTS.fast.total;
  return (
    <V2Shell active="projects" breadcrumb={['Проекты', 'P-2026-118']} role="rp"
      actions={<>
        <Btn size="sm" kind="ghost" icon="download">Экспорт КП</Btn>
        <Btn size="sm" kind="ghost">Поделиться</Btn>
        <Btn size="sm" kind="primary" icon="edit">Редактировать</Btn>
      </>}
    >
      <div style={{ marginBottom: 8 }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: 12, marginBottom: 6 }}>
          <h1 style={{ margin: 0, fontSize: 26, fontWeight: 600, letterSpacing: -0.4 }}>СКС и сетевое ядро ТЦ «Орбита»</h1>
          <StatusPill status="Ожидание оплаты"/>
        </div>
        <div style={{ display: 'flex', alignItems: 'center', gap: 14, color: 'var(--text-2)', fontSize: 13 }}>
          <span className="mono" style={{ fontSize: 11.5 }}>P-2026-118</span>
          <span>·</span>
          <span>Создан 24.04.2026</span>
          <span>·</span>
          <span>Заказчик ООО «ТЦ Орбита Девелопмент»</span>
        </div>
      </div>

      {/* Lifecycle stripe — full 8 steps */}
      <div style={{ marginTop: 16, marginBottom: 22, padding: 4, background: 'var(--surface)', border: '1px solid var(--border)', borderRadius: 10 }}>
        <div style={{ display: 'flex', gap: 0 }}>
          {window.LIFECYCLE_STAGES.map((s, i, arr) => {
            const idx = 4; // оплата
            const done = i < idx, active = i === idx;
            return (
              <React.Fragment key={s.id}>
                <div style={{
                  flex: 1, padding: '9px 10px',
                  borderRadius: 7,
                  background: active ? 'var(--accent-soft)' : 'transparent',
                  display: 'flex', alignItems: 'center', gap: 7,
                  fontSize: 12,
                  color: active ? 'var(--accent)' : done ? 'var(--text-2)' : 'var(--text-3)',
                  fontWeight: active ? 600 : done ? 500 : 400,
                }}>
                  <span style={{
                    width: 16, height: 16, borderRadius: 999,
                    background: done || active ? 'var(--accent)' : 'var(--surface-2)',
                    color: done || active ? 'white' : 'var(--text-3)',
                    display: 'flex', alignItems: 'center', justifyContent: 'center',
                    fontSize: 9, fontWeight: 600, flexShrink: 0,
                  }}>{done ? '✓' : i + 1}</span>
                  {s.short}
                </div>
                {i < arr.length - 1 && <div style={{ width: 1, alignSelf: 'center', height: 18, background: 'var(--border)' }}/>}
              </React.Fragment>
            );
          })}
        </div>
      </div>

      {/* Property table — Notion key-value */}
      <div style={{ background: 'var(--surface-2)', borderRadius: 9, padding: '14px 18px', marginBottom: 22 }}>
        <div style={{ display: 'grid', gridTemplateColumns: 'auto 1fr auto 1fr auto 1fr', gap: '10px 22px', fontSize: 13 }}>
          <span style={{ color: 'var(--text-2)' }}>Тендер</span>
          <span className="mono">44-ФЗ 0173200012526000132</span>
          <span style={{ color: 'var(--text-2)' }}>НМЦ</span>
          <span style={{ fontWeight: 500 }}>17 540 000 ₽</span>
          <span style={{ color: 'var(--text-2)' }}>Цена клиенту</span>
          <span style={{ fontWeight: 500, color: 'var(--accent)' }}>{fmtMoneyShort(total * 1.225 * 1.2)}</span>

          <span style={{ color: 'var(--text-2)' }}>РП</span>
          <span>Смирнов А.</span>
          <span style={{ color: 'var(--text-2)' }}>Подача КП</span>
          <span>выполнено 24.04</span>
          <span style={{ color: 'var(--text-2)' }}>Маржа</span>
          <span style={{ fontWeight: 500, color: '#15803d' }}>22,5% · {fmtMoneyShort(total * 0.225)}</span>

          <span style={{ color: 'var(--text-2)' }}>Подразделение</span>
          <span>Инжиниринг</span>
          <span style={{ color: 'var(--text-2)' }}>Поставка до</span>
          <span>12.06.2026 <span style={{ color: 'var(--text-2)' }}>· 48 дн.</span></span>
          <span style={{ color: 'var(--text-2)' }}>Тэги</span>
          <span style={{ display: 'flex', gap: 5 }}>
            <span style={{ padding: '2px 8px', borderRadius: 4, background: '#fed7aa', color: '#9a3412', fontSize: 11, fontWeight: 500 }}>СКС</span>
            <span style={{ padding: '2px 8px', borderRadius: 4, background: '#bae6fd', color: '#1e40af', fontSize: 11, fontWeight: 500 }}>сетевое ядро</span>
          </span>
        </div>
      </div>

      <div style={{ display: 'grid', gridTemplateColumns: '1fr 320px', gap: 22 }}>
        <div>
          {/* Tabs */}
          <div style={{ display: 'flex', alignItems: 'center', gap: 16, padding: '0 0 10px', borderBottom: '1px solid var(--border)' }}>
            {[
              ['Версии КП',  3, true],
              ['Позиции',    84, false],
              ['NOC',        1, false],
              ['Документы',  7, false],
              ['Согласования', 5, false],
              ['Активность', 28, false],
            ].map(([l, n, active], i) => (
              <div key={i} style={{
                padding: '6px 4px', fontSize: 13, color: active ? 'var(--text)' : 'var(--text-2)',
                borderBottom: active ? '2px solid var(--text)' : '2px solid transparent',
                fontWeight: active ? 500 : 400, marginBottom: -11,
                display: 'flex', alignItems: 'center', gap: 7,
              }}>
                {l} <span style={{ fontSize: 11, color: 'var(--text-3)' }}>{n}</span>
              </div>
            ))}
          </div>

          {/* KP versions list */}
          {[
            { v: 3, kind: 'Балансированный + Скорость', sum: total, margin: 22.5, status: 'Утверждено', k: 'green', date: '24.04 16:48', author: 'Васильева Е.', note: 'Финальная версия для NOC. После согласования с руководством маржа поднята до 22,5%.', current: true },
            { v: 2, kind: 'Скорость',                    sum: total * 1.04, margin: 20.3, status: 'Откатано',    k: 'gray',  date: '24.04 15:18', author: 'Смирнов А.', note: 'Версия 2 после ручной замены поставщика по L02 на СвязьКомплект.' },
            { v: 1, kind: 'Себестоимость',                sum: total * 0.93, margin: 18.0, status: 'Откатано',    k: 'gray',  date: '24.04 14:47', author: 'Система', note: 'Авто-сформированный первый вариант. РП решил пересобрать.' },
          ].map((kp, i) => (
            <div key={i} style={{
              padding: '16px 18px',
              borderBottom: '1px solid var(--border)',
              background: kp.current ? 'var(--accent-soft)' : 'transparent',
              borderLeft: kp.current ? '3px solid var(--accent)' : '3px solid transparent',
            }}>
              <div style={{ display: 'flex', alignItems: 'center', gap: 12, marginBottom: 6 }}>
                <span className="mono" style={{ fontSize: 11, padding: '3px 8px', borderRadius: 4, background: 'var(--surface)', border: '1px solid var(--border)', color: 'var(--text-2)' }}>КП-v{kp.v}</span>
                <span style={{ fontWeight: 500, fontSize: 14 }}>{kp.kind}</span>
                <StatusPill status={kp.status} kind={kp.k}/>
                <div style={{ flex: 1 }}/>
                <span className="tnum" style={{ fontWeight: 600, fontSize: 15 }}>{fmtMoneyShort(kp.sum)}</span>
              </div>
              <div style={{ fontSize: 12, color: 'var(--text-2)', marginBottom: 6, display: 'flex', gap: 12 }}>
                <span>Маржа <b className="tnum" style={{ color: kp.margin >= 22 ? '#15803d' : 'var(--text)' }}>{kp.margin.toString().replace('.', ',')}%</b></span>
                <span>·</span>
                <span>{kp.date}</span>
                <span>·</span>
                <span>{kp.author}</span>
              </div>
              <div style={{ fontSize: 12.5, color: 'var(--text-2)', lineHeight: 1.55 }}>{kp.note}</div>
            </div>
          ))}
        </div>

        {/* Sidebar */}
        <aside style={{ display: 'flex', flexDirection: 'column', gap: 18 }}>
          <div>
            <div style={{ fontSize: 11.5, color: 'var(--text-2)', fontWeight: 500, textTransform: 'uppercase', letterSpacing: 0.5, marginBottom: 12 }}>Команда</div>
            {[
              { who: 'Смирнов А.',    role: 'РП',  bg: '#fde68a' },
              { who: 'Васильева Е.',  role: 'КБ',  bg: '#bae6fd' },
              { who: 'Петров К.',     role: 'Согл.', bg: '#bbf7d0' },
              { who: 'Никитина О.',   role: 'БУХ', bg: '#fbcfe8' },
              { who: 'Алексеев П.',   role: 'СК',  bg: '#e9d5ff' },
            ].map((p, i) => (
              <div key={i} style={{ display: 'flex', alignItems: 'center', gap: 9, padding: '7px 0', borderTop: i === 0 ? 'none' : '1px solid var(--border)' }}>
                <div style={{ width: 22, height: 22, borderRadius: 999, background: p.bg, color: '#1c1917', display: 'flex', alignItems: 'center', justifyContent: 'center', fontSize: 10, fontWeight: 600 }}>{p.who.split(' ')[0].slice(0, 2)}</div>
                <span style={{ flex: 1, fontSize: 13 }}>{p.who}</span>
                <span style={{ fontSize: 10.5, color: 'var(--text-2)', padding: '1px 6px', borderRadius: 4, background: 'var(--surface-2)', fontWeight: 500 }}>{p.role}</span>
              </div>
            ))}
          </div>

          <div>
            <div style={{ fontSize: 11.5, color: 'var(--text-2)', fontWeight: 500, textTransform: 'uppercase', letterSpacing: 0.5, marginBottom: 12 }}>Финансы</div>
            <div style={{ display: 'flex', flexDirection: 'column', gap: 10, fontSize: 12.5 }}>
              <div style={{ display: 'flex' }}><span style={{ flex: 1, color: 'var(--text-2)' }}>Себестоимость</span><span className="tnum">{fmtMoneyShort(total)}</span></div>
              <div style={{ display: 'flex' }}><span style={{ flex: 1, color: 'var(--text-2)' }}>Наценка 22,5%</span><span className="tnum" style={{ color: '#15803d' }}>+ {fmtMoneyShort(total * 0.225)}</span></div>
              <div style={{ display: 'flex', paddingTop: 8, borderTop: '1px solid var(--border)' }}><span style={{ flex: 1, fontWeight: 500 }}>Цена без НДС</span><span className="tnum" style={{ fontWeight: 600 }}>{fmtMoneyShort(total * 1.225)}</span></div>
              <div style={{ display: 'flex' }}><span style={{ flex: 1, fontWeight: 500 }}>С НДС 20%</span><span className="tnum" style={{ fontWeight: 600, color: 'var(--accent)' }}>{fmtMoneyShort(total * 1.225 * 1.2)}</span></div>
              <div style={{ display: 'flex', paddingTop: 6, borderTop: '1px solid var(--border)' }}>
                <span style={{ flex: 1, color: 'var(--text-2)' }}>vs НМЦ</span>
                <span style={{ color: '#15803d', fontWeight: 500 }} className="tnum">−5,8%</span>
              </div>
            </div>
          </div>

          <div>
            <div style={{ fontSize: 11.5, color: 'var(--text-2)', fontWeight: 500, textTransform: 'uppercase', letterSpacing: 0.5, marginBottom: 12 }}>Последняя активность</div>
            {window.EVENT_LOG.slice(0, 5).map((e, i) => (
              <div key={i} style={{ display: 'flex', flexDirection: 'column', padding: '7px 0', borderTop: i === 0 ? 'none' : '1px solid var(--border)' }}>
                <div style={{ fontSize: 12 }}><b>{e.user}</b> <span style={{ color: 'var(--text-2)' }}>{e.action}</span></div>
                <span style={{ fontSize: 11, color: 'var(--text-3)', marginTop: 2 }}>{e.ts}</span>
              </div>
            ))}
          </div>
        </aside>
      </div>
    </V2Shell>
  );
}

// ─── B21 — Settings & integrations ────────────────────────────────
function V2Settings() {
  return (
    <V2Shell active="users" breadcrumb={['Администрирование', 'Настройки']} role="admin"
      actions={<>
        <Btn size="sm" kind="ghost">Отмена</Btn>
        <Btn size="sm" kind="primary" icon="check">Сохранить</Btn>
      </>}
    >
      <div style={{ marginBottom: 22 }}>
        <h1 style={{ margin: 0, fontSize: 26, fontWeight: 600, letterSpacing: -0.4 }}>Настройки и интеграции</h1>
        <p style={{ margin: '6px 0 0', color: 'var(--text-2)', fontSize: 13.5 }}>
          Алгоритмы подбора, KPI системы, статус интеграций с NOC, 1С и корпоративным AD.
        </p>
      </div>

      <div style={{ display: 'grid', gridTemplateColumns: '220px 1fr', gap: 22 }}>
        {/* Side nav */}
        <aside>
          {[
            ['Общие',          true],
            ['Алгоритмы подбора', false],
            ['KPI и пороги',    false],
            ['Интеграции',      false, true],
            ['Безопасность',    false],
            ['Уведомления',     false],
            ['Хранение и резерв', false],
            ['Журнал',          false],
          ].map(([l, active, hl], i) => (
            <div key={i} style={{
              padding: '8px 12px', borderRadius: 7, marginBottom: 1,
              fontSize: 13,
              background: active ? 'var(--accent-soft)' : 'transparent',
              color: active ? 'var(--accent)' : 'var(--text)',
              fontWeight: active ? 500 : 400,
              display: 'flex', alignItems: 'center', gap: 8,
            }}>
              <span style={{ flex: 1 }}>{l}</span>
              {hl && <span style={{ width: 6, height: 6, borderRadius: 999, background: '#b45309' }}/>}
            </div>
          ))}
        </aside>

        <div style={{ display: 'flex', flexDirection: 'column', gap: 26 }}>

          {/* Algorithms section */}
          <section>
            <div style={{ fontSize: 11, color: 'var(--text-2)', fontWeight: 600, textTransform: 'uppercase', letterSpacing: 0.6, marginBottom: 14 }}>Алгоритмы подбора</div>
            <div style={{ background: 'var(--surface)', border: '1px solid var(--border)', borderRadius: 10, padding: '4px 0' }}>
              {[
                { l: 'Порог нечёткого сопоставления',  v: '≥ 75%',       d: 'минимум совпадения по Levenshtein + TF-IDF', edit: true },
                { l: 'Алгоритм fuzzy-поиска',           v: 'Levenshtein + TF-IDF', d: 'на этапе технического проектирования возможен векторный поиск' },
                { l: 'Веса балансированного варианта',  v: '60% цена + 40% срок', d: 'требует уточнения у заказчика', warn: true },
                { l: 'Предлагать аналоги',              v: 'Включено',     d: 'из той же иерархии номенклатуры, до 5 вариантов' },
                { l: 'Учёт скидок по умолчанию',         v: 'Базовая скидка вендора', d: 'без даты окончания' },
              ].map((r, i, arr) => (
                <div key={i} style={{ display: 'flex', alignItems: 'center', padding: '14px 18px', borderTop: i === 0 ? 'none' : '1px solid var(--border)', gap: 14 }}>
                  <div style={{ flex: 1 }}>
                    <div style={{ fontSize: 13, fontWeight: 500, display: 'flex', alignItems: 'center', gap: 8 }}>
                      {r.l}
                      {r.warn && <span style={{ padding: '1px 7px', borderRadius: 999, background: '#fef3c7', color: '#92400e', fontSize: 10.5, fontWeight: 600 }}>требует уточнения</span>}
                    </div>
                    <div style={{ fontSize: 11.5, color: 'var(--text-3)', marginTop: 3 }}>{r.d}</div>
                  </div>
                  <span style={{
                    padding: '4px 10px', borderRadius: 6,
                    border: r.edit ? '1.5px solid var(--accent)' : '1px solid var(--border-strong)',
                    background: 'var(--surface)', fontFamily: 'JetBrains Mono', fontSize: 12, fontWeight: 500,
                    color: r.edit ? 'var(--accent)' : 'var(--text)',
                    minWidth: 220, textAlign: 'right',
                  }}>{r.v}</span>
                </div>
              ))}
            </div>
          </section>

          {/* Integrations */}
          <section>
            <div style={{ display: 'flex', alignItems: 'center', gap: 10, marginBottom: 14 }}>
              <span style={{ fontSize: 11, color: 'var(--text-2)', fontWeight: 600, textTransform: 'uppercase', letterSpacing: 0.6 }}>Интеграции</span>
              <span style={{ fontSize: 11, color: 'var(--text-3)' }}>5 систем · 2 требуют настройки</span>
            </div>
            <div style={{ display: 'grid', gridTemplateColumns: 'repeat(2, 1fr)', gap: 12 }}>
              {[
                { name: 'NOC',  desc: 'Внутренний инструмент ведения заказов', status: 'Активна', kind: 'green', d: 'API · последняя синхр. 12 мин назад · 8 счетов в очереди', uptime: '99,8%' },
                { name: '1С Бухгалтерия 8.3', desc: 'Отражение приходов и счетов-фактур', status: 'Активна', kind: 'green', d: 'API · последняя синхр. 5 мин назад · 0 ошибок за сегодня', uptime: '99,2%' },
                { name: 'AD / LDAP', desc: 'Корпоративные учётные записи', status: 'Не настроена', kind: 'red', d: 'требует согласования с ИБ · ожидание спецификации SSO', uptime: '—', warn: true },
                { name: 'Email (SMTP)', desc: 'Уведомления о статусах и согласованиях', status: 'Активна', kind: 'green', d: 'mail.quartz.ru · 1284 писем за сутки · 0 отказов', uptime: '100%' },
                { name: 'Корпоративный мессенджер', desc: 'Уведомления и быстрые согласования', status: 'Не настроена', kind: 'gray',  d: 'способ доставки требует уточнения у заказчика', uptime: '—', warn: true },
                { name: 'S3-совместимое хранилище', desc: 'Прайсы, документы, КП-версии', status: 'Активна', kind: 'green', d: 'minio.quartz.lan · 12,8 ТБ занято из 50 · 5 лет ретенция', uptime: '99,9%' },
              ].map((it, i) => (
                <div key={i} style={{
                  background: 'var(--surface)', borderRadius: 10, padding: '14px 16px',
                  border: '1px solid', borderColor: it.warn ? '#fecaca' : 'var(--border)',
                }}>
                  <div style={{ display: 'flex', alignItems: 'flex-start', gap: 10, marginBottom: 10 }}>
                    <div style={{ width: 32, height: 32, borderRadius: 7, background: ['#fde68a','#bbf7d0','#fecaca','#bae6fd','#e9d5ff','#fed7aa'][i], color: '#1c1917', display: 'flex', alignItems: 'center', justifyContent: 'center', fontWeight: 600 }}>{it.name[0]}</div>
                    <div style={{ flex: 1, minWidth: 0 }}>
                      <div style={{ fontWeight: 500, fontSize: 13.5 }}>{it.name}</div>
                      <div style={{ fontSize: 11.5, color: 'var(--text-2)' }}>{it.desc}</div>
                    </div>
                    <StatusPill status={it.status} kind={it.kind}/>
                  </div>
                  <div style={{ fontSize: 11.5, color: 'var(--text-2)', lineHeight: 1.55, marginBottom: 10 }}>{it.d}</div>
                  <div style={{ display: 'flex', alignItems: 'center', gap: 10, fontSize: 11, color: 'var(--text-3)', paddingTop: 10, borderTop: '1px solid var(--border)' }}>
                    <span>Аптайм 30 дн.: <b style={{ color: it.uptime === '—' ? 'var(--text-3)' : 'var(--text)' }} className="tnum">{it.uptime}</b></span>
                    <div style={{ flex: 1 }}/>
                    <span style={{ color: 'var(--accent)', fontWeight: 500 }}>Настроить →</span>
                  </div>
                </div>
              ))}
            </div>
          </section>

          {/* Performance KPIs */}
          <section>
            <div style={{ fontSize: 11, color: 'var(--text-2)', fontWeight: 600, textTransform: 'uppercase', letterSpacing: 0.6, marginBottom: 14 }}>Производительность · цели</div>
            <div style={{ background: 'var(--surface)', border: '1px solid var(--border)', borderRadius: 10, padding: '4px 0' }}>
              {[
                ['Формирование 3 вариантов КП (до 500 поз.)', '≤ 30 сек', 'факт 18 сек'],
                ['Загрузка Excel-файла (до 10 000 строк)',     '≤ 60 сек', 'факт 42 сек'],
                ['Доступность системы (рабочее время)',         '≥ 99%',     'факт 99,7%'],
                ['Хранение прайсов и версий КП',                '5 лет',     'требует уточнения', true],
                ['Резервное копирование БД',                     'Ежедневно', 'каждые 4 ч'],
                ['Срок хранения журнала событий',                '3 года',    'требует уточнения', true],
              ].map(([l, target, fact, warn], i) => (
                <div key={i} style={{ display: 'flex', alignItems: 'center', padding: '12px 18px', borderTop: i === 0 ? 'none' : '1px solid var(--border)', gap: 14, fontSize: 13 }}>
                  <span style={{ flex: 1 }}>{l}</span>
                  <span style={{ color: 'var(--text-2)', fontSize: 12 }}>цель</span>
                  <span style={{ minWidth: 110, fontWeight: 500, fontFamily: 'JetBrains Mono', fontSize: 12 }}>{target}</span>
                  <span style={{ color: 'var(--text-2)', fontSize: 12 }}>факт</span>
                  <span style={{ minWidth: 150, fontFamily: 'JetBrains Mono', fontSize: 12, color: warn ? '#b45309' : '#15803d' }}>{fact}</span>
                </div>
              ))}
            </div>
          </section>
        </div>
      </div>
    </V2Shell>
  );
}

Object.assign(window, { V2ProjectDetail, V2Settings });
