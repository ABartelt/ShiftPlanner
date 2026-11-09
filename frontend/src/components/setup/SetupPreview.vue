<script setup lang="ts">
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import { formatDayShort, instantAt, minutesToClock } from '@/domain/time'
import { cn } from '@/lib/utils'

export type PreviewFocus = 'location' | 'container' | 'shift' | 'seat' | null
export interface PreviewSeat {
  key: string
  name: string
  color: string
  count: number
}

/** Live mock of the location as it will appear in the plan, labelled with the domain terms. */
const props = defineProps<{
  locationName: string
  locationTypeName?: string
  days: string[]
  startMinutes: number
  shiftMinutes: number
  totalShifts: number
  color: string
  seats: PreviewSeat[]
  capacity: number
  focus: PreviewFocus
}>()
const { t } = useI18n()

const VISIBLE_SHIFTS = 4
const VISIBLE_DAYS = 3
const rows = computed(() =>
  Array.from({ length: Math.min(props.totalShifts, VISIBLE_SHIFTS) }, (_, i) => ({
    start: minutesToClock(props.startMinutes + i * props.shiftMinutes),
    end: minutesToClock(props.startMinutes + (i + 1) * props.shiftMinutes),
  })),
)
const blockEnd = computed(() =>
  minutesToClock(props.startMinutes + props.totalShifts * props.shiftMinutes),
)

const glossary = computed(() => [
  { id: 'location', label: t('wizard_gl_location'), text: t('wizard_gl_location_text') },
  { id: 'container', label: t('wizard_gl_container'), text: t('wizard_gl_container_text') },
  { id: 'shift', label: t('wizard_gl_shift'), text: t('wizard_gl_shift_text') },
  { id: 'seat', label: t('wizard_gl_seat'), text: t('wizard_gl_seat_text') },
])

function zone(id: Exclude<PreviewFocus, null>): string {
  return props.focus === id
    ? 'ring-2 ring-primary ring-offset-2 ring-offset-card'
    : 'ring-0 ring-transparent'
}
function tag(id: Exclude<PreviewFocus, null>): string {
  return cn(
    'font-display text-[0.625rem] font-semibold tracking-[0.18em] uppercase transition-colors',
    props.focus === id ? 'text-primary-ink' : 'text-muted-foreground/70',
  )
}
</script>

