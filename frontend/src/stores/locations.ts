import { defineStore } from 'pinia'
import { computed, ref } from 'vue'
import { locationsApi, type LocationResponse, type ShiftsCountResponse } from '@/api'

export interface LocationSummary extends LocationResponse {
  totalShifts: number
  assignedShifts: number
  /** rounded 0..100, or null when the location has no capacity */
  percent: number | null
}

export const useLocationsStore = defineStore('locations', () => {
  const items = ref<LocationSummary[]>([])
  const loading = ref(false)
  const seasonId = ref<string | null>(null)

  async function load(forSeasonId: string): Promise<void> {
    loading.value = true
    try {
      const [list, counts] = await Promise.all([
        locationsApi.list(forSeasonId),
        locationsApi.counts(forSeasonId),
      ])
      items.value = list.map((l) =>
        summarize(
          l,
          counts.find((c) => c.id === l.id),
        ),
      )
      seasonId.value = forSeasonId
    } finally {
      loading.value = false
    }
  }

  async function refreshCount(id: string): Promise<void> {
    const count = await locationsApi.count(id)
    items.value = items.value.map((l) => (l.id === id ? summarize(l, count) : l))
  }

  const byId = computed(() => new Map(items.value.map((l) => [l.id, l])))

  return { items, loading, seasonId, load, refreshCount, byId }
})

function summarize(l: LocationResponse, c: ShiftsCountResponse | undefined): LocationSummary {
  const total = c?.totalShifts ?? 0
  const assigned = Math.min(total, c?.assignedShifts ?? 0)
  return {
    ...l,
    totalShifts: total,
    assignedShifts: assigned,
    percent: total > 0 ? Math.round((assigned / total) * 100) : null,
  }
}
