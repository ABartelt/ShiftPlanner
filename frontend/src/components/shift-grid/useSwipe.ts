import { onBeforeUnmount, onMounted, type Ref } from 'vue'

/** Horizontal swipe detection like the old client: velocity ≥ 0.8 px/ms and clearly more horizontal than vertical. */
export function useSwipe(
  el: Ref<HTMLElement | null>,
  handlers: { left: () => void; right: () => void },
) {
  let startX = 0
  let startY = 0
  let startT = 0

  function onStart(e: TouchEvent): void {
    const t = e.touches[0]
    if (!t) return
    startX = t.clientX
    startY = t.clientY
    startT = Date.now()
  }
  function onEnd(e: TouchEvent): void {
    const t = e.changedTouches[0]
    if (!t) return
    const dt = Math.max(1, Date.now() - startT)
    const dx = t.clientX - startX
    const dy = t.clientY - startY
    const vx = Math.abs(dx) / dt
    const vy = Math.abs(dy) / dt
    if (vx < 0.8 || Math.abs(vx - vy) < 0.5 || Math.abs(dx) < 60) return
    if (dx < 0) handlers.left()
    else handlers.right()
  }

  onMounted(() => {
    el.value?.addEventListener('touchstart', onStart, { passive: true })
    el.value?.addEventListener('touchend', onEnd, { passive: true })
  })
  onBeforeUnmount(() => {
    el.value?.removeEventListener('touchstart', onStart)
    el.value?.removeEventListener('touchend', onEnd)
  })
}
