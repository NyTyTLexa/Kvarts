// Variant B — extra screens: Upload, KP Detail, Discounts, Approval.

function V2Upload() {
  return (
    <V2Shell active="prices" breadcrumb={['Прайсы', 'Новая загрузка']} role="rp"
      actions={<>
        <Btn size="sm" kind="ghost">Отмена</Btn>
        <Btn size="sm" kind="primary" icon="check">Сохранить в БД</Btn>
      </>}
    >
      <div style={{ marginBottom: 22 }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: 10, marginBottom: 6 }}>
          <h1 style={{ margin: 0, fontSize: 26, fontWeight: 600, letterSpacing: -0.4 }}>Загрузка прайса поставщика</h1>
        </div>
        <p style={{ margin: '6px 0 0 0', color: 'var(--text-2)', fontSize: 13.5, maxWidth: 600 }}>
          Поддерживаются <span className="mono" style={{ fontSize: 12 }}>.xlsx</span> и <span className="mono" style={{ fontSize: 12 }}>.xls</span> до 50 МБ. Система проверит структуру и предложит сопоставить колонки.
        </p>
      </div>

      <div style={{ display: 'grid', gridTemplateColumns: '380px 1fr', gap: 22 }}>
        <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
          <div style={{
            background: 'var(--accent-soft)', border: '1.5px dashed var(--accent)', borderRadius: 12,
            padding: '28px 20px', textAlign: 'center',
          }}>
            <div style={{ fontSize: 14, fontWeight: 500, marginBottom: 4 }}>Файл получен — обработка…</div>
            <div className="mono" style={{ fontSize: 12, color: 'var(--text-2)' }}>prajs_setevye_resheniya_Q2.xlsx</div>
            <div style={{ fontSize: 11.5, color: 'var(--text-2)', marginTop: 4 }}>2,4 МБ · 1 284 строки</div>
          </div>

          <div style={{ background: 'var(--surface-2)', borderRadius: 9, padding: '12px 16px' }}>
            <div style={{ display: 'grid', gridTemplateColumns: 'auto 1fr', gap: '8px 16px', fontSize: 13 }}>
              {[
                ['Поставщик',    'ТД Сетевые решения'],
                ['ИНН',          <span key="i" className="mono" style={{ fontSize: 12 }}>7707654321</span>],
                ['Дата прайса',  '24.04.2026'],
                ['Валюта',       'RUB ₽'],
                ['НДС',          'С НДС 20%'],
              ].map(([k, v], i) => (
                <React.Fragment key={i}>
                  <span style={{ color: 'var(--text-2)' }}>{k}</span>
                  <span style={{ fontWeight: 500 }}>{v}</span>
                </React.Fragment>
              ))}
            </div>
          </div>

          <div style={{ background: 'var(--surface)', border: '1px solid var(--accent-border)', borderLeft: '3px solid var(--accent)', borderRadius: 7, padding: '12px 14px', fontSize: 12.5 }}>
            <div style={{ fontWeight: 500, marginBottom: 4 }}>Найдено 12 дублей</div>
            <div style={{ color: 'var(--text-2)', lineHeight: 1.55 }}>
              Артикулы совпадают со строками в прайсе НетПром — можно объединить или загрузить отдельно.
            </div>
          </div>
        </div>

        <div style={{ display: 'flex', flexDirection: 'column', gap: 22 }}>
          <div>
            <div style={{ display: 'flex', alignItems: 'center', gap: 10, marginBottom: 10 }}>
              <span style={{ fontSize: 14, fontWeight: 600 }}>Маппинг колонок</span>
              <span style={{ fontSize: 12, color: 'var(--text-2)' }}>22 из 25 распознаны автоматически</span>
              <div style={{ flex: 1 }}/>
              <span style={{ padding: '2px 8px', borderRadius: 999, background: '#dcfce7', color: '#166534', fontSize: 11, fontWeight: 500 }}>✓ Структура валидна</span>
            </div>
            <div style={{ display: 'grid', gridTemplateColumns: 'repeat(3, 1fr)', gap: '10px 18px' }}>
              {[
                ['Артикул',          'A · "Артикул"',       true],
                ['Номенклатура',     'B · "Наименование"',  true],
                ['Ед. изм.',         'D · "Ед"',            true],
                ['Цена без НДС',     'F · "Цена опт."',     true],
                ['Цена с НДС',       'G · "Цена с НДС"',    true],
                ['Срок поставки',    'I · "Срок, дн."',     true],
                ['Иерархия 1 ур.',   'K · "Категория"',     true],
                ['Иерархия 2 ур.',   '— не найдено',         false],
                ['Сертификат',       'M · "Серт."',          true],
              ].map(([target, source, ok], i) => (
                <div key={i} style={{ display: 'flex', alignItems: 'center', gap: 8, fontSize: 12 }}>
                  <span style={{ minWidth: 110, color: 'var(--text-2)' }}>{target}</span>
                  <span style={{ color: 'var(--text-3)' }}>→</span>
                  <span style={{
                    flex: 1, padding: '3px 9px', borderRadius: 5,
                    background: ok ? 'var(--surface-2)' : '#fef3e8',
                    color: ok ? 'var(--text)' : '#9a3412',
                    border: '1px solid',
                    borderColor: ok ? 'var(--border)' : '#fdd9b4',
                    fontFamily: 'JetBrains Mono', fontSize: 11,
                  }}>{source}</span>
                </div>
              ))}
            </div>
          </div>

          <div>
            <div style={{ display: 'flex', alignItems: 'center', gap: 10, marginBottom: 10 }}>
              <span style={{ fontSize: 14, fontWeight: 600 }}>Предпросмотр</span>
              <span style={{ fontSize: 12, color: 'var(--text-2)' }}>первые 6 строк из 1 284</span>
            </div>
            <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: 12.5 }}>
              <thead>
                <tr>
                  {['Артикул', 'Наименование', 'Ед', 'Цена б/НДС', 'Цена с НДС', 'Срок'].map(h => (
                    <th key={h} style={{ padding: '8px 12px', textAlign: 'left', fontWeight: 500, fontSize: 11, color: 'var(--text-2)', textTransform: 'uppercase', letterSpacing: 0.5, borderBottom: '1px solid var(--border)' }}>{h}</th>
                  ))}
                </tr>
              </thead>
              <tbody>
                {window.PROJECT_ITEMS.slice(0, 6).map((it) => (
                  <tr key={it.id} style={{ borderBottom: '1px solid var(--border)' }}>
                    <td className="mono" style={{ padding: '10px 12px', fontSize: 11 }}>{it.artikul}</td>
                    <td style={{ padding: '10px 12px', maxWidth: 280, whiteSpace: 'nowrap', overflow: 'hidden', textOverflow: 'ellipsis' }}>{it.name}</td>
                    <td style={{ padding: '10px 12px', color: 'var(--text-2)' }}>{it.unit}</td>
                    <td className="tnum" style={{ padding: '10px 12px', textAlign: 'right' }}>{fmt(it.quotes[0][1])}</td>
                    <td className="tnum" style={{ padding: '10px 12px', textAlign: 'right' }}>{fmt(Math.round(it.quotes[0][1] * 1.2))}</td>
                    <td className="tnum" style={{ padding: '10px 12px', textAlign: 'right', color: 'var(--text-2)' }}>{it.quotes[0][2]} дн</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>
      </div>
    </V2Shell>
  );
}

function V2KPDetail() {
  const variant = window.VARIANTS.fast;
  return (
    <V2Shell active="kp" breadcrumb={['Проекты', 'P-2026-118', 'КП · Скорость']} role="rp"
      actions={<>
        <Btn size="sm" kind="ghost" icon="download">Excel</Btn>
        <Btn size="sm" kind="primary" icon="arrowR">Передать в КБ</Btn>
      </>}
    >
      <div style={{ marginBottom: 20 }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: 12, marginBottom: 6 }}>
          <h1 style={{ margin: 0, fontSize: 26, fontWeight: 600, letterSpacing: -0.4 }}>Вариант «Скорость»</h1>
          <StatusPill status="Согласование РП"/>
        </div>
        <p style={{ margin: '6px 0 0 0', color: 'var(--text-2)', fontSize: 13.5 }}>
          СКС и сетевое ядро ТЦ «Орбита» · <span className="mono" style={{ fontSize: 11.5 }}>P-2026-118-v3</span> · 8 поставщиков · мин. срок поставки
        </p>
      </div>

      <div style={{ background: 'var(--surface-2)', borderRadius: 9, padding: '14px 18px', marginBottom: 22 }}>
        <div style={{ display: 'grid', gridTemplateColumns: 'auto 1fr auto 1fr', gap: '10px 22px', fontSize: 13 }}>
          <span style={{ color: 'var(--text-2)' }}>Итого без НДС</span>
          <span style={{ fontWeight: 600 }}>{fmtMoneyShort(variant.total)}</span>
          <span style={{ color: 'var(--text-2)' }}>Маржа</span>
          <span style={{ fontWeight: 600, color: '#15803d' }}>20,3%</span>

          <span style={{ color: 'var(--text-2)' }}>С НДС</span>
          <span>{fmtMoneyShort(variant.total * 1.2)}</span>
          <span style={{ color: 'var(--text-2)' }}>Срок</span>
          <span>{variant.maxLead} раб. дн.</span>

          <span style={{ color: 'var(--text-2)' }}>Версия</span>
          <span>3 (24.04 16:31)</span>
          <span style={{ color: 'var(--text-2)' }}>Поставщиков</span>
          <span>{new Set(variant.rows.map(r => r.supplierId)).size}</span>
        </div>
      </div>

      <div style={{ display: 'grid', gridTemplateColumns: '1fr 300px', gap: 22 }}>
        <div>
          <div style={{ display: 'flex', alignItems: 'center', gap: 10, marginBottom: 10 }}>
            <span style={{ fontSize: 14, fontWeight: 600 }}>Позиции</span>
            <span style={{ fontSize: 12, color: 'var(--text-2)', padding: '2px 8px', borderRadius: 999, background: 'var(--surface-2)' }}>8 показано</span>
            <div style={{ flex: 1 }}/>
            <span style={{ fontSize: 12, color: 'var(--text-2)' }}>Клик меняет поставщика</span>
          </div>
          <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: 12.5 }}>
            <thead>
              <tr>
                {['Позиция', 'Поставщик', 'Цена', 'Скидка', 'Срок', 'Итог'].map((h, i) => (
                  <th key={h} style={{ padding: '8px 12px', textAlign: i >= 2 ? 'right' : 'left', fontWeight: 500, fontSize: 11, color: 'var(--text-2)', textTransform: 'uppercase', letterSpacing: 0.5, borderBottom: '1px solid var(--border)' }}>{h}</th>
                ))}
              </tr>
            </thead>
            <tbody>
              {variant.rows.map((r, i) => {
                const isEdit = i === 1;
                return (
                  <React.Fragment key={r.id}>
                    <tr style={{ borderBottom: '1px solid var(--border)', background: isEdit ? 'var(--accent-soft)' : 'transparent' }}>
                      <td style={{ padding: '11px 12px' }}>
                        <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
                          <Icon name="package" size={14} stroke="var(--text-2)"/>
                          <span style={{ fontWeight: 500, maxWidth: 280, overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>{r.name}</span>
                        </div>
                        <div className="mono" style={{ fontSize: 10.5, color: 'var(--text-3)', marginTop: 2, marginLeft: 22 }}>{r.artikul} · {r.qty} {r.unit}</div>
                      </td>
                      <td style={{ padding: '11px 12px' }}>
                        <span style={{ padding: '2px 8px', borderRadius: 4, background: '#dbeafe', color: '#1e40af', fontSize: 11.5, fontWeight: 500, display: 'inline-flex', alignItems: 'center', gap: 5 }}>
                          {r.supplierName} <Icon name="chevD" size={10}/>
                        </span>
                      </td>
                      <td className="tnum" style={{ padding: '11px 12px', textAlign: 'right' }}>{fmt(r.unitPrice)} ₽</td>
                      <td style={{ padding: '11px 12px', textAlign: 'right' }}>
                        <span style={{ padding: '2px 7px', borderRadius: 999, background: 'var(--accent-soft)', color: 'var(--accent)', fontSize: 11, fontWeight: 600 }}>−{r.discount}%</span>
                      </td>
                      <td className="tnum" style={{ padding: '11px 12px', textAlign: 'right', color: 'var(--text-2)' }}>{r.leadDays} дн</td>
                      <td className="tnum" style={{ padding: '11px 12px', textAlign: 'right', fontWeight: 600 }}>{fmtMoneyShort(r.total)}</td>
                    </tr>
                    {isEdit && (
                      <tr>
                        <td colSpan={6} style={{ background: 'var(--accent-soft)', padding: '0 12px 14px', borderBottom: '1px solid var(--accent-border)' }}>
                          <div style={{ fontSize: 11, color: 'var(--text-2)', marginBottom: 8, paddingTop: 2 }}>Альтернативы по этой позиции:</div>
                          {window.PROJECT_ITEMS.find(p => p.id === r.id).quotes.map(([sid, price, lead], k) => {
                            const sup = window.SUPPLIERS.find(s => s.id === sid);
                            const sel = sid === r.supplierId;
                            return (
                              <div key={k} style={{
                                display: 'grid', gridTemplateColumns: '1fr 110px 80px 110px 26px',
                                alignItems: 'center', gap: 12, padding: '8px 12px',
                                background: sel ? 'var(--surface)' : 'transparent',
                                border: sel ? '1px solid var(--accent)' : '1px solid transparent',
                                borderRadius: 6, fontSize: 12, marginBottom: 4,
                              }}>
                                <div style={{ display: 'flex', alignItems: 'center', gap: 9 }}>
                                  <span style={{ width: 14, height: 14, borderRadius: 999, border: sel ? '4px solid var(--accent)' : '1px solid var(--border-strong)', background: sel ? 'white' : 'var(--surface)' }}/>
                                  <span style={{ fontWeight: sel ? 500 : 400 }}>{sup.name}</span>
                                  <span style={{ fontSize: 10.5, color: 'var(--text-2)' }}>SLA {sup.slaDays} дн · ★{sup.rating}</span>
                                </div>
                                <span className="tnum" style={{ textAlign: 'right' }}>{fmt(price)} ₽</span>
                                <span style={{ textAlign: 'right' }}><span style={{ padding: '1px 6px', borderRadius: 4, background: 'var(--surface-2)', fontSize: 11, color: 'var(--text-2)' }}>−{sup.discount}%</span></span>
                                <span className="tnum" style={{ textAlign: 'right', color: 'var(--text-2)' }}>{lead} раб.дн</span>
                                {sel ? <Icon name="check" size={14} stroke="var(--accent)" strokeWidth={2}/> : <span/>}
                              </div>
                            );
                          })}
                        </td>
                      </tr>
                    )}
                  </React.Fragment>
                );
              })}
            </tbody>
          </table>
        </div>

        <aside style={{ display: 'flex', flexDirection: 'column', gap: 18 }}>
          <div>
            <div style={{ fontSize: 11.5, color: 'var(--text-2)', fontWeight: 500, textTransform: 'uppercase', letterSpacing: 0.5, marginBottom: 10 }}>Жизненный цикл</div>
            <div style={{ display: 'flex', flexDirection: 'column', gap: 8 }}>
              {window.LIFECYCLE_STAGES.slice(0, 5).map((s, i) => {
                const done = i < 1, active = i === 1;
                return (
                  <div key={s.id} style={{ display: 'flex', alignItems: 'center', gap: 10, fontSize: 13 }}>
                    <span style={{ width: 18, height: 18, borderRadius: 999, background: done || active ? 'var(--accent)' : 'var(--surface-2)', color: done || active ? 'white' : 'var(--text-3)', display: 'flex', alignItems: 'center', justifyContent: 'center', fontSize: 10, fontWeight: 600, flexShrink: 0 }}>{done ? '✓' : i + 1}</span>
                    <span style={{ color: active ? 'var(--text)' : done ? 'var(--text-2)' : 'var(--text-3)', fontWeight: active ? 500 : 400, flex: 1 }}>{s.full}</span>
                    <span style={{ fontSize: 11, color: 'var(--text-3)' }}>{done ? '24.04' : ''}</span>
                  </div>
                );
              })}
            </div>
          </div>

          <div>
            <div style={{ fontSize: 11.5, color: 'var(--text-2)', fontWeight: 500, textTransform: 'uppercase', letterSpacing: 0.5, marginBottom: 10 }}>Скидки</div>
            <div style={{ display: 'flex', flexDirection: 'column', gap: 6 }}>
              {window.VENDORS.slice(0, 4).map((v, i) => {
                const d = [12, 9, 0, 15][i];
                return (
                  <div key={v.id} style={{ display: 'flex', alignItems: 'center', gap: 8, fontSize: 12.5 }}>
                    <span style={{ flex: 1 }}>{v.name}</span>
                    <span style={{ padding: '1px 8px', borderRadius: 999, background: d > 0 ? 'var(--accent-soft)' : 'var(--surface-2)', color: d > 0 ? 'var(--accent)' : 'var(--text-3)', fontWeight: 500, fontSize: 11.5 }}>{d > 0 ? `−${d}%` : '—'}</span>
                  </div>
                );
              })}
            </div>
          </div>

          <div>
            <div style={{ fontSize: 11.5, color: 'var(--text-2)', fontWeight: 500, textTransform: 'uppercase', letterSpacing: 0.5, marginBottom: 10 }}>Комментарии</div>
            <div style={{ display: 'flex', flexDirection: 'column', gap: 12 }}>
              <div style={{ display: 'flex', gap: 8 }}>
                <div style={{ width: 22, height: 22, borderRadius: 999, background: '#fde68a', color: '#92400e', display: 'flex', alignItems: 'center', justifyContent: 'center', fontSize: 10, fontWeight: 600, flexShrink: 0 }}>ВЕ</div>
                <div style={{ fontSize: 12 }}>
                  <div><b>Васильева Е.</b> <span style={{ color: 'var(--text-3)' }}>· 1 ч</span></div>
                  <div style={{ color: 'var(--text-2)', marginTop: 2, lineHeight: 1.55 }}>L02 — лучше через СвязьКомплект, у них SLA 9 дней. Маржа всё ещё в норме.</div>
                </div>
              </div>
              <div style={{ display: 'flex', gap: 8 }}>
                <div style={{ width: 22, height: 22, borderRadius: 999, background: '#bae6fd', color: '#075985', display: 'flex', alignItems: 'center', justifyContent: 'center', fontSize: 10, fontWeight: 600, flexShrink: 0 }}>СА</div>
                <div style={{ fontSize: 12 }}>
                  <div><b>Смирнов А.</b> <span style={{ color: 'var(--text-3)' }}>· 40 мин</span></div>
                  <div style={{ color: 'var(--text-2)', marginTop: 2, lineHeight: 1.55 }}>Согласен, заменю. По L03 поставщик подтвердил резерв на 4 шт.</div>
                </div>
              </div>
            </div>
            <div style={{ marginTop: 10, padding: '6px 10px', border: '1px solid var(--border)', borderRadius: 6, fontSize: 12, color: 'var(--text-3)' }}>Написать комментарий…</div>
          </div>
        </aside>
      </div>
    </V2Shell>
  );
}

