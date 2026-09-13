import { useMemo } from 'react'
import { useAuth } from 'react-oidc-context'
import { createApi } from './client'

/** API-клиент, привязанный к access-token текущего пользователя. */
export function useApi() {
  const auth = useAuth()
  return useMemo(() => createApi(() => auth.user?.access_token), [auth.user?.access_token])
}
