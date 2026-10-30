<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import { ArrowDownUp } from '@lucide/vue'
import PageHeader from '@/components/PageHeader.vue'
import ResponsiveDialog from '@/components/ResponsiveDialog.vue'
import { Button } from '@/components/ui/button'
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table'
import { employeesApi, shiftsApi, type EmployeeResponse, type ShiftResponse } from '@/api'
import { useSeasonStore } from '@/stores/season'
import { useLocationsStore } from '@/stores/locations'
import { useErrors } from '@/composables/useErrors'
import { formatDayShort, formatTime, minutesToClock, minutesToDaysHours } from '@/domain/time'

const { t } = useI18n()
const season = useSeasonStore()
const locations = useLocationsStore()
const { report } = useErrors()

const loading = ref(false)
const rows = ref<{ employee: EmployeeResponse; shifts: ShiftResponse[]; minutes: number }[]>([])
const sortBy = ref<'name' | 'count' | 'minutes'>('count')
const sortDesc = ref(true)
const selected = ref<{ employee: EmployeeResponse; shifts: ShiftResponse[] } | null>(null)
const dialogOpen = ref(false)

async function load(): Promise<void> {
  if (!season.currentId) return
  loading.value = true
  try {
    const [employees, shifts] = await Promise.all([
      employeesApi.list(),
      shiftsApi.list(season.currentId),
    ])
    rows.value = employees.map((employee) => {
      const own = shifts.filter((s) => s.employeeId === employee.id)
      const minutes = own.reduce(
        (sum, s) => sum + (new Date(s.end).getTime() - new Date(s.start).getTime()) / 60000,
        0,
      )
      return { employee, shifts: own, minutes }
    })
  } catch (error) {
    await report(error)
  } finally {
    loading.value = false
  }
}
watch(() => season.currentId, load, { immediate: true })

const sorted = computed(() =>
  [...rows.value].sort((a, b) => {
    const v =
      sortBy.value === 'name'
        ? a.employee.fullName.localeCompare(b.employee.fullName, 'de')
        : sortBy.value === 'count'
          ? a.shifts.length - b.shifts.length
          : a.minutes - b.minutes
    return sortDesc.value ? -v : v
  }),
)
const totals = computed(() => {
  const count = rows.value.reduce((s, r) => s + r.shifts.length, 0)
  const minutes = rows.value.reduce((s, r) => s + r.minutes, 0)
  return { count, ...minutesToDaysHours(minutes) }
})
function sort(key: 'name' | 'count' | 'minutes'): void {
  if (sortBy.value === key) sortDesc.value = !sortDesc.value
  else {
    sortBy.value = key
    sortDesc.value = key !== 'name'
  }
}
function show(row: { employee: EmployeeResponse; shifts: ShiftResponse[] }): void {
  selected.value = {
    employee: row.employee,
    shifts: [...row.shifts].sort((a, b) => a.start.localeCompare(b.start)),
  }
  dialogOpen.value = true
}
</script>

<template>
  <div class="mx-auto max-w-4xl">
    <PageHeader :title="t('admin_statistics_title')" />
    <p class="mb-3 text-sm text-muted-foreground">
      {{
        t('admin_statistics_summary', {
          count: totals.count,
          days: totals.days,
          hours: totals.hours,
        })
      }}
    </p>
    <div class="overflow-hidden rounded-xl border border-border bg-card">
      <Table>
        <TableHeader>
          <TableRow>
            <TableHead
              ><button type="button" class="inline-flex items-center gap-1" @click="sort('name')">
                {{ t('admin_statistics_person') }} <ArrowDownUp class="size-3" /></button
            ></TableHead>
            <TableHead class="w-32"
              ><button type="button" class="inline-flex items-center gap-1" @click="sort('count')">
                {{ t('admin_statistics_shifts') }} <ArrowDownUp class="size-3" /></button
            ></TableHead>
            <TableHead class="w-32"
              ><button
                type="button"
                class="inline-flex items-center gap-1"
                @click="sort('minutes')"
              >
                {{ t('admin_statistics_total') }} <ArrowDownUp class="size-3" /></button
            ></TableHead>
          </TableRow>
        </TableHeader>
        <TableBody>
          <TableRow v-for="row in sorted" :key="row.employee.id">
            <TableCell>{{ row.employee.fullName }}</TableCell>
            <TableCell
              ><Button
                variant="link"
                class="h-auto p-0"
                :disabled="!row.shifts.length"
                @click="show(row)"
                >{{ row.shifts.length }}</Button
              ></TableCell
            >
            <TableCell class="tabular-nums">{{ minutesToClock(row.minutes) }} h</TableCell>
          </TableRow>
        </TableBody>
      </Table>
    </div>

    <ResponsiveDialog
      v-model:open="dialogOpen"
      :title="
        t('admin_statistics_shifts_of', {
          name: selected?.employee.firstName ?? selected?.employee.fullName ?? '',
        })
      "
    >
      <ul v-if="selected" class="space-y-2">
        <li v-for="s in selected.shifts" :key="s.id">
          <RouterLink
            :to="{ path: `/locations/${s.locationId}`, query: { date: s.start.slice(0, 10) } }"
            class="block rounded-lg px-3 py-2 text-sm text-white"
            :style="{ background: s.type?.color ?? '#555' }"
          >
            {{ locations.byId.get(s.locationId)?.name ?? '' }} · {{ s.type?.name }} ·
            {{ formatDayShort(s.start) }} {{ formatTime(s.start) }}
          </RouterLink>
        </li>
      </ul>
    </ResponsiveDialog>
  </div>
</template>
