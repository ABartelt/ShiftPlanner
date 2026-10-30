import type { ContainerResponse, FrameworkResponse, Guid, LocationResponse } from '@/api/types'

export interface Slot {
  containerId: Guid
  start: Date
  end: Date
  framework: FrameworkResponse
  color: string
}

export function containerMinutesPerShift(c: ContainerResponse): number {
  return Math.round(c.framework.secondsPerShift / 60)
}

export function containerEnd(c: ContainerResponse): Date {
  return new Date(new Date(c.start).getTime() + c.framework.secondsPerShift * 1000 * c.totalShifts)
}

/** Slot starts of a container: start + i * timePerShift. */
export function slotStarts(c: ContainerResponse): Date[] {
  const start = new Date(c.start).getTime()
  const step = c.framework.secondsPerShift * 1000
  return Array.from({ length: c.totalShifts }, (_, i) => new Date(start + i * step))
}

export function slotsOf(c: ContainerResponse): Slot[] {
  const step = c.framework.secondsPerShift * 1000
  return slotStarts(c).map((start) => ({
    containerId: c.id,
    start,
    end: new Date(start.getTime() + step),
    framework: c.framework,
    color: c.color,
  }))
}

/** Slots of all containers of a location that overlap [from, to). */
export function slotsInRange(location: LocationResponse, from: Date, to: Date): Slot[] {
  const out: Slot[] = []
  for (const c of location.containers ?? []) {
    for (const s of slotsOf(c)) {
      if (s.end > from && s.start < to) out.push(s)
    }
  }
  return out.sort((a, b) => a.start.getTime() - b.start.getTime())
}

/** The slot containing `t` (first start > t − timePerShift), or the last slot of the container. */
export function snapToSlot(c: ContainerResponse, t: Date): Date {
  const step = c.framework.secondsPerShift * 1000
  const starts = slotStarts(c)
  return starts.find((s) => s.getTime() > t.getTime() - step) ?? starts[starts.length - 1]!
}

export function containerContaining(
  location: LocationResponse,
  t: Date,
): ContainerResponse | undefined {
  return (location.containers ?? []).find((c) => new Date(c.start) <= t && t < containerEnd(c))
}

/** Capacity per slot = Σ counts of the framework. */
export function slotCapacity(framework: FrameworkResponse): number {
  return framework.shiftTypeCounts.reduce((sum, tc) => sum + tc.count, 0)
}

/** Total capacity of a location across all containers. */
export function locationCapacity(location: LocationResponse): number {
  return (location.containers ?? []).reduce(
    (sum, c) => sum + c.totalShifts * slotCapacity(c.framework),
    0,
  )
}
