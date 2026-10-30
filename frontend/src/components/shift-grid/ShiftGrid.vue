<script setup lang="ts">
import { computed, nextTick, onMounted, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import { cn } from '@/lib/utils'
import type { ShiftCard } from '@/domain/shifts'
import { typeColor } from '@/domain/shifts'
import { minutesIntoDay, minutesToClock } from '@/domain/time'
import type { DayCardPlacement, GridBand, WeekBlock } from './types'

/**
 * Time grid for one day (cards side by side per slot) or a week (aggregated blocks per slot).
 * The visible window comes from the app settings and may exceed midnight (e.g. 08:00–26:00).
 */
const props = defineProps<{
  mode: 'day' | 'week'
  /** yyyy-MM-dd of the first visible day */
  firstDay: string
  days: string[]
  windowStart: number
  windowEnd: number
  cards?: ShiftCard[]
  blocks?: WeekBlock[]
  bands?: GridBand[]
  /** container editor: clicks on empty cells are reported */
  selectable?: boolean
  loading?: boolean
}>()
const emit = defineEmits<{
  selectCard: [card: ShiftCard]
  selectBlock: [block: WeekBlock]
  selectBand: [band: GridBand]
  selectEmpty: [payload: { day: string; minutes: number }]
  moveBand: [payload: { band: GridBand; day: string; startMinutes: number; endMinutes: number }]
}>()

const { t } = useI18n()
const SLOT = 15
const ROW_PX = 12
const TIME_COL = 44
const headerPx = computed(() => (props.mode === 'week' ? 44 : 0))
const minColPx = computed(() => (props.mode === 'week' ? 96 : 140))

const rows = computed(() => Math.max(1, Math.round((props.windowEnd - props.windowStart) / SLOT)))
const container = ref<HTMLElement | null>(null)

const hourMarks = computed(() => {
  const marks: { minutes: number; label: string }[] = []
  for (let m = Math.ceil(props.windowStart / 60) * 60; m <= props.windowEnd; m += 60) {
    marks.push({ minutes: m, label: minutesToClock(m % 1440) })
  }
  return marks
})

function rowOf(minutes: number): number {
  return Math.round((minutes - props.windowStart) / SLOT) + 2
}
function clampRow(row: number): number {
  return Math.min(rows.value + 2, Math.max(2, row))
}
function columnOf(day: string): number {
  return props.days.indexOf(day) + 2
}

const gridStyle = computed(() => ({
  gridTemplateColumns: `${TIME_COL}px repeat(${props.days.length}, minmax(${minColPx.value}px, 1fr))`,
  gridTemplateRows: `${headerPx.value}px repeat(${rows.value}, ${ROW_PX}px)`,
}))

/** Day view: cards of the same slot share the column, split into sub-columns. */
const placements = computed<DayCardPlacement[]>(() => {
  if (props.mode !== 'day' || !props.cards) return []
  const day = props.firstDay
  const groups = new Map<string, ShiftCard[]>()
  for (const card of props.cards) {
    const key = `${card.start.getTime()}-${card.end.getTime()}`
    const list = groups.get(key) ?? []
    list.push(card)
    groups.set(key, list)
  }
  const out: DayCardPlacement[] = []
  for (const list of groups.values()) {
    list.forEach((card, i) => {
      out.push({
        card,
        day,
        startMinutes: minutesIntoDay(card.start, day) + card.offsetMinutes,
        endMinutes: minutesIntoDay(card.end, day) + card.offsetMinutes,
        column: i,
        columns: list.length,
      })
    })
  }
  return out
})

/** Overlapping time groups in the day view are laid out in a flex row inside one grid cell. */
const dayRows = computed(() => {
  const byRange = new Map<string, DayCardPlacement[]>()
  for (const p of placements.value) {
    const key = `${p.startMinutes}-${p.endMinutes}`
    const list = byRange.get(key) ?? []
    list.push(p)
    byRange.set(key, list)
  }
  return [...byRange.entries()].map(([key, list]) => ({
    key,
    rowStart: clampRow(rowOf(list[0]!.startMinutes)),
    rowEnd: clampRow(rowOf(list[0]!.endMinutes)),
    cards: list.map((p) => p.card),
  }))
})

function cardStyle(card: ShiftCard): Record<string, string> {
  const color = typeColor(card.type)
  switch (card.status) {
    case 'free':
      return { background: 'transparent', borderColor: color, color }
    case 'blocked':
      return {
        color: 'var(--shift-blocked-foreground)',
        borderColor: color,
        backgroundImage:
          'repeating-linear-gradient(45deg, var(--shift-blocked-stripe-a) 0 10px, var(--shift-blocked-stripe-b) 10px 20px)',
      }
    case 'mine':
      return {
        background: color,
        borderColor: 'var(--shift-mine-border)',
        color: '#fff',
        outline: '2px dashed #fff',
        outlineOffset: '-4px',
      }
    default:
      return { background: color, borderColor: color, color: '#fff' }
  }
}

function blockStyle(b: WeekBlock): Record<string, string> {
  const ratio = b.total > 0 ? b.available / b.total : 0
  const alpha = b.total === 0 || b.available === 0 ? 0.35 : Math.min(0.85, 0.4 + 0.45 * ratio)
  return {
    background: `color-mix(in oklch, var(--primary) ${Math.round(alpha * 100)}%, transparent)`,
  }
}

function onEmptyClick(event: MouseEvent, day: string): void {
  if (!props.selectable) return
  const el = container.value
  if (!el) return
  const rect = el.getBoundingClientRect()
  const y = event.clientY - rect.top + el.scrollTop - headerPx.value
  const minutes = props.windowStart + Math.floor(y / ROW_PX) * SLOT
  emit('selectEmpty', { day, minutes })
}

/* drag/resize of bands (container editor) */
interface Drag {
  band: GridBand
  kind: 'move' | 'resize'
  originY: number
  originStart: number
  originEnd: number
  startMinutes: number
  endMinutes: number
  day: string
  moved: boolean
}
const drag = ref<Drag | null>(null)
function onBandPointerDown(event: PointerEvent, band: GridBand, kind: 'move' | 'resize'): void {
  if (!props.selectable) return
  event.stopPropagation()
  drag.value = {
    band,
    kind,
    originY: event.clientY,
    originStart: band.startMinutes,
    originEnd: band.endMinutes,
    startMinutes: band.startMinutes,
    endMinutes: band.endMinutes,
    day: band.day,
    moved: false,
  }
  ;(event.currentTarget as HTMLElement).setPointerCapture(event.pointerId)
}
function dayAtX(clientX: number): string {
  const el = container.value
  if (!el) return props.firstDay
  const rect = el.getBoundingClientRect()
  const colWidth = (rect.width - TIME_COL) / props.days.length
  const i = Math.min(
    props.days.length - 1,
    Math.max(0, Math.floor((clientX - rect.left - TIME_COL) / colWidth)),
  )
  return props.days[i] ?? props.firstDay
}
function onPointerMove(event: PointerEvent): void {
  const d = drag.value
  if (!d) return
  const delta = Math.round((event.clientY - d.originY) / ROW_PX) * SLOT
  if (delta !== 0) d.moved = true
  if (d.kind === 'move') {
    const length = d.originEnd - d.originStart
    d.startMinutes = d.originStart + delta
    d.endMinutes = d.startMinutes + length
    const newDay = dayAtX(event.clientX)
    if (newDay !== d.day) d.moved = true
    d.day = newDay
  } else {
    d.endMinutes = Math.max(d.originStart + SLOT, d.originEnd + delta)
  }
}
function onPointerUp(): void {
  const d = drag.value
  drag.value = null
  if (!d) return
  if (!d.moved) {
    emit('selectBand', d.band)
    return
  }
  emit('moveBand', {
    band: d.band,
    day: d.day,
    startMinutes: d.startMinutes,
    endMinutes: d.endMinutes,
  })
}
const visibleBands = computed(() =>
  (props.bands ?? []).map((band) => {
    const d = drag.value?.band.key === band.key ? drag.value : null
    return d
      ? {
          ...band,
          day: d.day,
          startMinutes: d.startMinutes,
          endMinutes: d.endMinutes,
          dragging: true,
        }
      : { ...band, dragging: false }
  }),
)

function scrollToContent(): void {
  const el = container.value
  if (!el) return
  let first = Infinity
  for (const p of placements.value) first = Math.min(first, p.startMinutes)
  for (const b of props.blocks ?? []) first = Math.min(first, b.startMinutes)
  for (const b of props.bands ?? []) first = Math.min(first, b.startMinutes)
  if (!Number.isFinite(first)) return
  el.scrollTo({ top: Math.max(0, (rowOf(first) - 2) * ROW_PX - 16), behavior: 'auto' })
}
onMounted(() => void nextTick(scrollToContent))
watch(
  () => [props.firstDay, props.mode, props.loading],
  () => void nextTick(scrollToContent),
)

function dayLabel(day: string): string {
  const [y, m, d] = day.split('-').map(Number) as [number, number, number]
  const date = new Date(Date.UTC(y, m - 1, d, 12))
  return `${date.toLocaleDateString('de-DE', { weekday: 'short', timeZone: 'UTC' })} ${String(d).padStart(2, '0')}.${String(m).padStart(2, '0')}.`
}
</script>

<template>
  <div
    ref="container"
    :class="
      cn(
        'relative overflow-auto overscroll-contain rounded-xl border border-border bg-surface select-none',
        loading && 'opacity-60',
      )
    "
    @pointermove="onPointerMove"
    @pointerup="onPointerUp"
    @pointercancel="onPointerUp"
  >
    <div class="relative grid min-w-full" :style="gridStyle">
      <!-- week header -->
      <template v-if="mode === 'week'">
        <div class="sticky left-0 top-0 z-30 border-b border-r border-border bg-surface" />
        <div
          v-for="(day, i) in days"
          :key="day"
          class="sticky top-0 z-20 flex items-center justify-center border-b border-r border-border bg-surface px-1 text-center font-display text-xs font-semibold"
          :style="{ gridColumn: i + 2, gridRow: 1 }"
        >
          {{ dayLabel(day) }}
        </div>
      </template>

      <!-- empty cells -->
      <div
        v-for="(day, i) in days"
        :key="`bg-${day}`"
        :class="cn('border-r border-border', selectable && 'cursor-crosshair')"
        :style="{ gridColumn: i + 2, gridRow: `2 / span ${rows}` }"
        @click="onEmptyClick($event, day)"
      />

      <!-- container bands -->
      <div
        v-for="band in visibleBands"
        :key="band.key"
        :class="
          cn(
            'relative z-[5] m-px rounded-md border text-[11px] leading-tight',
            selectable ? 'cursor-grab' : 'pointer-events-none',
            band.dragging && 'ring-2 ring-primary',
          )
        "
        :style="{
          gridColumn: columnOf(band.day),
          gridRow: `${clampRow(rowOf(band.startMinutes))} / ${clampRow(rowOf(band.endMinutes))}`,
          background: band.color,
          borderColor: 'color-mix(in oklch, var(--foreground), transparent 70%)',
        }"
        @pointerdown="onBandPointerDown($event, band, 'move')"
      >
        <span
          v-if="band.label"
          class="absolute left-1 top-0.5 font-semibold text-foreground drop-shadow"
          >{{ band.label }}</span
        >
        <span
          v-if="selectable"
          class="absolute inset-x-0 bottom-0 h-2 cursor-ns-resize"
          @pointerdown.stop="onBandPointerDown($event, band, 'resize')"
        />
      </div>

      <!-- hour axis -->
      <template v-for="mark in hourMarks" :key="mark.minutes">
        <div
          class="sticky left-0 z-10 border-r border-border bg-surface pl-1 font-display text-[10px] font-semibold leading-none text-muted-foreground"
          :class="rowOf(mark.minutes) > 2 ? '-mt-[6px]' : 'pt-0.5'"
          :style="{ gridColumn: 1, gridRow: `${clampRow(rowOf(mark.minutes))} / span 2` }"
        >
          {{ mark.label }}
        </div>
        <div
          class="pointer-events-none border-t border-border/60"
          :style="{ gridColumn: `2 / span ${days.length}`, gridRow: clampRow(rowOf(mark.minutes)) }"
        />
      </template>

      <!-- day view: cards -->
      <div
        v-for="row in dayRows"
        :key="row.key"
        class="z-10 flex gap-1 overflow-x-auto px-1"
        :style="{ gridColumn: 2, gridRow: `${row.rowStart} / ${row.rowEnd}` }"
      >
        <button
          v-for="card in row.cards"
          :key="card.key"
          type="button"
          class="flex min-w-[96px] flex-1 flex-col items-center justify-center overflow-hidden rounded-lg border-2 px-1 py-0.5 text-center text-xs leading-tight transition hover:ring-2 hover:ring-primary"
          :style="cardStyle(card)"
          :title="
            card.status === 'blocked'
              ? t('location_blocked_reason', { reason: card.blockReason ?? '' })
              : undefined
          "
          @click="emit('selectCard', card)"
        >
          <span class="w-full truncate font-semibold">{{ card.type.name }}</span>
          <span v-if="card.status === 'free'" class="font-semibold">{{ t('location_free') }}</span>
          <span v-else-if="card.status === 'blocked'" class="w-full truncate">{{
            t('location_blocked')
          }}</span>
          <span v-else class="w-full truncate">{{ card.employeeName }}</span>
          <span v-if="row.rowEnd - row.rowStart >= 6" class="text-[10px] opacity-90">
            {{ minutesToClock(minutesIntoDay(card.start, firstDay) + card.offsetMinutes) }}–{{
              minutesToClock(minutesIntoDay(card.end, firstDay) + card.offsetMinutes)
            }}
          </span>
        </button>
      </div>

      <!-- week view: blocks -->
      <template v-if="mode === 'week'">
        <button
          v-for="block in blocks"
          :key="block.key"
          type="button"
          class="z-10 m-px flex flex-col items-center justify-center overflow-hidden rounded-lg border-2 px-1 text-center text-xs leading-tight transition hover:ring-2 hover:ring-primary"
          :style="{
            ...(block.card
              ? cardStyle(block.card)
              : { ...blockStyle(block), borderColor: 'transparent', color: 'var(--foreground)' }),
            gridColumn: columnOf(block.day),
            gridRow: `${clampRow(rowOf(block.startMinutes))} / ${clampRow(rowOf(block.endMinutes))}`,
          }"
          :class="!block.card && 'border-transparent'"
          :title="block.card ? block.card.type.name : undefined"
          @click="block.card ? emit('selectCard', block.card) : emit('selectBlock', block)"
        >
          <template v-if="block.card">
            <span class="w-full truncate font-semibold">{{ block.card.type.name }}</span>
            <span class="w-full truncate">{{ block.card.employeeName }}</span>
          </template>
          <template v-else>
            <span class="font-semibold">{{ block.available }}/{{ block.total }}</span>
            <span>{{ t('location_free') }}</span>
          </template>
        </button>
      </template>
    </div>
  </div>
</template>
