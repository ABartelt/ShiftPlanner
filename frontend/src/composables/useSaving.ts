import { ref } from 'vue'
import { toast } from 'vue-sonner'
import { useI18n } from 'vue-i18n'
import { ApiError } from '@/api'
import { useErrors } from './useErrors'

/** Wraps a save call: shows API errors inline (4xx) or as toast (5xx), success toast on completion. */
export function useSaving() {
  const { t } = useI18n()
  const { report, messageOf } = useErrors()
  const saving = ref(false)
  const error = ref('')

  async function run(action: () => Promise<void>, successMessage = t('saved')): Promise<boolean> {
    saving.value = true
    error.value = ''
    try {
      await action()
      if (successMessage) toast.success(successMessage)
      return true
    } catch (e) {
      if (e instanceof ApiError && e.status < 500) error.value = messageOf(e)
      else await report(e)
      return false
    } finally {
      saving.value = false
    }
  }

  return { saving, error, run }
}
