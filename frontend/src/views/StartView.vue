<script setup lang="ts">
import BrandText from '@/components/BrandText.vue'
import { computed, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import { useRoute } from 'vue-router'
import { CalendarArrowDown, Clock, Group, PieChart, Users } from '@lucide/vue'
import { toast } from 'vue-sonner'
import ShiftTile from '@/components/ShiftTile.vue'
import { Button } from '@/components/ui/button'
import { Skeleton } from '@/components/ui/skeleton'
import { Tooltip, TooltipContent, TooltipProvider, TooltipTrigger } from '@/components/ui/tooltip'
import { employeesApi, shiftsApi, type ShiftResponse, type ShiftTypesCountResponse } from '@/api'
import { useAuth } from '@/auth/useAuth'
import { useSettingsStore } from '@/stores/settings'
import { useSeasonStore } from '@/stores/season'
import { useLocationsStore } from '@/stores/locations'
import { useErrors } from '@/composables/useErrors'
import { useIcs } from '@/composables/useIcs'
import { usePageTitle } from '@/composables/usePageTitle'
import { minutesToDaysHours } from '@/domain/time'

const { t } = useI18n()
const route = useRoute()
const auth = useAuth()
const settings = useSettingsStore()
const season = useSeasonStore()
const locations = useLocationsStore()
const { report } = useErrors()
const ics = useIcs()
usePageTitle().set('')

const loading = ref(true)
const myShifts = ref<ShiftResponse[]>([])
const freeShifts = ref<ShiftTypesCountResponse[]>([])
const allSlots = ref<ShiftTypesCountResponse[]>([])
const memberCount = ref<number | null>(null)

const stats = computed(() => {
  let total = 0
  let available = 0
  let assignedMinutes = 0
  for (const s of allSlots.value) {
    total += s.totalCount
    available += s.availableCount
    const minutes = (new Date(s.end).getTime() - new Date(s.start).getTime()) / 60000
    assignedMinutes += minutes * (s.totalCount - s.availableCount)
  }
  const assigned = total - available
  const { days, hours } = minutesToDaysHours(assignedMinutes)
  return {
    total,
    available,
    assigned,
    days: (assignedMinutes / 1440).toFixed(1).replace('.', ','),
    daysInt: days,
    hours,
    percent: total > 0 ? Math.round((assigned / total) * 100) : 0,
  }
})

async function load(): Promise<void> {
  const seasonId = season.currentId
  const sub = auth.profile.value?.sub
  if (!seasonId || !sub) return
  loading.value = true
  try {
    const now = new Date()
    const [mine, free, all, members] = await Promise.all([
      employeesApi.shifts(sub, seasonId, { count: 6, startingFrom: now }),
      shiftsApi.availableTypes(seasonId, { limit: 12, startingFrom: now }),
      shiftsApi.availableTypes(seasonId, { includeNonAvailable: true }),
      auth.isAdmin.value ? employeesApi.list().then((l) => l.length) : Promise.resolve(null),
    ])
    myShifts.value = mine
    freeShifts.value = free
    allSlots.value = all
    memberCount.value = members
  } catch (error) {
    await report(error)
  } finally {
    loading.value = false
  }
}

watch(() => season.currentId, load, { immediate: true })
watch(
  () => route.query.denied,
  (denied) => {
    if (denied) toast.warning(t('not_allowed'))
  },
  { immediate: true },
)

const locationName = (id: string) => locations.byId.get(id)?.name ?? ''
</script>

<template>
  <div class="mx-auto max-w-5xl">
    <h1 class="headline text-3xl">
      <BrandText
        :text="
          t('start_greeting', {
            greeting: settings.settings.greeting,
            name: auth.profile.value?.firstName ?? '',
          })
        "
      />
    </h1>

    <TooltipProvider>
      <div class="mt-4 grid grid-cols-2 gap-3 md:grid-cols-4">
        <Tooltip v-if="memberCount !== null">
          <TooltipTrigger as-child>
            <div class="rounded-xl border border-border bg-card p-4">
              <Users class="size-5 text-primary-ink" />
              <Skeleton v-if="loading" class="mt-2 h-8 w-16" />
              <p v-else class="mt-2 font-display text-3xl font-semibold">{{ memberCount }}</p>
              <p class="text-xs text-muted-foreground">{{ settings.settings.memberName }}</p>
            </div>
          </TooltipTrigger>
          <TooltipContent>{{
            t('stat_members_tip', { count: memberCount, memberName: settings.settings.memberName })
          }}</TooltipContent>
        </Tooltip>
        <Tooltip>
          <TooltipTrigger as-child>
            <div class="rounded-xl border border-border bg-card p-4">
              <Group class="size-5 text-primary-ink" />
              <Skeleton v-if="loading" class="mt-2 h-8 w-16" />
              <p v-else class="mt-2 font-display text-3xl font-semibold">{{ stats.assigned }}</p>
              <p class="text-xs text-muted-foreground">{{ t('stat_shifts') }}</p>
            </div>
          </TooltipTrigger>
          <TooltipContent>{{
            t('stat_shifts_tip', { assigned: stats.assigned, total: stats.total })
          }}</TooltipContent>
        </Tooltip>
        <Tooltip>
          <TooltipTrigger as-child>
            <div class="rounded-xl border border-border bg-card p-4">
              <Clock class="size-5 text-primary-ink" />
              <Skeleton v-if="loading" class="mt-2 h-8 w-16" />
              <p v-else class="mt-2 font-display text-3xl font-semibold">{{ stats.days }}</p>
              <p class="text-xs text-muted-foreground">{{ t('stat_days') }}</p>
            </div>
          </TooltipTrigger>
          <TooltipContent>{{
            t('stat_days_tip', { days: stats.daysInt, hours: stats.hours })
          }}</TooltipContent>
        </Tooltip>
        <Tooltip>
          <TooltipTrigger as-child>
            <div class="rounded-xl border border-border bg-card p-4">
              <PieChart class="size-5 text-primary-ink" />
              <Skeleton v-if="loading" class="mt-2 h-8 w-16" />
              <p v-else class="mt-2 font-display text-3xl font-semibold">{{ stats.percent }}</p>
              <p class="text-xs text-muted-foreground">{{ t('stat_percent') }}</p>
            </div>
          </TooltipTrigger>
          <TooltipContent>{{
            t('stat_percent_tip', { available: stats.available, total: stats.total })
          }}</TooltipContent>
        </Tooltip>
      </div>
    </TooltipProvider>

    <section class="mt-8">
      <div class="flex flex-wrap items-center justify-between gap-2">
        <h2 class="text-lg font-semibold">{{ t('start_my_shifts') }}</h2>
        <Button
          variant="outline"
          size="sm"
          :disabled="!season.currentId"
          @click="ics.download(season.currentId!)"
        >
          <CalendarArrowDown /> {{ t('start_ics') }}
        </Button>
      </div>
      <div v-if="loading" class="mt-3 flex flex-wrap gap-3">
        <Skeleton v-for="i in 3" :key="i" class="h-20 min-w-[150px] flex-1" />
      </div>
      <p v-else-if="!myShifts.length" class="mt-3 text-sm text-muted-foreground">
        {{ t('start_my_shifts_empty') }}
      </p>
      <div v-else class="mt-3 flex flex-wrap gap-3">
        <ShiftTile
          v-for="s in myShifts"
          :key="s.id"
          :type="s.type!"
          :start="s.start"
          :end="s.end"
          :location-id="s.locationId"
          :location-name="locationName(s.locationId)"
          filled
          mine
        />
      </div>
    </section>

    <section class="mt-8">
      <h2 class="text-lg font-semibold">{{ t('start_free_shifts') }}</h2>
      <div v-if="loading" class="mt-3 flex flex-wrap gap-3">
        <Skeleton v-for="i in 4" :key="i" class="h-20 min-w-[150px] flex-1" />
      </div>
      <p v-else-if="!freeShifts.length" class="mt-3 text-sm text-muted-foreground">
        {{ t('start_free_shifts_empty') }}
      </p>
      <div v-else class="mt-3 flex flex-wrap gap-3">
        <ShiftTile
          v-for="s in freeShifts"
          :key="`${s.locationId}-${s.start}-${s.type.id}`"
          :type="s.type"
          :start="s.start"
          :end="s.end"
          :location-id="s.locationId"
          :location-name="locationName(s.locationId)"
          :note="t('location_free_count', { available: s.availableCount, total: s.totalCount })"
        />
      </div>
    </section>

    <p class="mt-10 text-sm text-muted-foreground">
      {{ t('start_help') }}
      <a :href="settings.contactLink" class="text-primary-ink underline">{{
        settings.settings.contact || t('error_contact_fallback')
      }}</a>
    </p>
  </div>
</template>
