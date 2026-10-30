<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import { useRoute, useRouter } from 'vue-router'
import { ChevronLeft, ChevronRight, Plus } from '@lucide/vue'
import { toast } from 'vue-sonner'
import PageHeader from '@/components/PageHeader.vue'
import ShiftGrid from '@/components/shift-grid/ShiftGrid.vue'
import type { GridBand } from '@/components/shift-grid/types'
import ContainerDialog from '@/dialogs/ContainerDialog.vue'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Tabs, TabsList, TabsTrigger } from '@/components/ui/tabs'
import {
  ApiError,
  containersApi,
  locationsApi,
  type ContainerResponse,
  type LocationResponse,
  type ShiftResponse,
} from '@/api'
import { useSettingsStore } from '@/stores/settings'
import { useSeasonStore } from '@/stores/season'
import { useLocationsStore } from '@/stores/locations'
import { useErrors } from '@/composables/useErrors'
import { containerEnd } from '@/domain/container'
import { addDays, dayKey, instantAt, minutesIntoDay, todayKey, weekStart } from '@/domain/time'

const props = defineProps<{ id: string }>()
const { t } = useI18n()
const route = useRoute()
const router = useRouter()
const settings = useSettingsStore()
const season = useSeasonStore()
const locationsStore = useLocationsStore()
const { report, messageOf } = useErrors()

const location = ref<LocationResponse | null>(null)
const shifts = ref<ShiftResponse[]>([])
const loading = ref(true)
const dialogOpen = ref(false)
const editing = ref<ContainerResponse | null>(null)
const initialStart = ref<Date | null>(null)

const view = computed<'day' | 'week'>(() => (route.query.view === 'day' ? 'day' : 'week'))
const day = computed(() =>
  typeof route.query.date === 'string'
    ? route.query.date
    : location.value?.containers?.[0]
      ? dayKey(location.value.containers[0].start)
      : season.current
        ? dayKey(season.current.startDate)
        : todayKey(),
)
const days = computed(() =>
  view.value === 'week'
    ? Array.from({ length: 7 }, (_, i) => addDays(weekStart(day.value), i))
    : [day.value],
)

function setQuery(patch: Record<string, string | undefined>): void {
  void router.replace({ query: { ...route.query, ...patch } })
}

async function load(): Promise<void> {
  loading.value = true
  try {
    location.value = await locationsApi.get(props.id)
    if (season.current) {
      shifts.value = await locationsApi.shifts(
        props.id,
        new Date(season.current.startDate),
        new Date(new Date(season.current.endDate).getTime() + 86400000),
      )
    }
  } catch (error) {
    await report(error)
  } finally {
    loading.value = false
  }
}
watch(() => props.id, load, { immediate: true })

const bands = computed<GridBand[]>(() => {
  const out: GridBand[] = []
  for (const c of location.value?.containers ?? []) {
    for (const d of days.value) {
      const start = minutesIntoDay(c.start, d)
      const end = minutesIntoDay(containerEnd(c), d)
      if (end <= settings.window.start || start >= settings.window.end) continue
      out.push({
        key: `${c.id}-${d}`,
        day: d,
        startMinutes: start,
        endMinutes: end,
        color: c.color || 'rgba(59,130,246,.3)',
        label: c.framework.name,
        payload: c,
      })
    }
  }
  return out
})

function shiftCountOf(c: ContainerResponse): number {
  return shifts.value.filter((s) => s.containerId === c.id).length
}

function onEmpty(payload: { day: string; minutes: number }): void {
  editing.value = null
  initialStart.value = instantAt(payload.day, Math.floor(payload.minutes / 15) * 15)
  dialogOpen.value = true
}
function onBand(band: GridBand): void {
  editing.value = band.payload as ContainerResponse
  dialogOpen.value = true
}
async function onMove(payload: {
  band: GridBand
  day: string
  startMinutes: number
  endMinutes: number
}): Promise<void> {
  const c = payload.band.payload as ContainerResponse
  const minutesPerShift = c.framework.secondsPerShift / 60
  const totalShifts = Math.max(
    1,
    Math.round((payload.endMinutes - payload.startMinutes) / minutesPerShift),
  )
  // the band only shows the part within the visible window; keep the original start when only the length changed
  const movedStart =
    payload.startMinutes !== payload.band.startMinutes || payload.day !== payload.band.day
  const start = movedStart ? instantAt(payload.day, payload.startMinutes) : new Date(c.start)
  try {
    await containersApi.update({
      id: c.id,
      frameworkId: c.framework.id,
      start: start.toISOString(),
      totalShifts,
      color: c.color,
    })
    toast.success(t('saved'))
    await load()
  } catch (error) {
    if (error instanceof ApiError && error.status < 500) toast.error(messageOf(error))
    else await report(error)
  }
}
async function onSaved(): Promise<void> {
  await load()
  if (season.currentId) await locationsStore.load(season.currentId).catch(() => undefined)
}
function go(delta: number): void {
  setQuery({ date: addDays(day.value, delta * (view.value === 'week' ? 7 : 1)) })
}
</script>

<template>
  <div
    class="flex h-[calc(100dvh-3.5rem-var(--nav-height)-2rem)] flex-col lg:h-[calc(100dvh-3.5rem-3rem)]"
  >
    <PageHeader
      :title="t('admin_containers_title', { name: location?.name ?? '' })"
      :subtitle="t('admin_containers_subtitle')"
    >
      <template #actions>
        <Button variant="outline" size="icon-sm" @click="go(-1)"><ChevronLeft /></Button>
        <Input
          type="date"
          class="w-[9.5rem]"
          :model-value="day"
          @update:model-value="setQuery({ date: String($event) })"
        />
        <Button variant="outline" size="icon-sm" @click="go(1)"><ChevronRight /></Button>
        <Tabs :model-value="view" @update:model-value="setQuery({ view: String($event) })">
          <TabsList>
            <TabsTrigger value="day">{{ t('location_view_day') }}</TabsTrigger>
            <TabsTrigger value="week">{{ t('location_view_week') }}</TabsTrigger>
          </TabsList>
        </Tabs>
        <Button size="sm" @click="onEmpty({ day, minutes: settings.window.start })"
          ><Plus /> {{ t('admin_container_new') }}</Button
        >
      </template>
    </PageHeader>
    <div class="min-h-0 flex-1">
      <ShiftGrid
        class="h-full"
        :mode="view"
        :first-day="days[0]!"
        :days="days"
        :window-start="settings.window.start"
        :window-end="settings.window.end"
        :bands="bands"
        selectable
        :loading="loading"
        @select-empty="onEmpty"
        @select-band="onBand"
        @move-band="onMove"
      />
    </div>
    <ContainerDialog
      v-if="season.currentId"
      v-model:open="dialogOpen"
      :container="editing"
      :location-id="id"
      :season-id="season.currentId"
      :initial-start="initialStart"
      :shift-count="editing ? shiftCountOf(editing) : 0"
      @saved="onSaved"
    />
  </div>
</template>
