// Variant B — role-specific screens: Warehouse, Accounting queue, Admin users.

// ─── B11 — Warehouse: receive shipment ────────────────────────────
function V2WarehouseReceive() {
  return (
    <V2Shell active="noc" breadcrumb={['NOC', 'NOC-2026-118-001', 'Приёмка']} role="wh"
      actions={<>
        <Btn size="sm" kind="ghost">Создать акт расхождений</Btn>
        <Btn size="sm" kind="primary" icon="check">Подтвердить приёмку</Btn>
      </>}
    >
      <div style={{ marginBottom: 22 }}>
        <h1 style={{ margin: 0, fontSize: 26, fontWeight: 600, letterSpacing: -0.4 }}>Приёмка партии</h1>
        <p style={{ margin: '6px 0 0', color: 'var(--text-2)', fontSize: 13.5 }}>
          Партия 2 из 4 · <span className="mono" style={{ fontSize: 11.5 }}>NOC-2026-118-001</span> · ТД Сетевые решения · накладная № ТТН-114-04
        </p>
      </div>

      {/* Property block */}
      <div style={{ background: 'var(--surface-2)', borderRadius: 9, padding: '14px 18px', marginBottom: 22 }}>
        <div style={{ display: 'grid', gridTemplateColumns: 'auto 1fr auto 1fr auto 1fr', gap: '10px 22px', fontSize: 13 }}>
          <span style={{ color: 'var(--text-2)' }}>Прибыло</span>
          <span style={{ fontWeight: 500 }}>сегодня, 11:14</span>
          <span style={{ color: 'var(--text-2)' }}>Перевозчик</span>
          <span>СДЭК-Логистика</span>
          <span style={{ color: 'var(--text-2)' }}>Принимает</span>
          <span>Карпов Д. (склад №1, Москва)</span>

          <span style={{ color: 'var(--text-2)' }}>Статус</span>
          <span><StatusPill status="В пути" kind="blue"/></span>
          <span style={{ color: 'var(--text-2)' }}>Договор</span>
          <span>№ 118-СКС от 18.04</span>
          <span style={{ color: 'var(--text-2)' }}>В 1С</span>
          <span style={{ color: 'var(--text-2)' }}>после полной приёмки</span>
        </div>
      </div>

      <div style={{ display: 'grid', gridTemplateColumns: '1fr 320px', gap: 22 }}>
        <div>
          <div style={{ display: 'flex', alignItems: 'center', gap: 10, marginBottom: 10 }}>
            <span style={{ fontSize: 14, fontWeight: 600 }}>Позиции в партии</span>
            <span style={{ fontSize: 12, color: 'var(--text-2)', padding: '2px 8px', borderRadius: 999, background: 'var(--surface-2)' }}>4 позиции · 60 ед.</span>
            <div style={{ flex: 1 }}/>
            <div style={{ display: 'flex', gap: 6 }}>
              <span style={{ padding: '4px 9px', background: 'var(--accent-soft)', color: 'var(--accent)', borderRadius: 999, fontSize: 11.5, fontWeight: 500 }}>Принять всю партию</span>
              <span style={{ padding: '4px 9px', background: 'var(--surface-2)', color: 'var(--text-2)', borderRadius: 999, fontSize: 11.5 }}>По одной</span>
            </div>
          </div>

          <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: 13 }}>
            <thead>
              <tr>
                {['Позиция', 'По счёту', 'Прибыло', 'Расхожд.', 'Состояние', 'Статус'].map((h, i) => (
                  <th key={h} style={{ padding: '10px 12px', textAlign: i >= 1 && i <= 3 ? 'right' : 'left', fontWeight: 500, fontSize: 11, color: 'var(--text-2)', textTransform: 'uppercase', letterSpacing: 0.5, borderBottom: '1px solid var(--border)' }}>{h}</th>
                ))}
              </tr>
            </thead>
            <tbody>
              {[
                { it: window.VARIANTS.fast.rows[0], expected: 8,  actual: 8,  state: 'OK',          status: 'Принято',    kind: 'green' },
                { it: window.VARIANTS.fast.rows[1], expected: 12, actual: 12, state: 'OK',          status: 'Принято',    kind: 'green' },
                { it: window.VARIANTS.fast.rows[3], expected: 32, actual: 30, state: '−2 шт',       status: 'С расхожд.', kind: 'amber', diff: true },
                { it: window.VARIANTS.fast.rows[6], expected: 24, actual: 24, state: 'Упаковка вскрыта', status: 'На проверке', kind: 'blue' },
              ].map((row, i) => (
                <tr key={i} style={{ borderBottom: '1px solid var(--border)', background: row.diff ? '#fef3e8' : 'transparent' }}>
                  <td style={{ padding: '12px 12px' }}>
                    <div style={{ fontWeight: 500, fontSize: 12.5, maxWidth: 300, overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>{row.it.name}</div>
                    <div className="mono" style={{ fontSize: 10.5, color: 'var(--text-3)', marginTop: 2 }}>{row.it.artikul}</div>
                  </td>
                  <td className="tnum" style={{ padding: '12px 12px', textAlign: 'right' }}>{row.expected}</td>
                  <td className="tnum" style={{ padding: '12px 12px', textAlign: 'right', fontWeight: 500 }}>
                    <span style={{
                      padding: '2px 8px', borderRadius: 6, background: 'var(--surface)',
                      border: '1px solid', borderColor: row.diff ? '#fdd9b4' : 'var(--border)',
                    }}>{row.actual}</span>
                  </td>
                  <td className="tnum" style={{ padding: '12px 12px', textAlign: 'right', color: row.diff ? '#b45309' : 'var(--text-3)', fontWeight: row.diff ? 600 : 400 }}>
                    {row.diff ? '−2' : '—'}
                  </td>
                  <td style={{ padding: '12px 12px', color: 'var(--text-2)', fontSize: 12 }}>{row.state}</td>
                  <td style={{ padding: '12px 12px' }}><StatusPill status={row.status} kind={row.kind}/></td>
                </tr>
              ))}
            </tbody>
          </table>

          <div style={{ marginTop: 16, padding: '14px 16px', background: 'var(--surface)', border: '1px solid #fdd9b4', borderLeft: '3px solid var(--accent)', borderRadius: 7 }}>
            <div style={{ fontWeight: 500, fontSize: 13, marginBottom: 4 }}>Обнаружены расхождения по 2 позициям</div>
            <div style={{ color: 'var(--text-2)', fontSize: 12.5, lineHeight: 1.55 }}>
              Hikvision DS-2CD2143G2-IS: не хватает 2 шт. Кабель UTP cat.6: упаковка вскрыта — нужно осмотреть.
              При подтверждении приёмки будет автоматически создан акт расхождений и отправлен в ТД Сетевые решения.
            </div>
          </div>
        </div>

        {/* Sidebar */}
        <aside style={{ display: 'flex', flexDirection: 'column', gap: 18 }}>
          <div>
            <div style={{ fontSize: 11.5, color: 'var(--text-2)', fontWeight: 500, textTransform: 'uppercase', letterSpacing: 0.5, marginBottom: 12 }}>Жизненный цикл</div>
            <div style={{ display: 'flex', flexDirection: 'column', gap: 8 }}>
              {window.LIFECYCLE_STAGES.slice(4).map((s, i) => {
                const idx = 2; // склад — current
                const done = i < idx, active = i === idx;
                return (
                  <div key={s.id} style={{ display: 'flex', alignItems: 'center', gap: 10, fontSize: 13 }}>
                    <span style={{ width: 18, height: 18, borderRadius: 999, background: done || active ? 'var(--accent)' : 'var(--surface-2)', color: done || active ? 'white' : 'var(--text-3)', display: 'flex', alignItems: 'center', justifyContent: 'center', fontSize: 10, fontWeight: 600, flexShrink: 0 }}>{done ? '✓' : i + 5}</span>
                    <span style={{ color: active ? 'var(--text)' : done ? 'var(--text-2)' : 'var(--text-3)', fontWeight: active ? 500 : 400, flex: 1 }}>{s.full}</span>
                  </div>
                );
              })}
            </div>
          </div>

          <div>
            <div style={{ fontSize: 11.5, color: 'var(--text-2)', fontWeight: 500, textTransform: 'uppercase', letterSpacing: 0.5, marginBottom: 12 }}>Другие партии заказа</div>
            <div style={{ display: 'flex', flexDirection: 'column', gap: 8 }}>
              {[
                { n: 1, supplier: 'ТД Сетевые решения', state: 'Принято', kind: 'green', when: '20.04' },
                { n: 2, supplier: 'ТД Сетевые решения', state: 'Приёмка идёт', kind: 'amber', when: 'сейчас', active: true },
                { n: 3, supplier: 'НетПром',            state: 'В пути',    kind: 'blue',  when: 'до 28.04' },
                { n: 4, supplier: 'СвязьКомплект',      state: 'Ожидается', kind: 'gray',  when: 'до 02.05' },
              ].map(p => (
                <div key={p.n} style={{
                  display: 'flex', alignItems: 'center', gap: 10, fontSize: 12.5,
                  padding: '10px 12px',
                  border: '1px solid', borderColor: p.active ? 'var(--accent)' : 'var(--border)',
                  borderRadius: 7, background: p.active ? 'var(--accent-soft)' : 'transparent',
                }}>
                  <span className="mono" style={{ color: 'var(--text-3)', fontSize: 11 }}>0{p.n}</span>
                  <div style={{ flex: 1, minWidth: 0 }}>
                    <div style={{ fontWeight: 500, overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>{p.supplier}</div>
                    <div style={{ fontSize: 11, color: 'var(--text-3)', marginTop: 2 }}>{p.when}</div>
                  </div>
                  <StatusPill status={p.state} kind={p.kind}/>
                </div>
              ))}
            </div>
          </div>

          <div>
            <div style={{ fontSize: 11.5, color: 'var(--text-2)', fontWeight: 500, textTransform: 'uppercase', letterSpacing: 0.5, marginBottom: 12 }}>Документы</div>
            {[
              ['Накладная ТТН-114-04.pdf', '312 КБ', 'ok'],
              ['Счёт-фактура.pdf',          '184 КБ', 'ok'],
              ['Сертификаты соотв.',        '4 файла', 'ok'],
              ['Акт расхождений',           'будет создан', 'wait'],
            ].map((d, i) => (
              <div key={i} style={{ display: 'flex', alignItems: 'center', gap: 9, padding: '8px 0', borderTop: i === 0 ? 'none' : '1px solid var(--border)', fontSize: 12 }}>
                <span style={{ width: 6, height: 6, borderRadius: 999, background: d[2] === 'ok' ? '#16a34a' : '#a8a29e' }}/>
                <span style={{ flex: 1 }}>{d[0]}</span>
                <span style={{ color: 'var(--text-3)', fontSize: 11 }}>{d[1]}</span>
              </div>
            ))}
          </div>
        </aside>
      </div>
    </V2Shell>
  );
}

// ─── B12 — Accounting: payment queue ──────────────────────────────
function V2AccountingQueue() {
  const rows = [
    { id: 'NOC-2026-118-001', supplier: 'ТД Сетевые решения',  sum: window.VARIANTS.fast.total * 1.2 * 0.4, due: '30.04', daysLeft: 6,  state: 'Согласован', kind: 'amber', doc: '✓', urgent: true },
    { id: 'NOC-2026-114-003', supplier: 'НетПром Дистрибуция', sum: 4_280_000, due: '28.04', daysLeft: 4,  state: 'Согласован', kind: 'amber', doc: '✓', urgent: true },
    { id: 'NOC-2026-109-002', supplier: 'ИТ-Альянс',            sum: 1_840_000, due: '02.05', daysLeft: 9,  state: 'Согласован', kind: 'amber', doc: '✓' },
    { id: 'NOC-2026-114-004', supplier: 'СвязьКомплект',        sum: 6_120_000, due: '05.05', daysLeft: 12, state: 'На согл.',   kind: 'blue',  doc: 'нет' },
    { id: 'NOC-2026-118-002', supplier: 'ТД Сетевые решения',   sum: 980_000,   due: '10.05', daysLeft: 17, state: 'На согл.',   kind: 'blue',  doc: '✓' },
    { id: 'NOC-2026-097-003', supplier: 'РТК-Снабжение',         sum: 540_000,   due: '14.05', daysLeft: 21, state: 'Черновик',   kind: 'gray',  doc: 'нет' },
  ];
  return (
    <V2Shell active="noc" breadcrumb={['Бухгалтерия', 'К оплате']} role="acc"
      actions={<>
        <Btn size="sm" kind="ghost" icon="download">Реестр в Excel</Btn>
        <Btn size="sm" kind="ghost" icon="filter">Фильтры</Btn>
      </>}
    >
      <div style={{ marginBottom: 22 }}>
        <h1 style={{ margin: 0, fontSize: 26, fontWeight: 600, letterSpacing: -0.4 }}>К оплате</h1>
        <p style={{ margin: '6px 0 0', color: 'var(--text-2)', fontSize: 13.5 }}>
          Очередь счетов от поставщиков. Подтверждайте оплату по мере фактического платежа в банк-клиенте — статус пройдёт в NOC автоматически.
        </p>
      </div>

      {/* KPI strip */}
      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(4, 1fr)', gap: 12, marginBottom: 22 }}>
        {[
          ['К оплате сегодня',  fmtMoneyShort(rows.filter(r => r.daysLeft <= 7 && r.state === 'Согласован').reduce((a, r) => a + r.sum, 0)), '2 счёта · до 30.04', '#b45309'],
          ['Сумма в очереди',   fmtMoneyShort(rows.reduce((a, r) => a + r.sum, 0)),                                                          '6 счетов · апр–май',  'var(--text)'],
          ['Без договора',      '1',                                                                                                          'СвязьКомплект',       '#b91c1c'],
          ['Оплачено в апреле', '14,2 млн ₽',                                                                                                 '9 счетов · 100% в срок','#15803d'],
        ].map(([l, v, sub, color], i) => (
          <div key={i} style={{ background: 'var(--surface)', border: '1px solid var(--border)', borderRadius: 10, padding: '14px 16px' }}>
            <div style={{ fontSize: 11, color: 'var(--text-2)', textTransform: 'uppercase', letterSpacing: 0.5, fontWeight: 500 }}>{l}</div>
            <div className="tnum" style={{ fontSize: 22, fontWeight: 600, marginTop: 4, color }}>{v}</div>
            <div style={{ fontSize: 11.5, color: 'var(--text-2)', marginTop: 2 }}>{sub}</div>
          </div>
        ))}
      </div>

      {/* Tabs */}
      <div style={{ display: 'flex', alignItems: 'center', gap: 16, padding: '0 0 10px', borderBottom: '1px solid var(--border)' }}>
        {[
          ['Все', 6, true],
          ['Срочно',  2, false],
          ['На согласовании', 2, false],
          ['Оплачено в этом месяце', 9, false],
        ].map(([l, n, active], i) => (
          <div key={i} style={{
            padding: '6px 4px', fontSize: 13,
            color: active ? 'var(--text)' : 'var(--text-2)',
            borderBottom: active ? '2px solid var(--text)' : '2px solid transparent',
            fontWeight: active ? 500 : 400, marginBottom: -11,
            display: 'flex', alignItems: 'center', gap: 7,
          }}>
            {l}
            <span style={{ fontSize: 11, padding: '0 7px', borderRadius: 999, background: 'var(--surface-2)', color: 'var(--text-2)' }}>{n}</span>
          </div>
        ))}
      </div>

      <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: 13 }}>
        <thead>
          <tr>
            {['Счёт', 'Поставщик', 'Срок', 'До оплаты', 'Сумма', 'Статус', 'Договор', ''].map((h, i) => (
              <th key={h} style={{ padding: '11px 12px', textAlign: i >= 3 && i <= 4 ? 'right' : 'left', fontWeight: 500, fontSize: 11, color: 'var(--text-2)', textTransform: 'uppercase', letterSpacing: 0.6, borderBottom: '1px solid var(--border)' }}>{h}</th>
            ))}
          </tr>
        </thead>
        <tbody>
          {rows.map((r) => (
            <tr key={r.id} style={{ borderBottom: '1px solid var(--border)' }}>
              <td className="mono" style={{ padding: '13px 12px', fontSize: 11.5, color: 'var(--text-2)' }}>{r.id}</td>
              <td style={{ padding: '13px 12px', fontWeight: 500 }}>{r.supplier}</td>
              <td style={{ padding: '13px 12px', color: 'var(--text-2)', fontVariantNumeric: 'tabular-nums' }}>{r.due}</td>
              <td style={{ padding: '13px 12px', textAlign: 'right' }}>
                <span style={{
                  padding: '3px 9px', borderRadius: 999,
                  background: r.daysLeft <= 7 ? '#fef3e8' : 'var(--surface-2)',
                  color: r.daysLeft <= 7 ? '#9a3412' : 'var(--text-2)',
                  fontSize: 11.5, fontWeight: 500,
                }}>{r.daysLeft} дн</span>
              </td>
              <td className="tnum" style={{ padding: '13px 12px', textAlign: 'right', fontWeight: 600 }}>{fmtMoneyShort(r.sum)}</td>
              <td style={{ padding: '13px 12px' }}><StatusPill status={r.state} kind={r.kind}/></td>
              <td style={{ padding: '13px 12px' }}>
                {r.doc === '✓'
                  ? <span style={{ display: 'inline-flex', alignItems: 'center', gap: 5, color: '#15803d', fontSize: 12 }}><Icon name="check" size={13} strokeWidth={2}/> приложен</span>
                  : <span style={{ display: 'inline-flex', alignItems: 'center', gap: 5, color: '#b91c1c', fontSize: 12 }}><Icon name="x" size={13} strokeWidth={2}/> не приложен</span>
                }
              </td>
              <td style={{ padding: '13px 12px', textAlign: 'right' }}>
                {r.state === 'Согласован'
                  ? <Btn size="sm" kind="primary">Отметить оплату</Btn>
                  : <Btn size="sm" kind="ghost">Открыть</Btn>
                }
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </V2Shell>
  );
}

// ─── B13 — Admin: users & roles ───────────────────────────────────
function V2AdminUsers() {
  const users = [
    { name: 'Смирнов Алексей',    email: 'a.smirnov@quartz.ru',  role: 'РП',  team: 'Инжиниринг',    last: '5 мин назад',  state: 'Активен', mfa: true },
    { name: 'Васильева Елена',    email: 'e.vasileva@quartz.ru', role: 'КБ',  team: 'Коммерческий',  last: '12 мин назад', state: 'Активен', mfa: true },
    { name: 'Карпов Дмитрий',     email: 'd.karpov@quartz.ru',   role: 'РП',  team: 'Инжиниринг',    last: '38 мин назад', state: 'Активен', mfa: true },
    { name: 'Лебедев Игорь',      email: 'i.lebedev@quartz.ru',  role: 'РП',  team: 'Инжиниринг',    last: '2 ч назад',    state: 'Активен', mfa: false },
    { name: 'Петров Кирилл',      email: 'k.petrov@quartz.ru',   role: 'КБ',  team: 'Руководство',   last: 'вчера, 18:14', state: 'Активен', mfa: true },
    { name: 'Никитина Ольга',     email: 'o.nikitina@quartz.ru', role: 'БУХ', team: 'Бухгалтерия',   last: '5 мин назад',  state: 'Активен', mfa: true },
    { name: 'Алексеев Павел',     email: 'p.alekseev@quartz.ru', role: 'СК',  team: 'Склад №1',      last: '1 ч назад',    state: 'Активен', mfa: false },
    { name: 'Громова Анна',       email: 'a.gromova@quartz.ru',  role: 'СК',  team: 'Склад №2',      last: '3 дня назад',  state: 'Заблокирован', mfa: false },
    { name: 'Захаров Тимур',      email: 't.zakharov@quartz.ru', role: 'АДМ', team: 'IT',            last: '2 мин назад',  state: 'Активен', mfa: true },
  ];
  const roleColor = (r) => ({
    'РП':  ['#fed7aa', '#9a3412'],
    'КБ':  ['#bae6fd', '#075985'],
    'БУХ': ['#bbf7d0', '#14532d'],
    'СК':  ['#e9d5ff', '#6b21a8'],
    'АДМ': ['#fecaca', '#991b1b'],
  })[r] || ['#e7e5e4', '#57534e'];

  return (
    <V2Shell active="users" breadcrumb={['Администрирование', 'Пользователи']} role="admin"
      actions={<>
        <Btn size="sm" kind="ghost" icon="download">Экспорт</Btn>
        <Btn size="sm" kind="primary" icon="plus">Пригласить</Btn>
      </>}
    >
      <div style={{ marginBottom: 22 }}>
        <h1 style={{ margin: 0, fontSize: 26, fontWeight: 600, letterSpacing: -0.4 }}>Пользователи и доступы</h1>
        <p style={{ margin: '6px 0 0', color: 'var(--text-2)', fontSize: 13.5 }}>
          9 активных учётных записей · 7 ролей · SSO через корпоративный AD требует уточнения у IT-службы.
        </p>
      </div>

      {/* KPIs */}
      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(5, 1fr)', gap: 12, marginBottom: 22 }}>
        {[
          ['Всего',     '9'],
          ['Активных',  '8',  '#15803d'],
          ['Заблок.',   '1',  '#b91c1c'],
          ['Без 2FA',   '3',  '#b45309'],
          ['Онлайн',    '4',  '#15803d'],
        ].map(([l, v, color], i) => (
          <div key={i} style={{ background: 'var(--surface)', border: '1px solid var(--border)', borderRadius: 10, padding: '14px 16px' }}>
            <div style={{ fontSize: 11, color: 'var(--text-2)', textTransform: 'uppercase', letterSpacing: 0.5, fontWeight: 500 }}>{l}</div>
            <div className="tnum" style={{ fontSize: 24, fontWeight: 600, marginTop: 4, color: color || 'var(--text)' }}>{v}</div>
          </div>
        ))}
      </div>

      <div style={{ display: 'grid', gridTemplateColumns: '1fr 280px', gap: 22 }}>
        <div>
          {/* Filter row */}
          <div style={{ display: 'flex', alignItems: 'center', gap: 8, marginBottom: 14 }}>
            <div style={{ display: 'flex', alignItems: 'center', gap: 7, padding: '6px 12px', background: 'var(--surface-2)', borderRadius: 7, flex: 1, fontSize: 12.5, color: 'var(--text-2)' }}>
              <Icon name="search" size={13}/> Поиск по имени, email или роли…
            </div>
            {['Все роли', 'Активные', 'Без 2FA'].map((c, i) => (
              <div key={c} style={{ padding: '5px 11px', borderRadius: 999, fontSize: 11.5, background: i === 0 ? 'var(--accent-soft)' : 'var(--surface-2)', color: i === 0 ? 'var(--accent)' : 'var(--text-2)', fontWeight: 500 }}>{c}</div>
            ))}
          </div>

          <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: 13 }}>
            <thead>
              <tr>
                {['Пользователь', 'Роль', 'Подразделение', 'Последний вход', '2FA', 'Статус'].map(h => (
                  <th key={h} style={{ padding: '10px 12px', textAlign: 'left', fontWeight: 500, fontSize: 11, color: 'var(--text-2)', textTransform: 'uppercase', letterSpacing: 0.6, borderBottom: '1px solid var(--border)' }}>{h}</th>
                ))}
              </tr>
            </thead>
            <tbody>
              {users.map((u, i) => {
                const [bg, fg] = roleColor(u.role);
                const initials = u.name.split(' ').map(w => w[0]).slice(0, 2).join('');
                const avBg = ['#fde68a','#bae6fd','#bbf7d0','#fecaca','#e9d5ff','#fed7aa','#fbcfe8','#d9f99d','#a5f3fc'][i];
                return (
                  <tr key={u.email} style={{ borderBottom: '1px solid var(--border)' }}>
                    <td style={{ padding: '12px 12px' }}>
                      <div style={{ display: 'flex', alignItems: 'center', gap: 10 }}>
                        <div style={{ width: 28, height: 28, borderRadius: 999, background: avBg, color: '#1c1917', display: 'flex', alignItems: 'center', justifyContent: 'center', fontSize: 11, fontWeight: 600, flexShrink: 0 }}>{initials}</div>
                        <div>
                          <div style={{ fontWeight: 500 }}>{u.name}</div>
                          <div className="mono" style={{ fontSize: 10.5, color: 'var(--text-3)' }}>{u.email}</div>
                        </div>
                      </div>
                    </td>
                    <td style={{ padding: '12px 12px' }}>
                      <span style={{ padding: '2px 8px', borderRadius: 4, background: bg, color: fg, fontSize: 11.5, fontWeight: 600 }}>{u.role}</span>
                    </td>
                    <td style={{ padding: '12px 12px', color: 'var(--text-2)' }}>{u.team}</td>
                    <td style={{ padding: '12px 12px', color: 'var(--text-2)', fontSize: 12 }}>{u.last}</td>
                    <td style={{ padding: '12px 12px' }}>
                      {u.mfa
                        ? <span style={{ display: 'inline-flex', alignItems: 'center', gap: 4, color: '#15803d', fontSize: 12 }}><Icon name="check" size={13} strokeWidth={2}/> вкл.</span>
                        : <span style={{ display: 'inline-flex', alignItems: 'center', gap: 4, color: '#b45309', fontSize: 12 }}><Icon name="x" size={13} strokeWidth={2}/> выкл.</span>
                      }
                    </td>
                    <td style={{ padding: '12px 12px' }}><StatusPill status={u.state} kind={u.state === 'Активен' ? 'green' : 'red'}/></td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        </div>

        {/* Side: role distribution + audit */}
        <aside style={{ display: 'flex', flexDirection: 'column', gap: 18 }}>
          <div style={{ background: 'var(--surface)', border: '1px solid var(--border)', borderRadius: 10, padding: 14 }}>
            <div style={{ fontSize: 11, color: 'var(--text-2)', textTransform: 'uppercase', letterSpacing: 0.5, fontWeight: 500, marginBottom: 12 }}>Распределение по ролям</div>
            {[
              ['РП',  3, 33],
              ['КБ',  2, 22],
              ['БУХ', 1, 11],
              ['СК',  2, 22],
              ['АДМ', 1, 11],
            ].map(([r, n, pct], i) => {
              const [bg, fg] = roleColor(r);
              return (
                <div key={i} style={{ display: 'flex', alignItems: 'center', gap: 10, padding: '6px 0', fontSize: 12.5 }}>
                  <span style={{ padding: '2px 8px', borderRadius: 4, background: bg, color: fg, fontSize: 10.5, fontWeight: 600, minWidth: 36, textAlign: 'center' }}>{r}</span>
                  <div style={{ flex: 1, height: 5, background: 'var(--surface-2)', borderRadius: 999, overflow: 'hidden' }}>
                    <div style={{ width: pct + '%', height: '100%', background: 'var(--accent)' }}/>
                  </div>
                  <span className="tnum" style={{ color: 'var(--text-2)', width: 22, textAlign: 'right' }}>{n}</span>
                </div>
              );
            })}
          </div>

          <div style={{ background: 'var(--surface)', border: '1px solid var(--border)', borderRadius: 10, padding: 14 }}>
            <div style={{ fontSize: 11, color: 'var(--text-2)', textTransform: 'uppercase', letterSpacing: 0.5, fontWeight: 500, marginBottom: 10 }}>Безопасность</div>
            {[
              { l: 'SSO через AD',         v: 'не настроен', kind: 'warn' },
              { l: 'Обязательный 2FA',     v: 'отключён',    kind: 'warn' },
              { l: 'Минимум 12 символов',  v: 'включено',    kind: 'ok' },
              { l: 'Сессия истекает',      v: '30 мин',      kind: 'ok' },
              { l: 'Журнал доступа',       v: '3 года',      kind: 'ok' },
            ].map((s, i) => (
              <div key={i} style={{ display: 'flex', alignItems: 'center', gap: 10, padding: '7px 0', borderTop: i === 0 ? 'none' : '1px solid var(--border)', fontSize: 12 }}>
                <span style={{ flex: 1 }}>{s.l}</span>
                <span style={{ color: s.kind === 'ok' ? '#15803d' : '#b45309', fontWeight: 500, fontSize: 11.5 }}>{s.v}</span>
              </div>
            ))}
          </div>

          <div style={{ background: 'var(--surface)', border: '1px solid var(--border)', borderRadius: 10, padding: 14 }}>
            <div style={{ fontSize: 11, color: 'var(--text-2)', textTransform: 'uppercase', letterSpacing: 0.5, fontWeight: 500, marginBottom: 10 }}>Последние события</div>
            {[
              ['Захаров Т. изменил права у Карпова Д.', '2 мин'],
              ['Громова А. — попытка входа отклонена',  '37 мин'],
              ['Захаров Т. пригласил нового пользователя','2 ч'],
              ['Сброс пароля Лебедева И.',               'вчера'],
            ].map(([t, when], i) => (
              <div key={i} style={{ display: 'flex', flexDirection: 'column', padding: '7px 0', borderTop: i === 0 ? 'none' : '1px solid var(--border)', fontSize: 12 }}>
                <span>{t}</span>
                <span style={{ color: 'var(--text-3)', fontSize: 11, marginTop: 2 }}>{when}</span>
              </div>
            ))}
          </div>
        </aside>
      </div>
    </V2Shell>
  );
}

Object.assign(window, { V2WarehouseReceive, V2AccountingQueue, V2AdminUsers });
