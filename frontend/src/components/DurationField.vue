<script setup lang="ts">
import { computed } from 'vue'
import { Input } from '@/components/ui/input'
import { clockToMinutes, minutesToClock } from '@/domain/time'

/** "HH:mm" text field bound to minutes; hours may exceed 24 (26:00). */
const props = defineProps<{ modelValue: number; step?: number; min?: number; max?: number }>()
const emit = defineEmits<{ 'update:modelValue': [value: number] }>()
const text = computed(() => minutesToClock(props.modelValue))

function onChange(value: string | number): void {
  const minutes = clockToMinutes(String(value))
  if (minutes === null) return
  let m = minutes
  if (props.step) m = Math.round(m / props.step) * props.step
  if (props.min !== undefined) m = Math.max(props.min, m)
  if (props.max !== undefined) m = Math.min(props.max, m)
  emit('update:modelValue', m)
}
</script>

<template>
  <Input
    class="w-28 tabular-nums"
    :model-value="text"
    placeholder="HH:mm"
    inputmode="numeric"
    @change="onChange(($event.target as HTMLInputElement).value)"
  />
</template>
