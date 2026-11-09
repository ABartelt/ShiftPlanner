<script setup lang="ts">
import { useI18n } from 'vue-i18n'
import { Checkbox } from '@/components/ui/checkbox'
import { useLocationsStore } from '@/stores/locations'

/** Multi-select of locations; an empty selection means all locations. */
const model = defineModel<string[]>({ required: true })
const { t } = useI18n()
const locations = useLocationsStore()

function toggle(id: string, on: boolean): void {
  model.value = on ? [...model.value, id] : model.value.filter((x) => x !== id)
}
</script>

<template>
  <div class="space-y-1.5">
    <div class="flex flex-wrap gap-x-4 gap-y-2">
      <label v-for="l in locations.items" :key="l.id" class="flex items-center gap-2 text-sm">
        <Checkbox
          :model-value="model.includes(l.id)"
          @update:model-value="toggle(l.id, $event === true)"
        />
        {{ l.name }}
      </label>
    </div>
    <p class="text-xs text-muted-foreground">
      {{ model.length ? t('promote_locations_some', model.length) : t('promote_locations_all') }}
    </p>
  </div>
</template>
