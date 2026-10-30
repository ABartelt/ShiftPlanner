<script setup lang="ts">
import { useI18n } from 'vue-i18n'
import { useRouter } from 'vue-router'
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select'
import { useAuth } from '@/auth/useAuth'
import { useSeasonStore } from '@/stores/season'

const { t } = useI18n()
const router = useRouter()
const auth = useAuth()
const season = useSeasonStore()

function onChange(id: unknown): void {
  if (typeof id !== 'string' || id === season.currentId) return
  season.select(id)
  void router.push('/')
}
</script>

<template>
  <div>
    <p class="eyebrow px-2 pb-1">{{ t('season_current') }}</p>
    <Select
      v-if="auth.isSuperAdmin.value"
      :model-value="season.currentId ?? undefined"
      @update:model-value="onChange"
    >
      <SelectTrigger class="w-full" :aria-label="t('season_switch')">
        <SelectValue :placeholder="t('loading')" />
      </SelectTrigger>
      <SelectContent>
        <SelectItem v-for="s in season.seasons" :key="s.id" :value="s.id">{{ s.name }}</SelectItem>
      </SelectContent>
    </Select>
    <p v-else class="rounded-lg bg-surface-raised px-3 py-2 text-sm">
      {{ season.current?.name ?? t('loading') }}
    </p>
  </div>
</template>
