<script setup lang="ts">
import BrandText from '@/components/BrandText.vue'
import { useI18n } from 'vue-i18n'
const { t } = useI18n()
</script>

<template>
  <div class="grid min-h-dvh place-items-center p-6 text-center">
    <div>
      <h1 class="headline text-4xl"><BrandText :text="t('not_found_title')" /></h1>
      <p class="mt-2 text-muted-foreground">{{ t('not_found_text') }}</p>
      <RouterLink to="/" class="mt-4 inline-block text-primary-ink underline">{{
        t('nav_start')
      }}</RouterLink>
    </div>
  </div>
</template>
