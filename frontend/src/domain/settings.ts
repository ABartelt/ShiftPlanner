import type { AppSettings } from '@/api/types'
import { durationToMinutes } from './time'

export const defaultSettings: AppSettings = {
  title: 'Schichtplaner',
  subtitle: '',
  contact: '',
  startTime: '08:00:00',
  endTime: '1.02:00:00',
  greeting: 'Moin',
  memberName: 'Helfer:innen',
}

export function contactHref(contact: string): string {
  if (!contact) return '#'
  if (contact.includes('@')) return `mailto:${contact}`
  return contact
}

/** Visible day window in minutes from midnight; end may exceed 1440 (e.g. 26:00). */
export function dayWindow(settings: AppSettings): { start: number; end: number } {
  const start = durationToMinutes(settings.startTime)
  let end = durationToMinutes(settings.endTime)
  if (end <= start) end = start + 12 * 60
  return { start, end }
}
