import { useNavigate } from 'react-router-dom'
import { Shell } from '../../components/Shell'
import { PageHead } from '../../components/PageHead'
import { Btn } from '../../ui/atoms'
import { Field, SectionTitle } from '../../ui/form'
import { inputStyle } from '../../ui/styles'

const TAGS = [
  { t: 'СКС', bg: '#fed7aa', fg: '#9a3412' },
  { t: 'сетевое ядро', bg: '#dbeafe', fg: '#1e40af' },
  { t: 'Q2', bg: '#bbf7d0', fg: '#14532d' },
]

export function NewProject() {
  const nav = useNavigate()
  return (
    <Shell breadcrumb={['Проекты', 'Новый проект']} actions={<>
      <Btn size="sm" kind="ghost" onClick={() => nav('/')}>Отмена</Btn>
      <Btn size="sm" kind="primary" icon="upload" onClick={() => nav('/specifications')}>Создать и загрузить перечень</Btn>
    </>}>
      <div style={{ maxWidth: 920 }}>
        <PageHead icon="folder" title="Новый проект" badge={{ text: 'демо-экран', kind: 'mock' }}
          subtitle="Карточка проекта перед загрузкой перечня оборудования. Поля — мок по макету «Кварц»." />

        <SectionTitle>Основное</SectionTitle>
        <div style={{ display: 'grid', gridTemplateColumns: '2fr 1fr', gap: 12 }}>
          <Field label="Название проекта"><input style={inputStyle} placeholder="СКС и сетевое ядро ТЦ «Орбита»" /></Field>
          <Field label="Код проекта"><input style={inputStyle} placeholder="P-2026-118" /></Field>
        </div>

        <SectionTitle>Заказчик</SectionTitle>
        <div style={{ display: 'grid', gridTemplateColumns: '2fr 1fr', gap: 12 }}>
          <Field label="Организация"><input style={inputStyle} placeholder="ООО «ТЦ Орбита Девелопмент»" /></Field>
          <Field label="ИНН"><input style={inputStyle} placeholder="7700000000" /></Field>
        </div>

        <SectionTitle>Тендер и сроки</SectionTitle>
        <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr 1fr', gap: 12 }}>
          <Field label="Номер тендера"><input style={inputStyle} placeholder="ЭА-2026-0418" /></Field>
          <Field label="Дедлайн КП"><input style={inputStyle} type="date" /></Field>
          <Field label="НМЦ, ₽"><input style={inputStyle} inputMode="numeric" placeholder="14 820 000" /></Field>
        </div>

        <SectionTitle>Команда</SectionTitle>
        <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 12 }}>
          <Field label="Руководитель проекта">
            <select style={inputStyle}><option>Смирнов А.</option><option>Васильева Е.</option><option>Карпов Д.</option></select>
          </Field>
          <Field label="Коммерческий блок">
            <select style={inputStyle}><option>Васильева Е.</option><option>Гончарова Л.</option></select>
          </Field>
        </div>

        <SectionTitle>Тэги</SectionTitle>
        <div style={{ display: 'flex', gap: 6, alignItems: 'center' }}>
          {TAGS.map(t => <span key={t.t} style={{ padding: '3px 10px', borderRadius: 999, background: t.bg, color: t.fg, fontSize: 12, fontWeight: 500 }}>{t.t}</span>)}
          <span style={{ padding: '3px 10px', borderRadius: 999, border: '1px dashed var(--border-strong)', color: 'var(--text-3)', fontSize: 12, cursor: 'pointer' }}>+ тэг</span>
        </div>
      </div>
    </Shell>
  )
}
