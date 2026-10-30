import { defineStore } from 'pinia'
import { computed, ref } from 'vue'
import { settingsApi, type AppSettings, type UpdateAppSettingsRequest } from '@/api'
import { contactHref, dayWindow, defaultSettings } from '@/domain/settings'

const CACHE_KEY = 'appSettings'

export const useSettingsStore = defineStore('settings', () => {
  const settings = ref<AppSettings>(readCache() ?? defaultSettings)
  const loaded = ref(false)

  async function load(): Promise<void> {
    settings.value = await settingsApi.get()
    loaded.value = true
    try {
      localStorage.setItem(CACHE_KEY, JSON.stringify(settings.value))
    } catch {
      /* private mode */
    }
  }

  async function update(body: UpdateAppSettingsRequest): Promise<void> {
    await settingsApi.update(body)
    await load()
  }

  const contactLink = computed(() => contactHref(settings.value.contact))
  const window = computed(() => dayWindow(settings.value))

  return { settings, loaded, load, update, contactLink, window }
})

function readCache(): AppSettings | null {
  try {
    const raw = localStorage.getItem(CACHE_KEY)
    return raw ? { ...defaultSettings, ...(JSON.parse(raw) as AppSettings) } : null
  } catch {
    return null
  }
}
