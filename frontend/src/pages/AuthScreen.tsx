import { useState, type FormEvent } from 'react'
import { User } from 'oidc-client-ts'
import { useAuth } from 'react-oidc-context'
import { userManager } from '../auth/oidc'
import { decodeJwt } from '../auth/roles'
import { AppMark, Btn } from '../ui/atoms'
import { Field } from '../ui/form'
import { inputStyle } from '../ui/styles'
import { PulseDots } from '../ui/feedback'

type TokenBundle = {
  accessToken: string
  refreshToken?: string
  idToken?: string
  expiresIn: number
  tokenType: string
  scope: string
}

type Mode = 'login' | 'register'

export function AuthScreen() {
  const auth = useAuth()
  const [mode, setMode] = useState<Mode>('login')
  const [username, setUsername] = useState('')
  const [password, setPassword] = useState('')
  const [confirm, setConfirm] = useState('')
  const [email, setEmail] = useState('')
  const [firstName, setFirstName] = useState('')
  const [lastName, setLastName] = useState('')
  const [company, setCompany] = useState('')
  const [error, setError] = useState<string>()
  const [busy, setBusy] = useState(false)

  async function login(user: string, pass: string) {
    const r = await fetch('/api/auth/login', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ username: user, password: pass }),
    })
    const body = await r.json().catch(() => ({ message: r.statusText })) as { ok?: boolean; message?: string; tokens?: TokenBundle }
    if (!r.ok || !body.tokens) throw new Error(body.message || `HTTP ${r.status}`)
    await applyTokens(body.tokens)
  }

  async function onLogin(e: FormEvent) {
    e.preventDefault()
    setError(undefined)
    setBusy(true)
    try {
      await login(username.trim(), password)
    } catch (err) {
      setError(human(err, 'Неверный логин или пароль.'))
    } finally { setBusy(false) }
  }

  async function onRegister(e: FormEvent) {
    e.preventDefault()
    setError(undefined)
    if (password !== confirm) { setError('Пароли не совпадают.'); return }
    setBusy(true)
    try {
      const r = await fetch('/api/auth/register', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          username: username.trim(),
          email: email.trim(),
          firstName: firstName.trim(),
          lastName: lastName.trim(),
          password,
          company: company.trim() || null,
        }),
      })
      const body = await r.json().catch(() => ({ message: r.statusText })) as { ok?: boolean; message?: string }
      if (!r.ok) throw new Error(body.message || `HTTP ${r.status}`)
      await login(username.trim(), password)
    } catch (err) {
      setError(human(err, 'Регистрация не прошла.'))
    } finally { setBusy(false) }
  }

  return (
    <div className="auth-screen">
      <header className="auth-bar">
        <AppMark size={28} />
        <div>
          <div className="brand-name">Подбор</div>
          <div className="brand-sub">коммерческое предложение</div>
        </div>
      </header>

      <div className="auth-form">
        <div className="auth-card page-enter">
          <h1>{mode === 'login' ? 'Вход в кабинет' : 'Регистрация'}</h1>
          <p className="auth-lead">Прайс поставщика и заявка собираются в КП.</p>
          <div className="auth-tabs">
            {(['login', 'register'] as const).map(m => (
              <button key={m} type="button" className={mode === m ? 'is-on' : ''}
                onClick={() => { setMode(m); setError(undefined) }}>
                <span>{m === 'login' ? 'Вход' : 'Регистрация'}</span>
                <small>{m === 'login' ? 'есть логин' : 'роль просмотра'}</small>
              </button>
            ))}
          </div>

          {error && (
            <div style={{ fontSize: 12.5, color: 'var(--err)', background: 'var(--err-bg)', border: '1px solid var(--err)', padding: '8px 10px', marginBottom: 12 }}>
              {error}
            </div>
          )}

          {mode === 'login' ? (
            <form onSubmit={onLogin} style={{ display: 'grid', gap: 12 }}>
              <Field label="Логин"><input value={username} onChange={e => setUsername(e.target.value)} autoComplete="username" style={inputStyle} placeholder="manager" /></Field>
              <Field label="Пароль"><input type="password" value={password} onChange={e => setPassword(e.target.value)} autoComplete="current-password" style={inputStyle} /></Field>
              <Btn kind="primary" size="md" type="submit" disabled={busy || !username.trim() || !password} style={{ width: '100%', justifyContent: 'center' }}>
                {busy ? <PulseDots invert label="Входим" /> : 'Войти'}
              </Btn>
              <details style={{ fontSize: 11.5, color: 'var(--text-3)' }}>
                <summary style={{ cursor: 'pointer' }}>Учётки стенда</summary>
                <p style={{ margin: '8px 0 0', lineHeight: 1.45 }}>
                  Пароль совпадает с логином: <span className="mono">manager</span>, <span className="mono">commercial</span>, <span className="mono">accounting</span>, <span className="mono">warehouse</span>, <span className="mono">admin</span>, <span className="mono">viewer</span>.
                </p>
              </details>
            </form>
          ) : (
            <form onSubmit={onRegister} style={{ display: 'grid', gap: 12 }}>
              <div className="form-cols" style={{ gridTemplateColumns: '1fr 1fr' }}>
                <Field label="Имя"><input value={firstName} onChange={e => setFirstName(e.target.value)} style={inputStyle} /></Field>
                <Field label="Фамилия"><input value={lastName} onChange={e => setLastName(e.target.value)} style={inputStyle} /></Field>
              </div>
              <Field label="Логин"><input value={username} onChange={e => setUsername(e.target.value)} autoComplete="username" style={inputStyle} placeholder="ivanov" /></Field>
              <Field label="Email"><input type="email" value={email} onChange={e => setEmail(e.target.value)} autoComplete="email" style={inputStyle} placeholder="ivanov@company.ru" /></Field>
              <Field label="Организация"><input value={company} onChange={e => setCompany(e.target.value)} style={inputStyle} placeholder="необязательно" /></Field>
              <Field label="Пароль"><input type="password" value={password} onChange={e => setPassword(e.target.value)} autoComplete="new-password" style={inputStyle} /></Field>
              <Field label="Ещё раз"><input type="password" value={confirm} onChange={e => setConfirm(e.target.value)} autoComplete="new-password" style={inputStyle} /></Field>
              <p style={{ margin: 0, fontSize: 12, color: 'var(--text-2)', lineHeight: 1.45 }}>
                Новая учётка получает права просмотра. Роль руководителя выдаёт администратор.
              </p>
              <Btn kind="primary" size="md" type="submit" disabled={busy} style={{ width: '100%', justifyContent: 'center' }}>
                {busy ? <PulseDots invert label="Создаём учётку" /> : 'Создать учётку'}
              </Btn>
            </form>
          )}

          <button type="button" onClick={() => auth.signinRedirect()} style={{
            marginTop: 16, width: '100%', background: 'transparent', border: 'none',
            color: 'var(--text-3)', fontSize: 12, cursor: 'pointer', textDecoration: 'underline',
          }}>
            Войти через учётку организации
          </button>
          {(window.location.hostname === 'localhost' || window.location.hostname === '127.0.0.1') && (
            <p className="auth-phone-hint">
              С телефона в той же Wi‑Fi откройте адрес <b>Network</b> из терминала Vite
              (не localhost) — например <span className="mono">http://192.168.0.10:5173</span>.
              В Safari: Поделиться → На экран «Домой».
            </p>
          )}
        </div>
      </div>
    </div>
  )
}

