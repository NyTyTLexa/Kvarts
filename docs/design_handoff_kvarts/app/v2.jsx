// Variant B — Notion/Airtable style: light sidebar, softer surfaces, chip indicators.
// Three alternate screens for stylistic comparison.

function V2Shell({ active = 'projects', breadcrumb, children, actions, role = 'rp' }) {
  const navGroups = [
    { title: 'Рабочее пространство', items: [
      { id: 'projects', icon: 'folder',   label: 'Проекты',    count: 6 },
      { id: 'kp',       icon: 'document', label: 'Предложения', count: 14 },
      { id: 'prices',   icon: 'tag',      label: 'Прайсы',     count: 48 },
      { id: 'noc',      icon: 'package',  label: 'Заказы NOC', count: 9 },
    ]},
    { title: 'Справочники', items: [
      { id: 'suppliers', icon: 'truck',   label: 'Поставщики' },
      { id: 'vendors',   icon: 'layers',  label: 'Производители' },
      { id: 'catalog',   icon: 'package', label: 'Номенклатура' },
    ]},
  ];
  return (
    <div className="artboard-root v2" style={{
      width: '100%', height: '100%', display: 'flex',
      background: 'var(--bg)', color: 'var(--text)', fontSize: 13.5, lineHeight: 1.5,
    }}>
      {/* Light sidebar */}
      <aside style={{
        width: 232, flexShrink: 0, background: 'var(--sidebar-bg)',
        display: 'flex', flexDirection: 'column', padding: '12px 0 10px',
        borderRight: '1px solid var(--border)',
      }}>
        <div style={{ padding: '4px 14px 10px', display: 'flex', alignItems: 'center', gap: 10 }}>
          <div style={{ width: 28, height: 28, borderRadius: 6, background: 'var(--accent)', display: 'flex', alignItems: 'center', justifyContent: 'center', color: 'white', fontSize: 16 }}>◆</div>
          <div style={{ flex: 1 }}>
            <div style={{ fontWeight: 600, fontSize: 13.5, color: 'var(--sidebar-text-strong)' }}>Подбор</div>
            <div style={{ fontSize: 11, color: 'var(--text-2)' }}>команда «Инжиниринг»</div>
          </div>
          <Icon name="chevD" size={13} stroke="var(--text-2)"/>
        </div>

        <div style={{ padding: '4px 10px' }}>
          {[
            ['Поиск',  'search'],
            ['Создать новое', 'plus'],
            ['Уведомления', 'bell'],
          ].map(([l, ic], i) => (
            <div key={l} style={{ display: 'flex', alignItems: 'center', gap: 9, padding: '5px 10px', borderRadius: 6, color: 'var(--text-2)', fontSize: 13, marginBottom: 1 }}>
              <Icon name={ic} size={14}/> {l}
              {i === 2 && <span style={{ marginLeft: 'auto', fontSize: 11, padding: '0 6px', borderRadius: 999, background: 'var(--accent)', color: 'white', fontWeight: 500 }}>3</span>}
            </div>
          ))}
        </div>

        <nav style={{ flex: 1, marginTop: 8 }}>
          {navGroups.map((g, gi) => (
            <div key={gi} style={{ marginTop: gi === 0 ? 6 : 16 }}>
              <div style={{ padding: '4px 14px', color: 'var(--text-3)', fontSize: 10.5, fontWeight: 500, textTransform: 'uppercase', letterSpacing: 0.5 }}>{g.title}</div>
              {g.items.map(it => (
                <div key={it.id} style={{
                  display: 'flex', alignItems: 'center', gap: 9,
                  padding: '5px 10px', margin: '1px 8px', borderRadius: 6,
                  color: it.id === active ? 'var(--sidebar-text-strong)' : 'var(--sidebar-text)',
                  background: it.id === active ? 'var(--sidebar-active-bg)' : 'transparent',
                  fontSize: 13, fontWeight: it.id === active ? 500 : 400,
                }}>
                  <Icon name={it.icon} size={14}/>
                  <span style={{ flex: 1 }}>{it.label}</span>
                  {it.count && <span style={{ fontSize: 10.5, color: 'var(--text-3)' }}>{it.count}</span>}
                </div>
              ))}
            </div>
          ))}
        </nav>

        <div style={{ padding: '8px 12px', marginTop: 6, borderTop: '1px solid var(--border)' }}>
          <div style={{ display: 'flex', alignItems: 'center', gap: 8, padding: 4 }}>
            <div style={{ width: 24, height: 24, borderRadius: 999, background: '#fde68a', color: '#92400e', display: 'flex', alignItems: 'center', justifyContent: 'center', fontSize: 11, fontWeight: 600 }}>АС</div>
            <div style={{ flex: 1, minWidth: 0 }}>
              <div style={{ fontSize: 12, fontWeight: 500 }}>Смирнов А.</div>
              <div style={{ fontSize: 10.5, color: 'var(--text-2)' }}>{(window.ROLES.find(r => r.id === role) || {}).label}</div>
            </div>
          </div>
        </div>
      </aside>

      <div style={{ flex: 1, display: 'flex', flexDirection: 'column', minWidth: 0 }}>
        <header style={{
          height: 50, flexShrink: 0, borderBottom: '1px solid var(--border)',
          background: 'var(--surface)', display: 'flex', alignItems: 'center',
          padding: '0 22px', gap: 12,
        }}>
          <div style={{ display: 'flex', alignItems: 'center', gap: 7, fontSize: 13, color: 'var(--text-2)' }}>
            {(breadcrumb || ['Проекты']).map((bc, i, arr) => (
              <React.Fragment key={i}>
                <span style={{ color: i === arr.length - 1 ? 'var(--text)' : 'var(--text-2)', fontWeight: i === arr.length - 1 ? 500 : 400 }}>{bc}</span>
                {i < arr.length - 1 && <span style={{ color: 'var(--text-3)' }}>/</span>}
              </React.Fragment>
            ))}
          </div>
          <div style={{ flex: 1 }}/>
          {actions}
          <div style={{ display: 'flex', alignItems: 'center', gap: 2, background: 'var(--surface-2)', padding: 3, borderRadius: 7 }}>
            {window.ROLES.map(r => (
              <div key={r.id} style={{
                padding: '3px 9px', borderRadius: 5, fontSize: 11.5, fontWeight: 500,
                background: r.id === role ? 'var(--surface)' : 'transparent',
                color: r.id === role ? 'var(--text)' : 'var(--text-2)',
              }}>{r.short}</div>
            ))}
          </div>
        </header>

        <main style={{ flex: 1, overflow: 'hidden', padding: '24px 32px' }}>{children}</main>
      </div>
    </div>
  );
}

