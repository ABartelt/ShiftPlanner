import { computed, ref } from 'vue'
import type { User } from 'oidc-client-ts'
import { getUserManager, ROLE_ADMIN, ROLE_SUPER_ADMIN, rolesOf } from './oidc'

const user = ref<User | null>(null)
const ready = ref(false)

export interface Profile {
  sub: string
  firstName: string
  fullName: string
  email: string
}

function profileOf(u: User | null): Profile | null {
  if (!u) return null
  const p = u.profile
  const first =
    (p.given_name as string | undefined) ?? (p.preferred_username as string | undefined) ?? ''
  const last = (p.family_name as string | undefined) ?? ''
  return {
    sub: p.sub,
    firstName: first,
    fullName: [first, last].filter(Boolean).join(' '),
    email: (p.email as string | undefined) ?? '',
  }
}

/** Reads the stored session once and wires the oidc-client events. */
export async function initAuth(): Promise<void> {
  const m = getUserManager()
  m.events.addUserLoaded((u) => {
    user.value = u
  })
  m.events.addUserUnloaded(() => {
    user.value = null
  })
  m.events.addSilentRenewError(() => {
    // The refresh token is gone (session expired in Keycloak): the next API call sends the user to login.
    user.value = null
  })
  m.events.addAccessTokenExpired(() => {
    user.value = null
  })
  try {
    const stored = await m.getUser()
    user.value = stored && !stored.expired ? stored : null
    if (stored?.expired && stored.refresh_token) {
      user.value = await m.signinSilent().catch(() => null)
    }
  } finally {
    ready.value = true
  }
}

export function useAuth() {
  const isAuthenticated = computed(() => !!user.value && !user.value.expired)
  const roles = computed(() => rolesOf(user.value))
  const isSuperAdmin = computed(() => roles.value.includes(ROLE_SUPER_ADMIN))
  const isAdmin = computed(() => isSuperAdmin.value || roles.value.includes(ROLE_ADMIN))
  const profile = computed(() => profileOf(user.value))

  async function login(returnTo?: string): Promise<void> {
    await getUserManager().signinRedirect({
      state: { returnTo: returnTo ?? window.location.pathname + window.location.search },
    })
  }

  async function logout(): Promise<void> {
    user.value = null
    await getUserManager().signoutRedirect()
  }

  /** Returns a valid access token or null when the session is gone. */
  async function getAccessToken(): Promise<string | null> {
    const m = getUserManager()
    let u = user.value ?? (await m.getUser())
    if (u && u.expired && u.refresh_token) {
      u = await m.signinSilent().catch(() => null)
      user.value = u
    }
    if (!u || u.expired) return null
    return u.access_token
  }

  return {
    user,
    ready,
    isAuthenticated,
    roles,
    isAdmin,
    isSuperAdmin,
    profile,
    login,
    logout,
    getAccessToken,
  }
}
