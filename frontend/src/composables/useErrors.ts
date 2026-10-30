import { toast } from 'vue-sonner'
import { useI18n } from 'vue-i18n'
import { useRouter } from 'vue-router'
import { ApiError, NotAuthenticatedError } from '@/api'
import { useSettingsStore } from '@/stores/settings'
import { useAuth } from '@/auth/useAuth'

/** Turns API failures into toasts; a lost session goes to the login page instead. */
export function useErrors() {
  const { t } = useI18n()
  const router = useRouter()

  function messageOf(error: unknown): string {
    if (error instanceof ApiError) {
      if (error.status === 403) return error.message || t('error_forbidden')
      if (error.status === 404) return error.message || t('error_not_found')
      if (error.status === 429) return t('error_rate_limit')
      return error.message
    }
    if (error instanceof TypeError) return t('error_network')
    return error instanceof Error ? error.message : String(error)
  }

  async function report(error: unknown, fallbackTitle?: string): Promise<void> {
    if (error instanceof NotAuthenticatedError) {
      await useAuth().login(router.currentRoute.value.fullPath)
      return
    }
    const settings = useSettingsStore()
    const contact = settings.settings.contact || t('error_contact_fallback')
    toast.error(fallbackTitle ?? t('error_title'), {
      description: `${messageOf(error)} ${t('error_generic', { contact })}`,
      duration: 8000,
    })
    console.error(error)
  }

  return { report, messageOf }
}
