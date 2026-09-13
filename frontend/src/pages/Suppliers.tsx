import { useState, type FormEvent } from 'react'
import { useQuery, useMutation, useQueryClient, keepPreviousData } from '@tanstack/react-query'
import { Shell } from '../components/Shell'
import { PageHead } from '../components/PageHead'
import { Btn, Icon } from '../ui/atoms'
import { Dialog } from '../ui/dialog'
import { Field } from '../ui/form'
import { inputStyle } from '../ui/styles'
import { useApi } from '../api/useApi'
import { useRoles } from '../auth/useRoles'
import type { Paged, VendorDto } from '../api/types'

const PAGE_SIZE = 20

export function Suppliers() {
  const api = useApi()
  const qc = useQueryClient()
  const { canWrite } = useRoles()

  const [input, setInput] = useState('')
  const [search, setSearch] = useState('')
  const [page, setPage] = useState(1)
  const [showCreate, setShowCreate] = useState(false)
  const [name, setName] = useState('')
  const [inn, setInn] = useState('')
  const [lead, setLead] = useState(14)
  const [err, setErr] = useState<string>()

  const list = useQuery({
    queryKey: ['vendors', page, search],
    queryFn: () => api.get<Paged<VendorDto>>(`/api/vendors?page=${page}&pageSize=${PAGE_SIZE}` + (search ? `&search=${encodeURIComponent(search)}` : '')),
    placeholderData: keepPreviousData,
  })

  const create = useMutation({
    mutationFn: () => api.post<VendorDto>('/api/vendors', { name: name.trim(), inn: inn.trim() || null, defaultLeadTimeDays: lead }),
    onSuccess: () => { setName(''); setInn(''); setLead(14); setShowCreate(false); qc.invalidateQueries({ queryKey: ['vendors'] }) },
    onError: (e) => setErr((e as Error).message),
  })
  const del = useMutation({
    mutationFn: (id: string) => api.del<void>(`/api/vendors/${id}`),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['vendors'] }),
    onError: (e) => setErr((e as Error).message),
  })

  const total = list.data?.total ?? 0
  const pages = Math.max(1, Math.ceil(total / PAGE_SIZE))

  function submit(e: FormEvent) { e.preventDefault(); setPage(1); setSearch(input.trim()) }

  return (
    <Shell breadcrumb={['Справочники', 'Поставщики']} actions={canWrite &&
      <Btn size="sm" kind="primary" icon="plus" onClick={() => setShowCreate(v => !v)}>Новый поставщик</Btn>}>
      <PageHead kicker="справочник · контрагенты" title="Поставщики"
        subtitle="ИНН и срок поставки по умолчанию. Из прайса сюда же сажается оффер." />

      <Dialog
        open={showCreate}
        onClose={() => setShowCreate(false)}
        title="Новый поставщик"
        width={620}
        footer={<>
          <Btn kind="ghost" size="sm" onClick={() => setShowCreate(false)}>Отмена</Btn>
          <Btn kind="primary" size="sm" disabled={!name.trim() || create.isPending} onClick={() => { setErr(undefined); create.mutate() }}>Создать</Btn>
        </>}
      >
        <div className="form-cols" style={{ gridTemplateColumns: '2fr 1fr 1fr' }}>
          <Field label="Название"><input value={name} onChange={e => setName(e.target.value)} placeholder="ООО «…»" style={inputStyle}/></Field>
          <Field label="ИНН"><input value={inn} onChange={e => setInn(e.target.value)} placeholder="7700000000" style={inputStyle}/></Field>
          <Field label="Срок, дн"><input type="number" min={1} value={lead} onChange={e => setLead(Math.max(1, +e.target.value || 1))} style={inputStyle}/></Field>
        </div>
      </Dialog>

      {err && <div style={{ color: 'var(--err)', background: 'var(--err-bg)', padding: '8px 12px', borderRadius: 7, marginBottom: 12, fontSize: 13 }}>{err}</div>}

      <form onSubmit={submit} style={{ display: 'flex', gap: 8, marginBottom: 14, maxWidth: 480 }}>
        <div style={{ flex: 1, display: 'flex', alignItems: 'center', gap: 8, background: 'var(--surface-2)', borderRadius: 7, padding: '7px 12px' }}>
          <Icon name="search" size={15} stroke="var(--text-3)"/>
          <input value={input} onChange={e => setInput(e.target.value)} placeholder="Поиск по названию…" style={{ flex: 1, border: 'none', background: 'transparent', outline: 'none', fontSize: 13.5 }}/>
        </div>
        <Btn type="submit" size="md">Найти</Btn>
      </form>

      <div className="doc-list">
      <table>
        <thead>
          <tr>{['Поставщик', 'ИНН', 'Срок по умолч.', ''].map((h, i) => (
            <th key={h} style={{ padding: '10px 12px', textAlign: i === 2 ? 'right' : 'left', fontWeight: 500, fontSize: 11, color: 'var(--text-2)', textTransform: 'uppercase', letterSpacing: 0.6, borderBottom: '1px solid var(--border)' }}>{h}</th>
          ))}</tr>
        </thead>
        <tbody>
          {list.data?.items.map(v => (
            <tr key={v.id} style={{ borderBottom: '1px solid var(--border)' }}>
              <td style={{ padding: '11px 12px' }}>
                <span style={{ display: 'inline-flex', alignItems: 'center', gap: 9 }}>
                  <span style={{ width: 24, height: 24, borderRadius: 6, background: 'var(--surface-2)', display: 'inline-flex', alignItems: 'center', justifyContent: 'center' }}><Icon name="truck" size={14} stroke="var(--text-2)"/></span>
                  <span style={{ fontWeight: 500 }}>{v.name}</span>
                </span>
              </td>
              <td className="mono" style={{ padding: '11px 12px', fontSize: 11.5, color: 'var(--text-2)' }}>{v.inn ?? '—'}</td>
              <td className="tnum" style={{ padding: '11px 12px', textAlign: 'right', color: 'var(--text-2)' }}>{v.defaultLeadTimeDays} дн</td>
              <td style={{ padding: '11px 12px', textAlign: 'right' }}>
                {canWrite && <button onClick={() => del.mutate(v.id)} title="Удалить" style={{ background: 'transparent', border: 'none', color: 'var(--text-3)', cursor: 'pointer', padding: 4 }}><Icon name="x" size={14}/></button>}
              </td>
            </tr>
          ))}
          {list.isLoading && <tr><td colSpan={4} style={{ padding: 16, color: 'var(--text-3)' }}>Загрузка…</td></tr>}
          {list.data && list.data.items.length === 0 && <tr><td colSpan={4} style={{ padding: 16, color: 'var(--text-3)' }}>Поставщики не найдены</td></tr>}
        </tbody>
      </table>
      </div>

      <div style={{ display: 'flex', alignItems: 'center', gap: 12, marginTop: 14, fontSize: 12.5, color: 'var(--text-2)' }}>
        <span>Всего: <b style={{ color: 'var(--text)' }}>{total.toLocaleString('ru-RU')}</b></span>
        <div style={{ flex: 1 }}/>
        <Btn size="sm" disabled={page <= 1} onClick={() => setPage(p => p - 1)}>← Назад</Btn>
        <span className="tnum">Стр. {page} / {pages}</span>
        <Btn size="sm" disabled={page >= pages} onClick={() => setPage(p => p + 1)}>Вперёд →</Btn>
      </div>
    </Shell>
  )
}


