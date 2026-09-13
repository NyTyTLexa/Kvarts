import { useState, useRef, type ReactNode } from 'react'
import { useNavigate } from 'react-router-dom'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { Shell } from '../components/Shell'
import { PageHead } from '../components/PageHead'
import { Btn, Icon } from '../ui/atoms'
import { TableRowsSkeleton } from '../ui/feedback'
import { Dialog } from '../ui/dialog'
import { Field } from '../ui/form'
import { inputStyle } from '../ui/styles'
import { SuggestBox, filterOptions } from '../ui/suggest'
import { useApi } from '../api/useApi'
import { useRoles } from '../auth/useRoles'
import type { SpecificationDto, SpecImportResult } from '../api/types'

export function SpecificationsList() {
  const api = useApi()
  const qc = useQueryClient()
  const nav = useNavigate()
  const { canWrite } = useRoles()
  const fileRef = useRef<HTMLInputElement>(null)

  const [showCreate, setShowCreate] = useState(false)
  const [title, setTitle] = useState('')
  const [customer, setCustomer] = useState('')
  const [err, setErr] = useState<string>()

  const list = useQuery({
    queryKey: ['specifications'],
    queryFn: () => api.get<SpecificationDto[]>('/api/specifications'),
  })

  const create = useMutation({
    mutationFn: () => api.post<SpecificationDto>('/api/specifications', { title: title.trim(), customer: customer.trim() || null }),
    onSuccess: (s) => { qc.invalidateQueries({ queryKey: ['specifications'] }); nav(`/specifications/${s.id}`) },
    onError: (e) => setErr((e as Error).message),
  })

  const importXlsx = useMutation({
    mutationFn: (file: File) => {
      const fd = new FormData(); fd.append('file', file)
      const t = title.trim() || file.name
      const q = `?title=${encodeURIComponent(t)}${customer.trim() ? `&customer=${encodeURIComponent(customer.trim())}` : ''}`
      return api.post<SpecImportResult>('/api/specifications/import' + q, fd)
    },
    onSuccess: (r) => { qc.invalidateQueries({ queryKey: ['specifications'] }); nav(`/specifications/${r.specificationId}`) },
    onError: (e) => setErr((e as Error).message),
  })

  return (
    <Shell breadcrumb={['Заявки']} actions={canWrite && <>
      <Btn size="sm" kind="default" icon="upload" onClick={() => fileRef.current?.click()}>Импорт Excel</Btn>
      <Btn size="sm" kind="primary" icon="plus" onClick={() => setShowCreate(v => !v)}>Новая спецификация</Btn>
      <input ref={fileRef} type="file" accept=".xlsx" hidden onChange={e => {
        const f = e.target.files?.[0]; if (f) importXlsx.mutate(f); e.target.value = ''
      }}/>
    </>}>
      <PageHead icon="document" kicker="перечень" title="Заявки"
        subtitle="Спецификация заказчика → сопоставление с каталогом → четыре варианта КП." />

      <Dialog
        open={showCreate}
        onClose={() => setShowCreate(false)}
        title="Новая спецификация"
        width={560}
        footer={<>
          <Btn kind="ghost" size="sm" onClick={() => setShowCreate(false)}>Отмена</Btn>
          <Btn kind="primary" size="sm" disabled={!title.trim() || create.isPending} onClick={() => { setErr(undefined); create.mutate() }}>Создать</Btn>
        </>}
      >
        <div className="form-cols" style={{ gridTemplateColumns: '1fr 1fr', marginBottom: 12 }}>
          <Field label="Название"><input value={title} onChange={e => setTitle(e.target.value)} placeholder="Заявка на оснащение…" style={inputStyle}/></Field>
          <Field label="Заказчик">
            <SuggestBox boxed value={customer} onChange={setCustomer}
              items={filterOptions([...(list.data ?? [])].map(s => s.customer).filter((x): x is string => !!x).filter((v, i, a) => a.indexOf(v) === i), customer)}
              onPick={setCustomer} placeholder="АО «…» — или выберите из уже бывших"
              render={s => <span>{s}</span>} />
          </Field>
        </div>
        <div style={{ fontSize: 12, color: 'var(--text-3)' }}>Название из формы используется и для импорта Excel.</div>
      </Dialog>

      {importXlsx.isPending && <Info>Импорт файла…</Info>}
      {err && <div style={{ color: 'var(--err)', background: 'var(--err-bg)', padding: '8px 12px', borderRadius: 7, marginBottom: 12, fontSize: 13 }}>{err}</div>}

      <div className="doc-list">
      <table>
        <thead>
          <tr>{['Название', 'Заказчик', 'Позиций', 'Сопоставлено', 'Создана'].map((h, i) => (
            <th key={h} style={{ padding: '10px 12px', textAlign: i >= 2 && i <= 3 ? 'right' : 'left', fontWeight: 500, fontSize: 11, color: 'var(--text-2)', textTransform: 'uppercase', letterSpacing: 0.6, borderBottom: '1px solid var(--border)' }}>{h}</th>
          ))}</tr>
        </thead>
        <tbody>
          {list.data?.map(s => (
            <tr key={s.id} onClick={() => nav(`/specifications/${s.id}`)} style={{ borderBottom: '1px solid var(--border)', cursor: 'pointer' }}>
              <td style={{ padding: '12px' }}>
                <div style={{ display: 'flex', alignItems: 'center', gap: 10 }}>
                  <Icon name="document" size={15} stroke="var(--text-2)"/>
                  <span style={{ fontWeight: 500 }}>{s.title}</span>
                </div>
              </td>
              <td style={{ padding: '12px', color: 'var(--text-2)' }}>{s.customer ?? '—'}</td>
              <td className="tnum" style={{ padding: '12px', textAlign: 'right' }}>{s.itemsCount}</td>
              <td className="tnum" style={{ padding: '12px', textAlign: 'right', color: s.matchedCount === s.itemsCount ? 'var(--ok)' : 'var(--warn)' }}>
                {s.matchedCount} / {s.itemsCount}
              </td>
              <td style={{ padding: '12px', textAlign: 'left', color: 'var(--text-2)' }}>{new Date(s.createdAtUtc).toLocaleDateString('ru-RU')}</td>
            </tr>
          ))}
          {list.isLoading && <TableRowsSkeleton rows={5} cols={5} />}
          {list.data && list.data.length === 0 && <tr><td colSpan={5} style={{ padding: 16, color: 'var(--text-3)' }}>Пока нет спецификаций. Создайте новую или импортируйте Excel.</td></tr>}
        </tbody>
      </table>
      </div>
    </Shell>
  )
}

function Info({ children }: { children: ReactNode }) {
  return <div style={{ fontSize: 13, color: 'var(--text-2)', background: 'var(--surface-2)', padding: '8px 12px', borderRadius: 7, marginBottom: 12 }}>{children}</div>
}
