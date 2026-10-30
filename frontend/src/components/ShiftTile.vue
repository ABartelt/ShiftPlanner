<script setup lang="ts">
import { computed } from 'vue'
import type { ShiftTypeResponse } from '@/api/types'
import { durationToMinutes, dayKey, formatDayShort, formatRange } from '@/domain/time'
import { typeColor } from '@/domain/shifts'

const props = defineProps<{
  type: ShiftTypeResponse
  start: string | Date
  end: string | Date
  locationId: string
  locationName: string
  /** filled tile = own shift, outlined tile = free shift */
  filled?: boolean
  mine?: boolean
  note?: string
}>()

const offset = computed(() => durationToMinutes(props.type.startingTimeShift))
const color = computed(() => typeColor(props.type))
const to = computed(() => ({
  path: `/locations/${props.locationId}`,
  query: { date: dayKey(props.start), view: 'day', ...(props.mine ? { mine: '1' } : {}) },
}))
</script>

<template>
  <RouterLink
    :to="to"
    class="flex min-w-[150px] flex-1 flex-col rounded-xl border-2 px-3 py-2 text-sm leading-snug transition hover:ring-2 hover:ring-primary/60"
    :style="
      filled
        ? { background: color, borderColor: color, color: '#fff' }
        : { borderColor: color, color }
    "
  >
    <span class="font-semibold">{{ locationName }}</span>
    <span>{{ type.name }}</span>
    <span class="text-xs opacity-90"
      >{{ formatDayShort(start) }} · {{ formatRange(start, end, offset) }}</span
    >
    <span v-if="note" class="text-xs opacity-90">{{ note }}</span>
  </RouterLink>
</template>
