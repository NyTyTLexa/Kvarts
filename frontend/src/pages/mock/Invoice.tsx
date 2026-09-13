import { Fragment, useEffect, useState, useRef } from 'react'
import { useSearchParams } from 'react-router-dom'
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
import type { ApprovalDto, InvoiceDto, InvoiceDetailDto, InvoiceStatus } from '../../api/commercial'
import type { InvoiceAttachmentDto } from '../../api/invoiceAttachments'

const INVOICE_FLOW: InvoiceStatus[] = ['Создан', 'Согласован', 'ОжиданиеОплаты', 'ЧастичнаяОплата', 'Оплачено', 'ОжиданиеПоставки', 'ПришёлНаСклад', 'ОтраженоВ1С']

const INVOICE_STATUS_LABEL: Record<InvoiceStatus, string> = {
  'Создан': 'Создан', 'Согласован': 'Согласован', 'ОжиданиеОплаты': 'Ожидание оплаты',
  'ЧастичнаяОплата': 'Частичная оплата', 'Оплачено': 'Оплачено', 'ОжиданиеПоставки': 'Ожидание поставки',
  'ПришёлНаСклад': 'Пришёл на склад', 'ОтраженоВ1С': 'Отражено в 1С', 'Отменён': 'Отменён',
}

const INVOICE_STATUS_KIND: Record<InvoiceStatus, PillKind> = {
  'Создан': 'gray', 'Согласован': 'blue', 'ОжиданиеОплаты': 'amber', 'ЧастичнаяОплата': 'amber',
  'Оплачено': 'green', 'ОжиданиеПоставки': 'blue', 'ПришёлНаСклад': 'green', 'ОтраженоВ1С': 'green', 'Отменён': 'red',
}

const emptyForm = { approvalId: '', contract: '', dueDate: '' }

const fmtDate = (iso?: string) => (iso ? new Date(iso).toLocaleDateString('ru-RU') : null)
const toIso = (d: string) => (d ? `${d}T00:00:00Z` : null)





