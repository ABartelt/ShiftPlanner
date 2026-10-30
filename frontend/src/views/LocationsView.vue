<script setup lang="ts">
import { useI18n } from 'vue-i18n'
import { ChevronRight, MapPin } from '@lucide/vue'
import PageHeader from '@/components/PageHeader.vue'
import { useLocationsStore } from '@/stores/locations'

const { t } = useI18n()
const locations = useLocationsStore()
</script>

<template>
  <div class="mx-auto max-w-3xl">
    <PageHeader :title="t('locations_title')" />
    <p v-if="!locations.items.length && !locations.loading" class="text-sm text-muted-foreground">
      {{ t('locations_empty') }}
    </p>
    <ul class="divide-y divide-border overflow-hidden rounded-xl border border-border bg-card">
      <li v-for="l in locations.items" :key="l.id">
        <RouterLink
          :to="`/locations/${l.id}`"
          class="flex items-center gap-3 px-4 py-3 hover:bg-accent"
        >
          <MapPin class="size-5 text-primary-ink" />
          <div class="min-w-0 flex-1">
            <p class="truncate font-medium">{{ l.name }}</p>
            <p class="text-xs text-muted-foreground">
              {{ l.assignedShifts }}/{{ l.totalShifts }} ·
              <span v-if="l.percent !== null">{{
                t('locations_percent', { percent: l.percent })
              }}</span>
            </p>
          </div>
          <div
            v-if="l.percent !== null"
            class="h-2 w-20 overflow-hidden rounded-full bg-surface-raised"
          >
            <div class="h-full bg-primary" :style="{ width: `${l.percent}%` }" />
          </div>
          <ChevronRight class="size-4 text-muted-foreground" />
        </RouterLink>
      </li>
    </ul>
  </div>
</template>
