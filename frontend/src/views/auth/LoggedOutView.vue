<script setup lang="ts">
import { useI18n } from 'vue-i18n'
const { t } = useI18n()
</script>

<template>
  <div class="grid min-h-dvh place-items-center p-6 text-center">
    <div>
      <p>{{ t('logged_out') }}</p>
      <RouterLink to="/" class="mt-4 inline-block text-primary-ink underline">{{
        t('login')
      }}</RouterLink>
    </div>
  </div>
</template>
