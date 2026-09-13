// Variant B — UC-02: Project create → upload list → match results with fuzzy + analogs.

// ─── B14 — Create new project ─────────────────────────────────────
function V2NewProject() {
  return (
    <V2Shell active="projects" breadcrumb={['Проекты', 'Новый']} role="rp"
      actions={<>
        <Btn size="sm" kind="ghost">Отмена</Btn>
        <Btn size="sm" kind="ghost">Сохранить черновик</Btn>
        <Btn size="sm" kind="primary" icon="arrowR" iconRight="arrowR">Создать и загрузить перечень</Btn>
      </>}
    >
      <div style={{ maxWidth: 920 }}>
        <div style={{ marginBottom: 24 }}>
          <h1 style={{ margin: 0, fontSize: 26, fontWeight: 600, letterSpacing: -0.4 }}>Новый проект</h1>
          <p style={{ margin: '6px 0 0', color: 'var(--text-2)', fontSize: 13.5 }}>
            Карточка проекта создаётся первой. После сохранения система предложит загрузить перечень оборудования.
          </p>
        </div>

        {/* Section headers in document style */}
        <div style={{ display: 'flex', flexDirection: 'column', gap: 26 }}>
          <section>
            <div style={{ fontSize: 11, color: 'var(--text-2)', fontWeight: 600, textTransform: 'uppercase', letterSpacing: 0.6, marginBottom: 14 }}>Основное</div>
            <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 16 }}>
              <Field label="Название проекта" required value="СКС и сетевое ядро ТЦ «Аврора»"/>
              <Field label="ID проекта" mono helper="присвоится автоматически" value="P-2026-119"/>
              <Field label="Тип проекта" select selected="Проектирование + поставка" options={['Проектирование + поставка', 'Только поставка', 'НИР']}/>
              <Field label="Подразделение" select selected="Инжиниринг" options={['Инжиниринг', 'Энергетика', 'Слаботочка']}/>
            </div>
          </section>

          <section>
            <div style={{ fontSize: 11, color: 'var(--text-2)', fontWeight: 600, textTransform: 'uppercase', letterSpacing: 0.6, marginBottom: 14 }}>Заказчик</div>
            <div style={{ display: 'grid', gridTemplateColumns: '1.4fr 1fr', gap: 16, marginBottom: 16 }}>
              <Field label="Контрагент" combo value="ООО «Аврора Девелопмент»" helper="найден в справочнике контрагентов"/>
              <Field label="ИНН" mono value="7728341205"/>
            </div>
            <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr 1fr', gap: 16 }}>
              <Field label="Контактное лицо" value="Сидоренко М. В."/>
              <Field label="Email" mono value="m.sidorenko@avr-dev.ru"/>
              <Field label="Телефон" mono value="+7 495 765-43-21"/>
            </div>
          </section>

          <section>
            <div style={{ fontSize: 11, color: 'var(--text-2)', fontWeight: 600, textTransform: 'uppercase', letterSpacing: 0.6, marginBottom: 14 }}>Тендер и сроки</div>
            <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr 1fr 1fr', gap: 16 }}>
              <Field label="Тендер №" mono value="44-ФЗ 0173200012526000148"/>
              <Field label="НМЦ тендера" value="22 480 000 ₽" hint="с НДС"/>
              <Field label="Подача КП до" value="08.05.2026" hint="14 дней"/>
              <Field label="Поставка до" value="22.07.2026" hint="3 мес."/>
            </div>
          </section>

          <section>
            <div style={{ fontSize: 11, color: 'var(--text-2)', fontWeight: 600, textTransform: 'uppercase', letterSpacing: 0.6, marginBottom: 14 }}>Команда</div>
            <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr 1fr', gap: 16 }}>
              <Field label="Руководитель проекта" combo value="Смирнов А." avatar/>
              <Field label="Коммерческий менеджер" combo value="Васильева Е." avatar bg="#fde68a"/>
              <Field label="Согласующие" combo value="Петров К., +2 ещё" helper="круг согласования требует уточнения"/>
            </div>
          </section>

          <section>
            <div style={{ fontSize: 11, color: 'var(--text-2)', fontWeight: 600, textTransform: 'uppercase', letterSpacing: 0.6, marginBottom: 14 }}>Тэги</div>
            <div style={{ display: 'flex', flexWrap: 'wrap', gap: 6 }}>
              {[
                ['СКС', '#fed7aa', '#9a3412'],
                ['сетевое ядро', '#bae6fd', '#075985'],
                ['Q2-2026', '#bbf7d0', '#14532d'],
                ['ТЦ', '#fbcfe8', '#9d174d'],
              ].map(([t, bg, fg]) => (
                <span key={t} style={{ padding: '4px 10px', borderRadius: 999, background: bg, color: fg, fontSize: 12, fontWeight: 500 }}>{t}</span>
              ))}
              <span style={{ padding: '4px 10px', borderRadius: 999, background: 'var(--surface-2)', color: 'var(--text-2)', fontSize: 12, border: '1px dashed var(--border-strong)' }}>+ добавить</span>
            </div>
          </section>

          <section style={{ background: 'var(--accent-soft)', border: '1px solid var(--accent-border)', borderLeft: '3px solid var(--accent)', borderRadius: 7, padding: '14px 16px', display: 'flex', gap: 14, fontSize: 12.5 }}>
            <div style={{ flex: 1 }}>
              <div style={{ fontWeight: 600, marginBottom: 4 }}>Следующий шаг — загрузка перечня оборудования</div>
              <div style={{ color: 'var(--text-2)', lineHeight: 1.55 }}>
                После создания проекта откроется экран загрузки Excel-файла с перечнем. Система автоматически
                сопоставит позиции с базой прайсов и сформирует 3 варианта КП.
              </div>
            </div>
          </section>
        </div>
      </div>
    </V2Shell>
  );
}

