// Тонкий клиент к бэкенду. Базовый путь пустой — ходим на same-origin /api,
// dev-сервер Vite проксирует на ASP.NET Core (:5165). Токен прокидывается из OIDC.

export class ApiError extends Error {
  status: number
  body?: unknown
  constructor(status: number, message: string, body?: unknown) {
    super(message)
    this.status = status
    this.body = body
  }
}

export type TokenGetter = () => string | undefined

function humanError(parsed: unknown, fallback: string) {
  if (typeof parsed === 'string' && parsed.trim()) return parsed.trim()
  if (parsed && typeof parsed === 'object') {
    const o = parsed as Record<string, unknown>
    if (typeof o.message === 'string' && o.message.trim()) return o.message
    if (typeof o.title === 'string' && o.title.trim()) return o.title
    if (typeof o.detail === 'string' && o.detail.trim()) return o.detail
    if (o.errors && typeof o.errors === 'object') {
      const first = Object.values(o.errors as Record<string, unknown>).flat()[0]
      if (typeof first === 'string') return first
      if (Array.isArray(first) && typeof first[0] === 'string') return first[0]
    }
  }
  if (fallback.endsWith('→ 403')) return 'Недостаточно прав. Войдите как manager или admin.'
  if (fallback.endsWith('→ 401')) return 'Сессия истекла. Выйдите и войдите снова.'
  if (fallback.endsWith('→ 413')) return 'Файл слишком большой (лимит 64 МБ).'
  return fallback
}

async function request<T>(token: string | undefined, method: string, path: string, body?: unknown): Promise<T> {
  const headers: Record<string, string> = {}
  if (token) headers['Authorization'] = 'Bearer ' + token
  let payload: BodyInit | undefined
  if (body instanceof FormData) {
    payload = body
  } else if (body !== undefined) {
    headers['Content-Type'] = 'application/json'
    payload = JSON.stringify(body)
  }

  const res = await fetch(path, { method, headers, body: payload })
  const text = await res.text()
  let parsed: unknown = undefined
  if (text) {
    try { parsed = JSON.parse(text) } catch { parsed = text }
  }
  if (!res.ok) {
    const fallback = `${method} ${path} → ${res.status}`
    if (res.status >= 500 && (parsed == null || parsed === '' || (typeof parsed === 'string' && /proxy error|ECONNREFUSED|Bad Gateway/i.test(parsed)))) {
      throw new ApiError(res.status, 'Сервер закупок не отвечает (порт 5165). Запустите API и обновите страницу.', parsed)
    }
    throw new ApiError(res.status, humanError(parsed, fallback), parsed)
  }
  return parsed as T
}

async function download(token: string | undefined, path: string, filename: string): Promise<void> {
  const headers: Record<string, string> = {}
  if (token) headers['Authorization'] = 'Bearer ' + token
  const res = await fetch(path, { headers })
  if (!res.ok) throw new ApiError(res.status, `GET ${path} → ${res.status}`)
  const blob = await res.blob()
  const url = URL.createObjectURL(blob)
  const a = document.createElement('a')
  a.href = url
  a.download = filename
  document.body.appendChild(a)
  a.click()
  a.remove()
  URL.revokeObjectURL(url)
}

export function createApi(getToken: TokenGetter) {
  return {
    get:  <T>(path: string) => request<T>(getToken(), 'GET', path),
    post: <T>(path: string, body?: unknown) => request<T>(getToken(), 'POST', path, body),
    put:  <T>(path: string, body?: unknown) => request<T>(getToken(), 'PUT', path, body),
    del:  <T>(path: string) => request<T>(getToken(), 'DELETE', path),
    download: (path: string, filename: string) => download(getToken(), path, filename),
  }
}

export type Api = ReturnType<typeof createApi>
