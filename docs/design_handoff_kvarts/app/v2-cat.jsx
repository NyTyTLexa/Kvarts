// Variant B — catalogs: manufacturers, nomenclature, supplier profile.

// ─── B17 — Manufacturers directory ────────────────────────────────
function V2Vendors() {
  const data = [
    { name: 'Eltex',      country: 'Россия',  city: 'Новосибирск', positions: 1284, suppliers: 4, growth: '+12%', cat: 'Сетевое' },
    { name: 'QTECH',      country: 'Россия',  city: 'Москва',      positions: 612,  suppliers: 3, growth: '+8%',  cat: 'Сетевое' },
    { name: 'Hikvision',  country: 'Китай',   city: 'Ханчжоу',     positions: 2418, suppliers: 5, growth: '+18%', cat: 'Видео' },
    { name: 'Supermicro', country: 'США',     city: 'Сан-Хосе',    positions: 384,  suppliers: 2, growth: '−4%',  cat: 'Серверы' },
    { name: 'Hyperline',  country: 'Россия',  city: 'СПб',         positions: 1812, suppliers: 4, growth: '+9%',  cat: 'СКС' },
    { name: 'APC',        country: 'Ирландия',city: 'Дублин',      positions: 524,  suppliers: 3, growth: '+3%',  cat: 'Электро' },
    { name: 'Cisco',      country: 'США',     city: 'Сан-Хосе',    positions: 102,  suppliers: 1, growth: '−22%', cat: 'Сетевое',  status: 'Ограничено' },
    { name: 'Huawei',     country: 'Китай',   city: 'Шэньчжэнь',   positions: 218,  suppliers: 2, growth: '+5%',  cat: 'Сетевое' },
  ];
  const catColor = (c) => ({
    'Сетевое': ['#bae6fd', '#075985'],
    'Видео':   ['#fed7aa', '#9a3412'],
    'Серверы': ['#e9d5ff', '#6b21a8'],
    'СКС':     ['#d9f99d', '#3f6212'],
    'Электро': ['#fde68a', '#92400e'],
  })[c] || ['#e7e5e4', '#57534e'];
  return (
    <V2Shell active="vendors" breadcrumb={['Справочники', 'Производители']} role="rp"
      actions={<>
        <Btn size="sm" kind="ghost" icon="upload">Импорт</Btn>
        <Btn size="sm" kind="primary" icon="plus">Добавить</Btn>
      </>}
    >
      <div style={{ marginBottom: 22 }}>
        <h1 style={{ margin: 0, fontSize: 26, fontWeight: 600, letterSpacing: -0.4 }}>Производители</h1>
        <p style={{ margin: '6px 0 0', color: 'var(--text-2)', fontSize: 13.5 }}>
          8 производителей · 23 поставщика работают с этими брендами · скидки применяются на уровне производителя.
        </p>
      </div>

      {/* KPI strip */}
      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(5, 1fr)', gap: 12, marginBottom: 22 }}>
        {[
          ['Производителей',     '8'],
          ['Позиций в номенкл.', '7 364'],
          ['Российские',          '4', '50%', '#15803d'],
          ['С ограничениями',     '1', 'Cisco',  '#b45309'],
          ['Скидки настроены',    '6 / 8'],
        ].map(([l, v, sub, color], i) => (
          <div key={i} style={{ background: 'var(--surface)', border: '1px solid var(--border)', borderRadius: 10, padding: '14px 16px' }}>
            <div style={{ fontSize: 11, color: 'var(--text-2)', textTransform: 'uppercase', letterSpacing: 0.5, fontWeight: 500 }}>{l}</div>
            <div className="tnum" style={{ fontSize: 22, fontWeight: 600, marginTop: 4, color: color || 'var(--text)' }}>{v}</div>
            {sub && <div style={{ fontSize: 11, color: 'var(--text-2)' }}>{sub}</div>}
          </div>
        ))}
      </div>

      {/* Tabs */}
      <div style={{ display: 'flex', alignItems: 'center', gap: 16, padding: '0 0 10px', borderBottom: '1px solid var(--border)' }}>
        {['Все', 'По категории', 'По стране', 'С ограничениями'].map((l, i) => (
          <div key={i} style={{
            padding: '6px 4px', fontSize: 13, color: i === 0 ? 'var(--text)' : 'var(--text-2)',
            borderBottom: i === 0 ? '2px solid var(--text)' : '2px solid transparent',
            fontWeight: i === 0 ? 500 : 400, marginBottom: -11,
          }}>{l}</div>
        ))}
      </div>

      <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: 13 }}>
        <thead>
          <tr>
            {['Бренд', 'Категория', 'Страна', 'Позиций в БД', 'Поставщиков', 'Динамика поставок', 'Статус'].map(h => (
              <th key={h} style={{ padding: '10px 12px', textAlign: 'left', fontWeight: 500, fontSize: 11, color: 'var(--text-2)', textTransform: 'uppercase', letterSpacing: 0.6, borderBottom: '1px solid var(--border)' }}>{h}</th>
            ))}
          </tr>
        </thead>
        <tbody>
          {data.map((v, i) => {
            const [bg, fg] = catColor(v.cat);
            const initial = v.name[0];
            const avBg = ['#fde68a','#bae6fd','#bbf7d0','#fecaca','#e9d5ff','#fbcfe8','#d9f99d','#a5f3fc'][i];
            return (
              <tr key={v.name} style={{ borderBottom: '1px solid var(--border)' }}>
                <td style={{ padding: '13px 12px' }}>
                  <div style={{ display: 'flex', alignItems: 'center', gap: 11 }}>
                    <div style={{ width: 28, height: 28, borderRadius: 6, background: avBg, color: '#1c1917', display: 'flex', alignItems: 'center', justifyContent: 'center', fontWeight: 700, fontSize: 13 }}>{initial}</div>
                    <span style={{ fontWeight: 500 }}>{v.name}</span>
                  </div>
                </td>
                <td style={{ padding: '13px 12px' }}>
                  <span style={{ padding: '2px 8px', borderRadius: 4, background: bg, color: fg, fontSize: 11.5, fontWeight: 500 }}>{v.cat}</span>
                </td>
                <td style={{ padding: '13px 12px', color: 'var(--text-2)' }}>{v.country} <span style={{ color: 'var(--text-3)', fontSize: 11.5 }}>· {v.city}</span></td>
                <td className="tnum" style={{ padding: '13px 12px' }}>{fmt(v.positions)}</td>
                <td className="tnum" style={{ padding: '13px 12px', color: 'var(--text-2)' }}>{v.suppliers}</td>
                <td style={{ padding: '13px 12px' }}>
                  <span className="tnum" style={{ color: v.growth.startsWith('+') ? '#15803d' : '#b91c1c', fontWeight: 500 }}>{v.growth}</span>
                  <span style={{ color: 'var(--text-3)', fontSize: 11, marginLeft: 6 }}>к прошл. году</span>
                </td>
                <td style={{ padding: '13px 12px' }}>
                  {v.status
                    ? <StatusPill status={v.status} kind="amber"/>
                    : <StatusPill status="Активен" kind="green"/>
                  }
                </td>
              </tr>
            );
          })}
        </tbody>
      </table>
    </V2Shell>
  );
}

