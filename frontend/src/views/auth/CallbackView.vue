<script setup lang="ts">
import BrandText from '@/components/BrandText.vue'
import { onMounted, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { useRouter } from 'vue-router'
import { getUserManager } from '@/auth/oidc'
import { initAuth } from '@/auth/useAuth'

const { t } = useI18n()
const router = useRouter()
const error = ref('')

onMounted(async () => {
  try {
    const user = await getUserManager().signinCallback()
    await initAuth()
    const state = (user?.state ?? {}) as { returnTo?: string }
    await router.replace(state.returnTo && state.returnTo.startsWith('/') ? state.returnTo : '/')
  } catch (e) {
    error.value = e instanceof Error ? e.message : String(e)
  }
})
</script>

<template>
  <div class="grid min-h-dvh place-items-center p-6 text-center">
    <div v-if="error">
      <h1 class="headline text-2xl"><BrandText :text="t('error_title')" /></h1>
      <p class="mt-2 text-sm text-muted-foreground">{{ error }}</p>
      <RouterLink to="/" class="mt-4 inline-block text-primary-ink underline">{{
        t('btn_retry')
      }}</RouterLink>
    </div>
    <p v-else class="text-muted-foreground">{{ t('login_in_progress') }}</p>
  </div>
</template>