// Field helper for the new-project form
function Field({ label, required, value, helper, hint, mono, select, combo, options, selected, avatar, bg }) {
  return (
    <div>
      <div style={{ fontSize: 11, color: 'var(--text-2)', fontWeight: 500, marginBottom: 6, display: 'flex', alignItems: 'center', gap: 6 }}>
        {label} {required && <span style={{ color: 'var(--accent)' }}>*</span>}
        {hint && <span style={{ marginLeft: 'auto', color: 'var(--text-3)', fontWeight: 400 }}>{hint}</span>}
      </div>
      <div style={{
        display: 'flex', alignItems: 'center', gap: 8,
        padding: '8px 12px',
        background: 'var(--surface)', border: '1px solid var(--border-strong)', borderRadius: 7,
        fontSize: 13.5,
        fontFamily: mono ? 'JetBrains Mono' : 'inherit',
      }}>
        {avatar && (
          <div style={{ width: 20, height: 20, borderRadius: 999, background: bg || '#bae6fd', color: '#1c1917', display: 'flex', alignItems: 'center', justifyContent: 'center', fontSize: 9.5, fontWeight: 600, flexShrink: 0 }}>
            {(value || '').split(' ')[0].slice(0, 2)}
          </div>
        )}
        <span style={{ flex: 1, color: 'var(--text)' }}>{value}</span>
        {(select || combo) && <Icon name="chevD" size={13} stroke="var(--text-3)"/>}
      </div>
      {helper && <div style={{ fontSize: 11, color: 'var(--text-3)', marginTop: 4 }}>{helper}</div>}
    </div>
  );
}

