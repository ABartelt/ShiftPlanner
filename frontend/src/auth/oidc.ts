import { User, UserManager, WebStorageStateStore, type UserManagerSettings } from 'oidc-client-ts'
import type { AppConfig } from '@/config'

export const ROLE_ADMIN = 'admin'
export const ROLE_SUPER_ADMIN = 'super-admin'

let manager: UserManager | null = null

export function createUserManager(config: AppConfig): UserManager {
  const origin = window.location.origin
  const settings: UserManagerSettings = {
    authority: config.authority,
    client_id: config.clientId,
    redirect_uri: `${origin}/auth/callback`,
    post_logout_redirect_uri: `${origin}/`,
    silent_redirect_uri: `${origin}/auth/silent`,
    response_type: 'code',
    scope: config.scopes,
    // Keep the session in sessionStorage like the previous client; refresh tokens renew silently.
    userStore: new WebStorageStateStore({ store: window.sessionStorage }),
    automaticSilentRenew: true,
    includeIdTokenInSilentRenew: false,
    monitorSession: false,
    loadUserInfo: false,
    revokeTokensOnSignout: true,
  }
  manager = new UserManager(settings)
  return manager
}

export function getUserManager(): UserManager {
  if (!manager) throw new Error('UserManager not created')
  return manager
}

/** Roles live in the access token ("roles" claim, mapped from the backend client roles). */
export function rolesOf(user: User | null | undefined): string[] {
  if (!user?.access_token) return []
  try {
    const payload = JSON.parse(
      atob(user.access_token.split('.')[1]!.replace(/-/g, '+').replace(/_/g, '/')),
    )
    const roles = payload.roles
    return Array.isArray(roles) ? roles.map(String) : []
  } catch {
    return []
  }
}