// ─── B18 — Nomenclature directory ─────────────────────────────────
function V2Catalog() {
  // Hierarchy tree + list view
  return (
    <V2Shell active="catalog" breadcrumb={['Справочники', 'Номенклатура']} role="rp"
      actions={<>
        <Btn size="sm" kind="ghost" icon="upload">Импорт</Btn>
        <Btn size="sm" kind="primary" icon="plus">Создать</Btn>
      </>}
    >
      <div style={{ marginBottom: 22 }}>
        <h1 style={{ margin: 0, fontSize: 26, fontWeight: 600, letterSpacing: -0.4 }}>Номенклатура</h1>
        <p style={{ margin: '6px 0 0', color: 'var(--text-2)', fontSize: 13.5 }}>
          7 364 позиции · 14 категорий · нечёткий поиск находит совпадения по артикулу и наименованию.
        </p>
      </div>

      <div style={{ display: 'grid', gridTemplateColumns: '260px 1fr', gap: 22 }}>
        {/* Tree */}
        <aside style={{ background: 'var(--surface)', border: '1px solid var(--border)', borderRadius: 10, padding: '14px 0' }}>
          <div style={{ padding: '0 14px 10px', fontSize: 11, color: 'var(--text-2)', textTransform: 'uppercase', letterSpacing: 0.5, fontWeight: 500 }}>Категории</div>
          {[
            { l: 'Сетевое оборудование', n: 1896, depth: 0, open: true, active: true },
            { l: 'Коммутаторы',         n: 412,  depth: 1, open: true },
            { l: 'Маршрутизаторы',      n: 218,  depth: 1 },
            { l: 'Wi-Fi точки',         n: 184,  depth: 1, active: true },
            { l: 'SFP и трансиверы',    n: 1082, depth: 1 },
            { l: 'СКС и кабели',        n: 1812, depth: 0, open: true },
            { l: 'Медные кабели',       n: 624,  depth: 1 },
            { l: 'Оптика',              n: 412,  depth: 1 },
            { l: 'Патч-корды',          n: 776,  depth: 1 },
            { l: 'Видеонаблюдение',     n: 2418, depth: 0 },
            { l: 'Серверы и СХД',       n: 384,  depth: 0 },
            { l: 'Электропитание',      n: 524,  depth: 0 },
            { l: 'Аксессуары',          n: 330,  depth: 0 },
          ].map((c, i) => (
            <div key={i} style={{
              display: 'flex', alignItems: 'center', gap: 7,
              padding: '6px 14px', paddingLeft: 14 + c.depth * 16,
              fontSize: c.depth === 0 ? 13 : 12.5,
              color: c.active ? 'var(--accent)' : 'var(--text)',
              background: c.active ? 'var(--accent-soft)' : 'transparent',
              fontWeight: c.active || c.depth === 0 ? 500 : 400,
              cursor: 'pointer',
            }}>
              {c.depth === 0
                ? <Icon name={c.open ? 'chevD' : 'chevR'} size={11} stroke={c.active ? 'var(--accent)' : 'var(--text-2)'}/>
                : <span style={{ width: 11 }}/>
              }
              <span style={{ flex: 1 }}>{c.l}</span>
              <span style={{ fontSize: 10.5, color: 'var(--text-3)' }}>{fmt(c.n)}</span>
            </div>
          ))}
        </aside>

        {/* List */}
        <div>
          {/* Search + filters */}
          <div style={{ display: 'flex', alignItems: 'center', gap: 8, marginBottom: 14 }}>
            <div style={{ display: 'flex', alignItems: 'center', gap: 7, padding: '6px 12px', background: 'var(--surface-2)', borderRadius: 7, flex: 1, fontSize: 12.5, color: 'var(--text-2)' }}>
              <Icon name="search" size={13}/> Поиск по артикулу или наименованию · нечёткое включено
            </div>
            {['Все', 'Активные', 'Снятые'].map((c, i) => (
              <div key={c} style={{ padding: '5px 11px', borderRadius: 999, fontSize: 11.5, background: i === 0 ? 'var(--accent-soft)' : 'var(--surface-2)', color: i === 0 ? 'var(--accent)' : 'var(--text-2)', fontWeight: 500 }}>{c}</div>
            ))}
          </div>

          {/* Breadcrumb in catalog */}
          <div style={{ fontSize: 12, color: 'var(--text-2)', marginBottom: 12, display: 'flex', alignItems: 'center', gap: 6 }}>
            <span>Сетевое оборудование</span>
            <span style={{ color: 'var(--text-3)' }}>/</span>
            <span style={{ color: 'var(--text)', fontWeight: 500 }}>Wi-Fi точки</span>
            <span style={{ color: 'var(--text-3)' }}>· 184 позиции</span>
          </div>

          <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: 12.5 }}>
            <thead>
              <tr>
                {['Артикул', 'Наименование', 'Производитель', 'Ед.', 'Поставщиков', 'Мин. цена', 'Статус'].map(h => (
                  <th key={h} style={{ padding: '9px 12px', textAlign: 'left', fontWeight: 500, fontSize: 11, color: 'var(--text-2)', textTransform: 'uppercase', letterSpacing: 0.5, borderBottom: '1px solid var(--border)' }}>{h}</th>
                ))}
              </tr>
            </thead>
            <tbody>
              {[
                { artikul: 'WB-2P-LR2',       name: 'Eltex WB-2P-LR2, indoor, 2x2 Wi-Fi 5',         vendor: 'Eltex',     unit: 'шт', sup: 4, min: 24_700, st: 'Активен' },
                { artikul: 'WB-1P-AC',         name: 'Eltex WB-1P-AC, indoor, 1x1 Wi-Fi 5',           vendor: 'Eltex',     unit: 'шт', sup: 3, min: 17_400, st: 'Активен' },
                { artikul: 'WB-2P-LR3-AX',     name: 'Eltex WB-2P-LR3-AX, indoor Wi-Fi 6',            vendor: 'Eltex',     unit: 'шт', sup: 3, min: 38_200, st: 'Активен' },
                { artikul: 'QWO-95-WIFI6E',    name: 'QTECH QWO-95-WIFI6E, 6 GHz',                    vendor: 'QTECH',     unit: 'шт', sup: 2, min: 41_600, st: 'Активен' },
                { artikul: 'QWO-94',           name: 'QTECH QWO-94, indoor Wi-Fi 6',                  vendor: 'QTECH',     unit: 'шт', sup: 2, min: 28_900, st: 'Активен' },
                { artikul: 'AIR-AP3802I-K9',   name: 'Cisco Aironet 3802I, Wi-Fi 5 dual radio',       vendor: 'Cisco',     unit: 'шт', sup: 1, min: 78_400, st: 'Снят с произв.' },
              ].map((p, i) => (
                <tr key={i} style={{ borderBottom: '1px solid var(--border)' }}>
                  <td className="mono" style={{ padding: '12px 12px', fontSize: 11.5, color: 'var(--text-2)' }}>{p.artikul}</td>
                  <td style={{ padding: '12px 12px', fontWeight: 500 }}>{p.name}</td>
                  <td style={{ padding: '12px 12px', color: 'var(--text-2)' }}>{p.vendor}</td>
                  <td style={{ padding: '12px 12px', color: 'var(--text-2)' }}>{p.unit}</td>
                  <td className="tnum" style={{ padding: '12px 12px', color: 'var(--text-2)' }}>{p.sup}</td>
                  <td className="tnum" style={{ padding: '12px 12px', fontWeight: 500 }}>{fmt(p.min)} ₽</td>
                  <td style={{ padding: '12px 12px' }}>
                    <StatusPill status={p.st} kind={p.st === 'Активен' ? 'green' : 'gray'}/>
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

// ─── B19 — Supplier profile ───────────────────────────────────────
function V2SupplierProfile() {
  const s = window.SUPPLIERS[0]; // ТД Сетевые решения
  return (
    <V2Shell active="suppliers" breadcrumb={['Справочники', 'Поставщики', s.name]} role="rp"
      actions={<>
        <Btn size="sm" kind="ghost">На паузу</Btn>
        <Btn size="sm" kind="primary" icon="edit">Редактировать</Btn>
      </>}
    >
      <div style={{ display: 'flex', alignItems: 'flex-start', gap: 18, marginBottom: 22 }}>
        <div style={{ width: 56, height: 56, borderRadius: 10, background: '#fef3c7', color: '#92400e', display: 'flex', alignItems: 'center', justifyContent: 'center', fontWeight: 700, fontSize: 22 }}>
          ТС
        </div>
        <div style={{ flex: 1 }}>
          <h1 style={{ margin: 0, fontSize: 26, fontWeight: 600, letterSpacing: -0.4 }}>{s.name}</h1>
          <div style={{ display: 'flex', alignItems: 'center', gap: 12, marginTop: 8, fontSize: 13, color: 'var(--text-2)' }}>
            <StatusPill status={s.status} kind="green"/>
            <span className="mono">ИНН {s.inn}</span>
            <span>·</span>
            <span><span style={{ color: '#f59e0b' }}>★</span> <b className="tnum" style={{ color: 'var(--text)' }}>{s.rating}</b></span>
            <span>·</span>
            <span>{s.deals} сделок · SLA {s.slaDays} дн.</span>
          </div>
        </div>
      </div>

      {/* KPI cards */}
      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(5, 1fr)', gap: 12, marginBottom: 22 }}>
        {[
          ['Скидка',             `−${s.discount}%`,  'базовая · 01.04.26',           'var(--accent)'],
          ['Оборот за 12 мес.',  '38,4 млн ₽',       '+18% год к году',              '#15803d'],
          ['Позиций в прайсе',   '1 284',            'обновл. 24.04',                'var(--text)'],
          ['Точность сроков',     '94%',             'по последним 50 поставкам',    '#15803d'],
          ['Возвратов / актов',   '1,2%',             '2 случая за полгода',          'var(--text)'],
        ].map(([l, v, sub, color], i) => (
          <div key={i} style={{ background: 'var(--surface)', border: '1px solid var(--border)', borderRadius: 10, padding: '14px 16px' }}>
            <div style={{ fontSize: 11, color: 'var(--text-2)', textTransform: 'uppercase', letterSpacing: 0.5, fontWeight: 500 }}>{l}</div>
            <div className="tnum" style={{ fontSize: 22, fontWeight: 600, marginTop: 4, color }}>{v}</div>
            <div style={{ fontSize: 11, color: 'var(--text-2)' }}>{sub}</div>
          </div>
        ))}
      </div>

      <div style={{ display: 'grid', gridTemplateColumns: '1fr 320px', gap: 22 }}>
        <div>
          {/* Tabs */}
          <div style={{ display: 'flex', alignItems: 'center', gap: 16, padding: '0 0 10px', borderBottom: '1px solid var(--border)' }}>
            {[
              ['Сделки', 124, true],
              ['Прайсы', 12,  false],
              ['Реквизиты', null, false],
              ['Документы', 8, false],
              ['Комментарии', 5, false],
            ].map(([l, n, active], i) => (
              <div key={i} style={{
                padding: '6px 4px', fontSize: 13, color: active ? 'var(--text)' : 'var(--text-2)',
                borderBottom: active ? '2px solid var(--text)' : '2px solid transparent',
                fontWeight: active ? 500 : 400, marginBottom: -11,
                display: 'flex', alignItems: 'center', gap: 7,
              }}>
                {l}{n != null && <span style={{ fontSize: 11, color: 'var(--text-3)' }}>{n}</span>}
              </div>
            ))}
          </div>

          <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: 12.5 }}>
            <thead>
              <tr>
                {['Проект', 'Позиций', 'Сумма', 'Срок факт / план', 'Статус', 'Закрыт'].map(h => (
                  <th key={h} style={{ padding: '10px 12px', textAlign: 'left', fontWeight: 500, fontSize: 11, color: 'var(--text-2)', textTransform: 'uppercase', letterSpacing: 0.5, borderBottom: '1px solid var(--border)' }}>{h}</th>
                ))}
              </tr>
            </thead>
            <tbody>
              {[
                { p: 'P-2026-118 СКС ТЦ Орбита',         items: 4, sum: 1_412_000, fact: '12 / 14', state: 'В работе',     k: 'amber', date: '—' },
                { p: 'P-2026-102 Wi-Fi офис',             items: 18, sum: 2_220_000, fact: '14 / 14', state: 'Закрыто',      k: 'green', date: '19.05' },
                { p: 'P-2026-097 Замена коммутаторов', items: 12, sum: 3_840_000, fact: '13 / 14', state: 'Закрыто',      k: 'green', date: '15.04' },
                { p: 'P-2026-091 Видео склада',           items: 21, sum: 612_000,   fact: '11 / 7',  state: 'Опоздание',   k: 'red',   date: '02.04' },
                { p: 'P-2026-084 СКС цех №2',             items: 32, sum: 1_980_000, fact: '8 / 10',  state: 'Закрыто',      k: 'green', date: '25.03' },
              ].map((r, i) => (
                <tr key={i} style={{ borderBottom: '1px solid var(--border)' }}>
                  <td style={{ padding: '12px 12px', fontWeight: 500 }}>{r.p}</td>
                  <td className="tnum" style={{ padding: '12px 12px', color: 'var(--text-2)' }}>{r.items}</td>
                  <td className="tnum" style={{ padding: '12px 12px', fontWeight: 500 }}>{fmtMoneyShort(r.sum)}</td>
                  <td style={{ padding: '12px 12px', color: 'var(--text-2)' }}>{r.fact} <span style={{ fontSize: 11, color: 'var(--text-3)' }}>раб.дн.</span></td>
                  <td style={{ padding: '12px 12px' }}><StatusPill status={r.state} kind={r.k}/></td>
                  <td style={{ padding: '12px 12px', color: 'var(--text-2)', fontVariantNumeric: 'tabular-nums' }}>{r.date}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>

        {/* Sidebar */}
        <aside style={{ display: 'flex', flexDirection: 'column', gap: 18 }}>
          <div style={{ background: 'var(--surface-2)', borderRadius: 9, padding: '14px 16px' }}>
            <div style={{ display: 'grid', gridTemplateColumns: 'auto 1fr', gap: '8px 14px', fontSize: 12.5 }}>
              <span style={{ color: 'var(--text-2)' }}>Юр. адрес</span>
              <span>г. Москва, ул. Бутырский Вал, 24</span>
              <span style={{ color: 'var(--text-2)' }}>КПП</span>
              <span className="mono">770701001</span>
              <span style={{ color: 'var(--text-2)' }}>Менеджер</span>
              <span>Иванова Ольга</span>
              <span style={{ color: 'var(--text-2)' }}>Email</span>
              <span className="mono" style={{ fontSize: 11.5 }}>sales@td-set.ru</span>
              <span style={{ color: 'var(--text-2)' }}>Телефон</span>
              <span className="mono">+7 495 123-45-67</span>
              <span style={{ color: 'var(--text-2)' }}>Договор</span>
              <span>Рамочный №12-Р от 14.02.24</span>
            </div>
          </div>

          <div>
            <div style={{ fontSize: 11.5, color: 'var(--text-2)', fontWeight: 500, textTransform: 'uppercase', letterSpacing: 0.5, marginBottom: 10 }}>Топ брендов в прайсе</div>
            {[
              ['Eltex',      382, 30],
              ['Hyperline',  214, 17],
              ['Hikvision',  186, 14],
              ['Cisco',      102, 8],
              ['Прочие',     400, 31],
            ].map(([l, n, pct], i) => (
              <div key={i} style={{ display: 'flex', alignItems: 'center', gap: 10, padding: '5px 0', fontSize: 12.5 }}>
                <span style={{ flex: 1 }}>{l}</span>
                <span className="tnum" style={{ color: 'var(--text-2)', width: 40, textAlign: 'right' }}>{n}</span>
                <div style={{ width: 70, height: 5, background: 'var(--surface-2)', borderRadius: 999 }}>
                  <div style={{ width: pct + '%', height: '100%', background: 'var(--accent)', borderRadius: 999 }}/>
                </div>
              </div>
            ))}
          </div>

          <div>
            <div style={{ fontSize: 11.5, color: 'var(--text-2)', fontWeight: 500, textTransform: 'uppercase', letterSpacing: 0.5, marginBottom: 10 }}>Поставки 12 мес.</div>
            <div style={{ display: 'flex', alignItems: 'flex-end', gap: 3, height: 60 }}>
              {[8, 10, 12, 9, 14, 11, 13, 15, 18, 14, 19, 16].map((n, i) => (
                <div key={i} style={{ flex: 1, height: (n / 19) * 100 + '%', background: i === 11 ? 'var(--accent)' : 'var(--accent-soft)', borderRadius: '2px 2px 0 0', minHeight: 2 }}/>
              ))}
            </div>
            <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: 10, color: 'var(--text-3)', marginTop: 4 }}>
              <span>май '25</span><span>апр '26</span>
            </div>
          </div>
        </aside>
      </div>
    </V2Shell>
  );
}

Object.assign(window, { V2Vendors, V2Catalog, V2SupplierProfile });
