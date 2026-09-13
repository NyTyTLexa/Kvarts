import React, { useEffect, useMemo, useState } from 'react'
import { Link, useLocation } from 'react-router-dom'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useAuth } from 'react-oidc-context'
import { AppMark, Icon } from '../ui/atoms'
import { PulseDots } from '../ui/feedback'
import { useRoles } from '../auth/useRoles'
import { isNavActive, navFor, phoneTabsFor } from '../auth/access'
import { useApi } from '../api/useApi'
import { THEMES, useTheme } from '../ui/theme'

type NotificationDto = {
  id: string
  type: string
  title: string
  message: string
  relatedEntityType?: string
  relatedEntityId?: string
  createdAtUtc: string
  readAtUtc?: string
  isRead: boolean
}

function ThemeSwitch() {
  const [id, setTheme] = useTheme()
  const [open, setOpen] = useState(false)
  const cur = THEMES.find(t => t.id === id) ?? THEMES[0]
  useEffect(() => {
    if (!open) return
    const close = () => setOpen(false)
    window.addEventListener('click', close)
    return () => window.removeEventListener('click', close)
  }, [open])
  return (
    <div className="theme-switch">
      <button type="button" className="theme-switch-btn" title="Цвет темы" aria-haspopup="listbox" aria-expanded={open}
        onClick={e => { e.stopPropagation(); setOpen(v => !v) }}>
        <span className="theme-dot" style={{ background: cur.swatch }} />
        <span className="theme-label">{cur.label}</span>
      </button>
      {open && (
        <div className="theme-menu pop-enter" role="listbox" aria-label="Цвет темы" onClick={e => e.stopPropagation()}>
          {THEMES.map(t => (
            <button key={t.id} type="button" role="option" aria-selected={id === t.id}
              className={id === t.id ? 'is-on' : ''}
              onClick={() => { setTheme(t.id); setOpen(false) }}>
              <span className="theme-dot" style={{ background: t.swatch }} />
              <span>
                <span style={{ display: 'block', fontWeight: 600 }}>{t.label}</span>
                <span style={{ display: 'block', fontSize: 11.5, color: 'var(--text-3)' }}>{t.hint}</span>
              </span>
            </button>
          ))}
        </div>
      )}
    </div>
  )
}

function initials(name: string) {
  const parts = name.trim().split(/\s+/)
  return ((parts[0]?.[0] ?? '') + (parts[1]?.[0] ?? '')).toUpperCase() || name.slice(0, 2).toUpperCase()
}

function notificationLink(n: NotificationDto) {
  if (n.relatedEntityType === 'Approval') return '/approval'
  if (n.relatedEntityType === 'Invoice') return '/invoice'
  if (n.relatedEntityType === 'GoodsReceipt') return '/warehouse'
  if (n.relatedEntityType === 'Order') return '/orders'
  return undefined
}

