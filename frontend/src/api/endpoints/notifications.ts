import { http } from '../http'
import type {
  CreateChannelLinkResponse,
  GetNotificationSettingsResponse,
  NotificationChannel,
  UpdateNotificationSettingsRequest,
} from '../types'

export const notificationsApi = {
  get: () => http.get<GetNotificationSettingsResponse>('/me/notifications'),
  update: (body: UpdateNotificationSettingsRequest) => http.put('/me/notifications', body),
  /** Starts linking; e-mail is linked at once, messengers return a code for the bot. */
  link: (channel: NotificationChannel) =>
    http.post<CreateChannelLinkResponse>(`/me/notifications/channels/${channel}/link`),
  unlink: (channel: NotificationChannel) => http.delete(`/me/notifications/channels/${channel}`),
}
