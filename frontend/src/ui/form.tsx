import type { ReactNode } from 'react'

export function Field({ label, children }: { label: string; children: ReactNode }) {
  return (
    <label style={{ display: 'block' }}>
      <div className="field-label">{label}</div>
      {children}
    </label>
  )
}

export function SectionTitle({ children }: { children: ReactNode }) {
  return (
    <div className="field-label" style={{ margin: '20px 0 10px' }}>{children}</div>
  )
}