function V2Discounts() {
  return (
    <V2Shell active="suppliers" breadcrumb={['Скидки и условия']} role="rp"
      actions={<>
        <Btn size="sm" kind="ghost">Отмена</Btn>
        <Btn size="sm" kind="primary" icon="check">Сохранить</Btn>
      </>}
    >
      <div style={{ marginBottom: 22 }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: 10, marginBottom: 6 }}>
          <h1 style={{ margin: 0, fontSize: 26, fontWeight: 600, letterSpacing: -0.4 }}>Скидки</h1>
        </div>
        <p style={{ margin: '6px 0 0 0', color: 'var(--text-2)', fontSize: 13.5, maxWidth: 600 }}>
          Управление процентами скидок по вендорам и производителям. Применяется при формировании КП автоматически.
        </p>
      </div>

      <div style={{ display: 'flex', alignItems: 'center', gap: 16, padding: '0 0 10px', borderBottom: '1px solid var(--border)', marginBottom: 0 }}>
        {[
          ['По поставщикам', true],
          ['По производителям', false],
          ['Спец. условия', false],
        ].map(([l, active], i) => (
          <div key={i} style={{
            padding: '6px 4px', fontSize: 13, color: active ? 'var(--text)' : 'var(--text-2)',
            borderBottom: active ? '2px solid var(--text)' : '2px solid transparent',
            fontWeight: active ? 500 : 400, marginBottom: -11,
          }}>{l}</div>
        ))}
        <div style={{ flex: 1 }}/>
        <div style={{ marginBottom: -11, fontSize: 12, color: 'var(--text-2)' }}>
          <Icon name="filter" size={12}/> Фильтр
        </div>
      </div>

      <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: 13 }}>
        <thead>
          <tr>
            {['Поставщик', 'ИНН', 'Скидка', 'Действует с', 'Действует до', 'Изменено'].map(h => (
              <th key={h} style={{ padding: '11px 12px', textAlign: 'left', fontWeight: 500, fontSize: 11, color: 'var(--text-2)', textTransform: 'uppercase', letterSpacing: 0.6, borderBottom: '1px solid var(--border)' }}>{h}</th>
            ))}
          </tr>
        </thead>
        <tbody>
          {window.SUPPLIERS.map((s, i) => (
            <tr key={s.id} style={{ borderBottom: '1px solid var(--border)' }}>
              <td style={{ padding: '13px 12px' }}>
                <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
                  <Icon name="truck" size={14} stroke="var(--text-2)"/>
                  <span style={{ fontWeight: 500 }}>{s.name}</span>
                </div>
              </td>
              <td className="mono" style={{ padding: '13px 12px', color: 'var(--text-2)', fontSize: 11.5 }}>{s.inn}</td>
              <td style={{ padding: '13px 12px' }}>
                <span style={{
                  display: 'inline-flex', alignItems: 'center', gap: 4,
                  padding: '3px 10px', borderRadius: 6,
                  border: i === 0 ? '1.5px solid var(--accent)' : '1px solid var(--border)',
                  background: 'var(--surface)', fontFamily: 'JetBrains Mono', fontSize: 12, fontWeight: 500,
                  color: i === 0 ? 'var(--accent)' : 'var(--text)',
                }}>
                  {s.discount}<span style={{ color: 'var(--text-2)' }}>%</span>
                </span>
              </td>
              <td style={{ padding: '13px 12px', color: 'var(--text-2)', fontVariantNumeric: 'tabular-nums' }}>01.04.2026</td>
              <td style={{ padding: '13px 12px', color: 'var(--text-3)' }}>— бессрочно</td>
              <td style={{ padding: '13px 12px', color: 'var(--text-2)', fontSize: 12 }}>
                <div style={{ display: 'flex', alignItems: 'center', gap: 6 }}>
                  <div style={{ width: 18, height: 18, borderRadius: 999, background: ['#fde68a','#bae6fd','#bbf7d0','#fecaca','#e9d5ff'][i], color: '#1c1917', display: 'flex', alignItems: 'center', justifyContent: 'center', fontSize: 9, fontWeight: 600 }}>
                    {['СА','КД','ЛИ','ВЕ','СА'][i]}
                  </div>
                  <span>{['Смирнов · 2 ч', 'Карпов · вчера', 'Лебедев · 3 дня', 'Васильева · нед.', 'Смирнов · 12.03'][i]}</span>
                </div>
              </td>
            </tr>
          ))}
          <tr>
            <td colSpan={6} style={{ padding: '11px 12px', color: 'var(--text-3)', fontSize: 13 }}>
              <span style={{ display: 'inline-flex', alignItems: 'center', gap: 7 }}><Icon name="plus" size={13}/> Добавить поставщика</span>
            </td>
          </tr>
        </tbody>
      </table>

      <div style={{ marginTop: 20, padding: '12px 16px', background: 'var(--accent-soft)', border: '1px solid var(--accent-border)', borderRadius: 8, display: 'flex', gap: 12, fontSize: 12.5 }}>
        <div>
          <div style={{ fontWeight: 500, marginBottom: 2 }}>Изменение скидки пересчитает 3 проекта</div>
          <div style={{ color: 'var(--text-2)' }}>P-2026-118, P-2026-114, P-2026-088 — diff будет показан перед применением.</div>
        </div>
      </div>
    </V2Shell>
  );
}

