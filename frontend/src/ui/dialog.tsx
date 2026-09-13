import { useEffect, useRef, type ReactNode } from 'react'
import { Icon } from './atoms'

/**
 * Модальное окно поверх страницы.
 *
 * Раньше формы создания разворачивались прямо в разметке и раздвигали таблицу под собой:
 * список прыгал, а на длинных страницах форма уезжала за экран.
 *
 * Сделано на нативном <dialog> с showModal(), а не на своём div с position: fixed.
 * Браузер тогда сам берёт на себя то, что руками пишется плохо и почти всегда с ошибками:
 * удержание фокуса внутри окна, возврат фокуса на кнопку-инициатор при закрытии,
 * закрытие по Escape, подложку и исключение фона из навигации с клавиатуры.
 */
export function Dialog({ open, onClose, title, subtitle, children, footer, width = 520 }: {
  open: boolean
  onClose: () => void
  title: string
  subtitle?: string
  children: ReactNode
  footer?: ReactNode
  width?: number
}) {
  const ref = useRef<HTMLDialogElement>(null)

  useEffect(() => {
    const el = ref.current
    if (!el) return
    if (open && !el.open) el.showModal()
    else if (!open && el.open) el.close()
  }, [open])

  // Escape и системное закрытие идут мимо React — синхронизируем состояние обратно.
  useEffect(() => {
    const el = ref.current
    if (!el) return
    const onNativeClose = () => onClose()
    el.addEventListener('close', onNativeClose)
    return () => el.removeEventListener('close', onNativeClose)
  }, [onClose])

  return (
    <dialog
      ref={ref}
      className="dlg"
      style={{ width, maxWidth: 'calc(100vw - 32px)' }}
      aria-labelledby="dlg-title"
      // Клик по подложке закрывает: событие приходит на сам dialog,
      // потому что содержимое лежит во вложенном блоке.
      onClick={e => { if (e.target === ref.current) onClose() }}
    >
      <form method="dialog" className="dlg-body" onSubmit={e => e.preventDefault()}>
        <div className="dlg-head">
          {/* flex: 1 — иначе заголовок не растягивается и крестик липнет к тексту
              вместо правого края окна. */}
          <div style={{ flex: 1, minWidth: 0 }}>
            <div id="dlg-title" className="dlg-title">{title}</div>
            {subtitle && <div className="dlg-sub">{subtitle}</div>}
          </div>
          <button type="button" className="icon-btn" aria-label="Закрыть" onClick={onClose}>
            <Icon name="x" size={16} />
          </button>
        </div>

        <div className="dlg-content">{children}</div>

        {footer && <div className="dlg-foot">{footer}</div>}
      </form>
    </dialog>
  )
}
