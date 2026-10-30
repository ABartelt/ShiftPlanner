import { http } from '../http'
import type {
  CreateShiftTypeRequest,
  Guid,
  ShiftTypeResponse,
  UpdateShiftTypeRequest,
} from '../types'

export const shiftTypesApi = {
  list: (seasonId: Guid) => http.get<ShiftTypeResponse[]>('/shift-types', { seasonId }),
  create: (body: CreateShiftTypeRequest) => http.post<ShiftTypeResponse>('/shift-types', body),
  update: (body: UpdateShiftTypeRequest) => http.put(`/shift-types/${body.id}`, body),
  remove: (id: Guid) => http.delete(`/shift-types/${id}`),
}
