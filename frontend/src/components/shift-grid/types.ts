import type { ShiftCard } from '@/domain/shifts'

/** A coloured background band per container (day and week view). */
export interface GridBand {
  key: string
  day: string
  startMinutes: number
  endMinutes: number
  color: string
  label?: string
  /** used by the container editor */
  payload?: unknown
}

/** Aggregated week block for one slot: "n/m frei". */
export interface WeekBlock {
  key: string
  day: string
  startMinutes: number
  endMinutes: number
  available: number
  total: number
  card?: ShiftCard
}

export interface DayCardPlacement {
  card: ShiftCard
  day: string
  startMinutes: number
  endMinutes: number
  column: number
  columns: number
}
