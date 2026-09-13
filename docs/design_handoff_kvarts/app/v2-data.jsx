// Variant B — Suppliers, Analytics, Log.

function V2Suppliers() {
  return (
    <V2Shell active="suppliers" breadcrumb={['Справочники', 'Поставщики']} role="rp"
      actions={<>
        <Btn size="sm" kind="ghost" icon="upload">Импорт</Btn>
        <Btn size="sm" kind="primary" icon="plus">Добавить</Btn>
      </>}
    >
      <div style={{ marginBottom: 22 }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: 10, marginBottom: 6 }}>
          <h1 style={{ margin: 0, fontSize: 26, fontWeight: 600, letterSpacing: -0.4 }}>Поставщики</h1>
        </div>
        <p style={{ margin: '6px 0 0 0', color: 'var(--text-2)', fontSize: 13.5, maxWidth: 600 }}>
          5 активных · 1 на паузе · 12 потенциальных дублей в номенклатуре. Карточки показывают SLA, скидку и историю сделок.
        </p>
      </div>

      {/* Gallery — Notion gallery view */}
      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(3, 1fr)', gap: 14, marginBottom: 22 }}>
        {window.SUPPLIERS.slice(0, 3).map((s, i) => (
          <div key={s.id} style={{
            background: 'var(--surface)', border: '1px solid var(--border)', borderRadius: 10, padding: 18,
          }}>
            <div style={{ display: 'flex', alignItems: 'flex-start', gap: 11, marginBottom: 14 }}>
              <div style={{
                width: 38, height: 38, borderRadius: 8,
                background: ['#fef3c7', '#dbeafe', '#dcfce7'][i],
                color: ['#92400e', '#1e40af', '#166534'][i],
                display: 'flex', alignItems: 'center', justifyContent: 'center',
                fontWeight: 700, fontSize: 14,
              }}>{s.name.split(' ').map(w => w[0]).slice(0, 2).join('')}</div>
              <div style={{ flex: 1 }}>
                <div style={{ fontWeight: 600, fontSize: 14 }}>{s.name}</div>
                <div className="mono" style={{ fontSize: 11, color: 'var(--text-2)', marginTop: 2 }}>ИНН {s.inn}</div>
              </div>
              <StatusPill status={s.status} kind={s.status === 'Активен' ? 'green' : 'gray'}/>
            </div>
            <div style={{ display: 'grid', gridTemplateColumns: 'repeat(3, 1fr)', gap: 14, marginBottom: 14 }}>
              <div>
                <div style={{ fontSize: 10.5, color: 'var(--text-2)', textTransform: 'uppercase', letterSpacing: 0.5, fontWeight: 500 }}>Скидка</div>
                <div className="tnum" style={{ fontSize: 18, fontWeight: 600, color: 'var(--accent)', marginTop: 2 }}>−{s.discount}%</div>
              </div>
              <div>
                <div style={{ fontSize: 10.5, color: 'var(--text-2)', textTransform: 'uppercase', letterSpacing: 0.5, fontWeight: 500 }}>SLA</div>
                <div className="tnum" style={{ fontSize: 18, fontWeight: 600, marginTop: 2 }}>{s.slaDays} <span style={{ fontSize: 11, color: 'var(--text-2)' }}>дн</span></div>
              </div>
              <div>
                <div style={{ fontSize: 10.5, color: 'var(--text-2)', textTransform: 'uppercase', letterSpacing: 0.5, fontWeight: 500 }}>Сделок</div>
                <div className="tnum" style={{ fontSize: 18, fontWeight: 600, marginTop: 2 }}>{s.deals}</div>
              </div>
            </div>
            <div style={{ paddingTop: 14, borderTop: '1px solid var(--border)', display: 'flex', alignItems: 'center', gap: 8, fontSize: 12 }}>
              <span style={{ color: '#f59e0b' }}>★</span>
              <span style={{ fontWeight: 600, color: 'var(--text)' }}>{s.rating}</span>
              <span style={{ color: 'var(--text-3)' }}>· обновлено {['2 ч назад', '3 дня', 'неделю назад'][i]}</span>
            </div>
          </div>
        ))}
      </div>

      {/* All as table */}
      <div style={{ display: 'flex', alignItems: 'center', gap: 10, marginBottom: 8 }}>
        <span style={{ fontSize: 14, fontWeight: 600 }}>Все поставщики</span>
        <span style={{ fontSize: 12, color: 'var(--text-2)', padding: '2px 8px', borderRadius: 999, background: 'var(--surface-2)' }}>{window.SUPPLIERS.length} записей</span>
      </div>
      <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: 13 }}>
        <thead>
          <tr>
            {['Поставщик', 'ИНН', 'Статус', 'Скидка', 'SLA', 'Рейтинг', 'Сделок', 'Активность'].map(h => (
              <th key={h} style={{ padding: '10px 12px', textAlign: 'left', fontWeight: 500, fontSize: 11, color: 'var(--text-2)', textTransform: 'uppercase', letterSpacing: 0.6, borderBottom: '1px solid var(--border)' }}>{h}</th>
            ))}
          </tr>
        </thead>
        <tbody>
          {window.SUPPLIERS.map((s, i) => (
            <tr key={s.id} style={{ borderBottom: '1px solid var(--border)' }}>
              <td style={{ padding: '13px 12px' }}>
                <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
                  <span style={{ width: 6, height: 6, borderRadius: 999, background: s.status === 'Активен' ? '#16a34a' : '#a8a29e' }}/>
                  <span style={{ fontWeight: 500 }}>{s.name}</span>
                </div>
              </td>
              <td className="mono" style={{ padding: '13px 12px', color: 'var(--text-2)', fontSize: 11.5 }}>{s.inn}</td>
              <td style={{ padding: '13px 12px' }}><StatusPill status={s.status} kind={s.status === 'Активен' ? 'green' : 'gray'}/></td>
              <td className="tnum" style={{ padding: '13px 12px', color: 'var(--accent)', fontWeight: 600 }}>−{s.discount}%</td>
              <td className="tnum" style={{ padding: '13px 12px' }}>{s.slaDays} дн</td>
              <td style={{ padding: '13px 12px' }}><span style={{ color: '#f59e0b' }}>★</span> <span className="tnum">{s.rating}</span></td>
              <td className="tnum" style={{ padding: '13px 12px' }}>{s.deals}</td>
              <td style={{ padding: '13px 12px' }}>
                <div style={{ display: 'flex', alignItems: 'center', gap: 1 }}>
                  {Array.from({ length: 12 }).map((_, k) => (
                    <div key={k} style={{
                      width: 3, height: 16 - Math.abs(6 - k) * 1.5,
                      background: k < 8 ? 'var(--accent)' : 'var(--accent-soft)',
                    }}/>
                  ))}
                </div>
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </V2Shell>
  );
}

function V2Analytics() {
  const months = ['Янв', 'Фев', 'Мар', 'Апр', 'Май', 'Июн', 'Июл', 'Авг', 'Сен', 'Окт', 'Ноя', 'Дек'];
  const won  = [4, 6, 5, 8, 7, 9, 6, 8, 11, 10, 12, 9];
  const lost = [3, 2, 4, 3, 5, 3, 4, 2, 3, 4, 3, 2];
  return (
    <V2Shell active="analytics" breadcrumb={['Аналитика']} role="comm"
      actions={<>
        <Btn size="sm" kind="ghost" icon="filter">2026 Q1–Q2</Btn>
        <Btn size="sm" kind="ghost" icon="download">PDF</Btn>
      </>}
    >
      <div style={{ marginBottom: 22 }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: 10, marginBottom: 6 }}>
          <h1 style={{ margin: 0, fontSize: 26, fontWeight: 600, letterSpacing: -0.4 }}>Аналитика</h1>
        </div>
        <p style={{ margin: '6px 0 0 0', color: 'var(--text-2)', fontSize: 13.5, maxWidth: 600 }}>
          Динамика тендеров, экономия закупки и узкие места по поставщикам.
        </p>
      </div>

      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(4, 1fr)', gap: 12, marginBottom: 22 }}>
        {[
          ['Выиграно тендеров', '64%',   '↑ +8 пп. к 2025'],
          ['Экономия закупки', '11,8%', '↑ против НМЦ'],
          ['Среднее время КП', '2,1 дн', '↑ −40% к ручному'],
          ['Просрочка',        '3,2%',   '↑ цель ≤ 5%'],
        ].map(([l, v, sub], i) => (
          <div key={i} style={{ background: 'var(--surface)', border: '1px solid var(--border)', borderRadius: 10, padding: '16px 18px' }}>
            <div style={{ fontSize: 11, color: 'var(--text-2)', textTransform: 'uppercase', letterSpacing: 0.5, fontWeight: 500 }}>{l}</div>
            <div className="tnum" style={{ fontSize: 28, fontWeight: 600, marginTop: 6, letterSpacing: -0.4 }}>{v}</div>
            <div style={{ fontSize: 11.5, color: '#15803d', marginTop: 4 }}>{sub}</div>
          </div>
        ))}
      </div>

      <div style={{ display: 'grid', gridTemplateColumns: '1.6fr 1fr', gap: 18 }}>
        <div style={{ background: 'var(--surface)', border: '1px solid var(--border)', borderRadius: 10, padding: 18 }}>
          <div style={{ display: 'flex', alignItems: 'center', marginBottom: 14 }}>
            <div style={{ fontSize: 14, fontWeight: 600 }}>Тендеры по месяцам</div>
            <div style={{ flex: 1 }}/>
            <div style={{ display: 'flex', gap: 14, fontSize: 11.5, color: 'var(--text-2)' }}>
              <span style={{ display: 'inline-flex', alignItems: 'center', gap: 6 }}><span style={{ width: 10, height: 10, background: 'var(--accent)', borderRadius: 2 }}/> Выиграно</span>
              <span style={{ display: 'inline-flex', alignItems: 'center', gap: 6 }}><span style={{ width: 10, height: 10, background: '#e7e5e4', borderRadius: 2 }}/> Проиграно</span>
            </div>
          </div>
          <div style={{ display: 'flex', alignItems: 'flex-end', gap: 8, padding: '20px 4px 30px', borderBottom: '1px solid var(--border)', position: 'relative' }}>
            {months.map((m, i) => (
              <div key={m} style={{ flex: 1, display: 'flex', flexDirection: 'column', alignItems: 'center' }}>
                <div style={{ width: '100%', maxWidth: 28, display: 'flex', flexDirection: 'column', justifyContent: 'flex-end', height: 220 }}>
                  <div style={{ height: lost[i] * 14, background: '#e7e5e4', borderRadius: '0 0 3px 3px' }}/>
                  <div style={{ height: won[i] * 14, background: 'var(--accent)', borderRadius: '3px 3px 0 0' }}/>
                </div>
              </div>
            ))}
            <div style={{ position: 'absolute', bottom: 8, left: 0, right: 0, display: 'flex', justifyContent: 'space-around', fontSize: 10.5, color: 'var(--text-2)' }}>
              {months.map(m => <span key={m}>{m}</span>)}
            </div>
          </div>
        </div>

        <div style={{ background: 'var(--surface)', border: '1px solid var(--border)', borderRadius: 10, padding: 18 }}>
          <div style={{ fontSize: 14, fontWeight: 600, marginBottom: 14 }}>Топ поставщиков по экономии</div>
          <div style={{ display: 'flex', flexDirection: 'column', gap: 14 }}>
            {window.SUPPLIERS.slice(0, 4).map((s, i) => {
              const econ = [1_240_000, 980_000, 760_000, 510_000][i];
              const pct = [86, 70, 55, 38][i];
              return (
                <div key={s.id}>
                  <div style={{ display: 'flex', alignItems: 'center', gap: 10, fontSize: 13 }}>
                    <span style={{ flex: 1, fontWeight: 500 }}>{s.name}</span>
                    <span className="tnum" style={{ fontWeight: 600 }}>{fmtMoneyShort(econ)}</span>
                  </div>
                  <div style={{ marginTop: 6, height: 6, background: 'var(--surface-2)', borderRadius: 999, overflow: 'hidden' }}>
                    <div style={{ width: pct + '%', height: '100%', background: 'var(--accent)', borderRadius: 999 }}/>
                  </div>
                  <div style={{ marginTop: 4, fontSize: 11, color: 'var(--text-2)', display: 'flex', justifyContent: 'space-between' }}>
                    <span>{s.deals} сделок · SLA {s.slaDays}</span>
                    <span>★ {s.rating}</span>
                  </div>
                </div>
              );
            })}
          </div>
        </div>
      </div>
    </V2Shell>
  );
}

