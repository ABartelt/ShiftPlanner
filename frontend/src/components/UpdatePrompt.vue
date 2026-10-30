<script setup lang="ts">
import { useI18n } from 'vue-i18n'
import { Button } from '@/components/ui/button'
import { useAppStore } from '@/stores/app'

const { t } = useI18n()
const app = useAppStore()
</script>

<template>
  <div
    v-if="app.needRefresh"
    class="fixed inset-x-4 top-4 z-[60] mx-auto max-w-md rounded-xl border border-primary/40 bg-surface-raised p-4 shadow-2xl"
    role="status"
  >
    <p class="font-display font-semibold">{{ t('pwa_update_title') }}</p>
    <p class="mt-1 text-sm text-muted-foreground">{{ t('pwa_update_text') }}</p>
    <div class="mt-3 flex justify-end gap-2">
      <Button variant="ghost" size="sm" @click="app.needRefresh = false">{{
        t('pwa_update_later')
      }}</Button>
      <Button size="sm" @click="app.applyUpdate()">{{ t('pwa_update_now') }}</Button>
    </div>
  </div>
</template>
