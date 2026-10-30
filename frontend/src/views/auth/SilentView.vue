<script setup lang="ts">
import { onMounted } from 'vue'
import { getUserManager } from '@/auth/oidc'

onMounted(() => {
  void getUserManager().signinSilentCallback()
})
</script>

<template>
  <div />
</template>
