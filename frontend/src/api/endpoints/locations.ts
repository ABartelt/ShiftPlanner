import { http } from '../http'
import type {
  CreateLocationRequest,
  CreateLocationTypeRequest,
  Guid,
  LocationResponse,
  LocationTypeResponse,
  ShiftResponse,
  ShiftsCountResponse,
  ShiftTypesCountResponse,
  UpdateLocationRequest,
} from '../types'

export const locationsApi = {
  list: (seasonId: Guid) => http.get<LocationResponse[]>('/locations', { seasonId }),
  get: (id: Guid) => http.get<LocationResponse>(`/locations/${id}`),
  create: (body: CreateLocationRequest) => http.post<LocationResponse>('/locations', body),
  update: (body: UpdateLocationRequest) => http.put(`/locations/${body.id}`, body),
  remove: (id: Guid) => http.delete(`/locations/${id}`),
  counts: (seasonId: Guid) =>
    http.get<ShiftsCountResponse[]>('/locations/get-all-shifts-count', { seasonId }),
  count: (id: Guid) => http.get<ShiftsCountResponse>(`/locations/${id}/get-shifts-count`),
  /** Booked and blocked shifts of a location in [start, end). */
  shifts: (id: Guid, start: Date, end: Date, keycloakEmployeeId?: Guid) =>
    http.get<ShiftResponse[]>(`/locations/${id}/shifts`, { start, end, keycloakEmployeeId }),
  /** Availability per slot and type for containers starting in [startTime, endTime). limit 0 = all. */
  availability: (id: Guid, startTime?: Date, endTime?: Date, limit = 0) =>
    http.get<ShiftTypesCountResponse[]>(`/locations/${id}/get-all-available-shifts-types`, {
      locationId: id,
      startTime,
      endTime,
      limit,
    }),
  exportXlsx: (id: Guid, anonymous: boolean) =>
    http.blob(`/locations/${id}/export/xlsx`, { locationId: id, anonymous }),
  types: () => http.get<LocationTypeResponse[]>('/location-types'),
  createType: (body: CreateLocationTypeRequest) =>
    http.post<LocationTypeResponse>('/location-types', body),
}
