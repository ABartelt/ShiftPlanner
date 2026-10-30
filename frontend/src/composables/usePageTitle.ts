import { ref, watch } from 'vue'
import { useRoute } from 'vue-router'

const title = ref('')

/** Pages set their header title; it is cleared on every route change. */
export function usePageTitle() {
  const route = useRoute()
  watch(
    () => route.path,
    () => (title.value = ''),
  )
  function set(value: string): void {
    title.value = value
    document.title = value
      ? `${value} · ${document.title.split(' · ').pop()}`
      : (document.title.split(' · ').pop() ?? '')
  }
  return { title, set }
}
