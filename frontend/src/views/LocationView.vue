<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import { useRoute, useRouter } from 'vue-router'
import { ChevronLeft, ChevronRight } from '@lucide/vue'
import { toast } from 'vue-sonner'
import PageHeader from '@/components/PageHeader.vue'
import ShiftGrid from '@/components/shift-grid/ShiftGrid.vue'
import type { GridBand, WeekBlock } from '@/components/shift-grid/types'
import { useSwipe } from '@/components/shift-grid/useSwipe'
import ShiftDialog from '@/dialogs/ShiftDialog.vue'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Tabs, TabsList, TabsTrigger } from '@/components/ui/tabs'
import {
  locationsApi,
  type LocationResponse,
  type ShiftResponse,
  type ShiftTypesCountResponse,
} from '@/api'
import { useAuth } from '@/auth/useAuth'
import { useSettingsStore } from '@/stores/settings'
import { useSeasonStore } from '@/stores/season'
import { useLocationsStore } from '@/stores/locations'
import { useErrors } from '@/composables/useErrors'
import { buildDayCards, cardFromShift, type ShiftCard } from '@/domain/shifts'
import { containerContaining, containerEnd, snapToSlot } from '@/domain/container'
import { addDays, dayKey, instantAt, minutesIntoDay, todayKey, weekStart } from '@/domain/time'
import { cn } from '@/lib/utils'

const props = defineProps<{ id: string }>()
const { t } = useI18n()
const route = useRoute()
const router = useRouter()
const auth = useAuth()
const settings = useSettingsStore()
const season = useSeasonStore()
const locations = useLocationsStore()
const { report } = useErrors()

const location = ref<LocationResponse | null>(null)
const shifts = ref<ShiftResponse[]>([])
const availability = ref<ShiftTypesCountResponse[]>([])
const loading = ref(true)
const firstFreeDay = ref<string | null>(null)
const dialogCard = ref<ShiftCard | null>(null)
const dialogOpen = ref(false)
const gridWrap = ref<HTMLElement | null>(null)

const view = computed<'day' | 'week'>(() => (route.query.view === 'week' ? 'week' : 'day'))
const onlyMine = computed(() => route.query.mine === '1')
const day = computed(() =>
  typeof route.query.date === 'string' && /^\d{4}-\d{2}-\d{2}$/.test(route.query.date)
    ? route.query.date
    : todayKey(),
)
const days = computed(() =>
  view.value === 'week'
    ? Array.from({ length: 7 }, (_, i) => addDays(weekStart(day.value), i))
    : [day.value],
)
const summary = computed(() => locations.byId.get(props.id))
const mySub = computed(() => auth.profile.value?.sub ?? null)

function setQuery(patch: Record<string, string | undefined>): void {
  void router.replace({ query: { ...route.query, ...patch } })
}

/** Start date rule of the old client: never before the first free shift; default max(today, first free). */
function clampDay(value: string): string {
  const floor = firstFreeDay.value
  if (floor && value < floor) return floor
  return value
}

async function loadLocation(): Promise<void> {
  loading.value = true
  try {
    const [loc, first] = await Promise.all([
      locationsApi.get(props.id),
      season.current
        ? locationsApi.availability(
            props.id,
            new Date(season.current.startDate),
            new Date(season.current.endDate),
            1,
          )
        : Promise.resolve([]),
    ])
    location.value = loc
    const firstFree = first.find((a) => a.availableCount > 0) ?? first[0]
    firstFreeDay.value = firstFree ? dayKey(firstFree.start) : null
    // a query change triggers loadRange through the watcher; only load directly when nothing changes
    if (typeof route.query.date !== 'string') {
      const initial =
        firstFreeDay.value && firstFreeDay.value > todayKey() ? firstFreeDay.value : todayKey()
      setQuery({ date: initial })
      if (initial === day.value) await loadRange()
    } else if (clampDay(day.value) !== day.value) {
      setQuery({ date: clampDay(day.value) })
    } else {
      await loadRange()
    }
  } catch (error) {
    await report(error)
  } finally {
    loading.value = false
  }
}

async function loadRange(): Promise<void> {
  if (!location.value) return
  const from = instantAt(days.value[0]!, settings.window.start)
  const to = instantAt(days.value[days.value.length - 1]!, settings.window.end)
  loading.value = true
  try {
    if (view.value === 'day') {
      shifts.value = await locationsApi.shifts(props.id, from, to)
    } else {
      const [mine, avail] = await Promise.all([
        locationsApi.shifts(props.id, from, to, mySub.value ?? undefined),
        locationsApi.availability(props.id, from, to),
      ])
      shifts.value = mine
      availability.value = avail
    }
  } catch (error) {
    await report(error)
  } finally {
    loading.value = false
  }
}

watch(() => props.id, loadLocation, { immediate: true })
watch([day, view], () => void loadRange())

const dayCards = computed(() => {
  if (!location.value || view.value !== 'day') return []
  const from = instantAt(day.value, settings.window.start)
  const to = instantAt(day.value, settings.window.end)
  let cards = buildDayCards(location.value, shifts.value, from, to, mySub.value, {
    isAdmin: auth.isAdmin.value,
  })
  if (onlyMine.value) cards = cards.filter((c) => c.status === 'mine')
  return cards
})

