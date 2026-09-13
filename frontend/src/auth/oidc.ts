import { UserManager, WebStorageStateStore } from 'oidc-client-ts'

export const KEYCLOAK_REALM_URL =
  import.meta.env.VITE_KEYCLOAK_URL || 'http://localhost:8088/realms/procurement'

export const userManager = new UserManager({
  authority: KEYCLOAK_REALM_URL,
  client_id: 'procurement-api',
  redirect_uri: window.location.origin + '/',
  post_logout_redirect_uri: window.location.origin + '/',
  response_type: 'code',
  scope: 'openid profile email',
  userStore: new WebStorageStateStore({ store: window.localStorage }),
})
