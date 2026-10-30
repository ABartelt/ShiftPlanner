import { http } from '../http'
import type { CreateShiftRequest, Guid, ShiftResponse, ShiftTypesCountResponse } from '../types'

export const shiftsApi = {
  get: (id: Guid) => http.get<ShiftResponse>(`/shifts/${id}`),
  update: (id: Guid, body: CreateShiftRequest) => http.put(`/shifts/${id}`, body),
  remove: (id: Guid) => http.delete(`/shifts/${id}`),
  /** All shifts of a season (admin). */
  list: (seasonId: Guid, limit?: number) =>
    http.get<ShiftResponse[]>('/shifts', { seasonId, limit }),
  /** Free slots of the season across all locations; includeNonAvailable adds full slots. */
  availableTypes: (
    seasonId: Guid,
    options: { limit?: number; includeNonAvailable?: boolean; startingFrom?: Date } = {},
  ) =>
    http.get<ShiftTypesCountResponse[]>('/shifts/available-types', {
      seasonId,
      limit: options.limit,
      includeNonAvailable: options.includeNonAvailable ?? false,
      startingFrom: options.startingFrom,
    }),
}