const weekBlocks = computed<WeekBlock[]>(() => {
  if (view.value !== 'week') return []
  const groups = new Map<string, WeekBlock>()
  for (const a of availability.value) {
    if (a.type.onlyAssignableByAdmin && !auth.isAdmin.value) continue
    const d = dayKey(a.start)
    const key = `${a.start}-${a.end}`
    const block = groups.get(key) ?? {
      key,
      day: d,
      startMinutes: minutesIntoDay(a.start, d),
      endMinutes: minutesIntoDay(a.end, d),
      available: 0,
      total: 0,
    }
    block.available += Math.max(0, a.availableCount)
    block.total += a.totalCount
    groups.set(key, block)
  }
  for (const s of shifts.value) {
    const card = cardFromShift(s, mySub.value)
    if (!card || card.status !== 'mine') continue
    const d = dayKey(s.start)
    const key = `${s.start}-${s.end}`
    groups.set(key, {
      key,
      day: d,
      startMinutes: minutesIntoDay(s.start, d) + card.offsetMinutes,
      endMinutes: minutesIntoDay(s.end, d) + card.offsetMinutes,
      available: 0,
      total: 0,
      card,
    })
  }
  const list = [...groups.values()]
  return onlyMine.value ? list.filter((b) => b.card) : list
})

const bands = computed<GridBand[]>(() => {
  if (!location.value) return []
  const out: GridBand[] = []
  for (const c of location.value.containers ?? []) {
    for (const d of days.value) {
      const start = minutesIntoDay(c.start, d)
      const end = minutesIntoDay(containerEnd(c), d)
      if (end <= settings.window.start || start >= settings.window.end) continue
      out.push({
        key: `${c.id}-${d}`,
        day: d,
        startMinutes: Math.max(start, settings.window.start),
        endMinutes: Math.min(end, settings.window.end),
        color: c.color,
      })
    }
  }
  return out
})

function onSelectCard(card: ShiftCard): void {
  if (card.status === 'free' && card.type.onlyAssignableByAdmin && !auth.isAdmin.value) {
    toast.warning(t('location_admin_only_type', { type: card.type.name }))
    return
  }
  if (card.status === 'free' && location.value) {
    const container =
      containerContaining(location.value, card.start) ??
      (location.value.containers ?? []).find((c) => c.id === card.containerId)
    if (!container) return
    const start = snapToSlot(container, card.start)
    card = {
      ...card,
      start,
      end: new Date(start.getTime() + container.framework.secondsPerShift * 1000),
    }
  }
  dialogCard.value = card
  dialogOpen.value = true
}

function onSelectBlock(block: WeekBlock): void {
  setQuery({ view: 'day', date: block.day })
}

function go(delta: number): void {
  setQuery({ date: clampDay(addDays(day.value, delta * (view.value === 'week' ? 7 : 1))) })
}
useSwipe(gridWrap, { left: () => go(1), right: () => go(-1) })

async function onSaved(): Promise<void> {
  await loadRange()
  await locations.refreshCount(props.id).catch(() => undefined)
}

const title = computed(() =>
  summary.value
    ? t('location_title', {
        name: summary.value.name,
        assigned: summary.value.assignedShifts,
        total: summary.value.totalShifts,
      })
    : (location.value?.name ?? ''),
)
</script>

<template>
  <div
    class="flex h-[calc(100dvh-3.5rem-var(--nav-height)-2rem)] flex-col lg:h-[calc(100dvh-3.5rem-3rem)]"
  >
    <PageHeader :title="title" hide-heading>
      <template #actions>
        <Button variant="outline" size="icon-sm" :aria-label="t('btn_back')" @click="go(-1)"
          ><ChevronLeft
        /></Button>
        <Input
          type="date"
          class="w-[9.5rem]"
          :model-value="day"
          :min="firstFreeDay ?? undefined"
          @update:model-value="setQuery({ date: clampDay(String($event)) })"
        />
        <Button variant="outline" size="icon-sm" :aria-label="t('btn_next')" @click="go(1)"
          ><ChevronRight
        /></Button>
        <Button variant="ghost" size="sm" @click="setQuery({ date: clampDay(todayKey()) })">{{
          t('btn_today')
        }}</Button>
        <Tabs :model-value="view" @update:model-value="setQuery({ view: String($event) })">
          <TabsList>
            <TabsTrigger value="day">{{ t('location_view_day') }}</TabsTrigger>
            <TabsTrigger value="week">{{ t('location_view_week') }}</TabsTrigger>
          </TabsList>
        </Tabs>
        <Button
          :variant="onlyMine ? 'default' : 'outline'"
          size="sm"
          :class="cn(onlyMine && 'shadow-inner')"
          @click="setQuery({ mine: onlyMine ? undefined : '1' })"
        >
          {{ t('location_only_mine') }}
        </Button>
      </template>
    </PageHeader>

    <div ref="gridWrap" class="relative min-h-0 flex-1">
      <ShiftGrid
        class="h-full"
        :mode="view"
        :first-day="days[0]!"
        :days="days"
        :window-start="settings.window.start"
        :window-end="settings.window.end"
        :cards="dayCards"
        :blocks="weekBlocks"
        :bands="bands"
        :loading="loading"
        @select-card="onSelectCard"
        @select-block="onSelectBlock"
      />
      <div v-if="loading" class="pointer-events-none absolute inset-0 grid place-items-center">
        <span class="rounded-full bg-surface-raised px-3 py-1 text-sm text-muted-foreground">{{
          t('loading_shifts')
        }}</span>
      </div>
      <p
        v-else-if="view === 'day' && !dayCards.length"
        class="pointer-events-none absolute inset-0 grid place-items-center text-sm text-muted-foreground"
      >
        {{ t('location_empty_day') }}
      </p>
    </div>

    <ShiftDialog v-model:open="dialogOpen" :card="dialogCard" @saved="onSaved" />
  </div>
</template>
