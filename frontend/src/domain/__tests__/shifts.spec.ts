import { describe, expect, it } from 'vitest'
import {
  EMPTY_GUID,
  type ContainerResponse,
  type FrameworkResponse,
  type LocationResponse,
  type ShiftResponse,
  type ShiftTypeResponse,
} from '@/api/types'
import { buildDayCards, shortName, statusOf } from '../shifts'

const bar: ShiftTypeResponse = {
  id: 't1',
  name: 'Bar',
  color: '#f00',
  startingTimeShift: '00:00:00',
  onlyAssignableByAdmin: false,
}
const chef: ShiftTypeResponse = {
  id: 't2',
  name: 'Chef',
  color: '#0f0',
  startingTimeShift: '-00:15:00',
  onlyAssignableByAdmin: true,
}
const framework: FrameworkResponse = {
  id: 'f1',
  name: 'Früh',
  secondsPerShift: 3600,
  shiftTypeCounts: [
    { shiftType: bar, count: 2 },
    { shiftType: chef, count: 1 },
  ],
}
const container: ContainerResponse = {
  id: 'c1',
  start: '2026-07-10T08:00:00Z',
  totalShifts: 2,
  color: '',
  framework,
}
const location: LocationResponse = {
  id: 'l1',
  name: 'Bar 1',
  type: { id: 'lt', name: 'Bar', adminOnly: false },
  containers: [container],
}
const me = 'me'
const shift = (over: Partial<ShiftResponse>): ShiftResponse => ({
  id: 's',
  containerId: 'c1',
  locationId: 'l1',
  employeeId: 'other',
  employeeFullName: 'Anna B',
  start: '2026-07-10T08:00:00Z',
  end: '2026-07-10T09:00:00Z',
  type: bar,
  blockReason: null,
  ...over,
})

describe('status and names', () => {
  it('classifies shifts', () => {
    expect(statusOf(shift({}), me)).toBe('taken')
    expect(statusOf(shift({ employeeId: me }), me)).toBe('mine')
    expect(statusOf(shift({ employeeId: EMPTY_GUID, blockReason: 'Umbau' }), me)).toBe('blocked')
  })
  it('shortens names', () => {
    expect(shortName('Max Mustermann')).toBe('Max M.')
    expect(shortName('Max')).toBe('Max')
  })
})

describe('buildDayCards', () => {
  const from = new Date('2026-07-10T00:00:00Z')
  const to = new Date('2026-07-11T00:00:00Z')
  it('fills free positions per slot and type, blocked shifts count as occupied', () => {
    const shifts = [
      shift({ id: 'a' }),
      shift({
        id: 'b',
        employeeId: EMPTY_GUID,
        blockReason: 'Umbau',
        start: '2026-07-10T09:00:00Z',
        end: '2026-07-10T10:00:00Z',
      }),
    ]
    const cards = buildDayCards(location, shifts, from, to, me, { isAdmin: true })
    // slot 1: bar taken + 1 free bar + 1 free chef; slot 2: bar blocked + 1 free bar + 1 free chef
    expect(cards).toHaveLength(6)
    expect(cards.filter((c) => c.status === 'free')).toHaveLength(4)
    expect(cards.filter((c) => c.status === 'blocked')).toHaveLength(1)
    expect(cards[0]!.status).toBe('taken')
  })
  it('hides admin-only types for helpers', () => {
    const cards = buildDayCards(location, [], from, to, me, { isAdmin: false })
    expect(cards.every((c) => c.type.id === bar.id)).toBe(true)
    expect(cards).toHaveLength(4)
  })
  it('keeps the display offset of the type', () => {
    const cards = buildDayCards(location, [], from, to, me, { isAdmin: true })
    expect(cards.find((c) => c.type.id === chef.id)?.offsetMinutes).toBe(-15)
  })
})
