import { useAuth } from 'react-oidc-context'
import { realmRoles } from './roles'
import { mapBackendRole } from '../data/constants'
import { appRoles, canAccess, homePath, primaryRole } from './access'

/**
 * Роли из access-token + флаги по ТЗ UC-01..09.
 * Меню и маршруты режутся в access.ts; мутации на API — политиками write/approve/postPayment/receive.
 */
export function useRoles() {
  const auth = useAuth()
  const raw = realmRoles(auth.user?.access_token)
  const roles = appRoles(raw)
  const isAdmin = roles.includes('admin')
  return {
    roles,
    role: mapBackendRole(roles),
    primary: primaryRole(roles),
    home: homePath(roles),
    canWrite: isAdmin || roles.includes('manager'),
    canAdmin: isAdmin,
    canApprove: isAdmin || roles.includes('commercial'),
    canPostPayment: isAdmin || roles.includes('accounting'),
    canReceive: isAdmin || roles.includes('warehouse'),
    canAccess: (path: string) => canAccess(path, roles),
  }
}
