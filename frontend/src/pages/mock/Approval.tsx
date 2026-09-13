import { useEffect, useState } from 'react'
import { Link, useSearchParams } from 'react-router-dom'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { Shell } from '../../components/Shell'
import { PageHead } from '../../components/PageHead'
import { Btn, Icon, StatusPill, type PillKind } from '../../ui/atoms'
import { Dialog } from '../../ui/dialog'
import { Field } from '../../ui/form'
import { inputStyle } from '../../ui/styles'
import { useApi } from '../../api/useApi'
import { useRoles } from '../../auth/useRoles'
import { fmtMoney, fmtMoneyShort } from '../../data/constants'
import { STRATEGY_META } from '../../data/orders'
import type { SpecificationDto } from '../../api/types'
import type { ApprovalDto, ApprovalStatus, InvoiceDto, QuoteStrategy } from '../../api/commercial'

const STRATEGIES: QuoteStrategy[] = ['MinCost', 'MinLeadTime', 'Balanced', 'MlRelevance']

const APPROVAL_STAGES: { id: ApprovalStatus; label: string }[] = [
  { id: 'НаСогласованииРП', label: 'Согласование РП' },
  { id: 'ВКоммерческомБлоке', label: 'Коммерческий блок' },
  { id: 'Согласовано', label: 'Согласовано' },
]

const APPROVAL_STATUS_LABEL: Record<ApprovalStatus, string> = {
  'НаСогласованииРП': 'На согласовании РП',
  'ВКоммерческомБлоке': 'В коммерческом блоке',
  'Согласовано': 'Согласовано',
  'Отклонено': 'Отклонено',
}

const APPROVAL_STATUS_KIND: Record<ApprovalStatus, PillKind> = {
  'НаСогласованииРП': 'amber',
  'ВКоммерческомБлоке': 'blue',
  'Согласовано': 'green',
  'Отклонено': 'red',
}

const emptyForm = { specificationId: '', strategy: 'Balanced' as QuoteStrategy, markupPercent: 10 }

const fmtPct = (n: number) => (Number.isInteger(n) ? String(n) : n.toFixed(1).replace('.', ',')) + '%'
const fmtMargin = (n: number) => n.toFixed(1).replace('.', ',')
const isDecided = (s: ApprovalStatus) => s === 'Согласовано' || s === 'Отклонено'



function Block({ label, value, sub, accent }: { label: string; value: string; sub?: string; accent?: boolean }) {
  return (
    <div style={{
      background: accent ? 'var(--accent-soft)' : 'var(--surface-2)',
      border: accent ? '1.5px solid var(--accent)' : '1px solid var(--border)',
      borderRadius: 10, padding: '16px 18px',
    }}>
      <div style={{ fontSize: 11, color: accent ? 'var(--accent)' : 'var(--text-2)', textTransform: 'uppercase', letterSpacing: 0.5, fontWeight: 600, marginBottom: 8 }}>{label}</div>
      <div className="tnum" style={{ fontSize: 22, fontWeight: 600, color: accent ? 'var(--accent)' : 'var(--text)' }}>{value}</div>
      {sub && <div className="tnum" style={{ fontSize: 12, color: 'var(--text-2)', marginTop: 2 }}>{sub}</div>}
    </div>
  )
}

function Arrow() {
  return <div className="calc-arrow" style={{ textAlign: 'center', color: 'var(--text-3)', fontSize: 18 }}>→</div>
}

function RouteStepper({ status }: { status: ApprovalStatus }) {
  if (status === 'Отклонено') {
    return (
      <div style={{ display: 'flex', alignItems: 'center', gap: 10, color: 'var(--err)', fontSize: 13 }}>
        <span style={{ width: 22, height: 22, borderRadius: 999, background: 'var(--err)', color: 'var(--on-accent)', display: 'inline-flex', alignItems: 'center', justifyContent: 'center' }}><Icon name="x" size={12} stroke="var(--on-accent)" /></span>
        КП отклонено
      </div>
    )
  }
  const completed = status === 'Согласовано'
  const idx = APPROVAL_STAGES.findIndex(s => s.id === status)
  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 0 }}>
      {APPROVAL_STAGES.map((s, i, arr) => {
        const done = completed || i < idx
        const active = !completed && i === idx
        return (
          <div key={s.id} style={{ display: 'flex', gap: 12, alignItems: 'flex-start' }}>
            <div style={{ display: 'flex', flexDirection: 'column', alignItems: 'center' }}>
              <div style={{
                width: 22, height: 22, borderRadius: 999, display: 'flex', alignItems: 'center', justifyContent: 'center',
                fontSize: 11, fontWeight: 600, flexShrink: 0,
                background: done ? 'var(--ok)' : active ? 'var(--accent)' : 'var(--surface)',
                color: done || active ? 'white' : 'var(--text-3)',
                border: !done && !active ? '1.5px dashed var(--border-strong)' : 'none',
                boxShadow: active ? '0 0 0 4px var(--accent-soft)' : 'none',
              }}>{done ? <Icon name="check" size={12} stroke="var(--on-accent)" /> : i + 1}</div>
              {i < arr.length - 1 && <div style={{ width: 1.5, flex: 1, minHeight: 26, background: 'var(--border)' }} />}
            </div>
            <div style={{ paddingBottom: 18 }}>
              <div style={{ fontSize: 13, fontWeight: 500, color: done || active ? 'var(--text)' : 'var(--text-2)' }}>{s.label}</div>
              <div style={{ fontSize: 11.5, color: active ? 'var(--accent)' : 'var(--text-3)' }}>{done ? 'согласовано' : active ? 'на рассмотрении' : 'ожидает'}</div>
            </div>
          </div>
        )
      })}
    </div>
  )
}