function V2Log() {
  const tagStyles = {
    upload:  { bg: '#dbeafe', fg: '#1e40af', label: 'загрузка' },
    create:  { bg: '#dcfce7', fg: '#166534', label: 'создание' },
    edit:    { bg: '#fef3c7', fg: '#92400e', label: 'правка' },
    approve: { bg: '#fed7aa', fg: '#9a3412', label: 'согласование' },
    system:  { bg: '#f5f5f4', fg: '#57534e', label: 'система' },
  };
  return (
    <V2Shell active="log" breadcrumb={['Журнал событий']} role="admin"
      actions={<>
        <Btn size="sm" kind="ghost" icon="filter">Фильтры</Btn>
        <Btn size="sm" kind="ghost" icon="download">CSV</Btn>
      </>}
    >
      <div style={{ marginBottom: 22 }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: 10, marginBottom: 6 }}>
          <h1 style={{ margin: 0, fontSize: 26, fontWeight: 600, letterSpacing: -0.4 }}>Журнал событий</h1>
        </div>
        <p style={{ margin: '6px 0 0 0', color: 'var(--text-2)', fontSize: 13.5, maxWidth: 600 }}>
          Все действия фиксируются автоматически. Срок хранения требует уточнения (рекоменд. ≥ 3 лет).
        </p>
      </div>

      <div style={{ display: 'grid', gridTemplateColumns: '1fr 280px', gap: 22 }}>
        <div>
          {/* Filter pills */}
          <div style={{ display: 'flex', alignItems: 'center', gap: 8, marginBottom: 16 }}>
            <div style={{ display: 'flex', alignItems: 'center', gap: 7, padding: '6px 12px', background: 'var(--surface-2)', borderRadius: 7, flex: 1, fontSize: 12.5, color: 'var(--text-2)' }}>
              <Icon name="search" size={13}/> Поиск по пользователю, объекту, действию…
            </div>
            {['Все', 'Сегодня', 'Скидки', 'Система'].map((c, i) => (
              <div key={c} style={{ padding: '5px 11px', borderRadius: 999, fontSize: 11.5, background: i === 0 ? 'var(--accent-soft)' : 'var(--surface-2)', color: i === 0 ? 'var(--accent)' : 'var(--text-2)', fontWeight: 500 }}>{c}</div>
            ))}
          </div>

          {/* Timeline-style log */}
          <div>
            {window.EVENT_LOG.map((e, i) => {
              const t = tagStyles[e.tag];
              return (
                <div key={i} style={{ display: 'flex', gap: 14, padding: '12px 0', borderBottom: '1px solid var(--border)' }}>
                  <span className="mono" style={{ fontSize: 11, color: 'var(--text-2)', minWidth: 100 }}>{e.ts}</span>
                  <span style={{ padding: '1px 8px', borderRadius: 999, background: t.bg, color: t.fg, fontSize: 10.5, fontWeight: 500, textTransform: 'uppercase', letterSpacing: 0.4, height: 18, alignSelf: 'center', minWidth: 78, textAlign: 'center' }}>{t.label}</span>
                  <span style={{ minWidth: 110, fontWeight: 500, fontSize: 12.5 }}>{e.user}</span>
                  <span style={{ flex: 1, fontSize: 12.5 }}>{e.action}</span>
                  <span style={{ color: 'var(--text-2)', fontSize: 12, maxWidth: 280, overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap', textAlign: 'right' }}>{e.obj}</span>
                </div>
              );
            })}
          </div>
        </div>

        <aside style={{ display: 'flex', flexDirection: 'column', gap: 18 }}>
          <div style={{ background: 'var(--surface)', border: '1px solid var(--border)', borderRadius: 10, padding: 16 }}>
            <div style={{ fontSize: 11, color: 'var(--text-2)', textTransform: 'uppercase', letterSpacing: 0.5, fontWeight: 500 }}>За сегодня</div>
            <div className="tnum" style={{ fontSize: 30, fontWeight: 600, marginTop: 6 }}>147</div>
            <div style={{ fontSize: 11.5, color: 'var(--text-2)', marginTop: 2 }}>событий · 11 пользователей · 2 системных</div>
          </div>
          <div style={{ background: 'var(--surface)', border: '1px solid var(--border)', borderRadius: 10, padding: 16 }}>
            <div style={{ fontSize: 11, color: 'var(--text-2)', textTransform: 'uppercase', letterSpacing: 0.5, fontWeight: 500, marginBottom: 12 }}>Активность по часам</div>
            <div style={{ display: 'flex', alignItems: 'flex-end', gap: 2, height: 90 }}>
              {[2, 1, 0, 0, 0, 1, 3, 8, 14, 18, 22, 19, 12, 16, 24, 21, 18, 12, 8, 5, 3, 2, 1, 1].map((n, h) => (
                <div key={h} style={{ flex: 1, height: (n / 24) * 100 + '%', background: h === 14 ? 'var(--accent)' : 'var(--accent-soft)', borderRadius: '2px 2px 0 0', minHeight: 2 }}/>
              ))}
            </div>
            <div style={{ display: 'flex', justifyContent: 'space-between', marginTop: 6, fontSize: 10, color: 'var(--text-3)' }}>
              <span>00</span><span>06</span><span>12</span><span>18</span><span>23</span>
            </div>
          </div>
          <div style={{ background: 'var(--surface)', border: '1px solid var(--border)', borderRadius: 10, padding: 16 }}>
            <div style={{ fontSize: 11, color: 'var(--text-2)', textTransform: 'uppercase', letterSpacing: 0.5, fontWeight: 500, marginBottom: 12 }}>Топ типов событий</div>
            {[
              ['Правка скидок',       28],
              ['Загрузка прайсов',    19],
              ['Согласование КП',     17],
              ['Создание проекта',    11],
              ['Передача в NOC',       9],
            ].map(([l, n], i) => (
              <div key={i} style={{ display: 'flex', alignItems: 'center', gap: 10, padding: '6px 0', fontSize: 12 }}>
                <span style={{ flex: 1 }}>{l}</span>
                <span className="tnum" style={{ color: 'var(--text-2)' }}>{n}</span>
                <div style={{ width: 56, height: 5, background: 'var(--surface-2)', borderRadius: 999, overflow: 'hidden' }}>
                  <div style={{ width: (n / 28 * 100) + '%', height: '100%', background: 'var(--accent)' }}/>
                </div>
              </div>
            ))}
          </div>
        </aside>
      </div>
    </V2Shell>
  );
}

Object.assign(window, { V2Suppliers, V2Analytics, V2Log });
