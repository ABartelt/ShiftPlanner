import { useConfig } from '@/config'
import { useAuth } from '@/auth/useAuth'

/** FastEndpoints validation body: { statusCode, message, errors: { field: [msg] } } */
export interface ValidationBody {
  statusCode?: number
  message?: string
  errors?: Record<string, string[]>
}

export class ApiError extends Error {
  constructor(
    message: string,
    public readonly status: number,
    public readonly body: string | ValidationBody | null = null,
  ) {
    super(message)
  }

  /** Field errors from the validation body, or an empty object. */
  get fieldErrors(): Record<string, string[]> {
    return typeof this.body === 'object' && this.body?.errors ? this.body.errors : {}
  }
}

/** Thrown when no valid session exists; the router sends the user to login. */
export class NotAuthenticatedError extends Error {
  constructor() {
    super('not authenticated')
  }
}

export type Query = Record<string, string | number | boolean | Date | null | undefined>

function buildQuery(query?: Query): string {
  if (!query) return ''
  const params = new URLSearchParams()
  for (const [key, value] of Object.entries(query)) {
    if (value === undefined || value === null || value === '') continue
    params.set(key, value instanceof Date ? value.toISOString() : String(value))
  }
  const s = params.toString()
  return s ? `?${s}` : ''
}

async function parseError(response: Response): Promise<ApiError> {
  const text = await response.text().catch(() => '')
  let body: string | ValidationBody | null = text || null
  let message = text
  if (text && text.trim().startsWith('{')) {
    try {
      const json = JSON.parse(text) as ValidationBody & { message?: string }
      body = json
      message = json.message ?? text
      const first = Object.values(json.errors ?? {}).flat()[0]
      if (first) message = first
    } catch {
      /* keep text */
    }
  } else if (text.startsWith('"') && text.endsWith('"')) {
    message = text.slice(1, -1)
  }
  return new ApiError(message || `${response.status} ${response.statusText}`, response.status, body)
}

interface RequestOptions {
  method?: 'GET' | 'POST' | 'PUT' | 'DELETE'
  query?: Query
  body?: unknown
  /** 'json' (default), 'blob' for downloads, 'text' or 'none' for empty responses */
  as?: 'json' | 'blob' | 'text' | 'none'
  auth?: boolean
}

export async function request<T>(path: string, options: RequestOptions = {}): Promise<T> {
  const { apiBaseUrl } = useConfig()
  const headers: Record<string, string> = { Accept: 'application/json, text/plain, */*' }
  if (options.auth !== false) {
    const token = await useAuth().getAccessToken()
    if (!token) throw new NotAuthenticatedError()
    headers.Authorization = `Bearer ${token}`
  }
  let body: BodyInit | undefined
  if (options.body !== undefined) {
    headers['Content-Type'] = 'application/json'
    body = JSON.stringify(options.body)
  }
  const response = await fetch(`${apiBaseUrl}${path}${buildQuery(options.query)}`, {
    method: options.method ?? 'GET',
    headers,
    body,
  })
  if (response.status === 401) throw new NotAuthenticatedError()
  if (!response.ok) throw await parseError(response)
  switch (options.as ?? 'json') {
    case 'none':
      return undefined as T
    case 'text':
      return (await response.text()) as T
    case 'blob':
      return (await response.blob()) as T
    default: {
      const text = await response.text()
      return (text ? JSON.parse(text) : null) as T
    }
  }
}

export const http = {
  get: <T>(path: string, query?: Query, auth = true) => request<T>(path, { query, auth }),
  post: <T>(path: string, body?: unknown) => request<T>(path, { method: 'POST', body }),
  put: (path: string, body?: unknown) => request<void>(path, { method: 'PUT', body, as: 'none' }),
  delete: (path: string) => request<void>(path, { method: 'DELETE', as: 'none' }),
  blob: (path: string, query?: Query) => request<Blob>(path, { query, as: 'blob' }),
}