export function Approval() {
  const api = useApi()
  const qc = useQueryClient()
  const { canWrite, canApprove } = useRoles()

  const [params] = useSearchParams()
  const [show, setShow] = useState(false)
  const [f, setF] = useState({ ...emptyForm })
  const [selId, setSelId] = useState<string | null>(params.get('id'))
  const [markup, setMarkup] = useState(10)
  const [comment, setComment] = useState('')
  const [err, setErr] = useState<string>()

  const list = useQuery({ queryKey: ['approvals'], queryFn: () => api.get<ApprovalDto[]>('/api/approvals') })
  const invoices = useQuery({ queryKey: ['invoices'], queryFn: () => api.get<InvoiceDto[]>('/api/invoices') })
  useEffect(() => {
    const qid = params.get('id')
    if (qid) setSelId(qid)
  }, [params])
  useEffect(() => {
    const a = list.data?.find(x => x.id === selId)
    if (a) setMarkup(a.markupPercent)
  }, [selId, list.data])
  const specs = useQuery({ queryKey: ['specs-all'], queryFn: () => api.get<SpecificationDto[]>('/api/specifications') })

  const sel = list.data?.find(a => a.id === selId) ?? null
  const invoiceForSel = sel ? invoices.data?.find(i => i.approvalId === sel.id) : undefined

  function reset() { setShow(false); setF({ ...emptyForm }); setErr(undefined) }
  function pick(a: ApprovalDto) { setSelId(a.id); setMarkup(a.markupPercent); setComment(''); setErr(undefined) }

  const create = useMutation({
    mutationFn: () => api.post<ApprovalDto>('/api/approvals', {
      specificationId: f.specificationId, strategy: f.strategy, markupPercent: f.markupPercent,
    }),
    onSuccess: (d) => { reset(); qc.invalidateQueries({ queryKey: ['approvals'] }); setSelId(d.id); setMarkup(d.markupPercent) },
    onError: (e) => setErr((e as Error).message),
  })
  const setMargin = useMutation({
    mutationFn: (id: string) => api.post<void>(`/api/approvals/${id}/margin`, { markupPercent: markup }),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['approvals'] }),
    onError: (e) => setErr((e as Error).message),
  })
  const decide = useMutation({
    mutationFn: (vars: { id: string; approved: boolean; comment?: string }) =>
      api.post<void>(`/api/approvals/${vars.id}/decision`, { approved: vars.approved, comment: vars.comment }),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['approvals'] })
      qc.invalidateQueries({ queryKey: ['invoices'] })
      setComment('')
    },
    onError: (e) => setErr((e as Error).message),
  })
  const resubmit = useMutation({
    mutationFn: (id: string) => api.post<void>(`/api/approvals/${id}/resubmit`),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['approvals'] }),
    onError: (e) => setErr((e as Error).message),
  })

  const cost = sel?.costPrice ?? 0
  const sell = Math.round(cost * (1 + markup / 100))
  const margin = sell > 0 ? (sell - cost) / sell * 100 : 0
  // Разделение обязанностей по ТЗ: наценку задаёт РП (write), решение принимает КБ (approve).
  const canEditMargin = !!sel && canWrite && !isDecided(sel.status)
  const canDecide = !!sel && canApprove && !isDecided(sel.status)
  // Возврат на доработку (ТЗ п.5.6: «при отклонении процесс возвращается на шаг назад») — РП.
  const canResubmit = !!sel && canWrite && sel.status === 'Отклонено'
  const canSubmit = !!f.specificationId && f.markupPercent >= 0

  return (
    <Shell breadcrumb={['Согласование', 'Коммерческий блок']} actions={canWrite &&
      <Btn size="sm" kind="primary" icon="plus" onClick={() => (show ? reset() : setShow(true))}>Новое согласование</Btn>}>
      <PageHead kicker="согласование · маржа" title="Маржа"
        subtitle="Наценка и решение по цене для клиента. Себестоимость берётся из выбранного листа КП." />

      <Dialog
        open={show}
        onClose={reset}
        title="Новое согласование"
        width={720}
        footer={<>
          <Btn kind="ghost" size="sm" onClick={reset}>Отмена</Btn>
          <Btn kind="primary" size="sm" disabled={!canSubmit || create.isPending} onClick={() => { setErr(undefined); create.mutate() }}>Создать согласование</Btn>
        </>}
      >
        <div className="form-cols" style={{ gridTemplateColumns: '2fr 1fr 1fr' }}>
          <Field label="Спецификация">
            <select value={f.specificationId} onChange={e => setF({ ...f, specificationId: e.target.value })} style={inputStyle}>
              <option value="">— выберите спецификацию —</option>
              {specs.data?.map(s => <option key={s.id} value={s.id}>{s.title}{s.customer ? ` · ${s.customer}` : ''}</option>)}
            </select>
          </Field>
          <Field label="Стратегия КП">
            <select value={f.strategy} onChange={e => setF({ ...f, strategy: e.target.value as QuoteStrategy })} style={inputStyle}>
              {STRATEGIES.map(s => <option key={s} value={s}>{STRATEGY_META[s].label}</option>)}
            </select>
          </Field>
          <Field label="Наценка, %">
            <input type="number" min={0} value={f.markupPercent} onChange={e => setF({ ...f, markupPercent: Math.max(0, +e.target.value || 0) })} style={inputStyle}/>
          </Field>
        </div>
      </Dialog>

      {err && <div style={{ color: 'var(--err)', background: 'var(--err-bg)', padding: '8px 12px', borderRadius: 7, marginBottom: 12, fontSize: 13 }}>{err}</div>}

      <div className="doc-list">
      <table>
        <thead>
          <tr>{['Спецификация', 'Стратегия', 'Себестоимость', 'Наценка', 'Цена', 'Маржа', 'Статус'].map((h, i) => (
            <th key={h} style={{ padding: '10px 12px', textAlign: i >= 2 && i <= 5 ? 'right' : 'left', fontWeight: 500, fontSize: 11, color: 'var(--text-2)', textTransform: 'uppercase', letterSpacing: 0.6, borderBottom: '1px solid var(--border)' }}>{h}</th>
          ))}</tr>
        </thead>
        <tbody>
          {list.data?.map(a => (
            <tr key={a.id} onClick={() => pick(a)} style={{ borderBottom: '1px solid var(--border)', cursor: 'pointer', background: a.id === selId ? 'var(--accent-soft)' : 'transparent' }}>
              <td style={{ padding: '11px 12px' }}>
                <span style={{ display: 'inline-flex', alignItems: 'center', gap: 9 }}>
                  <span style={{ width: 24, height: 24, borderRadius: 6, background: 'var(--surface-2)', display: 'inline-flex', alignItems: 'center', justifyContent: 'center' }}><Icon name="document" size={14} stroke="var(--text-2)"/></span>
                  <span style={{ fontWeight: 500 }}>{a.title}</span>
                </span>
              </td>
              <td style={{ padding: '11px 12px' }}><span style={{ padding: '2px 8px', borderRadius: 4, background: 'var(--surface-2)', fontSize: 11.5 }}>{STRATEGY_META[a.strategy]?.tag ?? a.strategy}</span></td>
              <td className="tnum" style={{ padding: '11px 12px', textAlign: 'right', color: 'var(--text-2)' }}>{fmtMoney(a.costPrice)}</td>
              <td className="tnum" style={{ padding: '11px 12px', textAlign: 'right' }}>{fmtPct(a.markupPercent)}</td>
              <td className="tnum" style={{ padding: '11px 12px', textAlign: 'right', fontWeight: 600 }}>{fmtMoney(a.sellPrice)}</td>
              <td className="tnum" style={{ padding: '11px 12px', textAlign: 'right', color: a.marginPercent >= 20 ? 'var(--ok)' : 'var(--warn)' }}>{fmtMargin(a.marginPercent)}%</td>
              <td style={{ padding: '11px 12px' }}><StatusPill status={APPROVAL_STATUS_LABEL[a.status]} kind={APPROVAL_STATUS_KIND[a.status]} /></td>
            </tr>
          ))}
          {list.isLoading && <tr><td colSpan={7} style={{ padding: 16, color: 'var(--text-3)' }}>Загрузка…</td></tr>}
          {list.data && list.data.length === 0 && <tr><td colSpan={7} style={{ padding: 16, color: 'var(--text-3)' }}>Согласований пока нет. Создайте новое из спецификации.</td></tr>}
        </tbody>
      </table>
      </div>

      {sel && (
        <div style={{ background: 'var(--surface)', border: '1px solid var(--border)', borderRadius: 10, padding: 18, marginTop: 18 }}>
          <div style={{ display: 'flex', alignItems: 'center', gap: 10, marginBottom: 14 }}>
            <Icon name="scale" size={18} stroke="var(--accent)"/>
            <div style={{ fontWeight: 600 }}>{sel.title}</div>
            <div style={{ flex: 1 }}/>
            <StatusPill status={APPROVAL_STATUS_LABEL[sel.status]} kind={APPROVAL_STATUS_KIND[sel.status]} />
          </div>

          {sel.status === 'Согласовано' && (
            <div style={{ marginBottom: 14, fontSize: 13 }}>
              {invoiceForSel
                ? <Link to={`/invoice?id=${invoiceForSel.id}`} style={{ color: 'var(--accent)' }}>Счёт {invoiceForSel.number}</Link>
                : <span style={{ color: 'var(--text-3)' }}>Счёт создаётся…</span>}
            </div>
          )}

          <div className="calc-row" style={{ marginBottom: 18, maxWidth: 820 }}>
            <Block label="Себестоимость" value={fmtMoney(cost)} />
            <Arrow/>
            <Block label="Наценка" accent value={fmtPct(markup)} sub={`+${fmtMoneyShort(sell - cost)}`} />
            <Arrow/>
            <Block label="Цена клиенту" value={fmtMoney(sell)} />
          </div>

          {canEditMargin && (
            <div style={{ maxWidth: 820, marginBottom: 16 }}>
              <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: 12, color: 'var(--text-2)', marginBottom: 6 }}>
                <span>Наценка (задаёт РП)</span>
                <span>Маржа <b className="tnum" style={{ color: margin >= 20 ? 'var(--ok)' : 'var(--warn)' }}>{fmtMargin(margin)}%</b></span>
              </div>
              <input type="range" min={0} max={200} value={markup} onChange={e => setMarkup(+e.target.value)} style={{ width: '100%', accentColor: 'var(--accent)' }}/>
              <div style={{ display: 'flex', gap: 8, marginTop: 8, alignItems: 'center' }}>
                <input type="number" min={0} value={markup} onChange={e => setMarkup(Math.max(0, +e.target.value || 0))} style={{ ...inputStyle, width: 120 }}/>
                <Btn kind="primary" size="sm" disabled={setMargin.isPending} onClick={() => { setErr(undefined); setMargin.mutate(sel.id) }}>
                  {sel.status === 'НаСогласованииРП' ? 'Отправить в КБ' : 'Пересчитать'}
                </Btn>
              </div>
            </div>
          )}
          {canDecide && (
            <div style={{ display: 'flex', gap: 8, alignItems: 'center', flexWrap: 'wrap', marginBottom: 18 }}>
              <span style={{ fontSize: 11, color: 'var(--text-3)', marginRight: 4 }}>Решение КБ:</span>
              <input value={comment} onChange={e => setComment(e.target.value)} placeholder="Комментарий (необязательно)" style={{ ...inputStyle, maxWidth: 320 }}/>
              <Btn kind="primary" size="sm" icon="check" disabled={decide.isPending} onClick={() => { setErr(undefined); decide.mutate({ id: sel.id, approved: true, comment: comment.trim() || undefined }) }}>Согласовать</Btn>
              <Btn kind="danger" size="sm" icon="x" disabled={decide.isPending} onClick={() => { setErr(undefined); decide.mutate({ id: sel.id, approved: false, comment: comment.trim() || undefined }) }}>Отклонить</Btn>
            </div>
          )}
          {canResubmit && (
            <div style={{ marginBottom: 18 }}>
              <Btn kind="primary" size="sm" icon="edit" disabled={resubmit.isPending}
                onClick={() => { setErr(undefined); resubmit.mutate(sel.id) }}>Отправить на доработку повторно</Btn>
            </div>
          )}

          <div style={{ maxWidth: 520 }}>
            <div style={{ fontSize: 11, color: 'var(--text-2)', textTransform: 'uppercase', letterSpacing: 0.5, fontWeight: 500, marginBottom: 12 }}>Маршрут согласования</div>
            <RouteStepper status={sel.status} />
          </div>

          {sel.comment && <div style={{ marginTop: 12, fontSize: 12.5, color: 'var(--text-2)' }}>Комментарий: {sel.comment}</div>}
        </div>
      )}
    </Shell>
  )
}
