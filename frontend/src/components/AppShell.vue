<script setup lang="ts">
import { computed, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { useRoute } from 'vue-router'
import { CircleHelp, LogOut, Menu, Monitor, Moon, Sun, WifiOff } from '@lucide/vue'
import SideNav from './SideNav.vue'
import BottomNav from './BottomNav.vue'
import { Button } from '@/components/ui/button'
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuLabel,
  DropdownMenuRadioGroup,
  DropdownMenuRadioItem,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu'
import { Sheet, SheetContent, SheetTitle } from '@/components/ui/sheet'
import { useAuth } from '@/auth/useAuth'
import { useSettingsStore } from '@/stores/settings'
import { useAppStore } from '@/stores/app'
import { useConfig } from '@/config'
import { usePageTitle } from '@/composables/usePageTitle'
import { useTheme, type ThemePreference } from '@/composables/useTheme'

const { t } = useI18n()
const route = useRoute()
const auth = useAuth()
const settings = useSettingsStore()
const app = useAppStore()
const config = useConfig()
const { title: pageTitle } = usePageTitle()
const { preference: themePreference, theme } = useTheme()
const menuOpen = ref(false)

const appTitle = computed(() => settings.settings.title || config.brand.name)
const themeOptions = computed(() => [
  { value: 'light', label: t('theme_light'), icon: Sun },
  { value: 'dark', label: t('theme_dark'), icon: Moon },
  { value: 'system', label: t('theme_system'), icon: Monitor },
])

function setTheme(value: unknown): void {
  themePreference.value = value as ThemePreference
}
</script>

<template>
  <div class="flex min-h-dvh">
    <aside
      class="hidden w-64 shrink-0 border-r border-sidebar-border bg-sidebar lg:flex lg:flex-col"
    >
      <RouterLink to="/" class="flex items-center gap-2 px-5 pt-5 pb-3">
        <img
          v-if="config.brand.logoUrl"
          :src="config.brand.logoUrl"
          alt=""
          class="h-7 w-7 rounded"
        />
        <span class="font-display text-lg font-semibold leading-tight">{{ appTitle }}</span>
      </RouterLink>
      <SideNav class="flex-1 overflow-y-auto px-3 pb-4" />
    </aside>

    <div class="flex min-w-0 flex-1 flex-col">
      <header
        class="sticky top-0 z-30 border-b border-border bg-background/90 backdrop-blur safe-top"
      >
        <div class="flex h-14 items-center gap-2 px-4">
          <Button
            variant="ghost"
            size="icon"
            class="lg:hidden"
            :aria-label="t('nav_more')"
            @click="menuOpen = true"
          >
            <Menu />
          </Button>
          <div class="min-w-0 flex-1">
            <RouterLink to="/" class="hidden text-sm text-muted-foreground lg:inline">{{
              appTitle
            }}</RouterLink>
            <span v-if="pageTitle" class="hidden text-muted-foreground lg:inline"> » </span>
            <span class="block truncate font-display font-semibold lg:inline">{{
              pageTitle || appTitle
            }}</span>
          </div>
          <span
            v-if="!app.online"
            class="flex items-center gap-1 text-xs text-status-yellow"
            :title="t('offline')"
          >
            <WifiOff class="size-4" />
          </span>
          <DropdownMenu>
            <DropdownMenuTrigger as-child>
              <Button variant="ghost" size="icon" :title="t('theme')" :aria-label="t('theme')">
                <Moon v-if="theme === 'dark'" />
                <Sun v-else />
              </Button>
            </DropdownMenuTrigger>
            <DropdownMenuContent align="end">
              <DropdownMenuLabel>{{ t('theme') }}</DropdownMenuLabel>
              <DropdownMenuRadioGroup :model-value="themePreference" @update:model-value="setTheme">
                <DropdownMenuRadioItem v-for="o in themeOptions" :key="o.value" :value="o.value">
                  <component :is="o.icon" /> {{ o.label }}
                </DropdownMenuRadioItem>
              </DropdownMenuRadioGroup>
            </DropdownMenuContent>
          </DropdownMenu>
          <Button variant="ghost" size="icon" as-child :title="t('nav_help')">
            <RouterLink to="/help"><CircleHelp /></RouterLink>
          </Button>
          <Button variant="ghost" size="icon" :title="t('nav_logout')" @click="auth.logout()">
            <LogOut />
          </Button>
        </div>
      </header>

      <main
        class="flex-1 px-4 pb-[calc(var(--nav-height)+1rem)] pt-4 lg:px-8 lg:pb-8"
        :key="route.fullPath.split('?')[0]"
      >
        <slot />
      </main>

      <footer class="hidden px-8 pb-4 text-center text-xs text-muted-foreground lg:block">
        <a
          v-if="config.brand.footer"
          :href="config.brand.footerUrl || '#'"
          target="_blank"
          rel="noopener"
          >{{ config.brand.footer }}</a
        >
      </footer>
    </div>

    <BottomNav class="lg:hidden" />

    <Sheet v-model:open="menuOpen">
      <SheetContent side="left" class="w-72 bg-sidebar p-0">
        <SheetTitle class="px-5 pt-5 pb-3 font-display">{{ appTitle }}</SheetTitle>
        <SideNav class="px-3 pb-4" @navigate="menuOpen = false" />
      </SheetContent>
    </Sheet>
  </div>
</template>
