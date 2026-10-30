import { http } from '../http'
import type {
  ContainerResponse,
  CreateContainerRequest,
  CreatedResponse,
  CreateShiftRequest,
  Guid,
  ShiftTypesCountResponse,
  UpdateContainerRequest,
} from '../types'

export const containersApi = {
  get: (id: Guid) => http.get<ContainerResponse>(`/containers/${id}`),
  create: (body: CreateContainerRequest) => http.post<ContainerResponse>('/containers', body),
  update: (body: UpdateContainerRequest) => http.put(`/containers/${body.id}`, body),
  remove: (id: Guid) => http.delete(`/containers/${id}`),
  /** Availability of every type of the container's framework at an exact slot start. */
  availableTypes: (id: Guid, start: Date) =>
    http.get<ShiftTypesCountResponse[]>(`/containers/${id}/get-available-shift-types`, { start }),
  createShift: (id: Guid, body: CreateShiftRequest) =>
    http.post<CreatedResponse>(`/containers/${id}/shifts`, body),
}
