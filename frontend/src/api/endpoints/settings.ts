import { http } from '../http'
import type { AppSettings, GetAppSettingsResponse, UpdateAppSettingsRequest } from '../types'

export const settingsApi = {
  get: async (): Promise<AppSettings> =>
    (await http.get<GetAppSettingsResponse>('/settings')).settings,
  update: (body: UpdateAppSettingsRequest) => http.put('/settings', body),
  version: async (): Promise<string> =>
    (await http.get<{ version: string }>('/version', undefined, false)).version,
}
