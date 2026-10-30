import { http } from '../http'
import type { EmployeeResponse, Guid, ShiftResponse } from '../types'

export const employeesApi = {
  list: () => http.get<EmployeeResponse[]>('/employees'),
  get: (id: Guid) => http.get<EmployeeResponse>(`/employees/${id}`),
  /** count -1 = all. Only the own id is allowed for non-admins. */
  shifts: (id: Guid, seasonId: Guid, options: { count?: number; startingFrom?: Date } = {}) =>
    http.get<ShiftResponse[]>(`/employees/${id}/shifts`, {
      seasonId,
      count: options.count ?? -1,
      startingFrom: options.startingFrom,
    }),
  ics: (seasonId: Guid) => http.blob('/employees/shifts-ics', { seasonId }),
}