// ─── V2 Dashboard ─────────────────────────────────────────────────
function V2Dashboard() {
  return (
    <V2Shell active="projects" breadcrumb={['Проекты']} role="rp"
      actions={<>
        <Btn size="sm" kind="ghost">Поделиться</Btn>
        <Btn size="sm" kind="primary" icon="plus">Новый</Btn>
      </>}
    >
      {/* Page header — Notion-style: emoji + big title */}
      <div style={{ marginBottom: 26 }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: 10, marginBottom: 8 }}>
          <div style={{ width: 40, height: 40, borderRadius: 9, background: 'var(--accent-soft)', display: 'flex', alignItems: 'center', justifyContent: 'center' }}>
            <Icon name="folder" size={22} stroke="var(--accent)" strokeWidth={1.5}/>
          </div>
          <h1 style={{ margin: 0, fontSize: 28, fontWeight: 600, letterSpacing: -0.4 }}>Проекты</h1>
        </div>
        <p style={{ margin: '6px 0 0 50px', color: 'var(--text-2)', fontSize: 13.5, maxWidth: 680 }}>
          Активные тендеры и поставки. Кликните по проекту, чтобы открыть КП и его маршрут согласования.
        </p>
      </div>

      {/* View tabs — Notion-style */}
      <div style={{ display: 'flex', alignItems: 'center', gap: 16, padding: '0 0 10px', borderBottom: '1px solid var(--border)', marginBottom: 0 }}>
        {[
          ['Все проекты', true],
          ['По статусам', false],
          ['Доска', false],
          ['Календарь', false],
        ].map(([l, active], i) => (
          <div key={i} style={{
            padding: '6px 4px', fontSize: 13, color: active ? 'var(--text)' : 'var(--text-2)',
            borderBottom: active ? '2px solid var(--text)' : '2px solid transparent',
            fontWeight: active ? 500 : 400, marginBottom: -11, display: 'flex', alignItems: 'center', gap: 6,
          }}>
            <Icon name={['layers', 'tag', 'dashboard', 'clock'][i]} size={14}/>
            {l}
          </div>
        ))}
        <div style={{ flex: 1 }}/>
        <div style={{ display: 'flex', gap: 8, marginBottom: -11 }}>
          <div style={{ display: 'flex', alignItems: 'center', gap: 5, fontSize: 12, color: 'var(--text-2)' }}><Icon name="filter" size={13}/> Фильтр</div>
          <div style={{ display: 'flex', alignItems: 'center', gap: 5, fontSize: 12, color: 'var(--text-2)' }}><Icon name="arrowDown" size={13}/> Сортировка</div>
        </div>
      </div>

      {/* Table — denser, with chip statuses */}
      <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: 13 }}>
        <thead>
          <tr>
            {['Проект', 'РП', 'Статус', 'Маржа', 'Срок', 'Сумма', ''].map((h, i) => (
              <th key={h} style={{
                padding: '11px 12px', textAlign: i >= 3 && i <= 5 ? 'right' : 'left',
                fontWeight: 500, fontSize: 11, color: 'var(--text-2)',
                textTransform: 'uppercase', letterSpacing: 0.6,
                borderBottom: '1px solid var(--border)',
              }}>{h}</th>
            ))}
          </tr>
        </thead>
        <tbody>
          {window.PROJECTS.map((p) => (
            <tr key={p.id} style={{ borderBottom: '1px solid var(--border)' }}>
              <td style={{ padding: '13px 12px' }}>
                <div style={{ display: 'flex', alignItems: 'center', gap: 10 }}>
                  <span style={{ width: 6, height: 6, borderRadius: 999, background: 'var(--accent)' }}/>
                  <span style={{ fontWeight: 500 }}>{p.name}</span>
                  <span className="mono" style={{ fontSize: 10.5, color: 'var(--text-3)' }}>{p.id}</span>
                </div>
              </td>
              <td style={{ padding: '13px 12px' }}>
                <div style={{ display: 'flex', alignItems: 'center', gap: 7 }}>
                  <div style={{ width: 20, height: 20, borderRadius: 999, background: '#fde68a', color: '#92400e', display: 'flex', alignItems: 'center', justifyContent: 'center', fontSize: 9.5, fontWeight: 600 }}>
                    {p.rp.split(' ')[0].slice(0, 2)}
                  </div>
                  <span style={{ fontSize: 12.5, color: 'var(--text-2)' }}>{p.rp}</span>
                </div>
              </td>
              <td style={{ padding: '13px 12px' }}><StatusPill status={p.status}/></td>
              <td className="tnum" style={{ padding: '13px 12px', textAlign: 'right', fontWeight: 500, color: p.margin == null ? 'var(--text-3)' : p.margin >= 20 ? '#15803d' : 'var(--text)' }}>
                {p.margin == null ? '—' : p.margin.toFixed(1).replace('.', ',') + '%'}
              </td>
              <td style={{ padding: '13px 12px', textAlign: 'right', color: 'var(--text-2)', fontVariantNumeric: 'tabular-nums' }}>{p.due}</td>
              <td className="tnum" style={{ padding: '13px 12px', textAlign: 'right', fontWeight: 500 }}>{fmtMoneyShort(p.value)}</td>
              <td style={{ padding: '13px 12px', textAlign: 'right', color: 'var(--text-3)' }}><Icon name="dots" size={14}/></td>
            </tr>
          ))}
          <tr>
            <td colSpan={7} style={{ padding: '11px 12px', color: 'var(--text-3)', fontSize: 13 }}>
              <span style={{ display: 'inline-flex', alignItems: 'center', gap: 7 }}>
                <Icon name="plus" size={13}/> Добавить проект
              </span>
            </td>
          </tr>
          <tr>
            <td colSpan={7} style={{ padding: '12px 12px', color: 'var(--text-3)', fontSize: 12, borderTop: '1px solid var(--border)' }}>
              Подсчёт: <b style={{ color: 'var(--text-2)' }}>6 проектов</b> · сумма <b style={{ color: 'var(--text-2)' }}>74,7 млн ₽</b> · средняя маржа <b style={{ color: 'var(--text-2)' }}>20,4%</b>
            </td>
          </tr>
        </tbody>
      </table>
    </V2Shell>
  );
}

