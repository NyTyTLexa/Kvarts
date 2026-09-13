import { useState, type FormEvent } from 'react'
import { useNavigate } from 'react-router-dom'
import { useQuery, useMutation, useQueryClient, keepPreviousData } from '@tanstack/react-query'
import { Shell } from '../components/Shell'
import { PageHead } from '../components/PageHead'
import { Icon, Btn } from '../ui/atoms'
import { TableRowsSkeleton } from '../ui/feedback'
import { Dialog } from '../ui/dialog'
import { Field } from '../ui/form'
import { inputStyle } from '../ui/styles'
import { SuggestBox, filterOptions } from '../ui/suggest'
import { useApi } from '../api/useApi'
import { useRoles } from '../auth/useRoles'

interface ProductDto { id: string; sku: string; name: string; manufacturer?: string; category?: string }
interface CategoryDto { path: string; productsCount: number }
interface Paged<T> { items: T[]; total: number; page: number; pageSize: number }

const PAGE_SIZE = 20

export function Catalog() {
  const api = useApi()
  const nav = useNavigate()
  const qc = useQueryClient()
  const { canWrite } = useRoles()
  const [input, setInput] = useState('')
  const [search, setSearch] = useState('')
  const [page, setPage] = useState(1)
  const [catFilter, setCatFilter] = useState('')
  const [analogsFor, setAnalogsFor] = useState<ProductDto | null>(null)
  const [showCreate, setShowCreate] = useState(false)
  const [sku, setSku] = useState('')
  const [name, setName] = useState('')
  const [manufacturer, setManufacturer] = useState('')
  const [category, setCategory] = useState('')
  const [err, setErr] = useState<string>()

  const q = useQuery({
    queryKey: ['products', page, search, catFilter],
    queryFn: () => api.get<Paged<ProductDto>>(
      `/api/products?page=${page}&pageSize=${PAGE_SIZE}`
      + (search ? `&search=${encodeURIComponent(search)}` : '')
      + (catFilter ? `&category=${encodeURIComponent(catFilter)}` : '')),
    placeholderData: keepPreviousData,
  })

  const cats = useQuery({
    queryKey: ['product-categories'],
    queryFn: () => api.get<CategoryDto[]>('/api/products/categories'),
  })

  const analogs = useQuery({
    queryKey: ['product-analogs', analogsFor?.id],
    queryFn: () => api.get<ProductDto[]>(`/api/products/${analogsFor!.id}/analogs`),
    enabled: !!analogsFor,
  })

  // Верхние уровни иерархии для фильтра: сегмент до первого « / », с суммой позиций.
  const topCats = (() => {
    const m = new Map<string, number>()
    for (const c of cats.data ?? []) {
      const top = c.path.split(' / ')[0]
      m.set(top, (m.get(top) ?? 0) + c.productsCount)
    }
    return [...m.entries()].sort((a, b) => a[0].localeCompare(b[0], 'ru'))
  })()

  const total = q.data?.total ?? 0
  const pages = Math.max(1, Math.ceil(total / PAGE_SIZE))

  const create = useMutation({
    mutationFn: () => api.post<ProductDto>('/api/products', {
      sku: sku.trim(), name: name.trim(),
      manufacturer: manufacturer.trim() || null, category: category.trim() || null,
    }),
    onSuccess: () => {
      setSku(''); setName(''); setManufacturer(''); setCategory(''); setShowCreate(false)
      qc.invalidateQueries({ queryKey: ['products'] })
    },
    onError: (e) => setErr((e as Error).message),
  })

  function submit(e: FormEvent) {
    e.preventDefault()
    setPage(1)
    setSearch(input.trim())
  }

  return (
    <Shell breadcrumb={['Справочники', 'Номенклатура']} actions={
      <div style={{ display: 'flex', gap: 8 }}>
        <Btn size="sm" kind="default" icon="layers" onClick={() => nav('/ecatalog')}>Витрина</Btn>
        {canWrite && <Btn size="sm" kind="primary" icon="plus" onClick={() => setShowCreate(v => !v)}>Добавить</Btn>}
      </div>
    }>
      <PageHead kicker="справочник · артикулы" title="Номенклатура" subtitle="Поиск по наименованию и артикулу." />

      <Dialog
        open={showCreate}
        onClose={() => setShowCreate(false)}
        title="Добавить товар"
        width={640}
        footer={<>
          <Btn kind="ghost" size="sm" onClick={() => setShowCreate(false)}>Отмена</Btn>
          <Btn kind="primary" size="sm" disabled={!sku.trim() || !name.trim() || create.isPending} onClick={() => { setErr(undefined); create.mutate() }}>Создать</Btn>
        </>}
      >
        <div className="form-cols" style={{ gridTemplateColumns: '1fr 2fr', marginBottom: 10 }}>
          <Field label="Артикул"><input value={sku} onChange={e => setSku(e.target.value)} placeholder="P82804-B21" style={inputStyle}/></Field>
          <Field label="Наименование"><input value={name} onChange={e => setName(e.target.value)} placeholder="Блок питания HPE 800 Вт Platinum" style={inputStyle}/></Field>
        </div>
        <div className="form-cols" style={{ gridTemplateColumns: '1fr 1fr' }}>
          <Field label="Производитель">
            <SuggestBox boxed value={manufacturer} onChange={setManufacturer}
              items={filterOptions([...(q.data?.items ?? [])].map(p => p.manufacturer).filter((x): x is string => !!x).filter((v, i, a) => a.indexOf(v) === i), manufacturer)}
              onPick={setManufacturer} placeholder="HPE"
              render={s => <span>{s}</span>} />
          </Field>
          <Field label="Категория">
            <SuggestBox boxed value={category} onChange={setCategory}
              items={filterOptions((cats.data ?? []).map(c => c.path), category)}
              onPick={setCategory} placeholder="Комплектующие"
              render={s => <span>{s}</span>} />
          </Field>
        </div>
      </Dialog>

      {err && <div style={{ color: 'var(--err)', background: 'var(--err-bg)', padding: '8px 12px', borderRadius: 7, marginBottom: 12, fontSize: 13 }}>{err}</div>}

      <form onSubmit={submit} style={{ display: 'flex', gap: 8, marginBottom: 14, maxWidth: 860 }}>
        <div style={{ flex: 1, display: 'flex', alignItems: 'center', gap: 8, background: 'var(--surface-2)', borderRadius: 7, padding: '7px 12px' }}>
          <Icon name="search" size={15} stroke="var(--text-3)"/>
          <input value={input} onChange={e => setInput(e.target.value)} placeholder="Поиск по названию или артикулу…"
            style={{ flex: 1, border: 'none', background: 'transparent', outline: 'none', fontSize: 13.5, color: 'var(--text)' }}/>
        </div>
        <select value={catFilter} onChange={e => { setCatFilter(e.target.value); setPage(1) }}
          style={{ ...inputStyle, width: 300 }} title="Фильтр по категории (иерархия, ТЗ п.5.3)">
          <option value="">Все категории</option>
          {topCats.map(([top, count]) => <option key={top} value={top}>{top} ({count})</option>)}
        </select>
        <Btn kind="default" size="md" type="submit">Найти</Btn>
      </form>

      {q.isError && <div style={{ color: 'var(--err)', padding: 12 }}>Ошибка загрузки: {(q.error as Error).message}</div>}

      <div className="doc-list">
      <table>
        <thead>
          <tr>
            {['Артикул', 'Наименование', 'Производитель', 'Категория'].map((h, i) => (
              <th key={h} style={{ padding: '10px 12px', textAlign: 'left', fontWeight: 500, fontSize: 11, color: 'var(--text-2)', textTransform: 'uppercase', letterSpacing: 0.6, borderBottom: '1px solid var(--border)', width: i === 0 ? 150 : undefined }}>{h}</th>
            ))}
          </tr>
        </thead>
        <tbody>
          {q.data?.items.map(p => (
            <tr key={p.id} onClick={() => setAnalogsFor(analogsFor?.id === p.id ? null : p)}
              style={{ borderBottom: '1px solid var(--border)', cursor: 'pointer', background: analogsFor?.id === p.id ? 'var(--accent-soft)' : 'transparent' }}>
              <td className="mono" style={{ padding: '11px 12px', fontSize: 11.5, color: 'var(--text-2)' }}>{p.sku}</td>
              <td style={{ padding: '11px 12px', fontWeight: 500 }}>{p.name}</td>
              <td style={{ padding: '11px 12px' }}>
                {p.manufacturer && <span style={{ padding: '2px 8px', borderRadius: 4, background: 'var(--surface-2)', fontSize: 11.5 }}>{p.manufacturer}</span>}
              </td>
              <td style={{ padding: '11px 12px', color: 'var(--text-2)' }}>{p.category ?? '—'}</td>
            </tr>
          ))}
          {q.isLoading && <TableRowsSkeleton rows={6} cols={4} />}
          {q.data && q.data.items.length === 0 && <tr><td colSpan={4} style={{ padding: 16, color: 'var(--text-3)' }}>Ничего не найдено</td></tr>}
        </tbody>
      </table>
      </div>

      {analogsFor && (
        <div style={{ background: 'var(--surface)', border: '1px solid var(--border)', borderRadius: 10, padding: 16, marginTop: 16 }}>
          <div style={{ display: 'flex', alignItems: 'center', gap: 10, marginBottom: 10 }}>
            <Icon name="package" size={16} stroke="var(--accent)"/>
            <span style={{ fontWeight: 600, fontSize: 13.5 }}>Аналоги: {analogsFor.name}</span>
            <span style={{ fontSize: 11.5, color: 'var(--text-3)' }}>та же категория{analogsFor.category ? ` · ${analogsFor.category}` : ''}</span>
            <div style={{ flex: 1 }}/>
            <Btn size="sm" kind="ghost" icon="x" onClick={() => setAnalogsFor(null)}>{''}</Btn>
          </div>
          {analogs.isLoading && <div style={{ color: 'var(--text-3)', fontSize: 13 }}>Подбираем…</div>}
          {analogs.data && analogs.data.length === 0 && (
            <div style={{ color: 'var(--text-3)', fontSize: 13 }}>
              {analogsFor.category ? 'Аналогов в этой категории нет' : 'У позиции не задана категория — подбор аналогов невозможен'}
            </div>
          )}
          {analogs.data && analogs.data.length > 0 && (
            <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: 12.5 }}>
              <tbody>
                {analogs.data.map(a => (
                  <tr key={a.id} style={{ borderBottom: '1px solid var(--border)' }}>
                    <td className="mono" style={{ padding: '8px 12px', fontSize: 11.5, color: 'var(--text-2)', width: 150 }}>{a.sku}</td>
                    <td style={{ padding: '8px 12px', fontWeight: 500 }}>{a.name}</td>
                    <td style={{ padding: '8px 12px', width: 160 }}>
                      {a.manufacturer && <span style={{ padding: '2px 8px', borderRadius: 4, background: a.manufacturer !== analogsFor.manufacturer ? 'var(--accent-soft)' : 'var(--surface-2)', fontSize: 11.5 }}>{a.manufacturer}</span>}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
        </div>
      )}

      <div style={{ display: 'flex', alignItems: 'center', gap: 12, marginTop: 14, fontSize: 12.5, color: 'var(--text-2)' }}>
        <span>Всего: <b style={{ color: 'var(--text)' }}>{total.toLocaleString('ru-RU')}</b></span>
        <div style={{ flex: 1 }}/>
        <Btn size="sm" kind="default" disabled={page <= 1} onClick={() => setPage(p => p - 1)} icon="chevR" style={{ transform: 'scaleX(-1)' }}>{''}</Btn>
        <span className="tnum">Стр. {page} / {pages}</span>
        <Btn size="sm" kind="default" disabled={page >= pages} onClick={() => setPage(p => p + 1)} icon="chevR">{''}</Btn>
      </div>
    </Shell>
  )
}