// ─── B15 — Upload project list (with progress) ────────────────────
function V2UploadProjectList() {
  return (
    <V2Shell active="projects" breadcrumb={['Проекты', 'P-2026-119', 'Загрузка перечня']} role="rp"
      actions={<>
        <Btn size="sm" kind="ghost">Отмена</Btn>
        <Btn size="sm" kind="primary" icon="arrowR" iconRight="arrowR" style={{ pointerEvents: 'none', opacity: 0.6 }}>Перейти к сопоставлению</Btn>
      </>}
    >
      <div style={{ marginBottom: 22 }}>
        <h1 style={{ margin: 0, fontSize: 26, fontWeight: 600, letterSpacing: -0.4 }}>Загрузка перечня оборудования</h1>
        <p style={{ margin: '6px 0 0', color: 'var(--text-2)', fontSize: 13.5 }}>
          <span className="mono" style={{ fontSize: 11.5 }}>P-2026-119</span> · ТЦ «Аврора» · 22,48 млн ₽ НМЦ ·
          приложите Excel-файл с проектным перечнем, система сопоставит позиции с базой прайсов.
        </p>
      </div>

      <div style={{ display: 'grid', gridTemplateColumns: '420px 1fr', gap: 22, alignItems: 'flex-start' }}>
        {/* Left: file + progress */}
        <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
          <div style={{
            background: 'var(--accent-soft)', border: '1.5px dashed var(--accent)', borderRadius: 12,
            padding: '24px 20px',
          }}>
            <div style={{ display: 'flex', alignItems: 'center', gap: 12, marginBottom: 14 }}>
              <div style={{ width: 36, height: 36, borderRadius: 8, background: 'var(--accent)', color: 'white', display: 'flex', alignItems: 'center', justifyContent: 'center' }}>
                <Icon name="excel" size={18}/>
              </div>
              <div style={{ flex: 1 }}>
                <div style={{ fontWeight: 500, fontSize: 13.5 }}>perechen_avrora_v3.xlsx</div>
                <div className="mono" style={{ fontSize: 11, color: 'var(--text-2)', marginTop: 2 }}>318 КБ · 142 строки · загружен 24.04 14:08</div>
              </div>
            </div>
            <div style={{ marginBottom: 10 }}>
              <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: 11.5, color: 'var(--text-2)', marginBottom: 6 }}>
                <span>Обработка</span>
                <span className="tnum" style={{ color: 'var(--accent)', fontWeight: 600 }}>67%</span>
              </div>
              <div style={{ height: 6, background: 'var(--surface-2)', borderRadius: 999, overflow: 'hidden' }}>
                <div style={{ width: '67%', height: '100%', background: 'var(--accent)', borderRadius: 999 }}/>
              </div>
            </div>
            <div style={{ display: 'grid', gridTemplateColumns: 'auto 1fr', gap: '4px 12px', fontSize: 11.5 }}>
              <span style={{ color: 'var(--text-2)' }}>Этап</span>
              <span style={{ fontWeight: 500 }}>Сопоставление с БД</span>
              <span style={{ color: 'var(--text-2)' }}>Осталось</span>
              <span>~ 12 секунд</span>
            </div>
          </div>

          {/* Steps timeline */}
          <div style={{ background: 'var(--surface)', border: '1px solid var(--border)', borderRadius: 10, padding: 14 }}>
            <div style={{ fontSize: 11, color: 'var(--text-2)', fontWeight: 500, textTransform: 'uppercase', letterSpacing: 0.5, marginBottom: 12 }}>Этапы обработки</div>
            {[
              { l: 'Валидация структуры',   d: '142 строки · OK', state: 'done' },
              { l: 'Нормализация данных',    d: 'регистр, пробелы, единицы', state: 'done' },
              { l: 'Сопоставление по артикулу', d: 'точное совпадение', state: 'done' },
              { l: 'Нечёткий поиск',         d: 'для непросопоставленных', state: 'active' },
              { l: 'Подбор аналогов',        d: 'из той же иерархии', state: 'wait' },
              { l: 'Формирование 3 вариантов', d: 'по цене / сроку / балансу', state: 'wait' },
            ].map((s, i, arr) => (
              <div key={i} style={{ display: 'flex', gap: 11, position: 'relative', paddingBottom: i < arr.length - 1 ? 12 : 0 }}>
                {i < arr.length - 1 && <div style={{ position: 'absolute', left: 8, top: 18, bottom: 0, width: 1, background: 'var(--border)' }}/>}
                <div style={{
                  width: 17, height: 17, borderRadius: 999, flexShrink: 0, marginTop: 1,
                  background: s.state === 'done' ? '#15803d' : s.state === 'active' ? 'var(--accent)' : 'var(--surface-2)',
                  border: '1.5px solid',
                  borderColor: s.state === 'done' ? '#15803d' : s.state === 'active' ? 'var(--accent)' : 'var(--border-strong)',
                  color: 'white', display: 'flex', alignItems: 'center', justifyContent: 'center', fontSize: 10, fontWeight: 600,
                }}>{s.state === 'done' ? '✓' : ''}</div>
                <div>
                  <div style={{ fontSize: 12.5, fontWeight: s.state !== 'wait' ? 500 : 400, color: s.state === 'wait' ? 'var(--text-2)' : 'var(--text)' }}>{s.l}</div>
                  <div style={{ fontSize: 11, color: 'var(--text-3)' }}>{s.d}</div>
                </div>
              </div>
            ))}
          </div>
        </div>

        {/* Right: preview + stats */}
        <div>
          <div style={{ display: 'grid', gridTemplateColumns: 'repeat(4, 1fr)', gap: 10, marginBottom: 18 }}>
            {[
              ['Всего позиций',  '142',           'в перечне'],
              ['Точное совп.',   '128',           '90,1%',  '#15803d'],
              ['Нечёткое',       '11',            'требуют проверки', '#b45309'],
              ['Не найдено',     '3',             'нужны новые',      '#b91c1c'],
            ].map(([l, v, sub, color], i) => (
              <div key={i} style={{ background: 'var(--surface)', border: '1px solid var(--border)', borderRadius: 9, padding: '12px 14px' }}>
                <div style={{ fontSize: 10.5, color: 'var(--text-2)', textTransform: 'uppercase', letterSpacing: 0.5, fontWeight: 500 }}>{l}</div>
                <div className="tnum" style={{ fontSize: 22, fontWeight: 600, marginTop: 2, color: color || 'var(--text)' }}>{v}</div>
                <div style={{ fontSize: 11, color: 'var(--text-2)' }}>{sub}</div>
              </div>
            ))}
          </div>

          <div style={{ display: 'flex', alignItems: 'center', gap: 10, marginBottom: 10 }}>
            <span style={{ fontSize: 14, fontWeight: 600 }}>Превью перечня</span>
            <span style={{ fontSize: 12, color: 'var(--text-2)' }}>первые 6 строк из 142</span>
          </div>
          <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: 12.5 }}>
            <thead>
              <tr>
                {['Стр.', 'Артикул', 'Наименование', 'Ед', 'Кол-во', 'Сопоставление'].map((h, i) => (
                  <th key={h} style={{ padding: '9px 12px', textAlign: i === 4 ? 'right' : 'left', fontWeight: 500, fontSize: 11, color: 'var(--text-2)', textTransform: 'uppercase', letterSpacing: 0.5, borderBottom: '1px solid var(--border)' }}>{h}</th>
                ))}
              </tr>
            </thead>
            <tbody>
              {[
                { ...window.PROJECT_ITEMS[0], match: 'exact' },
                { ...window.PROJECT_ITEMS[1], match: 'exact' },
                { ...window.PROJECT_ITEMS[2], match: 'fuzzy' },
                { ...window.PROJECT_ITEMS[3], match: 'exact' },
                { artikul: 'GBIC-SX-1G-MM',  name: 'SFP-модуль 1G SX MultiMode',  unit: 'шт', qty: 16, match: 'fuzzy' },
                { artikul: 'PATCH-CAT6-2M',   name: 'Патч-корд категории 6, 2 м (синий)', unit: 'шт', qty: 80, match: 'none' },
              ].map((it, i) => (
                <tr key={i} style={{ borderBottom: '1px solid var(--border)' }}>
                  <td className="mono" style={{ padding: '11px 12px', fontSize: 10.5, color: 'var(--text-3)' }}>{i + 1}</td>
                  <td className="mono" style={{ padding: '11px 12px', fontSize: 11 }}>{it.artikul}</td>
                  <td style={{ padding: '11px 12px', maxWidth: 320, whiteSpace: 'nowrap', overflow: 'hidden', textOverflow: 'ellipsis' }}>{it.name}</td>
                  <td style={{ padding: '11px 12px', color: 'var(--text-2)' }}>{it.unit}</td>
                  <td className="tnum" style={{ padding: '11px 12px', textAlign: 'right' }}>{it.qty}</td>
                  <td style={{ padding: '11px 12px' }}>
                    {it.match === 'exact'  && <StatusPill status="Точное" kind="green"/>}
                    {it.match === 'fuzzy'  && <StatusPill status="Нечёткое 87%" kind="amber"/>}
                    {it.match === 'none'   && <StatusPill status="Не найдено" kind="red"/>}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </div>
    </V2Shell>
  );
}

// ─── B16 — Match results with fuzzy + analogs ────────────────────
function V2MatchResults() {
  return (
    <V2Shell active="projects" breadcrumb={['Проекты', 'P-2026-119', 'Сопоставление позиций']} role="rp"
      actions={<>
        <Btn size="sm" kind="ghost" icon="download">Экспорт лога</Btn>
        <Btn size="sm" kind="primary" icon="arrowR" iconRight="arrowR">Перейти к КП</Btn>
      </>}
    >
      <div style={{ marginBottom: 18 }}>
        <h1 style={{ margin: 0, fontSize: 26, fontWeight: 600, letterSpacing: -0.4 }}>Сопоставление позиций</h1>
        <p style={{ margin: '6px 0 0', color: 'var(--text-2)', fontSize: 13.5 }}>
          <span className="mono" style={{ fontSize: 11.5 }}>P-2026-119</span> · ТЦ «Аврора» · 142 позиции ·
          проверьте нечёткие совпадения и решите, что делать с непросопоставленными.
        </p>
      </div>

      {/* Filter pills */}
      <div style={{ display: 'flex', alignItems: 'center', gap: 8, marginBottom: 18, padding: '12px 14px', background: 'var(--surface-2)', borderRadius: 9 }}>
        <span style={{ fontSize: 12, color: 'var(--text-2)', fontWeight: 500 }}>Показать:</span>
        {[
          ['Все', 142, false],
          ['Точные совпадения', 128, false, 'green'],
          ['Нечёткие — требуют решения', 11, true, 'amber'],
          ['Не найдено', 3, false, 'red'],
        ].map(([l, n, active, k], i) => (
          <span key={i} style={{
            display: 'inline-flex', alignItems: 'center', gap: 6, padding: '5px 11px',
            borderRadius: 999, fontSize: 12, fontWeight: 500,
            background: active ? 'var(--accent-soft)' : 'var(--surface)',
            border: '1px solid', borderColor: active ? 'var(--accent)' : 'var(--border)',
            color: active ? 'var(--accent)' : 'var(--text)',
          }}>
            {k && <span style={{ width: 7, height: 7, borderRadius: 999, background: k === 'green' ? '#16a34a' : k === 'amber' ? '#d97706' : '#dc2626' }}/>}
            {l}
            <span style={{ fontSize: 10.5, color: 'var(--text-3)' }}>{n}</span>
          </span>
        ))}
        <div style={{ flex: 1 }}/>
        <span style={{ fontSize: 12, color: 'var(--text-2)' }}>Порог нечёткого поиска:</span>
        <span style={{
          padding: '4px 10px', background: 'var(--surface)', border: '1px solid var(--border-strong)',
          borderRadius: 6, fontFamily: 'JetBrains Mono', fontSize: 12, fontWeight: 500,
        }}>≥ 75%</span>
      </div>

      {/* Match cards */}
      <div style={{ display: 'flex', flexDirection: 'column', gap: 12 }}>

        {/* Fuzzy match 1 */}
        <MatchCard
          status="fuzzy"
          rowNum={3}
          projItem={{ artikul: 'SYS-1029PWTRT', name: 'Серв. Supermicro 1U, 2xXeon Silver 4314, 128GB', qty: 4, unit: 'шт' }}
          matches={[
            { score: 87, supplier: 'ИТ-Альянс',          artikul: 'SYS-1029P-WTRT',    name: 'Сервер Supermicro 1U, 2×Xeon Silver 4314, 128GB DDR4', selected: true, price: 472_800, lead: 42 },
            { score: 74, supplier: 'СвязьКомплект',      artikul: 'SM-1029P-WT',       name: 'Сервер Supermicro 1029P-WT, 1U, конфигурация под заказ',                        price: 511_900, lead: 21 },
            { score: 68, supplier: 'ТД Сетевые решения', artikul: 'SYS-2029P-WTRT',    name: 'Сервер Supermicro 2U SYS-2029P-WTRT, 2×Xeon Silver',                            price: 498_400, lead: 30 },
          ]}
        />

        {/* Fuzzy match 2 */}
        <MatchCard
          status="fuzzy"
          rowNum={47}
          projItem={{ artikul: 'GBIC-SX-1G-MM', name: 'SFP-модуль 1G SX MultiMode', qty: 16, unit: 'шт' }}
          matches={[
            { score: 82, supplier: 'ТД Сетевые решения', artikul: 'SFP-SX-1G-MM',    name: 'SFP трансивер 1000Base-SX, MultiMode, 850нм', selected: true, price: 2_400, lead: 7 },
            { score: 79, supplier: 'НетПром',            artikul: 'SFP-1G-SX',         name: 'SFP 1G SX 850нм MM (Eltex совместимый)',                        price: 2_180, lead: 14 },
          ]}
        />

        {/* Unmatched */}
        <MatchCard
          status="none"
          rowNum={113}
          projItem={{ artikul: 'PATCH-CAT6-2M', name: 'Патч-корд категории 6, 2 м (синий)', qty: 80, unit: 'шт' }}
        />
      </div>

      {/* Bottom summary */}
      <div style={{ marginTop: 22, padding: '14px 16px', background: 'var(--surface)', border: '1px solid var(--accent-border)', borderLeft: '3px solid var(--accent)', borderRadius: 7, display: 'grid', gridTemplateColumns: '1fr auto', gap: 18, alignItems: 'center' }}>
        <div>
          <div style={{ fontWeight: 500, marginBottom: 4, fontSize: 13 }}>Осталось решить: 11 нечётких + 3 не найдено</div>
          <div style={{ color: 'var(--text-2)', fontSize: 12.5, lineHeight: 1.55 }}>
            Точные совпадения уже включены в КП. Для нечётких выберите вариант или отметьте как «требует уточнения».
            Для непросопоставленных можно добавить позицию в номенклатуру или запросить аналог у менеджера справочников.
          </div>
        </div>
        <div style={{ display: 'flex', gap: 8 }}>
          <Btn size="sm" kind="ghost">Принять все нечёткие</Btn>
          <Btn size="sm" kind="primary">Завершить сопоставление</Btn>
        </div>
      </div>
    </V2Shell>
  );
}

function MatchCard({ status, rowNum, projItem, matches }) {
  const isFuzzy = status === 'fuzzy';
  const isNone  = status === 'none';
  return (
    <div style={{
      background: 'var(--surface)',
      border: '1px solid', borderColor: isNone ? '#fecaca' : 'var(--border)',
      borderRadius: 10, overflow: 'hidden',
    }}>
      {/* Top — project item */}
      <div style={{ padding: '14px 18px', display: 'flex', alignItems: 'center', gap: 16, borderBottom: '1px solid var(--border)' }}>
        <span className="mono" style={{ fontSize: 10.5, color: 'var(--text-3)', minWidth: 36 }}>стр. {rowNum}</span>
        <div style={{ flex: 1 }}>
          <div style={{ fontSize: 11, color: 'var(--text-2)', fontWeight: 500, textTransform: 'uppercase', letterSpacing: 0.5, marginBottom: 2 }}>Из проектного перечня</div>
          <div style={{ fontWeight: 500, fontSize: 13.5 }}>{projItem.name}</div>
          <div className="mono" style={{ fontSize: 11, color: 'var(--text-2)', marginTop: 2 }}>{projItem.artikul} · {projItem.qty} {projItem.unit}</div>
        </div>
        {isFuzzy && <StatusPill status="Нечёткое совпадение" kind="amber"/>}
        {isNone && <StatusPill status="Не найдено" kind="red"/>}
      </div>

      {/* Matches */}
      {isFuzzy && (
        <div style={{ padding: '10px 18px 14px', background: 'var(--accent-soft)' }}>
          <div style={{ fontSize: 11, color: 'var(--text-2)', fontWeight: 500, textTransform: 'uppercase', letterSpacing: 0.5, marginBottom: 10 }}>Найдено в базе прайсов</div>
          <div style={{ display: 'flex', flexDirection: 'column', gap: 6 }}>
            {matches.map((m, i) => (
              <div key={i} style={{
                display: 'grid', gridTemplateColumns: '60px 1fr 130px 100px 100px 26px',
                alignItems: 'center', gap: 14, padding: '10px 14px',
                background: m.selected ? 'var(--surface)' : 'transparent',
                border: m.selected ? '1.5px solid var(--accent)' : '1px solid var(--border)',
                borderRadius: 7, fontSize: 12.5,
              }}>
                {/* Score */}
                <div style={{ display: 'flex', flexDirection: 'column', alignItems: 'center', gap: 2 }}>
                  <span style={{ fontSize: 10, color: 'var(--text-3)', textTransform: 'uppercase', letterSpacing: 0.4 }}>SCORE</span>
                  <span className="tnum" style={{ fontSize: 16, fontWeight: 700, color: m.score >= 80 ? '#15803d' : m.score >= 70 ? '#b45309' : '#b91c1c' }}>{m.score}%</span>
                </div>
                <div>
                  <div style={{ fontSize: 12, fontWeight: 500, maxWidth: 380, overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>{m.name}</div>
                  <div className="mono" style={{ fontSize: 10.5, color: 'var(--text-3)', marginTop: 2 }}>{m.artikul}</div>
                </div>
                <div style={{ fontSize: 11.5 }}>
                  <div style={{ color: 'var(--text-2)', fontSize: 10 }}>Поставщик</div>
                  <div style={{ fontWeight: 500 }}>{m.supplier}</div>
                </div>
                <div className="tnum" style={{ textAlign: 'right', fontWeight: 500, fontSize: 13 }}>
                  {fmt(m.price)} ₽
                  <div style={{ fontSize: 10.5, color: 'var(--text-3)', fontWeight: 400 }}>за {projItem.unit}</div>
                </div>
                <div style={{ textAlign: 'right' }}>
                  <div className="tnum" style={{ fontSize: 12.5 }}>{m.lead} дн</div>
                  <div style={{ fontSize: 10.5, color: 'var(--text-3)' }}>срок</div>
                </div>
                <div style={{ textAlign: 'right' }}>
                  {m.selected
                    ? <Icon name="check" size={16} stroke="var(--accent)" strokeWidth={2.5}/>
                    : <Icon name="plus" size={14} stroke="var(--text-3)"/>
                  }
                </div>
              </div>
            ))}
          </div>
          <div style={{ marginTop: 10, display: 'flex', alignItems: 'center', gap: 8 }}>
            <Btn size="sm" kind="ghost">Не подходит — пометить как «требует уточнения»</Btn>
            <span style={{ flex: 1 }}/>
            <span style={{ fontSize: 11.5, color: 'var(--text-3)' }}>Алгоритм нечёткого поиска: Levenshtein + TF-IDF · порог 75%</span>
          </div>
        </div>
      )}

      {isNone && (
        <div style={{ padding: '14px 18px', background: '#fef2f2', display: 'grid', gridTemplateColumns: '1fr 1fr 1fr', gap: 10 }}>
          {[
            ['Добавить в номенклатуру', 'Создать новую позицию и привязать к ней одного из поставщиков', 'plus',     true],
            ['Запросить аналог',         'Передать менеджеру справочников для поиска',                    'search',    false],
            ['Пропустить позицию',       'КП будет сформирован без неё, статус «не найдено»',             'x',         false],
          ].map(([l, d, ic, primary], i) => (
            <div key={i} style={{
              padding: '12px 14px', borderRadius: 8,
              background: 'var(--surface)', border: '1px solid', borderColor: primary ? 'var(--accent)' : 'var(--border)',
              cursor: 'pointer',
            }}>
              <div style={{ display: 'flex', alignItems: 'center', gap: 8, marginBottom: 6 }}>
                <Icon name={ic} size={14} stroke={primary ? 'var(--accent)' : 'var(--text-2)'}/>
                <span style={{ fontWeight: 500, fontSize: 13, color: primary ? 'var(--accent)' : 'var(--text)' }}>{l}</span>
              </div>
              <div style={{ fontSize: 11.5, color: 'var(--text-2)', lineHeight: 1.5 }}>{d}</div>
            </div>
          ))}
        </div>
      )}
    </div>
  );
}

Object.assign(window, { V2NewProject, V2UploadProjectList, V2MatchResults });
