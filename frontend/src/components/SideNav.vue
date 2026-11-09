<script setup lang="ts">
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import {
  BarChart3,
  Bell,
  CalendarRange,
  Download,
  Home,
  Layers,
  MapPin,
  Settings,
  Shapes,
  Sparkles,
  Tag,
} from '@lucide/vue'
import SeasonSwitcher from './SeasonSwitcher.vue'
import { useAuth } from '@/auth/useAuth'
import { useLocationsStore } from '@/stores/locations'

const emit = defineEmits<{ navigate: [] }>()
const { t } = useI18n()
const auth = useAuth()
const locations = useLocationsStore()

const adminItems = computed(() => [
  { to: '/admin/seasons', label: t('nav_seasons'), icon: CalendarRange },
  { to: '/admin/shift-types', label: t('nav_shift_types'), icon: Tag },
  { to: '/admin/frameworks', label: t('nav_frameworks'), icon: Layers },
  { to: '/admin/locations', label: t('nav_admin_locations'), icon: Shapes },
  { to: '/admin/setup', label: t('nav_setup'), icon: Sparkles },
  { to: '/statistics', label: t('nav_statistics'), icon: BarChart3 },
  { to: '/admin/export', label: t('nav_export'), icon: Download },
  ...(auth.isSuperAdmin.value
    ? [{ to: '/admin/settings', label: t('nav_settings'), icon: Settings }]
    : []),
])

const linkClass =
  'flex items-center gap-2 rounded-lg px-3 py-2 text-sm text-muted-foreground transition-colors hover:bg-accent hover:text-foreground'
</script>

<template>
  <nav class="flex flex-col gap-1">
    <RouterLink
      to="/"
      :class="linkClass"
      active-class="bg-accent text-foreground"
      exact-active-class="bg-accent text-foreground"
      @click="emit('navigate')"
    >
      <Home class="size-4" /> {{ t('nav_start') }}
    </RouterLink>
    <RouterLink
      to="/notifications"
      :class="linkClass"
      active-class="bg-accent text-foreground"
      @click="emit('navigate')"
    >
      <Bell class="size-4" /> {{ t('nav_notifications') }}
    </RouterLink>
    <p class="eyebrow mt-4 px-3 pb-1">{{ t('nav_locations') }}</p>
    <RouterLink
      v-for="l in locations.items"
      :key="l.id"
      :to="`/locations/${l.id}`"
      :class="linkClass"
      active-class="bg-accent text-foreground"
      @click="emit('navigate')"
    >
      <MapPin class="size-4 shrink-0" />
      <span class="min-w-0 flex-1 truncate">{{ l.name }}</span>
      <span v-if="l.percent !== null" class="text-xs tabular-nums">{{ l.percent }} %</span>
    </RouterLink>
    <p
      v-if="!locations.items.length && !locations.loading"
      class="px-3 text-xs text-muted-foreground"
    >
      {{ t('locations_empty') }}
    </p>

    <template v-if="auth.isAdmin.value">
      <p class="eyebrow mt-4 px-3 pb-1">{{ t('nav_admin') }}</p>
      <RouterLink
        v-for="item in adminItems"
        :key="item.to"
        :to="item.to"
        :class="linkClass"
        active-class="bg-accent text-foreground"
        @click="emit('navigate')"
      >
        <component :is="item.icon" class="size-4" /> {{ item.label }}
      </RouterLink>
    </template>

    <div class="mt-6 px-1">
      <SeasonSwitcher />
    </div>
  </nav>
</template>