function NotificationBell() {
  const api = useApi()
  const auth = useAuth()
  const { canAccess } = useRoles()
  const queryClient = useQueryClient()
  const [open, setOpen] = useState(false)
  const signedIn = !!auth.user?.access_token

  const count = useQuery({
    queryKey: ['notifications-count'],
    queryFn: () => api.get<{ unread: number }>('/api/notifications/count'),
    refetchInterval: 30_000,
    enabled: signedIn,
  })

  const list = useQuery({
    queryKey: ['notifications'],
    queryFn: () => api.get<NotificationDto[]>('/api/notifications'),
    enabled: open && signedIn,
  })

  const markRead = useMutation({
    mutationFn: (id: string) => api.post<void>(`/api/notifications/${id}/read`),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['notifications'] })
      queryClient.invalidateQueries({ queryKey: ['notifications-count'] })
    },
  })

  const markAllRead = useMutation({
    mutationFn: () => api.post<void>('/api/notifications/read-all'),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['notifications'] })
      queryClient.invalidateQueries({ queryKey: ['notifications-count'] })
    },
  })

  const unread = count.data?.unread ?? 0

  return (
    <div style={{ position: 'relative' }}>
      <button type="button" title="Уведомления" aria-label="Уведомления" aria-expanded={open} className="icon-btn" onClick={() => setOpen(v => !v)} style={{
        position: 'relative', width: 32, height: 32, border: '1px solid var(--border)',
        background: 'var(--surface)', color: 'var(--text-2)', borderRadius: 7, display: 'flex', alignItems: 'center', justifyContent: 'center', cursor: 'pointer',
      }}>
        <Icon name="bell" size={15}/>
        {unread > 0 && (
          <span className="tnum" style={{
            position: 'absolute', top: -5, right: -5, minWidth: 17, height: 17, padding: '0 5px',
            background: 'var(--accent)', color: 'var(--on-accent)', fontSize: 10, fontWeight: 600, lineHeight: '17px', textAlign: 'center',
            borderRadius: 999,
          }}>{unread > 9 ? '9+' : unread}</span>
        )}
      </button>

      {open && (
        <div className="notify-pop pop-enter">
          <div style={{ display: 'flex', alignItems: 'center', padding: '12px 14px', borderBottom: '1px solid var(--border)' }}>
            <div style={{ fontWeight: 600 }}>Уведомления</div>
            <div style={{ flex: 1 }}/>
            <button onClick={() => markAllRead.mutate()} disabled={unread === 0} style={{ background: 'transparent', border: 'none', color: unread ? 'var(--accent)' : 'var(--text-3)', cursor: unread ? 'pointer' : 'default', fontSize: 12, fontWeight: 500 }}>
              Прочитать все
            </button>
          </div>

          <div style={{ maxHeight: 420, overflowY: 'auto' }}>
            {list.isLoading && <div style={{ padding: 18 }}><PulseDots label="Уведомления" /></div>}
            {list.isError && <div style={{ padding: 14, color: 'var(--err)', fontSize: 13 }}>
              Не удалось загрузить уведомления{(list.error as Error)?.message ? `: ${(list.error as Error).message}` : '.'}
            </div>}
            {list.data?.map(n => {
              const toRaw = notificationLink(n)
              const to = toRaw && canAccess(toRaw) ? toRaw : undefined
              const content = (
                <div style={{ flex: 1, minWidth: 0 }}>
                  <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
                    {!n.isRead && <span style={{ width: 7, height: 7, background: 'var(--accent)', flexShrink: 0 }}/>} 
                    <div style={{ fontWeight: 600, fontSize: 13 }}>{n.title}</div>
                  </div>
                  <div style={{ marginTop: 3, color: 'var(--text-2)', fontSize: 12.5, lineHeight: 1.45 }}>{n.message}</div>
                  <div style={{ marginTop: 6, color: 'var(--text-3)', fontSize: 11.5 }}>{new Date(n.createdAtUtc).toLocaleString('ru-RU')}</div>
                </div>
              )
              return (
                <div key={n.id} className="notify-row" style={{ display: 'flex', gap: 10, padding: '11px 14px', borderBottom: '1px solid var(--border)', background: n.isRead ? 'transparent' : 'var(--accent-soft)' }}>
                  {to ? <Link to={to} onClick={() => { markRead.mutate(n.id); setOpen(false) }} style={{ flex: 1, minWidth: 0, color: 'inherit' }}>{content}</Link> : content}
                  {!n.isRead && (
                    <button title="Прочитать" onClick={() => markRead.mutate(n.id)} style={{ alignSelf: 'start', background: 'transparent', border: 'none', color: 'var(--text-3)', cursor: 'pointer', padding: 3 }}>
                      <Icon name="check" size={14}/>
                    </button>
                  )}
                </div>
              )
            })}
            {list.data && list.data.length === 0 && <div style={{ padding: 18, color: 'var(--text-3)', fontSize: 13 }}>Новых событий пока нет.</div>}
          </div>
        </div>
      )}
    </div>
  )
}

