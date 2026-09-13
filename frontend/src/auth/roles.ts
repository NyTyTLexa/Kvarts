// Разбор JWT. Payload в UTF-8 (кириллица в name/given_name) — не сырой atob.
export function decodeJwt(token?: string): Record<string, unknown> {
  if (!token) return {}
  try {
    const part = token.split('.')[1] ?? ''
    const b64 = part.replace(/-/g, '+').replace(/_/g, '/')
    const pad = b64 + '='.repeat((4 - (b64.length % 4)) % 4)
    const bytes = Uint8Array.from(atob(pad), c => c.charCodeAt(0))
    return JSON.parse(new TextDecoder('utf-8').decode(bytes))
  } catch {
    return {}
  }
}

const KNOWN = new Set(['admin', 'manager', 'commercial', 'accounting', 'warehouse', 'viewer'])

export function realmRoles(token?: string): string[] {
  const p = decodeJwt(token) as {
    realm_access?: { roles?: unknown }
    roles?: unknown
    resource_access?: Record<string, { roles?: unknown }>
  }
  const bag = new Set<string>()
  const add = (xs: unknown) => {
    if (!Array.isArray(xs)) return
    for (const x of xs) if (typeof x === 'string' && KNOWN.has(x)) bag.add(x)
  }
  add(p.realm_access?.roles)
  add(p.roles)
  if (p.resource_access) for (const c of Object.values(p.resource_access)) add(c?.roles)
  return [...bag]
}
