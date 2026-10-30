<script setup lang="ts">
import { Minus, Plus } from '@lucide/vue'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'

/** Integer input with − / + buttons, clamped to [min, max]. */
const props = withDefaults(defineProps<{ modelValue: number; min?: number; max?: number }>(), {
  min: 0,
  max: 99,
})
const emit = defineEmits<{ 'update:modelValue': [value: number] }>()

function set(value: number): void {
  if (!Number.isFinite(value)) return
  emit('update:modelValue', Math.min(props.max, Math.max(props.min, Math.round(value))))
}
</script>

<template>
  <div class="inline-flex items-center rounded-lg border border-input bg-input/30">
    <Button
      type="button"
      variant="ghost"
      size="icon-sm"
      class="rounded-r-none"
      :disabled="modelValue <= min"
      aria-label="−"
      @click="set(modelValue - 1)"
      ><Minus
    /></Button>
    <Input
      :model-value="modelValue"
      type="number"
      inputmode="numeric"
      :min="min"
      :max="max"
      class="h-8 w-12 [appearance:textfield] rounded-none border-0 bg-transparent px-0 text-center tabular-nums shadow-none focus-visible:ring-0 dark:bg-transparent [&::-webkit-inner-spin-button]:appearance-none"
      @update:model-value="set(Number($event))"
    />
    <Button
      type="button"
      variant="ghost"
      size="icon-sm"
      class="rounded-l-none"
      :disabled="modelValue >= max"
      aria-label="+"
      @click="set(modelValue + 1)"
      ><Plus
    /></Button>
  </div>
</template>
