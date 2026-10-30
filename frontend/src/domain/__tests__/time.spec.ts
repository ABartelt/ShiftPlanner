import { describe, expect, it } from 'vitest'
import {
  addDays,
  clockToMinutes,
  dayKey,
  durationToMinutes,
  formatTime,
  instantAt,
  minutesIntoDay,
  minutesToClock,
  minutesToDuration,
  setTimeZone,
  weekStart,
} from '../time'

setTimeZone('Europe/Berlin')

describe('durations (.NET TimeSpan "c")', () => {
  it('parses hh:mm:ss, negative values and day prefixes', () => {
    expect(durationToMinutes('00:15:00')).toBe(15)
    expect(durationToMinutes('-00:15:00')).toBe(-15)
    expect(durationToMinutes('1.02:00:00')).toBe(26 * 60)
    expect(durationToMinutes('26:00:00')).toBe(26 * 60)
    expect(durationToMinutes('08:00:00.0000000')).toBe(480)
    expect(durationToMinutes('')).toBe(0)
  })
  it('formats minutes back, using the day prefix above 24 h', () => {
    expect(minutesToDuration(15)).toBe('00:15:00')
    expect(minutesToDuration(-15)).toBe('-00:15:00')
    expect(minutesToDuration(26 * 60)).toBe('1.02:00:00')
    expect(minutesToDuration(90)).toBe('01:30:00')
  })
  it('clock strings keep hours above 24', () => {
    expect(minutesToClock(26 * 60)).toBe('26:00')
    expect(clockToMinutes('26:00')).toBe(1560)
    expect(clockToMinutes('x')).toBeNull()
  })
})

describe('zoned days', () => {
  it('maps UTC instants to Berlin days and minutes (CEST)', () => {
    expect(dayKey('2026-07-10T22:30:00Z')).toBe('2026-07-11')
    expect(minutesIntoDay('2026-07-10T22:30:00Z', '2026-07-10')).toBe(24 * 60 + 30)
    expect(instantAt('2026-07-10', 8 * 60).toISOString()).toBe('2026-07-10T06:00:00.000Z')
    expect(formatTime('2026-07-10T06:00:00Z')).toBe('08:00')
    expect(formatTime('2026-07-10T06:00:00Z', -15)).toBe('07:45')
  })
  it('handles winter time and DST changes', () => {
    expect(instantAt('2026-01-10', 8 * 60).toISOString()).toBe('2026-01-10T07:00:00.000Z')
    // 29.03.2026 02:00 → 03:00: the day has 23 hours, 26:00 is still +26h of wall clock from midnight? No: minutes are added to the instant.
    expect(instantAt('2026-03-29', 0).toISOString()).toBe('2026-03-28T23:00:00.000Z')
  })
  it('adds days and finds the Monday of a week', () => {
    expect(addDays('2026-07-31', 1)).toBe('2026-08-01')
    expect(weekStart('2026-07-10')).toBe('2026-07-06')
    expect(weekStart('2026-07-06')).toBe('2026-07-06')
    expect(weekStart('2026-07-12')).toBe('2026-07-06')
  })
})
