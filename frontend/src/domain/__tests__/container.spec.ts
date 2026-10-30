import { describe, expect, it } from 'vitest'
import type {
  ContainerResponse,
  FrameworkResponse,
  LocationResponse,
  ShiftTypeResponse,
} from '@/api/types'
import {
  containerEnd,
  locationCapacity,
  slotCapacity,
  slotStarts,
  slotsInRange,
  snapToSlot,
} from '../container'

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
  secondsPerShift: 90 * 60,
  shiftTypeCounts: [
    { shiftType: bar, count: 2 },
    { shiftType: chef, count: 1 },
  ],
}
const container: ContainerResponse = {
  id: 'c1',
  start: '2026-07-10T08:00:00Z',
  totalShifts: 4,
  color: 'rgba(0,0,255,.3)',
  framework,
}
const location: LocationResponse = {
  id: 'l1',
  name: 'Bar 1',
  type: { id: 'lt', name: 'Bar', adminOnly: false },
  containers: [container],
}

describe('containers', () => {
  it('derives slot starts and end from the framework', () => {
    expect(slotStarts(container).map((d) => d.toISOString())).toEqual([
      '2026-07-10T08:00:00.000Z',
      '2026-07-10T09:30:00.000Z',
      '2026-07-10T11:00:00.000Z',
      '2026-07-10T12:30:00.000Z',
    ])
    expect(containerEnd(container).toISOString()).toBe('2026-07-10T14:00:00.000Z')
  })
  it('snaps to the slot containing a time, or the last slot', () => {
    expect(snapToSlot(container, new Date('2026-07-10T09:00:00Z')).toISOString()).toBe(
      '2026-07-10T08:00:00.000Z',
    )
    expect(snapToSlot(container, new Date('2026-07-10T09:30:00Z')).toISOString()).toBe(
      '2026-07-10T09:30:00.000Z',
    )
    expect(snapToSlot(container, new Date('2026-07-10T20:00:00Z')).toISOString()).toBe(
      '2026-07-10T12:30:00.000Z',
    )
  })
  it('computes capacities', () => {
    expect(slotCapacity(framework)).toBe(3)
    expect(locationCapacity(location)).toBe(12)
  })
  it('lists slots overlapping a range', () => {
    const slots = slotsInRange(
      location,
      new Date('2026-07-10T09:00:00Z'),
      new Date('2026-07-10T11:00:00Z'),
    )
    expect(slots.map((s) => s.start.toISOString())).toEqual([
      '2026-07-10T08:00:00.000Z',
      '2026-07-10T09:30:00.000Z',
    ])
  })
})
