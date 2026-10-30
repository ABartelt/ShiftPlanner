import { http } from '../http'
import type {
  CreateFrameworkRequest,
  FrameworkResponse,
  Guid,
  UpdateFrameworkRequest,
} from '../types'

export const frameworksApi = {
  list: (seasonId: Guid) => http.get<FrameworkResponse[]>('/frameworks', { seasonId }),
  create: (body: CreateFrameworkRequest) => http.post<FrameworkResponse>('/frameworks', body),
  update: (body: UpdateFrameworkRequest) => http.put(`/frameworks/${body.id}`, body),
  remove: (id: Guid) => http.delete(`/frameworks/${id}`),
}
