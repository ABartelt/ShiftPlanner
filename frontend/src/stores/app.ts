import { defineStore } from 'pinia'
import { ref } from 'vue'
import { registerSW } from 'virtual:pwa-register'

/** Service-worker lifecycle (update prompt) and online state. */
export const useAppStore = defineStore('app', () => {
  const version = __APP_VERSION__
  const buildTime = __BUILD_TIME__
  const needRefresh = ref(false)
  const online = ref(navigator.onLine)
  let updateServiceWorker: ((reload?: boolean) => Promise<void>) | null = null

  function registerServiceWorker(): void {
    if (!('serviceWorker' in navigator) || import.meta.env.DEV) return
    updateServiceWorker = registerSW({
      immediate: true,
      onNeedRefresh: () => {
        needRefresh.value = true
      },
      onRegisteredSW: (_url, registration) => {
        if (registration) window.setInterval(() => void registration.update(), 60 * 60 * 1000)
      },
    })
  }

  async function applyUpdate(): Promise<void> {
    needRefresh.value = false
    await updateServiceWorker?.(true)
  }

  window.addEventListener('online', () => (online.value = true))
  window.addEventListener('offline', () => (online.value = false))

  return { version, buildTime, needRefresh, online, registerServiceWorker, applyUpdate }
})