// ─── V2 KP Compare ────────────────────────────────────────────────
function V2KPCompare() {
  const variants = window.VARIANTS;
  const cards = [
    { v: variants.cheap, tag: 'Себестоимость', desc: 'Минимальная закупка по каждой позиции' },
    { v: variants.fast,  tag: 'Скорость',      desc: 'Минимальный срок поставки', selected: true },
    { v: variants.bal,   tag: 'Баланс',        desc: '60% цена + 40% срок' },
  ];
  const nmcTotal = window.PROJECT_ITEMS.reduce((a, it) => a + it.nmc * it.qty, 0);

  return (
    <V2Shell active="kp" breadcrumb={['Проекты', 'P-2026-118', 'Сравнение']} role="rp"
      actions={<>
        <Btn size="sm" kind="ghost">Поделиться</Btn>
        <Btn size="sm" kind="primary" icon="check">Утвердить</Btn>
      </>}
    >
      <div style={{ marginBottom: 22 }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: 12, marginBottom: 6 }}>
          <h1 style={{ margin: 0, fontSize: 26, fontWeight: 600, letterSpacing: -0.4 }}>Коммерческое предложение</h1>
        </div>
        <p style={{ margin: '6px 0 0', color: 'var(--text-2)', fontSize: 13.5 }}>
          СКС и сетевое ядро ТЦ «Орбита» · <span className="mono" style={{ fontSize: 11.5 }}>P-2026-118</span> · 84 позиции · НМЦ {fmtMoneyShort(nmcTotal * 1.18)}
        </p>
      </div>

      {/* 3 cards as "callouts" Notion-style */}
      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(3, 1fr)', gap: 14, marginBottom: 22 }}>
        {cards.map(({ v, tag, desc, selected }) => {
          const margin = ((nmcTotal * 1.18 - v.total) / (nmcTotal * 1.18) * 100);
          return (
            <div key={v.id} style={{
              background: selected ? 'var(--accent-soft)' : 'var(--surface)',
              border: selected ? '1.5px solid var(--accent)' : '1px solid var(--border)',
              borderRadius: 12, padding: '18px 18px 16px',
              boxShadow: 'none',
            }}>
              <div style={{ display: 'flex', alignItems: 'center', gap: 10, marginBottom: 14 }}>
                <div style={{ flex: 1 }}>
                  <div style={{ fontSize: 11, color: selected ? 'var(--accent)' : 'var(--text-2)', fontWeight: 600, textTransform: 'uppercase', letterSpacing: 0.6 }}>{tag}</div>
                  <div style={{ fontSize: 14.5, fontWeight: 600, marginTop: 2 }}>{v.label}</div>
                </div>
                {selected && (
                  <span style={{ fontSize: 10.5, padding: '2px 8px', borderRadius: 999, background: 'var(--accent)', color: 'white', fontWeight: 600, textTransform: 'uppercase', letterSpacing: 0.5 }}>выбран</span>
                )}
              </div>
              <div style={{ fontSize: 13, color: 'var(--text-2)', marginBottom: 14, lineHeight: 1.5 }}>{desc}</div>
              <div className="tnum" style={{ fontSize: 26, fontWeight: 600, letterSpacing: -0.4, marginBottom: 4 }}>{fmtMoneyShort(v.total)}</div>
              <div style={{ display: 'flex', alignItems: 'center', gap: 10, fontSize: 12, color: 'var(--text-2)' }}>
                <span>Срок <b className="tnum" style={{ color: 'var(--text)' }}>{v.maxLead} дн</b></span>
                <span style={{ color: 'var(--text-3)' }}>·</span>
                <span>Маржа <b className="tnum" style={{ color: margin >= 20 ? '#15803d' : 'var(--text)' }}>{margin.toFixed(1).replace('.', ',')}%</b></span>
              </div>
            </div>
          );
        })}
      </div>

      {/* Items section — Notion table style */}
      <div style={{ marginBottom: 8 }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: 10, marginBottom: 8 }}>
          <span style={{ fontSize: 14, fontWeight: 600 }}>Позиции варианта «Скорость»</span>
          <span style={{ fontSize: 12, color: 'var(--text-2)', padding: '2px 8px', borderRadius: 999, background: 'var(--surface-2)' }}>8 показано</span>
          <div style={{ flex: 1 }}/>
          <div style={{ display: 'flex', gap: 12, fontSize: 12, color: 'var(--text-2)' }}>
            <span style={{ display: 'inline-flex', alignItems: 'center', gap: 5 }}><Icon name="filter" size={12}/> Фильтр</span>
            <span style={{ display: 'inline-flex', alignItems: 'center', gap: 5 }}><Icon name="search" size={12}/> Поиск</span>
          </div>
        </div>
      </div>

      <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: 12.5 }}>
        <thead>
          <tr>
            {['Позиция', 'Производитель', 'Поставщик', 'Цена', 'Скидка', 'Срок', 'Итог'].map((h, i) => (
              <th key={h} style={{ padding: '8px 12px', textAlign: i >= 3 ? 'right' : 'left', fontWeight: 500, fontSize: 11, color: 'var(--text-2)', textTransform: 'uppercase', letterSpacing: 0.5, borderBottom: '1px solid var(--border)' }}>{h}</th>
            ))}
          </tr>
        </thead>
        <tbody>
          {window.VARIANTS.fast.rows.slice(0, 6).map((r) => {
            const vendor = window.VENDORS.find(v => v.id === r.vendor);
            return (
              <tr key={r.id} style={{ borderBottom: '1px solid var(--border)' }}>
                <td style={{ padding: '11px 12px' }}>
                  <div>
                    <span style={{ fontWeight: 500, maxWidth: 280, overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap', display: 'inline-block' }}>{r.name}</span>
                  </div>
                  <div className="mono" style={{ fontSize: 10.5, color: 'var(--text-3)', marginTop: 2 }}>{r.artikul}</div>
                </td>
                <td style={{ padding: '11px 12px' }}>
                  <span style={{ padding: '2px 8px', borderRadius: 4, background: 'var(--surface-2)', fontSize: 11.5 }}>{vendor.name}</span>
                </td>
                <td style={{ padding: '11px 12px' }}>
                  <span style={{ padding: '2px 8px', borderRadius: 4, background: '#dbeafe', color: '#1e40af', fontSize: 11.5, fontWeight: 500 }}>{r.supplierName}</span>
                </td>
                <td className="tnum" style={{ padding: '11px 12px', textAlign: 'right' }}>{fmt(r.unitPrice)} ₽</td>
                <td style={{ padding: '11px 12px', textAlign: 'right' }}>
                  <span style={{ padding: '2px 7px', borderRadius: 999, background: 'var(--accent-soft)', color: 'var(--accent)', fontSize: 11, fontWeight: 600 }}>−{r.discount}%</span>
                </td>
                <td className="tnum" style={{ padding: '11px 12px', textAlign: 'right', color: 'var(--text-2)' }}>{r.leadDays} дн</td>
                <td className="tnum" style={{ padding: '11px 12px', textAlign: 'right', fontWeight: 600 }}>{fmtMoneyShort(r.total)}</td>
              </tr>
            );
          })}
          <tr>
            <td colSpan={7} style={{ padding: '11px 12px', color: 'var(--text-3)', fontSize: 13 }}>
              <span style={{ display: 'inline-flex', alignItems: 'center', gap: 7 }}><Icon name="plus" size={13}/> Добавить позицию</span>
            </td>
          </tr>
        </tbody>
      </table>
    </V2Shell>
  );
}

