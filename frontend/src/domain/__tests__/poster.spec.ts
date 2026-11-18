import { describe, expect, it } from 'vitest'
import {
  EMPTY_GUID,
  type ContainerResponse,
  type FrameworkResponse,
  type LocationResponse,
  type ShiftResponse,
  type ShiftTypeResponse,
} from '@/api/types'
import {
  buildPoster,
  chunkColumns,
  dayCutoffMinutes,
  festivalDay,
  locationSpan,
  type PosterDay,
} from '../poster'

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
  startingTimeShift: '00:00:00',
  onlyAssignableByAdmin: true,
}
const framework: FrameworkResponse = {
  id: 'f1',
  name: 'Früh',
  secondsPerShift: 3600,
  shiftTypeCounts: [
    { shiftType: chef, count: 1 },
    { shiftType: bar, count: 2 },
  ],
}
const day1: ContainerResponse = {
  id: 'c1',
  start: '2026-07-10T08:00:00Z',
  totalShifts: 2,
  color: '',
  framework,
}
const day2: ContainerResponse = {
  id: 'c2',
  start: '2026-07-11T08:00:00Z',
  totalShifts: 1,
  color: '',
  framework,
}
const location: LocationResponse = {
  id: 'l1',
  name: 'Bar 1',
  type: { id: 'lt', name: 'Bar', adminOnly: false },
  containers: [day2, day1],
}
const shift = (over: Partial<ShiftResponse>): ShiftResponse => ({
  id: 's',
  containerId: 'c1',
  locationId: 'l1',
  employeeId: 'other',
  employeeFullName: 'Anna Berg',
  start: '2026-07-10T08:00:00Z',
  end: '2026-07-10T09:00:00Z',
  type: bar,
  blockReason: null,
  ...over,
})

describe('locationSpan', () => {
  it('spans the first container start to the last container end', () => {
    expect(locationSpan(location)).toEqual({
      from: new Date('2026-07-10T08:00:00Z'),
      to: new Date('2026-07-11T09:00:00Z'),
    })
    expect(locationSpan({ ...location, containers: [] })).toBeNull()
  })
})

describe('buildPoster', () => {
  const shifts = [
    shift({ id: 'a', employeeFullName: 'Zoe Zander' }),
    shift({ id: 'b', employeeFullName: 'Anna Berg' }),
    shift({ id: 'c', type: chef, employeeFullName: 'Carl Chef' }),
    shift({
      id: 'd',
      start: '2026-07-10T09:00:00Z',
      end: '2026-07-10T10:00:00Z',
      employeeId: EMPTY_GUID,
      employeeFullName: 'FREI',
      blockReason: 'Umbau',
    }),
  ]

  it('builds one table per day with aligned type columns', () => {
    const days = buildPoster(location, shifts)
    expect(days.map((d) => d.day)).toEqual(['2026-07-10', '2026-07-11'])

    const [first, second] = days
    // framework order: Chef before Bar
    expect(first!.columns.map((c) => c.type.name)).toEqual(['Chef', 'Bar'])
    const [eight, nine] = first!.rows
    expect(eight!.cells).toEqual([
      { names: ['Carl Chef'], free: 0 },
      { names: ['Anna Berg', 'Zoe Zander'], free: 0 },
    ])
    expect(eight!.free).toBe(0)
    // 09:00: the blocked place counts as taken and is not shown; the free admin-only Chef place
    // is not offered to volunteers
    expect(nine!.cells).toEqual([
      { names: [], free: 0 },
      { names: [], free: 1 },
    ])
    expect(first!.freeCount).toBe(1)

    // nobody booked Chef on day two, so the column is dropped
    expect(second!.columns.map((c) => c.type.name)).toEqual(['Bar'])
    expect(second!.rows[0]!.cells).toEqual([{ names: [], free: 2 }])
    expect(second!.freeCount).toBe(2)
  })

  it('drops days before `from`', () => {
    const days = buildPoster(location, shifts, { from: new Date('2026-07-11T12:00:00Z') })
    expect(days.map((d) => d.day)).toEqual(['2026-07-11'])
    expect(buildPoster(location, shifts, { from: new Date('2026-08-01T00:00:00Z') })).toEqual([])
  })
})

describe('festival days', () => {
  it('puts the cutoff in the middle of the night gap of the day window', () => {
    expect(dayCutoffMinutes({ start: 8 * 60, end: 26 * 60 })).toBe(5 * 60)
    expect(dayCutoffMinutes({ start: 8 * 60, end: 22 * 60 })).toBe(3 * 60)
    expect(dayCutoffMinutes({ start: 0, end: 24 * 60 })).toBe(0)
  })

  it('assigns the night to the day before (Europe/Berlin, UTC+2 in July)', () => {
    const cutoff = 5 * 60
    expect(festivalDay(new Date('2026-07-10T22:30:00Z'), cutoff)).toBe('2026-07-10') // Sat 00:30
    expect(festivalDay(new Date('2026-07-11T02:59:00Z'), cutoff)).toBe('2026-07-10') // Sat 04:59
    expect(festivalDay(new Date('2026-07-11T03:00:00Z'), cutoff)).toBe('2026-07-11') // Sat 05:00
  })

  it('keeps a bar night past midnight on the page of its evening', () => {
    const night: LocationResponse = {
      ...location,
      containers: [{ ...day1, id: 'n', start: '2026-07-10T20:00:00Z', totalShifts: 4 }],
    }
    // Fri 22:00 – Sat 02:00 local
    const calendar = buildPoster(night, [])
    expect(calendar.map((d) => d.day)).toEqual(['2026-07-10', '2026-07-11'])
    const festival = buildPoster(night, [], { dayCutoffMinutes: 5 * 60 })
    expect(festival.map((d) => d.day)).toEqual(['2026-07-10'])
    expect(festival[0]!.rows).toHaveLength(4)
    expect(festival[0]!.start).toEqual(new Date('2026-07-10T03:00:00Z'))
  })

  it('keeps the current festival day with `from` after midnight', () => {
    const night: LocationResponse = {
      ...location,
      containers: [{ ...day1, id: 'n', start: '2026-07-10T20:00:00Z', totalShifts: 4 }],
    }
    const days = buildPoster(night, [], {
      dayCutoffMinutes: 5 * 60,
      from: new Date('2026-07-10T23:30:00Z'), // Sat 01:30 local, still Friday's night
    })
    expect(days.map((d) => d.day)).toEqual(['2026-07-10'])
    expect(days[0]!.rows).toHaveLength(4)
  })
})

describe('chunkColumns', () => {
  const col = (i: number) => ({ type: { ...bar, id: `t${i}`, name: `T${i}` }, offsetMinutes: 0 })
  const day: PosterDay = {
    day: '2026-07-10',
    start: new Date('2026-07-10T08:00:00Z'),
    columns: Array.from({ length: 8 }, (_, i) => col(i)),
    rows: [
      {
        key: 'r1',
        start: new Date('2026-07-10T08:00:00Z'),
        end: new Date('2026-07-10T09:00:00Z'),
        cells: Array.from({ length: 8 }, (_, i) => ({ names: [], free: i < 4 ? 1 : 0 })),
        free: 4,
      },
    ],
    freeCount: 4,
  }

  it('keeps narrow days and splits wide ones evenly', () => {
    expect(chunkColumns(day, 8)).toEqual([day])
    const parts = chunkColumns(day, 6)
    expect(parts.map((p) => p.columns.length)).toEqual([4, 4])
    expect(parts[0]!.freeCount).toBe(4)
    // the row is empty in the second part and is dropped there
    expect(parts[1]!.rows).toEqual([])
  })
})
