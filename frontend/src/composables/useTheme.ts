import { computed, watchEffect } from 'vue'
import { usePreferredDark, useStorage } from '@vueuse/core'

export type ThemePreference = 'light' | 'dark' | 'system'
export type Theme = 'light' | 'dark'

/* Keep in sync with the pre-paint script in index.html. */
const STORAGE_KEY = 'theme'
const THEME_COLOR: Record<Theme, string> = { light: '#f8f9fb', dark: '#141418' }

const preference = useStorage<ThemePreference>(STORAGE_KEY, 'system')
const prefersDark = usePreferredDark()
const theme = computed<Theme>(() =>
  preference.value === 'system' ? (prefersDark.value ? 'dark' : 'light') : preference.value,
)

watchEffect(() => {
  document.documentElement.classList.toggle('dark', theme.value === 'dark')
  document
    .querySelector('meta[name="theme-color"]')
    ?.setAttribute('content', THEME_COLOR[theme.value])
})

/** Light/dark theme: the user's choice (stored per device) and the theme actually shown. */
export function useTheme() {
  return { preference, theme }
}