export function Invoice() {
  const api = useApi()
  const qc = useQueryClient()
  const { canWrite, canApprove, canPostPayment } = useRoles()

  const [params] = useSearchParams()
  const [show, setShow] = useState(false)
  const [f, setF] = useState({ ...emptyForm })
  const [selId, setSelId] = useState<string | null>(params.get('id'))
  const [err, setErr] = useState<string>()

  const list = useQuery({ queryKey: ['invoices'], queryFn: () => api.get<InvoiceDto[]>('/api/invoices') })
  const approvals = useQuery({ queryKey: ['approvals'], queryFn: () => api.get<ApprovalDto[]>('/api/approvals') })
  useEffect(() => {
    const qid = params.get('id')
    if (qid) setSelId(qid)
  }, [params])

  const sel = list.data?.find(i => i.id === selId) ?? null
  const occupiedApprovalIds = new Set((list.data ?? []).map(i => i.approvalId))
  const approvedApprovals = approvals.data?.filter(a => a.status === 'Согласовано' && !occupiedApprovalIds.has(a.id)) ?? []

  function reset() { setShow(false); setF({ ...emptyForm }); setErr(undefined) }
  function pick(i: InvoiceDto) { setSelId(i.id); setErr(undefined) }

  const create = useMutation({
    mutationFn: () => api.post<InvoiceDetailDto>('/api/invoices', {
      approvalId: f.approvalId, contract: f.contract.trim() || null, dueDateUtc: toIso(f.dueDate),
    }),
    onSuccess: (d) => { reset(); qc.invalidateQueries({ queryKey: ['invoices'] }); setSelId(d.id) },
    onError: (e) => setErr((e as Error).message),
  })
  const setStatus = useMutation({
    mutationFn: (vars: { id: string; status: InvoiceStatus }) => api.post<void>(`/api/invoices/${vars.id}/status`, { status: vars.status }),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['invoices'] }),
    onError: (e) => setErr((e as Error).message),
  })

  // Детали счёта с позициями — снимок согласованного варианта КП (ТЗ п.6.1).
  const detail = useQuery({
    queryKey: ['invoice', selId],
    queryFn: () => api.get<InvoiceDetailDto>('/api/invoices/' + selId),
    enabled: !!selId,
  })

  // Прикреплённые документы (ТЗ п.6.1) — список и загрузка (УК-08, Бухгалтерия).
  const attachments = useQuery({
    queryKey: ['invoice-attachments', selId],
    queryFn: () => api.get<InvoiceAttachmentDto[]>('/api/invoices/' + selId + '/attachments'),
    enabled: !!selId,
  })
  const uploadAtt = useMutation({
    mutationFn: (file: File) => {
      const fd = new FormData(); fd.append('file', file)
      return api.post<InvoiceAttachmentDto>('/api/invoices/' + selId + '/attachments', fd)
    },
    onSuccess: () => qc.invalidateQueries({ queryKey: ['invoice-attachments', selId] }),
    onError: (e) => setErr((e as Error).message),
  })
  const attFileRef = useRef<HTMLInputElement>(null)

  const flowIdx = sel ? INVOICE_FLOW.indexOf(sel.status) : -1
  const next = sel && flowIdx >= 0 && flowIdx < INVOICE_FLOW.length - 1 ? INVOICE_FLOW[flowIdx + 1] : null
  // «Согласован» — КБ (canApprove); дальше — бухгалтерия (UC-08). Создание счёта — РП (ниже, canWrite).
  const canCancel = !!sel && canPostPayment && sel.status !== 'Отменён' && sel.status !== 'ОтраженоВ1С'
  const canAdvance = !!sel && next !== null && (next === 'Согласован' ? canApprove : canPostPayment)
  const canSubmit = !!f.approvalId

  return (
    <Shell breadcrumb={['Заказы NOC', 'Счета']} actions={canWrite &&
      <Btn size="sm" kind="primary" icon="plus" onClick={() => (show ? reset() : setShow(true))}>Новый счёт</Btn>}>
      <PageHead kicker="согласование · NOC" title="Счета"
        subtitle="Счёт из согласованного КП. Стадии склада и 1С выставляет приёмка." />

      <Dialog
        open={show}
        onClose={reset}
        title="Новый счёт"
        width={720}
        footer={<>
          <Btn kind="ghost" size="sm" onClick={reset}>Отмена</Btn>
          <Btn kind="primary" size="sm" disabled={!canSubmit || create.isPending} onClick={() => { setErr(undefined); create.mutate() }}>Создать счёт</Btn>
        </>}
      >
        <div className="form-cols" style={{ gridTemplateColumns: '2fr 1fr 1fr', marginBottom: 12 }}>
          <Field label="Согласованное КП">
            <select value={f.approvalId} onChange={e => setF({ ...f, approvalId: e.target.value })} style={inputStyle}>
              <option value="">— выберите согласование —</option>
              {approvedApprovals.map(a => <option key={a.id} value={a.id}>{a.title} · {fmtMoneyShort(a.sellPrice)}</option>)}
            </select>
          </Field>
          <Field label="Договор">
            <input value={f.contract} onChange={e => setF({ ...f, contract: e.target.value })} placeholder="№ 118-СКС" style={inputStyle}/>
          </Field>
          <Field label="Срок оплаты">
            <input type="date" value={f.dueDate} onChange={e => setF({ ...f, dueDate: e.target.value })} style={inputStyle}/>
          </Field>
        </div>
        {approvedApprovals.length === 0 && <div style={{ fontSize: 12, color: 'var(--text-3)' }}>Нет согласованных КП — сначала согласуйте КП на экране «Согласование КБ».</div>}
      </Dialog>

      {err && <div style={{ color: 'var(--err)', background: 'var(--err-bg)', padding: '8px 12px', borderRadius: 7, marginBottom: 12, fontSize: 13 }}>{err}</div>}

      <div className="doc-list">
      <table>
        <thead>
          <tr>{['Номер', 'Контрагент', 'Сумма', 'Статус', 'Срок оплаты'].map((h, i) => (
            <th key={h} style={{ padding: '10px 12px', textAlign: i === 2 ? 'right' : 'left', fontWeight: 500, fontSize: 11, color: 'var(--text-2)', textTransform: 'uppercase', letterSpacing: 0.6, borderBottom: '1px solid var(--border)' }}>{h}</th>
          ))}</tr>
        </thead>
        <tbody>
          {list.data?.map(i => (
            <tr key={i.id} onClick={() => pick(i)} style={{ borderBottom: '1px solid var(--border)', cursor: 'pointer', background: i.id === selId ? 'var(--accent-soft)' : 'transparent' }}>
              <td className="mono" style={{ padding: '11px 12px', fontSize: 11.5 }}>
                <span style={{ display: 'inline-flex', alignItems: 'center', gap: 8 }}><Icon name="bank" size={14} stroke="var(--text-2)"/>{i.number}</span>
              </td>
              <td style={{ padding: '11px 12px', fontWeight: 500 }}>{i.customer ?? '—'}</td>
              <td className="tnum" style={{ padding: '11px 12px', textAlign: 'right', fontWeight: 600 }}>{fmtMoney(i.sellPrice)}</td>
              <td style={{ padding: '11px 12px' }}><StatusPill status={INVOICE_STATUS_LABEL[i.status]} kind={INVOICE_STATUS_KIND[i.status]} /></td>
              <td style={{ padding: '11px 12px', color: 'var(--text-2)' }}>{fmtDate(i.dueDateUtc) ?? '—'}</td>
            </tr>
          ))}
          {list.isLoading && <tr><td colSpan={5} style={{ padding: 16, color: 'var(--text-3)' }}>Загрузка…</td></tr>}
          {list.data && list.data.length === 0 && <tr><td colSpan={5} style={{ padding: 16, color: 'var(--text-3)' }}>Счетов пока нет. Создайте счёт из согласованного КП.</td></tr>}
        </tbody>
      </table>
      </div>

      {sel && (
        <div style={{ background: 'var(--surface)', border: '1px solid var(--border)', borderRadius: 10, padding: 18, marginTop: 18 }}>
          <div style={{ display: 'flex', alignItems: 'center', gap: 10, marginBottom: 16 }}>
            <Icon name="bank" size={18} stroke="var(--accent)"/>
            <div style={{ fontWeight: 600, fontSize: 15 }}>Счёт {sel.number}</div>
            <div style={{ flex: 1 }}/>
            <StatusPill status={INVOICE_STATUS_LABEL[sel.status]} kind={INVOICE_STATUS_KIND[sel.status]} />
          </div>

          <div style={{ background: 'var(--surface-2)', borderRadius: 9, padding: '12px 16px', marginBottom: 22 }}>
            <div className="kv-grid" style={{ fontSize: 13 }}>
              <span style={{ color: 'var(--text-2)' }}>Контрагент</span><span style={{ fontWeight: 500 }}>{sel.customer ?? '—'}</span>
              <span style={{ color: 'var(--text-2)' }}>Договор</span><span>{sel.contract ?? '—'}</span>
              <span style={{ color: 'var(--text-2)' }}>Себестоимость</span><span className="tnum">{fmtMoney(sel.costPrice)}</span>
              <span style={{ color: 'var(--text-2)' }}>Срок оплаты</span><span>{fmtDate(sel.dueDateUtc) ?? '—'}</span>
              <span style={{ color: 'var(--text-2)' }}>Наценка</span><span className="tnum">{sel.markupPercent.toLocaleString('ru-RU')}%</span>
              <span style={{ color: 'var(--text-2)' }}>Сумма счёта</span><span className="tnum" style={{ fontWeight: 600 }}>{fmtMoney(sel.sellPrice)}</span>
            </div>
          </div>

          <div style={{ marginBottom: 18 }}>
            <div style={{ fontSize: 11, color: 'var(--text-2)', textTransform: 'uppercase', letterSpacing: 0.5, fontWeight: 500, marginBottom: 10 }}>Позиции</div>
            <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: 12.5 }}>
              <thead><tr>{['Позиция', 'Артикул', 'Кол-во', 'Поставщик', 'Цена за ед.', 'Сумма'].map((h, i) => (
                <th key={h} style={{ padding: '8px 10px', textAlign: i >= 4 ? 'right' : 'left', fontWeight: 500, fontSize: 11, color: 'var(--text-2)', textTransform: 'uppercase', borderBottom: '1px solid var(--border)' }}>{h}</th>
              ))}</tr></thead>
              <tbody>
                {detail.data?.lines.map(l => (
                  <tr key={l.id} style={{ borderBottom: '1px solid var(--border)' }}>
                    <td style={{ padding: '8px 10px' }}>{l.name}{l.manufacturer && <span style={{ color: 'var(--text-3)', fontSize: 11.5 }}> · {l.manufacturer}</span>}</td>
                    <td className="mono" style={{ padding: '8px 10px', fontSize: 11.5 }}>{l.sku ?? '—'}</td>
                    <td className="tnum" style={{ padding: '8px 10px' }}>{l.quantity}</td>
                    <td style={{ padding: '8px 10px', color: 'var(--text-2)' }}>{l.vendorName ?? '—'}</td>
                    <td className="tnum" style={{ padding: '8px 10px', textAlign: 'right' }}>{fmtMoney(l.unitPrice)}</td>
                    <td className="tnum" style={{ padding: '8px 10px', textAlign: 'right', fontWeight: 500 }}>{fmtMoney(l.lineTotal)}</td>
                  </tr>
                ))}
                {detail.data && detail.data.lines.length === 0 && <tr><td colSpan={6} style={{ padding: 12, color: 'var(--text-3)' }}>Позиций нет (счёт создан до ввода построчного учёта)</td></tr>}
                {detail.isLoading && <tr><td colSpan={6} style={{ padding: 12, color: 'var(--text-3)' }}>Загрузка…</td></tr>}
              </tbody>
            </table>
            {detail.data && detail.data.lines.length > 0 && (
              <div style={{ fontSize: 11.5, color: 'var(--text-3)', marginTop: 6 }}>
                Позиции — снимок согласованного варианта КП с наценкой {sel.markupPercent.toLocaleString('ru-RU')}%; итог счёта — в шапке.
              </div>
            )}
          </div>

          <div style={{ marginBottom: 18, display: 'flex', flexDirection: 'column', gap: 10 }}>
            <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
              <div style={{ fontSize: 11, color: 'var(--text-2)', textTransform: 'uppercase', letterSpacing: 0.5, fontWeight: 500 }}>Документы</div>
              {canPostPayment && (
                <div style={{ display: 'flex', gap: 6, alignItems: 'center' }}>
                  <input ref={attFileRef} type="file" style={{ display: 'none' }} onChange={e => { const f = e.target.files?.[0]; if (f) { setErr(undefined); uploadAtt.mutate(f) } e.target.value = '' }}/>
                  <Btn size="sm" kind="primary" icon="upload" disabled={uploadAtt.isPending} onClick={() => attFileRef.current?.click()}>Прикрепить документ</Btn>
                </div>
              )}
            </div>
            <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: 12.5 }}>
              <thead><tr>{['Файл', 'Загрузил', 'Дата', ''].map((h, i) => (
                <th key={i} style={{ padding: '8px 10px', textAlign: i === 3 ? 'right' : 'left', fontWeight: 500, fontSize: 11, color: 'var(--text-2)', textTransform: 'uppercase', borderBottom: '1px solid var(--border)' }}>{h}</th>
              ))}</tr></thead>
              <tbody>
                {attachments.data?.map(a => (
                  <tr key={a.id} style={{ borderBottom: '1px solid var(--border)' }}>
                    <td style={{ padding: '8px 10px' }}><span style={{ display: 'inline-flex', alignItems: 'center', gap: 6 }}><Icon name="document" size={14} stroke="var(--text-2)"/>{a.fileName}</span></td>
                    <td style={{ padding: '8px 10px', color: 'var(--text-2)' }}>{a.uploadedBy ?? '—'}</td>
                    <td style={{ padding: '8px 10px', color: 'var(--text-2)' }}>{new Date(a.uploadedAtUtc).toLocaleString('ru-RU')}</td>
                    <td style={{ padding: '8px 10px', textAlign: 'right' }}>
                      <button onClick={() => api.download('/api/invoices/attachments/' + a.id + '/download', a.fileName)} title="Скачать" style={{ background: 'transparent', border: 'none', color: 'var(--text-3)', cursor: 'pointer', padding: 4 }}>
                        <Icon name="download" size={14}/>
                      </button>
                    </td>
                  </tr>
                ))}
                {attachments.data && attachments.data.length === 0 && <tr><td colSpan={4} style={{ padding: 12, color: 'var(--text-3)' }}>Документов пока нет</td></tr>}
                {attachments.isLoading && <tr><td colSpan={4} style={{ padding: 12, color: 'var(--text-3)' }}>Загрузка…</td></tr>}
              </tbody>
            </table>
          </div>

          <div style={{ marginBottom: 18 }}>
            <div style={{ fontSize: 11, color: 'var(--text-2)', textTransform: 'uppercase', letterSpacing: 0.5, fontWeight: 500, marginBottom: 10 }}>Жизненный цикл</div>
            {sel.status === 'Отменён' && <div style={{ color: 'var(--err)', fontSize: 12.5, marginBottom: 8 }}>Счёт отменён</div>}
            <div className="flow-strip">
              {INVOICE_FLOW.map((s, i, arr) => {
                const done = i < flowIdx, active = i === flowIdx
                return (
                  <Fragment key={s}>
                    <div style={{
                      flex: 1, padding: '10px 12px', borderRadius: 6, fontSize: 12, border: '1px solid',
                      display: 'flex', flexDirection: 'column', gap: 4,
                      background: active ? 'var(--accent-soft)' : done ? 'var(--surface)' : 'var(--surface-2)',
                      borderColor: active ? 'var(--accent)' : 'var(--border)',
                      color: active ? 'var(--accent)' : done ? 'var(--text)' : 'var(--text-3)',
                    }}>
                      <div style={{ fontSize: 10, fontWeight: 600, textTransform: 'uppercase', letterSpacing: 0.4, color: 'var(--text-3)' }}>Этап {i + 1}</div>
                      <div style={{ fontWeight: active || done ? 500 : 400 }}>{INVOICE_STATUS_LABEL[s]}</div>
                      <div style={{ fontSize: 10.5, color: 'var(--text-3)' }}>{done ? 'готово' : active ? 'идёт' : '—'}</div>
                    </div>
                    {i < arr.length - 1 && <div className="flow-chevron">›</div>}
                  </Fragment>
                )
              })}
            </div>
          </div>

          {(canAdvance || canCancel) && (
            <div style={{ display: 'flex', gap: 8, alignItems: 'center', flexWrap: 'wrap' }}>
              {canAdvance && next && (
                <Btn kind="primary" size="sm" icon="arrowR" disabled={setStatus.isPending} onClick={() => { setErr(undefined); setStatus.mutate({ id: sel.id, status: next }) }}>
                  {next === 'Согласован' ? 'Согласован' : `Следующая стадия: ${INVOICE_STATUS_LABEL[next]}`}
                </Btn>
              )}
              {canCancel && (
                <Btn kind="danger" size="sm" icon="x" disabled={setStatus.isPending} onClick={() => { setErr(undefined); setStatus.mutate({ id: sel.id, status: 'Отменён' }) }}>Отменить счёт</Btn>
              )}
            </div>
          )}
        </div>
      )}
    </Shell>
  )
}
