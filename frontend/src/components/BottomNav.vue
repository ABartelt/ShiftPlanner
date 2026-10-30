<script setup lang="ts">
import { useI18n } from 'vue-i18n'
import { CalendarCheck, Home, MapPin, ShieldCheck } from '@lucide/vue'
import { useAuth } from '@/auth/useAuth'

const { t } = useI18n()
const auth = useAuth()
</script>

<template>
  <nav
    class="fixed inset-x-0 bottom-0 z-40 border-t border-border bg-surface/95 backdrop-blur safe-bottom"
    aria-label="main"
  >
    <ul
      class="mx-auto flex max-w-xl items-stretch justify-around"
      style="height: var(--nav-height)"
    >
      <li class="flex-1">
        <RouterLink
          to="/"
          class="flex h-full flex-col items-center justify-center gap-1 text-[11px] font-medium text-muted-foreground"
          exact-active-class="text-primary-ink"
        >
          <Home class="size-[22px]" /><span>{{ t('nav_start') }}</span>
        </RouterLink>
      </li>
      <li class="flex-1">
        <RouterLink
          to="/locations"
          class="flex h-full flex-col items-center justify-center gap-1 text-[11px] font-medium text-muted-foreground"
          active-class="text-primary-ink"
        >
          <MapPin class="size-[22px]" /><span>{{ t('nav_locations') }}</span>
        </RouterLink>
      </li>
      <li class="flex-1">
        <RouterLink
          to="/my-shifts"
          class="flex h-full flex-col items-center justify-center gap-1 text-[11px] font-medium text-muted-foreground"
          active-class="text-primary-ink"
        >
          <CalendarCheck class="size-[22px]" /><span>{{ t('nav_my_shifts') }}</span>
        </RouterLink>
      </li>
      <li v-if="auth.isAdmin.value" class="flex-1">
        <RouterLink
          to="/admin/locations"
          class="flex h-full flex-col items-center justify-center gap-1 text-[11px] font-medium text-muted-foreground"
          active-class="text-primary-ink"
        >
          <ShieldCheck class="size-[22px]" /><span>{{ t('nav_admin') }}</span>
        </RouterLink>
      </li>
    </ul>
  </nav>
</template>
