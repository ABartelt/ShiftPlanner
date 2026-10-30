<script setup lang="ts">
import { onMounted, watch } from 'vue'
import { useRoute } from 'vue-router'
import { Toaster } from '@/components/ui/sonner'
import AppShell from '@/components/AppShell.vue'
import UpdatePrompt from '@/components/UpdatePrompt.vue'
import { useAppStore } from '@/stores/app'
import { useAuth } from '@/auth/useAuth'
import { useSettingsStore } from '@/stores/settings'
import { useSeasonStore } from '@/stores/season'
import { useLocationsStore } from '@/stores/locations'
import { useErrors } from '@/composables/useErrors'
import { useTheme } from '@/composables/useTheme'

const route = useRoute()
const app = useAppStore()
const auth = useAuth()
const settings = useSettingsStore()
const season = useSeasonStore()
const locations = useLocationsStore()
const { report } = useErrors()
const { theme } = useTheme()

onMounted(() => app.registerServiceWorker())

async function bootstrapData(): Promise<void> {
  try {
    await Promise.all([settings.load(), season.load()])
    if (season.currentId) await locations.load(season.currentId)
  } catch (error) {
    await report(error)
  }
}

watch(
  () => auth.isAuthenticated.value,
  (yes) => {
    if (yes) void bootstrapData()
  },
  { immediate: true },
)
watch(
  () => season.currentId,
  (id, old) => {
    if (id && old && id !== old) void locations.load(id).catch(report)
  },
)
</script>

<template>
  <Toaster position="top-center" rich-colors close-button :theme="theme" />
  <UpdatePrompt />
  <RouterView v-if="route.meta.public" />
  <AppShell v-else>
    <RouterView />
  </AppShell>
</template>
