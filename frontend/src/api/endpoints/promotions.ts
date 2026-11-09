import { http } from '../http'
import type {
  CreatePromotionChannelRequest,
  Guid,
  PromotionChannelResponse,
  PromotionFormat,
  PromotionLogResponse,
  PromotionPreviewResponse,
  PromotionSchedule,
  PromotionScheduleResponse,
  PromotionTestResponse,
  SignalGroupResponse,
  TelegramChatResponse,
  UpdatePromotionChannelRequest,
} from '../types'

export interface PromotionRange {
  /** first event day, yyyy-MM-dd */
  from: string
  /** last event day, yyyy-MM-dd (inclusive) */
  to: string
}

const season = (seasonId: Guid) => `/seasons/${seasonId}/promotions`

export const promotionsApi = {
  channels: (seasonId: Guid) =>
    http.get<PromotionChannelResponse[]>(`${season(seasonId)}/channels`),
  createChannel: (seasonId: Guid, body: CreatePromotionChannelRequest) =>
    http.post<PromotionChannelResponse>(`${season(seasonId)}/channels`, body),
  updateChannel: (body: UpdatePromotionChannelRequest) =>
    http.put(`/promotions/channels/${body.id}`, body),
  removeChannel: (id: Guid) => http.delete(`/promotions/channels/${id}`),
  testChannel: (id: Guid) =>
    http.post<PromotionTestResponse>(`/promotions/channels/${id}/test`, {}),

  schedule: (seasonId: Guid) => http.get<PromotionScheduleResponse>(`${season(seasonId)}/schedule`),
  updateSchedule: (seasonId: Guid, schedule: PromotionSchedule) =>
    http.put(`${season(seasonId)}/schedule`, { schedule }),

  preview: (
    seasonId: Guid,
    range: PromotionRange,
    format: PromotionFormat,
    locationIds: Guid[] = [],
  ) =>
    http.get<PromotionPreviewResponse>(`${season(seasonId)}/preview`, {
      ...range,
      format,
      locations: locationIds.join(','),
      baseUrl: window.location.origin,
    }),
  send: (seasonId: Guid, range: PromotionRange, channelIds: Guid[]) =>
    http.post<PromotionLogResponse[]>(`${season(seasonId)}/send`, {
      ...range,
      channelIds,
      baseUrl: window.location.origin,
    }),
  log: (seasonId: Guid) => http.get<PromotionLogResponse[]>(`${season(seasonId)}/log`),

  telegramChats: (token: string | null, channelId: Guid | null) =>
    http.post<TelegramChatResponse[]>('/promotions/telegram/chats', { token, channelId }),
  signalAccounts: (serviceUrl: string) =>
    http.get<string[]>('/promotions/signal/accounts', { serviceUrl }),
  signalGroups: (serviceUrl: string, number: string) =>
    http.get<SignalGroupResponse[]>('/promotions/signal/groups', { serviceUrl, number }),
  signalLinkQr: (serviceUrl: string) => http.blob('/promotions/signal/link-qr', { serviceUrl }),
}
