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

export type PromotionChannelKind = 'Telegram' | 'Slack' | 'Signal'
export type PromotionRunKind = 'Daily' | 'Recruiting' | 'LastCall' | 'Manual' | 'Test'
export type PromotionFormat = 'Plain' | 'WhatsApp' | 'Signal' | 'Telegram' | 'Slack'
export type PromotionLogStatus = 'Sent' | 'Failed' | 'Skipped'

export interface PromotionChannelConfig {
  /** Telegram chat id or Signal group id */
  target: string
  targetName: string
  signalNumber: string
  serviceUrl: string
  /** empty = all locations */
  locationIds: Guid[]
}
export interface PromotionChannelResponse {
  id: Guid
  seasonId: Guid
  name: string
  kind: PromotionChannelKind
  enabled: boolean
  config: PromotionChannelConfig
  /** the secret itself is never returned */
  hasSecret: boolean
  secretHint?: string | null
  lastTestAt?: IsoDate | null
  lastTestOk?: boolean | null
  lastTestMessage?: string | null
}
export interface CreatePromotionChannelRequest {
  name: string
  kind: PromotionChannelKind
  enabled: boolean
  config: PromotionChannelConfig
  secret?: string | null
}
export interface UpdatePromotionChannelRequest {
  id: Guid
  name: string
  enabled: boolean
  config: PromotionChannelConfig
  /** null keeps the stored secret, '' removes it */
  secret: string | null
}
/** .NET DayOfWeek: 0 = Sunday */
export type DayOfWeek = 0 | 1 | 2 | 3 | 4 | 5 | 6
export interface PromotionSchedule {
  enabled: boolean
  timeZone: string
  publicBaseUrl: string
  minOpenShifts: number
  dailyEnabled: boolean
  dailySendTime: Duration
  dailyLookaheadDays: number
  recruitingEnabled: boolean
  recruitingDay: DayOfWeek
  recruitingSendTime: Duration
  lastCallEnabled: boolean
  lastCallHoursBefore: number
  quietFrom: Duration
  quietUntil: Duration
}
export interface PromotionScheduleResponse {
  schedule: PromotionSchedule
  nextRuns: { kind: PromotionRunKind; sendAt: IsoDate }[]
  enabledChannels: number
}
export interface PromotionPreviewResponse {
  text: string
  totalOpen: number
  signUpUrl: string
}
export interface PromotionLogResponse {
  id: Guid
  channelId?: Guid | null
  channelName: string
  runKind: PromotionRunKind
  sentAt: IsoDate
  openShifts: number
  status: PromotionLogStatus
  error?: string | null
}
export interface PromotionTestResponse {
  ok: boolean
  message: string
}
export interface TelegramChatResponse {
  id: string
  title: string
  type: string
}
export interface SignalGroupResponse {
  id: string
  name: string
}
