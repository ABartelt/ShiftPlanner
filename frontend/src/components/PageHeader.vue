<script setup lang="ts">
import BrandText from '@/components/BrandText.vue'
import { watchEffect } from 'vue'
import { usePageTitle } from '@/composables/usePageTitle'

const props = defineProps<{ title: string; subtitle?: string; hideHeading?: boolean }>()
const page = usePageTitle()
watchEffect(() => page.set(props.title))
</script>

<template>
  <div class="mb-4 flex flex-wrap items-end justify-between gap-3">
    <div v-if="!hideHeading">
      <h1 class="headline text-3xl"><BrandText :text="title" /></h1>
      <p v-if="subtitle" class="mt-1 max-w-2xl text-sm text-muted-foreground">{{ subtitle }}</p>
    </div>
    <div class="flex flex-wrap items-center gap-2">
      <slot name="actions" />
    </div>
  </div>
</template>
