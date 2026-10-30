<script setup lang="ts">
import BrandText from '@/components/BrandText.vue'
import { onMounted, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { settingsApi } from '@/api'
import { useAuth } from '@/auth/useAuth'
import { useSettingsStore } from '@/stores/settings'
import { useAppStore } from '@/stores/app'
import { useConfig } from '@/config'

const { t } = useI18n()
const auth = useAuth()
const settings = useSettingsStore()
const app = useAppStore()
const config = useConfig()
const apiVersion = ref('')

onMounted(async () => {
  apiVersion.value = await settingsApi.version().catch(() => '')
})
</script>

<template>
  <div class="mx-auto max-w-2xl p-6">
    <RouterLink to="/" class="text-sm text-primary-ink underline"
      >« {{ t('nav_start') }}</RouterLink
    >
    <h1 class="headline mt-4 text-3xl"><BrandText :text="t('help_title')" /></h1>
    <p class="mt-3">
      {{ t('help_contact') }}
      <a :href="settings.contactLink" class="text-primary-ink underline">{{
        settings.settings.contact || t('error_contact_fallback')
      }}</a>
    </p>
    <p class="mt-6 text-sm text-muted-foreground">
      {{ t('help_version') }}: {{ app.version
      }}<span v-if="apiVersion"> · {{ t('help_api_version') }} {{ apiVersion }}</span>
    </p>
    <p class="text-sm text-muted-foreground">{{ t('help_made_by') }}</p>
    <p v-if="config.brand.footer" class="mt-2 text-sm text-muted-foreground">
      <a :href="config.brand.footerUrl || '#'" target="_blank" rel="noopener">{{
        config.brand.footer
      }}</a>
    </p>
    <details v-if="auth.isSuperAdmin.value" class="mt-6">
      <summary class="cursor-pointer text-sm">{{ t('help_settings_json') }}</summary>
      <pre class="mt-2 overflow-auto rounded-lg bg-surface p-3 text-xs">{{
        JSON.stringify(settings.settings, null, 2)
      }}</pre>
    </details>
  </div>
</template>
