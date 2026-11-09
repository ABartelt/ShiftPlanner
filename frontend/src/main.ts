import '@fontsource-variable/roboto'
import './styles/main.css'

import { createApp } from 'vue'
import { createPinia } from 'pinia'
import App from './App.vue'
import { router } from './router'
import { i18n } from './i18n'
import { loadConfig } from './config'
import { createUserManager } from './auth/oidc'
import { initAuth } from './auth/useAuth'
import { setTimeZone } from './domain/time'

async function boot(): Promise<void> {
  const config = await loadConfig()
  setTimeZone(config.timeZone)
  document.title = config.brand.name
  createUserManager(config)
  // Silent-renew frames and the login callback must not wait for the stored session.
  if (!window.location.pathname.startsWith('/auth/')) await initAuth()

  const app = createApp(App)
  app.use(createPinia())
  app.use(i18n)
  app.use(router)
  app.mount('#app')
}

boot().catch((error: unknown) => {
  console.error(error)
  const el = document.getElementById('app')
  if (el) {
    el.innerHTML = `<div style="padding:2rem;font-family:sans-serif"><h1>Konfiguration fehlt</h1><p>${String(
      error instanceof Error ? error.message : error,
    )}</p></div>`
  }
})
