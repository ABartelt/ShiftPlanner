import { TZDate } from '@date-fns/tz'
import { addMinutes, format as dfFormat, startOfDay } from 'date-fns'
import { de } from 'date-fns/locale'
import type { Duration } from '@/api/types'

let zone = 'Europe/Berlin'

export function setTimeZone(tz: string): void {
  zone = tz
}
export function timeZone(): string {
  return zone
}

/** A Date whose wall-clock getters follow the configured zone. */
export function inZone(date: Date | string): TZDate {
  return new TZDate(new Date(date), zone)
}

/** Parses a .NET TimeSpan ("c" format: [-][d.]hh:mm:ss[.fffffff]) into minutes. "26:00:00" is accepted. */
export function durationToMinutes(value: Duration | null | undefined): number {
  if (!value) return 0
  const m = /^(-)?(?:(\d+)\.)?(\d+):(\d{2})(?::(\d{2})(?:\.\d+)?)?$/.exec(value.trim())
  if (!m) return 0
  const sign = m[1] ? -1 : 1
  const days = Number(m[2] ?? 0)
  const hours = Number(m[3])
  const minutes = Number(m[4])
  const seconds = Number(m[5] ?? 0)
  return sign * (days * 1440 + hours * 60 + minutes + Math.round(seconds / 60))
}

/** Formats minutes as a .NET TimeSpan string; values of 24 h and more become "d.hh:mm:ss". */
export function minutesToDuration(total: number): Duration {
  const sign = total < 0 ? '-' : ''
  const abs = Math.abs(Math.round(total))
  const days = Math.floor(abs / 1440)
  const hours = Math.floor((abs % 1440) / 60)
  const minutes = abs % 60
  const hhmmss = `${String(hours).padStart(2, '0')}:${String(minutes).padStart(2, '0')}:00`
  return `${sign}${days > 0 ? `${days}.` : ''}${hhmmss}`
}

/** "HH:mm" for a duration of possibly more than 24 h (26:00 stays 26:00). */
export function minutesToClock(total: number): string {
  const abs = Math.abs(Math.round(total))
  return `${total < 0 ? '-' : ''}${String(Math.floor(abs / 60)).padStart(2, '0')}:${String(abs % 60).padStart(2, '0')}`
}

export function clockToMinutes(value: string): number | null {
  const m = /^(-)?(\d{1,2}):(\d{2})$/.exec(value.trim())
  if (!m) return null
  return (m[1] ? -1 : 1) * (Number(m[2]) * 60 + Number(m[3]))
}

export function formatTime(date: Date | string, offsetMinutes = 0): string {
  return dfFormat(addMinutes(inZone(date), offsetMinutes), 'HH:mm', { locale: de })
}
export function formatRange(start: Date | string, end: Date | string, offsetMinutes = 0): string {
  return `${formatTime(start, offsetMinutes)} - ${formatTime(end, offsetMinutes)}`
}
/** "Fr 10.07." */
export function formatDayShort(date: Date | string): string {
  return dfFormat(inZone(date), 'EEEEEE dd.MM.', { locale: de })
}
/** "Freitag, 10.07.2026" */
export function formatDayLong(date: Date | string): string {
  return dfFormat(inZone(date), 'EEEE, dd.MM.yyyy', { locale: de })
}
export function formatDate(date: Date | string): string {
  return dfFormat(inZone(date), 'dd.MM.yyyy', { locale: de })
}
export function formatDateTime(date: Date | string): string {
  return dfFormat(inZone(date), 'dd.MM.yyyy HH:mm', { locale: de })
}
/** "yyyy-MM-dd" of the zoned day; used for URL state. */
export function dayKey(date: Date | string): string {
  return dfFormat(inZone(date), 'yyyy-MM-dd')
}
/** Start of the zoned day as an instant. */
export function zonedStartOfDay(date: Date | string): Date {
  return new Date(startOfDay(inZone(date)).getTime())
}
/** Instant for a day key plus minutes from midnight in the configured zone. */
export function instantAt(day: string, minutesFromMidnight: number): Date {
  const [y, m, d] = day.split('-').map(Number) as [number, number, number]
  const midnight = new TZDate(y, m - 1, d, 0, 0, 0, zone)
  return new Date(addMinutes(midnight, minutesFromMidnight).getTime())
}
/** Minutes since the zoned midnight of `day` (may be negative or above 1440). */
export function minutesIntoDay(date: Date | string, day: string): number {
  return Math.round((new Date(date).getTime() - instantAt(day, 0).getTime()) / 60000)
}
export function addDays(day: string, days: number): string {
  return dayKey(instantAt(day, days * 1440 + 720))
}
export function todayKey(): string {
  return dayKey(new Date())
}
/** Monday of the week containing `day`. */
export function weekStart(day: string): string {
  const d = inZone(instantAt(day, 720))
  const weekday = (d.getDay() + 6) % 7
  return addDays(day, -weekday)
}
export function dateToIso(date: Date): string {
  return date.toISOString()
}
/** Hours as "1,5 Tage" style number: total minutes → days with one decimal. */
export function minutesToDaysHours(total: number): { days: number; hours: number } {
  const days = Math.floor(total / 1440)
  const hours = Math.floor((total % 1440) / 60)
  return { days, hours }
}
