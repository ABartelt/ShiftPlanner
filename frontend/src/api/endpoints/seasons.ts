import { http } from '../http'
import type { CreateSeasonRequest, Guid, SeasonResponse, UpdateSeasonRequest } from '../types'

export const seasonsApi = {
  list: () => http.get<SeasonResponse[]>('/seasons'),
  get: (id: Guid) => http.get<SeasonResponse>(`/seasons/${id}`),
  create: (body: CreateSeasonRequest) => http.post<SeasonResponse>('/seasons', body),
  update: (body: UpdateSeasonRequest) => http.put(`/seasons/${body.id}`, body),
  setCurrent: (id: Guid) => http.put(`/seasons/current/${id}`, { id }),
  remove: (id: Guid) => http.delete(`/seasons/${id}`),
}