// ─── V2 NOC record ────────────────────────────────────────────────
function V2NOC() {
  return (
    <V2Shell active="noc" breadcrumb={['Заказы', 'NOC-2026-118-001']} role="acc"
      actions={<>
        <Btn size="sm" kind="ghost">Поделиться</Btn>
        <Btn size="sm" kind="primary" icon="check">Отметить оплату</Btn>
      </>}
    >
      <div style={{ marginBottom: 18 }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: 12, marginBottom: 8 }}>
          <h1 style={{ margin: 0, fontSize: 26, fontWeight: 600, letterSpacing: -0.4 }}>Счёт NOC-2026-118-001</h1>
        </div>
        <p style={{ margin: '6px 0 0 0', color: 'var(--text-2)', fontSize: 13.5 }}>
          ТЦ «Орбита» · {fmtMoneyShort(window.VARIANTS.fast.total * 1.2 * 1.225)} с НДС · оплатить до 30.04
        </p>
      </div>

      {/* Properties — Notion key/value style */}
      <div style={{ background: 'var(--surface-2)', borderRadius: 9, padding: '12px 16px', marginBottom: 22 }}>
        <div style={{ display: 'grid', gridTemplateColumns: 'auto 1fr auto 1fr', gap: '8px 18px', fontSize: 13 }}>
          <span style={{ color: 'var(--text-2)' }}>Контрагент</span>
          <span style={{ fontWeight: 500 }}>ООО «ТЦ Орбита Девелопмент»</span>
          <span style={{ color: 'var(--text-2)' }}>Договор</span>
          <span>№ 118-СКС от 18.04.2026</span>

          <span style={{ color: 'var(--text-2)' }}>Статус</span>
          <span><StatusPill status="Ожидание оплаты"/></span>
          <span style={{ color: 'var(--text-2)' }}>Срок оплаты</span>
          <span>30.04.2026 <span style={{ color: '#b45309' }}>· через 6 дней</span></span>

          <span style={{ color: 'var(--text-2)' }}>Тэги</span>
          <span style={{ display: 'flex', gap: 5 }}>
            <span style={{ padding: '2px 8px', borderRadius: 4, background: '#fed7aa', color: '#9a3412', fontSize: 11, fontWeight: 500 }}>СКС</span>
            <span style={{ padding: '2px 8px', borderRadius: 4, background: '#dbeafe', color: '#1e40af', fontSize: 11, fontWeight: 500 }}>сетевое ядро</span>
            <span style={{ padding: '2px 8px', borderRadius: 4, background: '#dcfce7', color: '#166534', fontSize: 11, fontWeight: 500 }}>Q2</span>
          </span>
          <span style={{ color: 'var(--text-2)' }}>Маршрут</span>
          <span>3 / 5 этапов завершено</span>
        </div>
      </div>

      {/* Lifecycle as horizontal flow */}
      <div style={{ marginBottom: 22 }}>
        <div style={{ fontSize: 11, color: 'var(--text-2)', textTransform: 'uppercase', letterSpacing: 0.5, fontWeight: 500, marginBottom: 10 }}>Жизненный цикл</div>
        <div style={{ display: 'flex', gap: 0, alignItems: 'stretch' }}>
          {window.LIFECYCLE_STAGES.map((s, i, arr) => {
            const idx = 4; // pay stage
            const done = i < idx, active = i === idx;
            return (
              <React.Fragment key={s.id}>
                <div style={{
                  flex: 1, padding: '10px 12px',
                  background: active ? 'var(--accent-soft)' : done ? 'var(--surface)' : 'var(--surface-2)',
                  border: '1px solid',
                  borderColor: active ? 'var(--accent)' : 'var(--border)',
                  borderRadius: 6, fontSize: 12,
                  display: 'flex', flexDirection: 'column', gap: 4,
                  color: active ? 'var(--accent)' : done ? 'var(--text)' : 'var(--text-3)',
                }}>
                  <div style={{ fontSize: 10, fontWeight: 600, textTransform: 'uppercase', letterSpacing: 0.4, color: 'var(--text-3)' }}>
                    Этап {i + 1}
                  </div>
                  <div style={{ fontWeight: active || done ? 500 : 400 }}>{s.full}</div>
                  <div style={{ fontSize: 10.5, color: 'var(--text-3)' }}>{done ? '✓ 24.04' : active ? 'идёт сейчас' : '—'}</div>
                </div>
                {i < arr.length - 1 && <div style={{ width: 8, alignSelf: 'center', color: 'var(--text-3)' }}>›</div>}
              </React.Fragment>
            );
          })}
        </div>
      </div>

      {/* Items as a list of toggles */}
      <div style={{ display: 'flex', alignItems: 'center', gap: 10, marginBottom: 10 }}>
        <span style={{ fontSize: 14, fontWeight: 600 }}>Позиции счёта</span>
        <span style={{ fontSize: 12, color: 'var(--text-2)' }}>8 позиций · 3 из 8 принято</span>
      </div>

      <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: 12.5 }}>
        <thead>
          <tr>
            {['Позиция', 'Кол-во', 'Цена', 'Поставка', 'Склад', 'Сумма'].map((h, i) => (
              <th key={h} style={{ padding: '8px 12px', textAlign: i === 0 ? 'left' : 'right', fontWeight: 500, fontSize: 11, color: 'var(--text-2)', textTransform: 'uppercase', letterSpacing: 0.5, borderBottom: '1px solid var(--border)' }}>{h}</th>
            ))}
          </tr>
        </thead>
        <tbody>
          {window.VARIANTS.fast.rows.slice(0, 5).map((r, i) => {
            const states = ['Доставлено', 'В пути', 'Доставлено', 'В пути', 'Ожидается'];
            const whs = ['Принято',   'Не принято', 'Принято',    'Не принято', 'Не принято'];
            return (
              <tr key={r.id} style={{ borderBottom: '1px solid var(--border)' }}>
                <td style={{ padding: '11px 12px' }}>
                  <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
                    <Icon name="package" size={14} stroke="var(--text-2)"/>
                    <span style={{ fontWeight: 500, maxWidth: 360, overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>{r.name}</span>
                  </div>
                </td>
                <td className="tnum" style={{ padding: '11px 12px', textAlign: 'right' }}>{r.qty}</td>
                <td className="tnum" style={{ padding: '11px 12px', textAlign: 'right' }}>{fmt(Math.round(r.unitPriceDisc * 1.2 * 1.225))} ₽</td>
                <td style={{ padding: '11px 12px', textAlign: 'right' }}><StatusPill status={states[i]} kind={states[i] === 'Доставлено' ? 'green' : states[i] === 'В пути' ? 'blue' : 'gray'}/></td>
                <td style={{ padding: '11px 12px', textAlign: 'right' }}><StatusPill status={whs[i]} kind={whs[i] === 'Принято' ? 'green' : 'gray'}/></td>
                <td className="tnum" style={{ padding: '11px 12px', textAlign: 'right', fontWeight: 600 }}>{fmtMoneyShort(r.total * 1.2 * 1.225)}</td>
              </tr>
            );
          })}
        </tbody>
      </table>
    </V2Shell>
  );
}

Object.assign(window, { V2Dashboard, V2KPCompare, V2NOC });
