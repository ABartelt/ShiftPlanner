import type { Guid, LocationResponse, ShiftResponse, ShiftTypeResponse } from '@/api/types'
import { containerEnd } from './container'
import { buildDayCards } from './shifts'
import { addDays, dayKey, instantAt, minutesIntoDay } from './time'

/** A column of the printed roster: one shift type. */
export interface PosterColumn {
  type: ShiftTypeResponse
  /** display offset of the type in minutes (e.g. −15 = starts 15 min before the slot) */
  offsetMinutes: number
}

/** Booked people and free places of one type in one slot. Both empty = not offered there. */
export interface PosterCell {
  names: string[]
  free: number
}

/** A time slot; `cells[i]` belongs to `columns[i]` of its day. */
export interface PosterRow {
  key: string
  start: Date
  end: Date
  cells: PosterCell[]
  free: number
}

export interface PosterDay {
  /** festival day "yyyy-MM-dd" in the configured zone */
  day: string
  /** when the festival day begins (its date + the cutoff) */
  start: Date
  columns: PosterColumn[]
  rows: PosterRow[]
  freeCount: number
}

/**
 * Where one festival day ends and the next begins, in minutes past midnight: the middle of the
 * night gap of the app's day window. 08:00–26:00 → 05:00, so the bar night until 02:00 stays with
 * Friday and a set-up shift at 06:00 belongs to Saturday. A window without a gap → midnight.
 */
export function dayCutoffMinutes(window: { start: number; end: number }): number {
  const middle = (window.end + window.start + 1440) / 2 - 1440
  return Math.round(Math.min(Math.max(middle, 0), window.start))
}

/** Festival day of an instant: anything before `cutoffMinutes` belongs to the day before. */
export function festivalDay(t: Date, cutoffMinutes: number): string {
  const calendar = dayKey(t)
  return minutesIntoDay(t, calendar) < cutoffMinutes ? addDays(calendar, -1) : calendar
}

/** The whole span of a location: first container start to last container end. */
export function locationSpan(location: LocationResponse): { from: Date; to: Date } | null {
  const containers = location.containers ?? []
  if (!containers.length) return null
  const from = Math.min(...containers.map((c) => new Date(c.start).getTime()))
  const to = Math.max(...containers.map((c) => containerEnd(c).getTime()))
  return { from: new Date(from), to: new Date(to) }
}

interface DraftRow {
  key: string
  containerId: Guid
  start: Date
  end: Date
  byType: Map<Guid, PosterCell>
}

/**
 * Roster of a location for a printed notice as one table per day: rows are time slots, columns
 * shift types. Cells list the full names of booked people and the number of free places. Free
 * places of admin-only types are not counted (they are not open to volunteers), blocked places
 * count as occupied and are not shown. Columns without any name or free place that day are
 * dropped. Slots are grouped by festival day (see `festivalDay`). With `from`, festival days
 * before the one containing `from` are left out.
 */
export function buildPoster(
  location: LocationResponse,
  shifts: ShiftResponse[],
  options: { from?: Date; dayCutoffMinutes?: number } = {},
): PosterDay[] {
  const span = locationSpan(location)
  if (!span) return []
  const cutoff = options.dayCutoffMinutes ?? 0
  const from = options.from
    ? new Date(
        Math.max(
          span.from.getTime(),
          instantAt(festivalDay(options.from, cutoff), cutoff).getTime(),
        ),
      )
    : span.from
  if (from >= span.to) return []

  const typeOrder = new Map<Guid, number>()
  for (const c of location.containers ?? []) {
    c.framework.shiftTypeCounts.forEach((tc, i) => {
      if (!typeOrder.has(tc.shiftType.id)) typeOrder.set(tc.shiftType.id, i)
    })
  }

  const cards = buildDayCards(location, shifts, from, span.to, null, { isAdmin: false }).filter(
    (c) => c.start >= from,
  )

  const days = new Map<
    string,
    { start: Date; rows: Map<string, DraftRow>; types: Map<Guid, PosterColumn> }
  >()
  for (const card of cards) {
    if (card.status === 'blocked') continue
    const key = festivalDay(card.start, cutoff)
    let day = days.get(key)
    if (!day) {
      day = { start: instantAt(key, cutoff), rows: new Map(), types: new Map() }
      days.set(key, day)
    }
    const rowKey = `${card.containerId}-${card.start.getTime()}`
    let row = day.rows.get(rowKey)
    if (!row) {
      row = {
        key: rowKey,
        containerId: card.containerId,
        start: card.start,
        end: card.end,
        byType: new Map(),
      }
      day.rows.set(rowKey, row)
    }
    if (!day.types.has(card.type.id)) {
      day.types.set(card.type.id, { type: card.type, offsetMinutes: card.offsetMinutes })
    }
    let cell = row.byType.get(card.type.id)
    if (!cell) {
      cell = { names: [], free: 0 }
      row.byType.set(card.type.id, cell)
    }
    if (card.status === 'free') cell.free++
    else cell.names.push(card.employeeName.trim() || '?')
  }

  return [...days.entries()]
    .sort(([a], [b]) => a.localeCompare(b))
    .map(([key, day]) => {
      const columns = [...day.types.values()].sort(
        (a, b) =>
          (typeOrder.get(a.type.id) ?? 999) - (typeOrder.get(b.type.id) ?? 999) ||
          a.type.name.localeCompare(b.type.name, 'de'),
      )
      const rows = [...day.rows.values()]
        .sort(
          (a, b) =>
            a.start.getTime() - b.start.getTime() || a.containerId.localeCompare(b.containerId),
        )
        .map((r): PosterRow => {
          const cells = columns.map((col) => {
            const cell = r.byType.get(col.type.id) ?? { names: [], free: 0 }
            return {
              names: [...cell.names].sort((a, b) => a.localeCompare(b, 'de')),
              free: cell.free,
            }
          })
          return {
            key: r.key,
            start: r.start,
            end: r.end,
            cells,
            free: cells.reduce((sum, c) => sum + c.free, 0),
          }
        })
      return {
        day: key,
        start: day.start,
        columns,
        rows,
        freeCount: rows.reduce((sum, r) => sum + r.free, 0),
      }
    })
}

/**
 * Splits a day with more than `max` columns into evenly sized parts (stacked tables on paper).
 * Rows that are empty within a part are dropped from it.
 */
export function chunkColumns(day: PosterDay, max = 6): PosterDay[] {
  const n = day.columns.length
  if (n <= max) return [day]
  const parts = Math.ceil(n / max)
  const size = Math.ceil(n / parts)
  const out: PosterDay[] = []
  for (let i = 0; i < n; i += size) {
    const rows = day.rows
      .map((r) => {
        const cells = r.cells.slice(i, i + size)
        return { ...r, cells, free: cells.reduce((sum, c) => sum + c.free, 0) }
      })
      .filter((r) => r.cells.some((c) => c.names.length || c.free))
    out.push({
      ...day,
      columns: day.columns.slice(i, i + size),
      rows,
      freeCount: rows.reduce((sum, r) => sum + r.free, 0),
    })
  }
  return out
}