function V2Approval() {
  return (
    <V2Shell active="kp" breadcrumb={['Согласование', 'P-2026-118']} role="comm"
      actions={<>
        <Btn size="sm" kind="ghost">Запросить уточнение</Btn>
        <Btn size="sm" kind="danger" icon="x">Вернуть</Btn>
        <Btn size="sm" kind="primary" icon="check">Согласовать</Btn>
      </>}
    >
      <div style={{ marginBottom: 20 }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: 12, marginBottom: 6 }}>
          <h1 style={{ margin: 0, fontSize: 26, fontWeight: 600, letterSpacing: -0.4 }}>Согласование маржинальности</h1>
          <StatusPill status="В коммерческом блоке" kind="blue"/>
        </div>
        <p style={{ margin: '6px 0 0 0', color: 'var(--text-2)', fontSize: 13.5 }}>
          КП-2026-118-v3 · передал РП Смирнов А. · 24.04 15:19 · ожидает 1 ч 38 мин
        </p>
      </div>

      <div style={{ background: 'var(--surface)', border: '1px solid var(--border)', borderRadius: 10, padding: 22, marginBottom: 22 }}>
        <div style={{ fontSize: 11.5, color: 'var(--text-2)', fontWeight: 500, textTransform: 'uppercase', letterSpacing: 0.5, marginBottom: 16 }}>Расчёт маржинальности</div>
        <div style={{ display: 'grid', gridTemplateColumns: '1fr 28px 1fr 28px 1fr', alignItems: 'center', gap: 14 }}>
          <div style={{ padding: '14px 16px', background: 'var(--surface-2)', borderRadius: 9 }}>
            <div style={{ fontSize: 12, color: 'var(--text-2)' }}>Себестоимость</div>
            <div className="tnum" style={{ fontSize: 22, fontWeight: 600, marginTop: 6 }}>{fmtMoney(window.VARIANTS.fast.total)}</div>
            <div style={{ fontSize: 11, color: 'var(--text-3)', marginTop: 2 }}>закупка по 8 поставщикам</div>
          </div>
          <div style={{ textAlign: 'center', color: 'var(--text-3)', fontSize: 18 }}>→</div>
          <div style={{ padding: '14px 16px', background: 'var(--accent-soft)', borderRadius: 9, border: '1.5px solid var(--accent)' }}>
            <div style={{ fontSize: 12, color: 'var(--accent)', fontWeight: 500 }}>Наценка</div>
            <div className="tnum" style={{ fontSize: 22, fontWeight: 600, marginTop: 6, color: 'var(--accent)' }}>22,5%</div>
            <div style={{ fontSize: 11, color: 'var(--text-2)', marginTop: 2 }}>цель ≥ 18% · норма ≤ 28%</div>
          </div>
          <div style={{ textAlign: 'center', color: 'var(--text-3)', fontSize: 18 }}>→</div>
          <div style={{ padding: '14px 16px', background: 'var(--surface-2)', borderRadius: 9 }}>
            <div style={{ fontSize: 12, color: 'var(--text-2)' }}>Цена клиенту</div>
            <div className="tnum" style={{ fontSize: 22, fontWeight: 600, marginTop: 6 }}>{fmtMoney(window.VARIANTS.fast.total * 1.225)}</div>
            <div style={{ fontSize: 11, color: 'var(--text-3)', marginTop: 2 }}>{fmtMoneyShort(window.VARIANTS.fast.total * 1.225 * 1.2)} с НДС</div>
          </div>
        </div>

        <div style={{ marginTop: 22 }}>
          <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: 11.5, color: 'var(--text-2)', marginBottom: 7 }}>
            <span>15% (мин.)</span>
            <span style={{ color: 'var(--accent)', fontWeight: 500 }}>22,5% — текущее</span>
            <span>30% (потолок)</span>
          </div>
          <div style={{ position: 'relative', height: 8, background: 'var(--surface-2)', borderRadius: 999 }}>
            <div style={{ position: 'absolute', left: '0%', width: '50%', height: '100%', background: 'linear-gradient(90deg, var(--accent-soft), var(--accent))', borderRadius: 999 }}/>
            <div style={{ position: 'absolute', left: '50%', top: -4, width: 16, height: 16, borderRadius: 999, background: 'white', border: '2px solid var(--accent)', transform: 'translateX(-50%)', boxShadow: '0 1px 4px rgba(0,0,0,0.1)' }}/>
            <div style={{ position: 'absolute', left: '80%', top: -4, height: 16, width: 2, background: '#dc2626' }}/>
          </div>
          <div style={{ display: 'flex', justifyContent: 'flex-end', fontSize: 11, color: '#dc2626', marginTop: 6 }}>
            ↑ НМЦ тендера достигается при 27,5%
          </div>
        </div>
      </div>

      <div style={{ display: 'grid', gridTemplateColumns: '1fr 360px', gap: 22 }}>
        <div>
          <div style={{ fontSize: 14, fontWeight: 600, marginBottom: 10 }}>Топ-5 позиций по влиянию на маржу</div>
          <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: 12.5 }}>
            <thead>
              <tr>
                {['Позиция', 'Себестоимость', 'Наценка', 'Цена', 'Маржа', 'Доля'].map(h => (
                  <th key={h} style={{ padding: '8px 12px', textAlign: 'left', fontWeight: 500, fontSize: 11, color: 'var(--text-2)', textTransform: 'uppercase', letterSpacing: 0.5, borderBottom: '1px solid var(--border)' }}>{h}</th>
                ))}
              </tr>
            </thead>
            <tbody>
              {window.VARIANTS.fast.rows.slice(0, 5).map((r, i) => {
                const markup = [22.5, 25, 22.5, 18, 22.5][i];
                const sale = r.total * (1 + markup / 100);
                const marg = sale - r.total;
                const share = (r.total / window.VARIANTS.fast.total) * 100;
                return (
                  <tr key={r.id} style={{ borderBottom: '1px solid var(--border)' }}>
                    <td style={{ padding: '11px 12px' }}>
                      <div style={{ fontWeight: 500, maxWidth: 220, overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>{r.name}</div>
                      <div className="mono" style={{ fontSize: 10.5, color: 'var(--text-3)', marginTop: 2 }}>{r.artikul}</div>
                    </td>
                    <td className="tnum" style={{ padding: '11px 12px' }}>{fmtMoneyShort(r.total)}</td>
                    <td style={{ padding: '11px 12px' }}>
                      <span style={{ padding: '2px 8px', borderRadius: 4, fontSize: 11.5, fontWeight: 500, background: markup === 18 ? '#fef3c7' : 'var(--accent-soft)', color: markup === 18 ? '#92400e' : 'var(--accent)' }}>{markup.toString().replace('.', ',')}%</span>
                    </td>
                    <td className="tnum" style={{ padding: '11px 12px', fontWeight: 500 }}>{fmtMoneyShort(sale)}</td>
                    <td className="tnum" style={{ padding: '11px 12px', color: '#15803d', fontWeight: 500 }}>+{fmtMoneyShort(marg)}</td>
                    <td style={{ padding: '11px 12px' }}>
                      <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
                        <div style={{ width: 70, height: 5, background: 'var(--surface-2)', borderRadius: 999 }}>
                          <div style={{ width: share + '%', height: '100%', background: 'var(--accent)', borderRadius: 999 }}/>
                        </div>
                        <span className="tnum" style={{ fontSize: 11, color: 'var(--text-2)' }}>{share.toFixed(0)}%</span>
                      </div>
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        </div>

        <aside>
          <div style={{ fontSize: 11.5, color: 'var(--text-2)', fontWeight: 500, textTransform: 'uppercase', letterSpacing: 0.5, marginBottom: 12 }}>Маршрут согласования</div>
          {[
            { stage: 'Утверждение РП',          who: 'Смирнов А.',   ts: '24.04 15:18', state: 'done',   note: 'Выбран вариант «Скорость»' },
            { stage: 'Расчёт маржинальности',   who: 'Васильева Е.', ts: '24.04 16:48', state: 'active', note: 'Идёт согласование цены' },
            { stage: 'Утверждение руководства', who: 'Петров К.',    ts: 'ожидается',   state: 'wait',   note: 'если маржа < 20%' },
            { stage: 'Передача в NOC',          who: 'Авто',         ts: 'ожидается',   state: 'wait' },
            { stage: 'Круг согласования',       who: '3 участника',  ts: 'ожидается',   state: 'wait',   note: 'состав уточняется' },
          ].map((step, i, arr) => {
            const c = step.state === 'done' ? '#15803d' : step.state === 'active' ? 'var(--accent)' : '#a8a29e';
            return (
              <div key={i} style={{ display: 'flex', gap: 12, paddingBottom: 14, position: 'relative' }}>
                {i < arr.length - 1 && <div style={{ position: 'absolute', left: 10, top: 22, bottom: 0, width: 1.5, background: 'var(--border)' }}/>}
                <div style={{
                  width: 22, height: 22, borderRadius: 999, flexShrink: 0,
                  background: step.state === 'wait' ? 'var(--surface)' : c,
                  border: step.state === 'wait' ? '1.5px dashed var(--border-strong)' : `1.5px solid ${c}`,
                  display: 'flex', alignItems: 'center', justifyContent: 'center', color: 'white', fontSize: 10, fontWeight: 600,
                }}>
                  {step.state === 'done' ? '✓' : step.state === 'active' ? <span style={{ width: 8, height: 8, background: 'white', borderRadius: 999 }}/> : ''}
                </div>
                <div style={{ flex: 1, minWidth: 0 }}>
                  <div style={{ fontSize: 13, fontWeight: step.state !== 'wait' ? 500 : 400, color: step.state === 'wait' ? 'var(--text-2)' : 'var(--text)' }}>{step.stage}</div>
                  <div style={{ fontSize: 11.5, color: 'var(--text-2)', marginTop: 2 }}>{step.who} · {step.ts}</div>
                  {step.note && <div style={{ fontSize: 11.5, color: 'var(--text-3)', marginTop: 3, fontStyle: 'italic' }}>{step.note}</div>}
                </div>
              </div>
            );
          })}
        </aside>
      </div>
    </V2Shell>
  );
}

Object.assign(window, { V2Upload, V2KPDetail, V2Discounts, V2Approval });
