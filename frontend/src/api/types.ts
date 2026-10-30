/** DTOs of the ShiftPlanner API (camelCase JSON). Dates are ISO-8601 UTC strings, durations .NET "c" strings. */

export type Guid = string
/** ISO-8601 UTC, e.g. 2026-07-10T08:00:00Z */
export type IsoDate = string
/** .NET TimeSpan "c" format: [-][d.]hh:mm:ss */
export type Duration = string

export const EMPTY_GUID: Guid = '00000000-0000-0000-0000-000000000000'

export interface AppSettings {
  title: string
  subtitle: string
  contact: string
  startTime: Duration
  endTime: Duration
  greeting: string
  memberName: string
  contactHref?: string
}
export interface GetAppSettingsResponse {
  settings: AppSettings
}
export type UpdateAppSettingsRequest = Omit<AppSettings, 'contactHref'>

export interface SeasonResponse {
  id: Guid
  name: string
  startDate: IsoDate
  endDate: IsoDate
  isSelectedAsCurrent: boolean
}
export interface CreateSeasonRequest {
  name: string
  startDate: IsoDate
  endDate: IsoDate
  copyFromSeasonId?: Guid | null
}
export interface UpdateSeasonRequest extends CreateSeasonRequest {
  id: Guid
}

export interface ShiftTypeResponse {
  id: Guid
  name: string
  color: string
  startingTimeShift: Duration
  onlyAssignableByAdmin: boolean
  description?: string | null
}
export interface CreateShiftTypeRequest {
  name: string
  color: string
  startingTimeShift: Duration
  onlyAssignableByAdmin: boolean
  seasonId: Guid
  description?: string | null
}
export interface UpdateShiftTypeRequest extends CreateShiftTypeRequest {
  id: Guid
}

export interface ShiftTypeCountResponse {
  shiftType: ShiftTypeResponse
  count: number
}
export interface FrameworkResponse {
  id: Guid
  name: string
  secondsPerShift: number
  shiftTypeCounts: ShiftTypeCountResponse[]
}
export interface ShiftTypeCountDto {
  shiftTypeId: Guid
  count: number
}
export interface CreateFrameworkRequest {
  name: string
  secondsPerShift: number
  seasonId: Guid
  typeCounts: ShiftTypeCountDto[]
}
export interface UpdateFrameworkRequest extends CreateFrameworkRequest {
  id: Guid
}

export interface LocationTypeResponse {
  id: Guid
  name: string
  adminOnly: boolean
}
export interface CreateLocationTypeRequest {
  name: string
  adminOnly: boolean
}

export interface ContainerResponse {
  id: Guid
  start: IsoDate
  totalShifts: number
  color: string
  framework: FrameworkResponse
}
export interface CreateContainerRequest {
  frameworkId: Guid
  locationId: Guid
  start: IsoDate
  totalShifts: number
  color: string
}
export interface UpdateContainerRequest {
  id: Guid
  frameworkId: Guid
  start: IsoDate
  totalShifts: number
  color: string
}

export interface LocationResponse {
  id: Guid
  name: string
  type: LocationTypeResponse
  containers?: ContainerResponse[] | null
}
export interface CreateLocationRequest {
  name: string
  typeId: Guid
  seasonId: Guid
}
export interface UpdateLocationRequest {
  id: Guid
  name: string
  typeId: Guid
  seasonId: Guid
}

export interface ShiftsCountResponse {
  id: Guid
  totalShifts: number
  assignedShifts: number
}

export interface ShiftResponse {
  id: Guid
  containerId: Guid
  locationId: Guid
  employeeId: Guid
  employeeFullName?: string | null
  start: IsoDate
  end: IsoDate
  type?: ShiftTypeResponse | null
  blockReason?: string | null
}
export interface CreateShiftRequest {
  employeeKeycloakId: Guid
  start: IsoDate
  shiftTypeId: Guid
  blockReason?: string | null
}

export interface ShiftTypesCountResponse {
  type: ShiftTypeResponse
  availableCount: number
  totalCount: number
  start: IsoDate
  end: IsoDate
  locationId: Guid
}

export interface EmployeeResponse {
  id: Guid
  firstName?: string | null
  lastName?: string | null
  fullName: string
}

export interface CreatedResponse {
  id: Guid
}
