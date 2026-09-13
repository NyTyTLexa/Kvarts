import { useState, type CSSProperties } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { Shell } from '../components/Shell'
import { PageHead } from '../components/PageHead'
import { Btn, Icon, StatusPill } from '../ui/atoms'
import { Dialog } from '../ui/dialog'
import { Field } from '../ui/form'
import { inputStyle } from '../ui/styles'
import { SuggestBox, filterOptions } from '../ui/suggest'
import { useApi } from '../api/useApi'
import { useRoles } from '../auth/useRoles'
import { fmt } from '../data/constants'
import type { Paged, VendorDto, DiscountDto } from '../api/types'

type Target = 'Vendor' | 'Manufacturer'
const emptyForm = { target: 'Vendor' as Target, vendorId: '', manufacturer: '', percent: 10, validFrom: '', validTo: '' }

function fmtDate(iso?: string) { return iso ? new Date(iso).toLocaleDateString('ru-RU') : null }
function toIso(d: string) { return d ? `${d}T00:00:00Z` : null }

export function Discounts() {
  const api = useApi()
  const qc = useQueryClient()
  const { canWrite } = useRoles()

  const [show, setShow] = useState(false)
  const [editId, setEditId] = useState<string | null>(null)
  const [f, setF] = useState({ ...emptyForm })
  const [err, setErr] = useState<string>()

  const list = useQuery({
    queryKey: ['discounts'],
    queryFn: () => api.get<DiscountDto[]>('/api/discounts'),
  })
  // Список поставщиков для выпадающего выбора цели-поставщика.
  const vendors = useQuery({
    queryKey: ['vendors-all'],
    queryFn: () => api.get<Paged<VendorDto>>('/api/vendors?page=1&pageSize=500'),
  })

  function reset() { setShow(false); setEditId(null); setF({ ...emptyForm }); setErr(undefined) }

  const save = useMutation({
    mutationFn: async () => {
      const body = { percent: f.percent, validFromUtc: toIso(f.validFrom), validToUtc: toIso(f.validTo) }
      if (editId) { await api.put<void>(`/api/discounts/${editId}`, body); return }
      await api.post<DiscountDto>('/api/discounts', {
        vendorId: f.target === 'Vendor' ? f.vendorId : null,
        manufacturer: f.target === 'Manufacturer' ? f.manufacturer.trim() : null,
        ...body,
      })
    },
    onSuccess: () => { reset(); qc.invalidateQueries({ queryKey: ['discounts'] }) },
    onError: (e) => setErr((e as Error).message),
  })
  const del = useMutation({
    mutationFn: (id: string) => api.del<void>(`/api/discounts/${id}`),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['discounts'] }),
    onError: (e) => setErr((e as Error).message),
  })

  function startEdit(d: DiscountDto) {
    setEditId(d.id)
    setF({
      target: d.target, vendorId: d.vendorId ?? '', manufacturer: d.manufacturer ?? '',
      percent: d.percent, validFrom: d.validFromUtc?.slice(0, 10) ?? '', validTo: d.validToUtc?.slice(0, 10) ?? '',
    })
    setShow(true); setErr(undefined)
  }

  const canSubmit = f.percent >= 0 && f.percent <= 100 &&
    (editId != null || (f.target === 'Vendor' ? !!f.vendorId : !!f.manufacturer.trim()))
  const targetLabel = editId
    ? (f.target === 'Vendor' ? vendors.data?.items.find(v => v.id === f.vendorId)?.name ?? 'поставщик' : f.manufacturer)
    : null

  return (
    <Shell breadcrumb={['Справочники', 'Скидки']} actions={canWrite &&
      <Btn size="sm" kind="primary" icon="plus" onClick={() => (show ? reset() : setShow(true))}>Новая скидка</Btn>}>
      <PageHead kicker="справочник · к цене КП" title="Скидки"
        subtitle="По поставщику или производителю. В КП сначала скидка поставщика, иначе производителя." />

      <Dialog
        open={show}
        onClose={reset}
        title={editId ? 'Редактирование скидки' : 'Новая скидка'}
        width={680}
        footer={<>
          <Btn kind="ghost" size="sm" onClick={reset}>Отмена</Btn>
          <Btn kind="primary" size="sm" disabled={!canSubmit || save.isPending} onClick={() => { setErr(undefined); save.mutate() }}>
            {editId ? 'Сохранить' : 'Создать'}
          </Btn>
        </>}
      >
        {editId ? (
          <div style={{ fontSize: 12.5, color: 'var(--text-2)', marginBottom: 12 }}>
            Цель: <b style={{ color: 'var(--text)' }}>{f.target === 'Vendor' ? 'Поставщик' : 'Производитель'}</b> · {targetLabel}
            <span style={{ color: 'var(--text-3)' }}> (цель менять нельзя — удалите и создайте заново)</span>
          </div>
        ) : (
          <>
            <div style={{ display: 'flex', gap: 6, marginBottom: 12 }}>
              {(['Vendor', 'Manufacturer'] as Target[]).map(t => (
                <button type="button" key={t} onClick={() => setF(s => ({ ...s, target: t }))} style={{
                  padding: '6px 12px', borderRadius: 7, fontSize: 12.5, cursor: 'pointer',
                  border: '1px solid ' + (f.target === t ? 'var(--accent)' : 'var(--border-strong)'),
                  background: f.target === t ? 'var(--accent-soft)' : 'var(--surface)',
                  color: f.target === t ? 'var(--accent)' : 'var(--text-2)', fontWeight: f.target === t ? 600 : 400,
                }}>{t === 'Vendor' ? 'По поставщику' : 'По производителю'}</button>
              ))}
            </div>
            <div style={{ marginBottom: 12 }}>
              {f.target === 'Vendor' ? (
                <Field label="Поставщик">
                  <select value={f.vendorId} onChange={e => setF(s => ({ ...s, vendorId: e.target.value }))} style={inputStyle}>
                    <option value="">— выберите поставщика —</option>
                    {vendors.data?.items.map(v => <option key={v.id} value={v.id}>{v.name}</option>)}
                  </select>
                </Field>
              ) : (
                <Field label="Производитель">
                  <SuggestBox boxed value={f.manufacturer} onChange={v => setF(s => ({ ...s, manufacturer: v }))}
                    items={filterOptions([...(list.data ?? [])].map(d => d.manufacturer).filter((x): x is string => !!x).filter((v, i, a) => a.indexOf(v) === i), f.manufacturer)}
                    onPick={v => setF(s => ({ ...s, manufacturer: v }))}
                    placeholder="HPE, Dell, Cisco…"
                    render={s => <span>{s}</span>} />
                </Field>
              )}
            </div>
          </>
        )}

        <div className="form-cols" style={{ gridTemplateColumns: '1fr 1fr 1fr', marginBottom: 12 }}>
          <Field label="Скидка, %"><input type="number" min={0} max={100} step={0.5} value={f.percent}
            onChange={e => setF(s => ({ ...s, percent: Math.min(100, Math.max(0, +e.target.value || 0)) }))} style={inputStyle} /></Field>
          <Field label="Действует с"><input type="date" value={f.validFrom} onChange={e => setF(s => ({ ...s, validFrom: e.target.value }))} style={inputStyle} /></Field>
          <Field label="по"><input type="date" value={f.validTo} onChange={e => setF(s => ({ ...s, validTo: e.target.value }))} style={inputStyle} /></Field>
        </div>
        <div style={{ fontSize: 11.5, color: 'var(--text-3)' }}>Даты необязательны: пусто = бессрочно.</div>
      </Dialog>

      {err && <div style={{ color: 'var(--err)', background: 'var(--err-bg)', padding: '8px 12px', borderRadius: 7, marginBottom: 12, fontSize: 13 }}>{err}</div>}

      <div className="doc-list">
      <table>
        <thead>
          <tr>{['Цель', 'Тип', 'Скидка', 'Срок действия', 'Статус', ''].map((h, i) => (
            <th key={h} style={{ padding: '10px 12px', textAlign: i === 2 ? 'right' : 'left', fontWeight: 500, fontSize: 11, color: 'var(--text-2)', textTransform: 'uppercase', letterSpacing: 0.6, borderBottom: '1px solid var(--border)' }}>{h}</th>
          ))}</tr>
        </thead>
        <tbody>
          {list.data?.map(d => {
            const from = fmtDate(d.validFromUtc), to = fmtDate(d.validToUtc)
            const period = from || to ? `${from ?? '…'} — ${to ?? '…'}` : 'бессрочно'
            return (
              <tr key={d.id} style={{ borderBottom: '1px solid var(--border)' }}>
                <td style={{ padding: '11px 12px' }}>
                  <span style={{ display: 'inline-flex', alignItems: 'center', gap: 9 }}>
                    <span style={{ width: 24, height: 24, borderRadius: 6, background: 'var(--surface-2)', display: 'inline-flex', alignItems: 'center', justifyContent: 'center' }}>
                      <Icon name={d.target === 'Vendor' ? 'truck' : 'layers'} size={14} stroke="var(--text-2)" /></span>
                    <span style={{ fontWeight: 500 }}>{d.target === 'Vendor' ? d.vendorName : d.manufacturer}</span>
                  </span>
                </td>
                <td style={{ padding: '11px 12px', color: 'var(--text-2)', fontSize: 12.5 }}>{d.target === 'Vendor' ? 'Поставщик' : 'Производитель'}</td>
                <td className="tnum" style={{ padding: '11px 12px', textAlign: 'right', fontWeight: 600, color: 'var(--ok)' }}>−{fmt(d.percent)}%</td>
                <td style={{ padding: '11px 12px', color: 'var(--text-2)', fontSize: 12.5 }}>{period}</td>
                <td style={{ padding: '11px 12px' }}><StatusPill status={d.isActive ? 'Активна' : 'Неактивна'} kind={d.isActive ? 'green' : 'gray'} /></td>
                <td style={{ padding: '11px 12px', textAlign: 'right', whiteSpace: 'nowrap' }}>
                  {canWrite && <>
                    <button onClick={() => startEdit(d)} title="Изменить" style={iconBtn}><Icon name="edit" size={14} /></button>
                    <button onClick={() => del.mutate(d.id)} title="Удалить" style={iconBtn}><Icon name="x" size={14} /></button>
                  </>}
                </td>
              </tr>
            )
          })}
          {list.isLoading && <tr><td colSpan={6} style={{ padding: 16, color: 'var(--text-3)' }}>Загрузка…</td></tr>}
          {list.data && list.data.length === 0 && <tr><td colSpan={6} style={{ padding: 16, color: 'var(--text-3)' }}>Скидок пока нет</td></tr>}
        </tbody>
      </table>
      </div>
    </Shell>
  )
}

const iconBtn: CSSProperties = { background: 'transparent', border: 'none', color: 'var(--text-3)', cursor: 'pointer', padding: 4 }
