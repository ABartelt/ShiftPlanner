<script setup lang="ts">
import { ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import { CalendarArrowDown } from '@lucide/vue'
import PageHeader from '@/components/PageHeader.vue'
import ShiftTile from '@/components/ShiftTile.vue'
import { Button } from '@/components/ui/button'
import { Skeleton } from '@/components/ui/skeleton'
import { employeesApi, type ShiftResponse } from '@/api'
import { useAuth } from '@/auth/useAuth'
import { useSeasonStore } from '@/stores/season'
import { useLocationsStore } from '@/stores/locations'
import { useErrors } from '@/composables/useErrors'
import { useIcs } from '@/composables/useIcs'

const { t } = useI18n()
const auth = useAuth()
const season = useSeasonStore()
const locations = useLocationsStore()
const { report } = useErrors()
const ics = useIcs()

const loading = ref(true)
const shifts = ref<ShiftResponse[]>([])

async function load(): Promise<void> {
  const seasonId = season.currentId
  const sub = auth.profile.value?.sub
  if (!seasonId || !sub) return
  loading.value = true
  try {
    shifts.value = (await employeesApi.shifts(sub, seasonId)).sort((a, b) =>
      a.start.localeCompare(b.start),
    )
  } catch (error) {
    await report(error)
  } finally {
    loading.value = false
  }
}
watch(() => season.currentId, load, { immediate: true })
</script>

<template>
  <div class="mx-auto max-w-3xl">
    <PageHeader :title="t('my_shifts_title')">
      <template #actions>
        <Button
          variant="outline"
          size="sm"
          :disabled="!season.currentId"
          @click="ics.download(season.currentId!)"
        >
          <CalendarArrowDown /> {{ t('start_ics') }}
        </Button>
      </template>
    </PageHeader>
    <div v-if="loading" class="flex flex-wrap gap-3">
      <Skeleton v-for="i in 3" :key="i" class="h-20 min-w-[150px] flex-1" />
    </div>
    <p v-else-if="!shifts.length" class="text-sm text-muted-foreground">
      {{ t('my_shifts_empty') }}
    </p>
    <div v-else class="flex flex-wrap gap-3">
      <ShiftTile
        v-for="s in shifts"
        :key="s.id"
        :type="s.type!"
        :start="s.start"
        :end="s.end"
        :location-id="s.locationId"
        :location-name="locations.byId.get(s.locationId)?.name ?? ''"
        filled
        mine
      />
    </div>
  </div>
</template>