async function applyTokens(tokens: TokenBundle) {
  const raw = decodeJwt(tokens.idToken || tokens.accessToken)
  const preferred = String(raw.preferred_username ?? raw.sub ?? '')
  const given = String(raw.given_name ?? '').trim()
  const family = String(raw.family_name ?? '').trim()
  const full = [given, family].filter(Boolean).join(' ')
  const profile = {
    sub: String(raw.sub ?? preferred),
    name: full || String(raw.name ?? preferred),
    preferred_username: preferred,
    given_name: given || undefined,
    family_name: family || undefined,
    email: typeof raw.email === 'string' ? raw.email : undefined,
  } as User['profile']
  const user = new User({
    access_token: tokens.accessToken,
    refresh_token: tokens.refreshToken,
    id_token: tokens.idToken,
    token_type: tokens.tokenType || 'Bearer',
    scope: tokens.scope || 'openid profile email',
    expires_at: Math.floor(Date.now() / 1000) + (tokens.expiresIn || 3600),
    profile,
  })
  await userManager.storeUser(user)
  userManager.events.load(user)
}

function human(err: unknown, fallback: string) {
  const m = err instanceof Error ? err.message : ''
  if (!m) return fallback
  if (/invalid_grant|401/i.test(m)) return 'Неверный логин или пароль.'
  if (/Failed to fetch|NetworkError|ECONNREFUSED/i.test(m)) return 'Нет связи с сервером входа. Проверьте, что сервис авторизации запущен.'
  return m
}
