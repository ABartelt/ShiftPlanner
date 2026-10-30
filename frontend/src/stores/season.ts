import { defineStore } from 'pinia'
import { computed, ref } from 'vue'
import { seasonsApi, type SeasonResponse } from '@/api'

export const useSeasonStore = defineStore('season', () => {
  const seasons = ref<SeasonResponse[]>([])
  const currentId = ref<string | null>(null)
  const loaded = ref(false)

  const current = computed(() => seasons.value.find((s) => s.id === currentId.value) ?? null)

  async function load(): Promise<void> {
    seasons.value = await seasonsApi.list()
    const stillThere = seasons.value.find((s) => s.id === currentId.value)
    const selected =
      stillThere ??
      seasons.value.find((s) => s.isSelectedAsCurrent) ??
      seasons.value[seasons.value.length - 1] ??
      null
    currentId.value = selected?.id ?? null
    loaded.value = true
  }

  /** Client-side switch (super-admins); the server's current season is unchanged. */
  function select(id: string): void {
    currentId.value = id
  }

  return { seasons, currentId, current, loaded, load, select }
})