<template>
  <div class="space-y-4">
    <section class="rounded-xl border border-border bg-card p-4">
      <p class="eyebrow">{{ t('wizard_preview') }}</p>
      <p class="mt-1 text-xs text-muted-foreground">{{ t('wizard_preview_hint') }}</p>

      <!-- location -->
      <div :class="cn('mt-4 rounded-lg p-2 transition-shadow', zone('location'))">
        <div class="flex items-center justify-between gap-2">
          <span :class="tag('location')">{{ t('wizard_gl_location') }}</span>
          <span v-if="locationTypeName" class="truncate text-[0.6875rem] text-muted-foreground">{{
            locationTypeName
          }}</span>
        </div>
        <p
          :class="
            cn(
              'mt-0.5 truncate font-display text-lg font-semibold',
              !locationName.trim() && 'text-muted-foreground/60 italic',
            )
          "
        >
          {{ locationName.trim() || t('wizard_preview_location') }}
        </p>
        <p class="truncate text-xs text-muted-foreground">
          <template v-if="days.length">
            {{
              days
                .slice(0, VISIBLE_DAYS)
                .map((d) => formatDayShort(instantAt(d, 720)))
                .join(' · ')
            }}
            <template v-if="days.length > VISIBLE_DAYS">
              {{ t('wizard_preview_more_days', days.length - VISIBLE_DAYS) }}</template
            >
          </template>
          <template v-else>{{ t('wizard_preview_no_days') }}</template>
        </p>
      </div>

      <!-- container -->
      <div :class="cn('mt-3 rounded-lg transition-shadow', zone('container'))">
        <div class="rounded-lg border border-foreground/10 p-2" :style="{ background: color }">
          <div class="mb-1.5 flex items-center justify-between px-1">
            <span :class="tag('container')">{{ t('wizard_gl_container') }}</span>
            <span class="text-xs font-semibold tabular-nums"
              >{{ minutesToClock(startMinutes) }} – {{ blockEnd }}</span
            >
          </div>

          <!-- shifts -->
          <ol class="space-y-1.5">
            <li
              v-for="(r, i) in rows"
              :key="i"
              :class="
                cn('rounded-md bg-background/80 p-2 transition-shadow', i === 0 && zone('shift'))
              "
            >
              <div class="flex items-center justify-between gap-2">
                <span class="text-xs font-medium tabular-nums">{{ r.start }} – {{ r.end }}</span>
                <span v-if="i === 0" :class="tag('shift')">{{ t('wizard_gl_shift') }}</span>
              </div>
              <!-- seats -->
              <div
                :class="
                  cn(
                    'mt-1.5 flex flex-wrap gap-1 rounded-sm transition-shadow',
                    i === 0 && seats.length && zone('seat'),
                  )
                "
              >
                <template v-for="s in seats" :key="s.key">
                  <span
                    v-for="n in Math.min(s.count, 6)"
                    :key="n"
                    class="inline-flex h-5 min-w-5 items-center justify-center rounded px-1 text-[0.625rem] font-bold text-white"
                    :style="{ background: s.color }"
                    :title="s.name"
                    >{{ s.name.trim().charAt(0).toUpperCase() }}</span
                  >
                  <span v-if="s.count > 6" class="self-center text-[0.625rem] text-muted-foreground"
                    >+{{ s.count - 6 }}</span
                  >
                </template>
                <span v-if="!seats.length" class="text-[0.6875rem] text-muted-foreground italic">{{
                  t('wizard_preview_no_seats')
                }}</span>
              </div>
              <p v-if="i === 0 && seats.length" :class="cn('mt-1', tag('seat'))">
                {{ t('wizard_gl_seat') }}
              </p>
            </li>
          </ol>
          <p
            v-if="totalShifts > VISIBLE_SHIFTS"
            class="mt-1.5 px-1 text-[0.6875rem] text-foreground/80"
          >
            {{ t('wizard_preview_more', totalShifts - VISIBLE_SHIFTS) }}
          </p>
        </div>
      </div>

      <!-- seat legend + total -->
      <ul v-if="seats.length" class="mt-3 space-y-1 text-xs">
        <li v-for="s in seats" :key="s.key" class="flex items-center gap-2">
          <span class="size-2.5 shrink-0 rounded-full" :style="{ background: s.color }" />
          <span class="flex-1 truncate">{{ s.name }}</span>
          <span class="tabular-nums text-muted-foreground">{{ s.count }}×</span>
        </li>
      </ul>
      <div class="mt-4 flex items-baseline gap-2 border-t border-border pt-3">
        <span class="font-display text-2xl font-semibold tabular-nums">{{ capacity }}</span>
        <span class="text-xs text-muted-foreground">{{ t('wizard_capacity', capacity) }}</span>
      </div>
    </section>

    <!-- glossary: nested to show the hierarchy -->
    <section class="rounded-xl border border-border bg-card p-4">
      <p class="eyebrow">{{ t('wizard_glossary') }}</p>
      <ol class="mt-3 space-y-2.5">
        <li
          v-for="(g, i) in glossary"
          :key="g.id"
          :style="{ paddingLeft: `${i * 0.75}rem` }"
          class="relative"
        >
          <div
            :class="
              cn(
                'border-l-2 py-0.5 pl-2.5 transition-colors',
                focus === g.id ? 'border-primary' : 'border-border',
              )
            "
          >
            <p
              :class="
                cn('text-sm font-semibold', focus === g.id ? 'text-primary-ink' : 'text-foreground')
              "
            >
              {{ g.label }}
            </p>
            <p class="text-xs leading-snug text-muted-foreground">{{ g.text }}</p>
          </div>
        </li>
      </ol>
    </section>
  </div>
</template>
