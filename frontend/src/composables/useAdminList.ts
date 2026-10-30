import { ref } from 'vue'
import { toast } from 'vue-sonner'
import { useI18n } from 'vue-i18n'
import { useErrors } from './useErrors'

/** Loading, reloading and deleting for the admin list pages. */
export function useAdminList<T extends { id: string }>(
  loader: () => Promise<T[]>,
  remover?: (id: string) => Promise<void>,
) {
  const { t } = useI18n()
  const { report } = useErrors()
  const items = ref<T[]>([]) as import('vue').Ref<T[]>
  const loading = ref(false)
  const pendingDelete = ref<T | null>(null) as import('vue').Ref<T | null>

  async function load(): Promise<void> {
    loading.value = true
    try {
      items.value = await loader()
    } catch (error) {
      await report(error)
    } finally {
      loading.value = false
    }
  }

  async function confirmDelete(): Promise<void> {
    const item = pendingDelete.value
    pendingDelete.value = null
    if (!item || !remover) return
    try {
      await remover(item.id)
      toast.success(t('deleted'))
      await load()
    } catch (error) {
      await report(error)
    }
  }

  return { items, loading, load, pendingDelete, confirmDelete }
}
