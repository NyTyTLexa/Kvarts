import { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { Shell } from '../components/Shell'
import { PageHead } from '../components/PageHead'
import { StatusPill, Btn } from '../ui/atoms'
import { Dialog } from '../ui/dialog'
import { Field } from '../ui/form'
import { inputStyle } from '../ui/styles'
import { SuggestBox, filterOptions } from '../ui/suggest'
import { useApi } from '../api/useApi'
import { useRoles } from '../auth/useRoles'
import { fmtMoneyShort } from '../data/constants'
import type { SpecificationDto } from '../api/types'
import type { ProjectDto } from '../api/projects'

export function Dashboard() {
  const nav = useNavigate()
  const api = useApi()
  const qc = useQueryClient()
  const { canWrite } = useRoles()

  const [showCreate, setShowCreate] = useState(false)
  const [name, setName] = useState('')
  const [rp, setRp] = useState('')
  const [specId, setSpecId] = useState('')
  const [err, setErr] = useState<string>()

  const projects = useQuery({ queryKey: ['projects'], queryFn: () => api.get<ProjectDto[]>('/api/projects') })
  const specs = useQuery({ queryKey: ['specs-all'], queryFn: () => api.get<SpecificationDto[]>('/api/specifications') })

  const create = useMutation({
    mutationFn: () => api.post<ProjectDto>('/api/projects', {
      name: name.trim(), rp: rp.trim() || null, dueDateUtc: null, specificationId: specId || null,
    }),
    onSuccess: (p) => { setName(''); setRp(''); setSpecId(''); setShowCreate(false); qc.invalidateQueries({ queryKey: ['projects'] }); nav(`/projects/${p.id}`) },
    onError: (e) => setErr((e as Error).message),
  })

  const list = projects.data ?? []
  const withValue = list.filter(p => p.value != null)
  const total = withValue.reduce((a, p) => a + (p.value ?? 0), 0)
  const withMargin = list.filter(p => p.marginPercent != null)
  const avgMargin = withMargin.length > 0 ? withMargin.reduce((a, p) => a + (p.marginPercent ?? 0), 0) / withMargin.length : null

  return (
    <Shell breadcrumb={['Проекты']} actions={
      canWrite && <Btn size="sm" kind="primary" icon="plus" onClick={() => setShowCreate(v => !v)}>Новый проект</Btn>
    }>
      <PageHead icon="folder" kicker="тендеры" title="Проекты"
        subtitle="Строка журнала открывает заявку, четыре листа КП и маршрут согласования." />

      <Dialog
        open={showCreate}
        onClose={() => setShowCreate(false)}
        title="Новый проект"
        width={640}
        footer={<>
          <Btn kind="ghost" size="sm" onClick={() => setShowCreate(false)}>Отмена</Btn>
          <Btn kind="primary" size="sm" disabled={!name.trim() || create.isPending} onClick={() => { setErr(undefined); create.mutate() }}>Создать</Btn>
        </>}
      >
        <div className="form-cols" style={{ gridTemplateColumns: '2fr 1fr', marginBottom: 10 }}>
          <Field label="Название проекта"><input value={name} onChange={e => setName(e.target.value)} placeholder="СКС и сетевое ядро ТЦ «Орбита»" style={inputStyle}/></Field>
          <Field label="РП">
            <SuggestBox boxed value={rp} onChange={setRp}
              items={filterOptions(list.map(p => p.rp).filter((x): x is string => !!x).filter((v, i, a) => a.indexOf(v) === i), rp)}
              onPick={setRp} placeholder="Смирнов А."
              render={s => <span>{s}</span>} />
          </Field>
        </div>
        <Field label="Спецификация (необязательно)">
          <select value={specId} onChange={e => setSpecId(e.target.value)} style={inputStyle}>
            <option value="">— без привязки —</option>
            {specs.data?.map(s => <option key={s.id} value={s.id}>{s.title}{s.customer ? ` · ${s.customer}` : ''}</option>)}
          </select>
        </Field>
      </Dialog>

      {(err || projects.isError) && <div style={{ color: 'var(--err)', background: 'var(--err-bg)', padding: '8px 12px', marginBottom: 14, fontSize: 13 }}>
        {err ?? ((projects.error as Error)?.message || 'Не удалось загрузить проекты.')}
      </div>}

      <div className="stat-grid-4" style={{ marginBottom: 16, gridTemplateColumns: '1.4fr 1fr 1fr' }}>
        {[
          { label: 'В работе', value: fmtMoneyShort(total) },
          { label: 'Маржа', value: avgMargin == null ? '—' : avgMargin.toFixed(1).replace('.', ',') + '%' },
          { label: 'Проектов', value: String(list.length) },
        ].map((k, i) => (
          <div key={k.label} className="panel" style={{ padding: '12px 14px', background: i === 0 ? 'var(--accent-soft)' : 'var(--surface)' }}>
            <div className="field-label">{k.label}</div>
            <div className="tnum" style={{ fontSize: 20, fontWeight: 700, marginTop: 4 }}>{k.value}</div>
          </div>
        ))}
      </div>

      {projects.isLoading && <div className="doc-list">{[1, 2, 3, 4].map(i => <div key={i} className="amicro-skel" style={{ height: 44, margin: 8, borderRadius: 4 }} />)}</div>}

      {projects.data && list.length === 0 && (
        <div className="panel" style={{ padding: '36px 24px', textAlign: 'center' }}>
          <div style={{ fontSize: 16, fontWeight: 700, marginBottom: 8 }}>Журнал проектов пуст</div>
          <p style={{ color: 'var(--text-2)', margin: '0 0 16px' }}>Создайте проект, чтобы открыть заявку и четыре варианта КП.</p>
          {canWrite && <Btn kind="primary" icon="plus" onClick={() => setShowCreate(true)}>Новый проект</Btn>}
        </div>
      )}

      {list.length > 0 && (
        <div className="doc-list">
          <table>
            <thead>
              <tr>
                {['Дата', 'Проект', 'Статус', 'РП', 'Заявка', 'Поз.', 'Сумма', 'Маржа'].map((h, i) => (
                  <th key={h} style={{ textAlign: i >= 5 ? 'right' : 'left' }}>{h}</th>
                ))}
              </tr>
            </thead>
            <tbody>
              {list.map(p => (
                <tr key={p.id} onClick={() => nav(`/projects/${p.id}`)} style={{ cursor: 'pointer' }}>
                  <td className="tnum" style={{ color: 'var(--text-2)', whiteSpace: 'nowrap' }}>
                    {new Date(p.createdAtUtc).toLocaleDateString('ru-RU')}
                  </td>
                  <td>
                    <div style={{ fontWeight: 600 }}>{p.name}</div>
                    <div className="mono" style={{ fontSize: 11, color: 'var(--text-3)', marginTop: 2 }}>{p.id.slice(0, 8)}</div>
                  </td>
                  <td><StatusPill status={p.status} /></td>
                  <td style={{ color: 'var(--text-2)' }}>{p.rp || '—'}</td>
                  <td style={{ color: 'var(--text-2)' }}>{p.specificationTitle || '—'}</td>
                  <td className="tnum" style={{ textAlign: 'right' }}>{p.itemsCount || '—'}</td>
                  <td className="tnum" style={{ textAlign: 'right', fontWeight: 600 }}>{p.value == null ? '—' : fmtMoneyShort(p.value)}</td>
                  <td className="tnum" style={{ textAlign: 'right', fontWeight: 600, color: p.marginPercent != null && p.marginPercent >= 20 ? 'var(--ok)' : undefined }}>
                    {p.marginPercent == null ? '—' : p.marginPercent.toFixed(1).replace('.', ',') + '%'}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </Shell>
  )
}
