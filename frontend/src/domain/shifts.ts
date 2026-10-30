import {
  EMPTY_GUID,
  type Guid,
  type LocationResponse,
  type ShiftResponse,
  type ShiftTypeResponse,
} from '@/api/types'
import { durationToMinutes } from './time'
import { slotsInRange } from './container'

export type ShiftStatus = 'free' | 'taken' | 'mine' | 'blocked'

/** A placed card in the day grid: a booked shift, a blocked shift or a free position. */
export interface ShiftCard {
  key: string
  shift: ShiftResponse | null
  containerId: Guid
  type: ShiftTypeResponse
  start: Date
  end: Date
  /** display offset of the type in minutes */
  offsetMinutes: number
  status: ShiftStatus
  employeeName: string
  blockReason: string | null
}

export function isBlocked(shift: ShiftResponse): boolean {
  return !!shift.blockReason
}

/** "Max M." for non-admin views; the API already shortens last names for non-admins. */
export function shortName(fullName: string | null | undefined): string {
  if (!fullName) return ''
  const [first, ...rest] = fullName.trim().split(/\s+/)
  const last = rest.join(' ')
  return last ? `${first} ${last.charAt(0)}.` : (first ?? '')
}

export function statusOf(shift: ShiftResponse, mySub: Guid | null): ShiftStatus {
  if (isBlocked(shift)) return 'blocked'
  if (shift.employeeId === EMPTY_GUID) return 'free'
  return shift.employeeId === mySub ? 'mine' : 'taken'
}

export function cardFromShift(shift: ShiftResponse, mySub: Guid | null): ShiftCard | null {
  if (!shift.type) return null
  return {
    key: shift.id,
    shift,
    containerId: shift.containerId,
    type: shift.type,
    start: new Date(shift.start),
    end: new Date(shift.end),
    offsetMinutes: durationToMinutes(shift.type.startingTimeShift),
    status: statusOf(shift, mySub),
    employeeName: shift.employeeFullName ?? '',
    blockReason: shift.blockReason ?? null,
  }
}

/**
 * Builds the day grid: booked/blocked shifts plus one free card per unfilled position
 * (framework count − shifts of that type at that slot), like the old client's FillShiftsWithUnassignedShifts.
 * Blocked shifts count as occupied positions.
 */
export function buildDayCards(
  location: LocationResponse,
  shifts: ShiftResponse[],
  from: Date,
  to: Date,
  mySub: Guid | null,
  options: { isAdmin: boolean },
): ShiftCard[] {
  const cards: ShiftCard[] = []
  for (const s of shifts) {
    const c = cardFromShift(s, mySub)
    if (c) cards.push(c)
  }
  for (const slot of slotsInRange(location, from, to)) {
    for (const tc of slot.framework.shiftTypeCounts) {
      if (tc.shiftType.onlyAssignableByAdmin && !options.isAdmin) continue
      const taken = shifts.filter(
        (s) =>
          s.containerId === slot.containerId &&
          s.type?.id === tc.shiftType.id &&
          new Date(s.start).getTime() === slot.start.getTime(),
      ).length
      for (let i = taken; i < tc.count; i++) {
        cards.push({
          key: `free-${slot.containerId}-${slot.start.getTime()}-${tc.shiftType.id}-${i}`,
          shift: null,
          containerId: slot.containerId,
          type: tc.shiftType,
          start: slot.start,
          end: slot.end,
          offsetMinutes: durationToMinutes(tc.shiftType.startingTimeShift),
          status: 'free',
          employeeName: '',
          blockReason: null,
        })
      }
    }
  }
  return cards.sort(
    (a, b) =>
      a.start.getTime() - b.start.getTime() ||
      a.type.name.localeCompare(b.type.name, 'de') ||
      rank(a) - rank(b),
  )
}

function rank(c: ShiftCard): number {
  return c.status === 'mine' ? 0 : c.status === 'taken' ? 1 : c.status === 'blocked' ? 2 : 3
}

/** Fallback colour when a type has none. */
export function typeColor(type: ShiftTypeResponse | null | undefined): string {
  return type?.color || '#888888'
}
