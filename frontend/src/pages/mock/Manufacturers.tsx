import { useState, type FormEvent } from 'react'
import { useQuery, useMutation, useQueryClient, keepPreviousData } from '@tanstack/react-query'
import { Shell } from '../../components/Shell'
import { PageHead } from '../../components/PageHead'
import { Btn, Icon, StatusPill } from '../../ui/atoms'
import { Dialog } from '../../ui/dialog'
import { Field } from '../../ui/form'
import { inputStyle } from '../../ui/styles'
import { useApi } from '../../api/useApi'
import { useRoles } from '../../auth/useRoles'
import type { Paged } from '../../api/types'
import type { ManufacturerDto } from '../../api/manufacturers'

const PAGE_SIZE = 50

export function Manufacturers() {
  const api = useApi()
  const qc = useQueryClient()
  const { canWrite } = useRoles()

  const [input, setInput] = useState('')
  const [search, setSearch] = useState('')
  const [showCreate, setShowCreate] = useState(false)
  const [name, setName] = useState('')
  const [country, setCountry] = useState('')
  const [err, setErr] = useState<string>()

  const list = useQuery({
    queryKey: ['manufacturers', search],
    queryFn: () => api.get<Paged<ManufacturerDto>>(`/api/manufacturers?pageSize=${PAGE_SIZE}` + (search ? `&search=${encodeURIComponent(search)}` : '')),
    placeholderData: keepPreviousData,
  })

  const totalProducts = list.data?.items.reduce((a, m) => a + m.productsCount, 0) ?? 0

  const create = useMutation({
    mutationFn: () => api.post<ManufacturerDto>('/api/manufacturers', { name: name.trim(), country: country.trim() || null }),
    onSuccess: () => { setName(''); setCountry(''); setShowCreate(false); qc.invalidateQueries({ queryKey: ['manufacturers'] }) },
    onError: (e) => setErr((e as Error).message),
  })
  const toggleActive = useMutation({
    mutationFn: (m: ManufacturerDto) => api.put<void>(`/api/manufacturers/${m.id}`, { name: m.name, country: m.country ?? null, isActive: !m.isActive }),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['manufacturers'] }),
    onError: (e) => setErr((e as Error).message),
  })
  const del = useMutation({
    mutationFn: (id: string) => api.del<void>(`/api/manufacturers/${id}`),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['manufacturers'] }),
    onError: (e) => setErr((e as Error).message),
  })

  function submit(e: FormEvent) { e.preventDefault(); setSearch(input.trim()) }

  return (
    <Shell breadcrumb={['Справочники', 'Производители']} actions={canWrite &&
      <Btn size="sm" kind="primary" icon="plus" onClick={() => setShowCreate(v => !v)}>Новый производитель</Btn>}>
      <PageHead kicker="справочник · вендоры" title="Производители"
        subtitle="Название, страна и доля в каталоге." />

      <Dialog
        open={showCreate}
        onClose={() => setShowCreate(false)}
        title="Новый производитель"
        width={560}
        footer={<>
          <Btn kind="ghost" size="sm" onClick={() => setShowCreate(false)}>Отмена</Btn>
          <Btn kind="primary" size="sm" disabled={!name.trim() || create.isPending} onClick={() => { setErr(undefined); create.mutate() }}>Создать</Btn>
        </>}
      >
        <div className="form-cols" style={{ gridTemplateColumns: '2fr 1fr' }}>
          <Field label="Название"><input value={name} onChange={e => setName(e.target.value)} placeholder="HPE, Dell, Cisco…" style={inputStyle}/></Field>
          <Field label="Страна"><input value={country} onChange={e => setCountry(e.target.value)} placeholder="США" style={inputStyle}/></Field>
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
          <tr>{['Производитель', 'Страна', 'Позиций', 'Доля в каталоге', 'Статус', ''].map((h, i) => (
            <th key={h} style={{ padding: '10px 12px', textAlign: i === 2 || i === 3 ? 'right' : 'left', fontWeight: 500, fontSize: 11, color: 'var(--text-2)', textTransform: 'uppercase', letterSpacing: 0.6, borderBottom: '1px solid var(--border)' }}>{h}</th>
          ))}</tr>
        </thead>
        <tbody>
          {list.data?.items.map(m => {
            const share = totalProducts > 0 ? (m.productsCount / totalProducts) * 100 : 0
            return (
              <tr key={m.id} style={{ borderBottom: '1px solid var(--border)' }}>
                <td style={{ padding: '11px 12px' }}>
                  <span style={{ display: 'inline-flex', alignItems: 'center', gap: 9 }}>
                    <span style={{ width: 24, height: 24, borderRadius: 6, background: 'var(--surface-2)', display: 'inline-flex', alignItems: 'center', justifyContent: 'center' }}><Icon name="layers" size={14} stroke="var(--text-2)"/></span>
                    <span style={{ fontWeight: 500 }}>{m.name}</span>
                  </span>
                </td>
                <td style={{ padding: '11px 12px', color: 'var(--text-2)' }}>{m.country ?? '—'}</td>
                <td className="tnum" style={{ padding: '11px 12px', textAlign: 'right' }}>{m.productsCount}</td>
                <td style={{ padding: '11px 12px' }}>
                  <div style={{ display: 'flex', alignItems: 'center', gap: 8, justifyContent: 'flex-end' }}>
                    <div style={{ width: 100, height: 6, background: 'var(--surface-2)', borderRadius: 999, overflow: 'hidden' }}>
                      <div style={{ width: `${Math.min(100, share)}%`, height: '100%', background: 'var(--accent)' }} />
                    </div>
                    <span className="tnum" style={{ fontSize: 12, color: 'var(--text-2)', width: 40, textAlign: 'right' }}>{share.toFixed(0)}%</span>
                  </div>
                </td>
                <td style={{ padding: '11px 12px' }}>
                  <span style={{ cursor: canWrite ? 'pointer' : 'default' }} onClick={() => canWrite && toggleActive.mutate(m)}>
                    <StatusPill status={m.isActive ? 'Активен' : 'Снят'} kind={m.isActive ? 'green' : 'gray'} />
                  </span>
                </td>
                <td style={{ padding: '11px 12px', textAlign: 'right' }}>
                  {canWrite && <button onClick={() => del.mutate(m.id)} title="Удалить" style={{ background: 'transparent', border: 'none', color: 'var(--text-3)', cursor: 'pointer', padding: 4 }}><Icon name="x" size={14}/></button>}
                </td>
              </tr>
            )
          })}
          {list.isLoading && <tr><td colSpan={6} style={{ padding: 16, color: 'var(--text-3)' }}>Загрузка…</td></tr>}
          {list.data && list.data.items.length === 0 && <tr><td colSpan={6} style={{ padding: 16, color: 'var(--text-3)' }}>Производители не найдены</td></tr>}
        </tbody>
      </table>
      </div>

      <div style={{ marginTop: 14, fontSize: 12.5, color: 'var(--text-2)' }}>
        Всего: <b style={{ color: 'var(--text)' }}>{list.data?.total ?? 0}</b>
      </div>
    </Shell>
  )
}


