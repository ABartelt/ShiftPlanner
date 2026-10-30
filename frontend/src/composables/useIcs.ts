import { toast } from 'vue-sonner'
import { useI18n } from 'vue-i18n'
import { employeesApi } from '@/api'
import { downloadBlob } from '@/domain/download'
import { useErrors } from './useErrors'

export function useIcs() {
  const { t } = useI18n()
  const { report } = useErrors()

  async function download(seasonId: string, filename = 'schichten.ics'): Promise<void> {
    try {
      const blob = await employeesApi.ics(seasonId)
      if (blob.size === 0) {
        toast.info(t('start_ics_empty'))
        return
      }
      downloadBlob(blob, filename)
    } catch (error) {
      await report(error)
    }
  }

  return { download }
}
