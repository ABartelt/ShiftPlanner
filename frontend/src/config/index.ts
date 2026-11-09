/** Runtime configuration, loaded from /config.json before the app mounts. */
export interface AppConfig {
  /** Keycloak realm URL, e.g. https://auth.example.org/realms/muddi */
  authority: string
  /** Public OIDC client id (code flow with PKCE). */
  clientId: string
  /** Base URL of the ShiftPlanner API, absolute or relative ("/api" behind the dev proxy). */
  apiBaseUrl: string
  /** IANA time zone used for every displayed date. */
  timeZone: string
  scopes: string
  brand: {
    name: string
    primaryColor: string
    /** Logo for the dark theme, e.g. the white tohoop wordmark. */
    logoUrl: string
    /** Logo for the light theme, e.g. the black wordmark; falls back to logoUrl. */
    logoLightUrl: string
    footer: string
    footerUrl: string
  }
}

const defaults: AppConfig = {
  authority: '',
  clientId: 'shift-planner-frontend',
  apiBaseUrl: '/api',
  timeZone: 'Europe/Berlin',
  scopes: 'openid profile email',
  brand: {
    name: 'Schichtplaner',
    primaryColor: '',
    logoUrl: '',
    logoLightUrl: '',
    footer: '',
    footerUrl: '',
  },
}

let current: AppConfig | null = null

export async function loadConfig(): Promise<AppConfig> {
  const response = await fetch('/config.json', { cache: 'no-cache' })
  if (!response.ok) throw new Error(`config.json: ${response.status}`)
  const json = (await response.json()) as Partial<AppConfig>
  current = {
    ...defaults,
    ...json,
    apiBaseUrl: (json.apiBaseUrl ?? defaults.apiBaseUrl).replace(/\/$/, ''),
    brand: { ...defaults.brand, ...json.brand },
  }
  if (!current.authority) throw new Error('config.json: "authority" is required')
  if (current.brand.primaryColor) {
    document.documentElement.style.setProperty('--primary', current.brand.primaryColor)
  }
  return current
}

export function useConfig(): AppConfig {
  if (!current) throw new Error('config not loaded')
  return current
}