export function Shell({ breadcrumb, actions, children }:
  { breadcrumb?: string[]; actions?: React.ReactNode; children: React.ReactNode }) {
  const { pathname } = useLocation()
  const auth = useAuth()
  const { roles, role } = useRoles()
  const roleKey = roles.join(',')
  const nav = useMemo(() => navFor(roles), [roleKey])
  const phoneTabs = useMemo(() => phoneTabsFor(roles), [roleKey])
  const [navOpen, setNavOpen] = useState(false)
  const [openGroups, setOpenGroups] = useState<Record<string, boolean>>({
    'Контур КП': true, 'Справочники': true, 'Согласование': true, 'Система': true,
  })
  useEffect(() => {
    const groups = navFor(roles)
    const g = groups.find(x => x.items.some(it => isNavActive(pathname, it.to)))
    const title = g?.title ?? groups[0]?.title
    if (title) setOpenGroups(s => s[title] ? s : { ...s, [title]: true })
    setNavOpen(false)
  }, [pathname, roleKey])
  useEffect(() => {
    if (!navOpen) return
    const onKey = (e: KeyboardEvent) => { if (e.key === 'Escape') setNavOpen(false) }
    window.addEventListener('keydown', onKey)
    return () => window.removeEventListener('keydown', onKey)
  }, [navOpen])

  const profile = auth.user?.profile
  const name = [profile?.given_name, profile?.family_name].filter(Boolean).join(' ')
    || (profile?.name as string | undefined)
    || (profile?.preferred_username as string | undefined)
    || 'Пользователь'

  const crumbs = breadcrumb || ['Проекты']
  const lastCrumb = crumbs[crumbs.length - 1]
  const onPhoneTab = phoneTabs.some(it => isNavActive(pathname, it.to))

  return (
    <div className="app-frame">
      <a className="skip-link" href="#main">К содержанию</a>
      {navOpen && <div className="nav-scrim" onClick={() => setNavOpen(false)} />}
      <aside className={'app-nav' + (navOpen ? ' is-open' : '')}>
        <div className="app-nav-brand">
          <AppMark size={28} />
          <div style={{ minWidth: 0 }}>
            <div className="brand-name">Подбор</div>
            <div className="brand-sub">{role.short} · {role.label}</div>
          </div>
          <button type="button" className="nav-close icon-btn" aria-label="Закрыть меню" onClick={() => setNavOpen(false)}>
            <Icon name="x" size={16} />
          </button>
        </div>

        <nav style={{ flex: 1 }} aria-label="Разделы">
          {nav.map(g => (
            <details
              key={g.title}
              className="nav-group"
              open={!!openGroups[g.title]}
              onToggle={e => {
                const next = (e.currentTarget as HTMLDetailsElement).open
                setOpenGroups(s => s[g.title] === next ? s : { ...s, [g.title]: next })
              }}
            >
              <summary className="nav-group-title">
                <span className="chev"><Icon name="chevR" size={11}/></span>
                {g.title}
              </summary>
              {g.items.map(it => {
                const active = isNavActive(pathname, it.to)
                return (
                  <Link key={it.id} to={it.to} className={'nav-link' + (active ? ' is-active' : '')} style={{
                    display: 'flex', alignItems: 'flex-start', gap: 9,
                    padding: '8px 10px', margin: '1px 8px',
                  }}>
                    <Icon name={it.icon} size={15}/>
                    <span style={{ flex: 1, minWidth: 0, lineHeight: 1.2 }}>
                      <span className="nav-link-label">{it.label}</span>
                      <span className="nav-link-hint">{it.hint}</span>
                    </span>
                  </Link>
                )
              })}
            </details>
          ))}
        </nav>

        <div style={{ padding: '8px 12px', marginTop: 6, borderTop: '1px solid rgba(255,255,255,0.08)' }}>
          <div style={{ display: 'flex', alignItems: 'center', gap: 8, padding: 4 }}>
            <div className="user-plate">{initials(name)}</div>
            <div style={{ flex: 1, minWidth: 0 }}>
              <div style={{ fontSize: 12, fontWeight: 500, color: 'var(--sidebar-text-strong)', whiteSpace: 'nowrap', overflow: 'hidden', textOverflow: 'ellipsis' }}>{name}</div>
              <div style={{ fontSize: 10.5, color: 'var(--sidebar-text)' }}>{role.label}</div>
            </div>
            <button type="button" title="Выйти" aria-label="Выйти" className="icon-btn" onClick={() => auth.signoutRedirect()} style={{ background: 'transparent', border: 'none', color: 'var(--sidebar-text)', cursor: 'pointer', padding: 4, display: 'flex' }}>
              <Icon name="logout" size={15}/>
            </button>
          </div>
        </div>
      </aside>

      <div className="app-body">
        <header className="app-top">
          <button type="button" className="nav-toggle icon-btn" aria-label="Меню" aria-expanded={navOpen}
            onClick={() => setNavOpen(v => !v)}>
            <Icon name="menu" size={16} />
          </button>
          <div className="crumb">
            <span className="crumb-full">
              {crumbs.map((bc, i, arr) => (
                <React.Fragment key={i}>
                  <span className={i === arr.length - 1 ? 'is-here' : undefined}>{bc}</span>
                  {i < arr.length - 1 && <span className="crumb-sep">/</span>}
                </React.Fragment>
              ))}
            </span>
            <span className="crumb-short">{lastCrumb}</span>
          </div>
          {actions ? <div className="app-top-actions">{actions}</div> : null}
          <div className="app-top-tools">
            <ThemeSwitch />
            <NotificationBell />
          </div>
        </header>

        <main id="main" className="app-main page-enter">{children}</main>
        {actions ? <div className="phone-actionbar">{actions}</div> : null}
        <nav className="phone-dock" aria-label="Разделы на телефоне">
          {phoneTabs.map(it => (
            <Link key={it.id} to={it.to} className={'phone-tab' + (isNavActive(pathname, it.to) ? ' is-on' : '')}>
              <Icon name={it.icon} size={18} />
              <span>{it.label}</span>
            </Link>
          ))}
          <button type="button" className={'phone-tab' + (navOpen || !onPhoneTab ? ' is-on' : '')}
            aria-label="Ещё разделы" aria-expanded={navOpen}
            onClick={() => setNavOpen(v => !v)}>
            <Icon name="menu" size={18} />
            <span>Ещё</span>
          </button>
        </nav>
      </div>
    </div>
  )
}
