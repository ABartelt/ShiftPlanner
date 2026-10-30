<script setup lang="ts">
import { computed } from 'vue'
import { Input } from '@/components/ui/input'

/**
 * Colour input with an optional alpha slider. v-model is a CSS colour string:
 * "#rrggbb" without alpha, "rgba(r,g,b,a)" with alpha (both accepted by the API as free text).
 */
const props = defineProps<{ modelValue: string; alpha?: boolean }>()
const emit = defineEmits<{ 'update:modelValue': [value: string] }>()

function parse(value: string): { hex: string; alpha: number } {
  const v = value.trim()
  const rgba = /^rgba?\((\d+)\s*,\s*(\d+)\s*,\s*(\d+)(?:\s*,\s*([\d.]+))?\)$/i.exec(v)
  if (rgba) {
    const toHex = (n: string) => Number(n).toString(16).padStart(2, '0')
    return {
      hex: `#${toHex(rgba[1]!)}${toHex(rgba[2]!)}${toHex(rgba[3]!)}`,
      alpha: rgba[4] !== undefined ? Number(rgba[4]) : 1,
    }
  }
  if (/^#[0-9a-f]{8}$/i.test(v))
    return { hex: v.slice(0, 7), alpha: parseInt(v.slice(7, 9), 16) / 255 }
  if (/^#[0-9a-f]{6}$/i.test(v)) return { hex: v, alpha: 1 }
  if (/^#[0-9a-f]{3}$/i.test(v))
    return { hex: `#${v[1]}${v[1]}${v[2]}${v[2]}${v[3]}${v[3]}`, alpha: 1 }
  return { hex: '#3b82f6', alpha: props.alpha ? 0.3 : 1 }
}

const parsed = computed(() => parse(props.modelValue))

function emitValue(hex: string, alpha: number): void {
  if (!props.alpha || alpha >= 1) {
    emit('update:modelValue', hex)
    return
  }
  const r = parseInt(hex.slice(1, 3), 16)
  const g = parseInt(hex.slice(3, 5), 16)
  const b = parseInt(hex.slice(5, 7), 16)
  emit('update:modelValue', `rgba(${r}, ${g}, ${b}, ${alpha.toFixed(2)})`)
}
</script>

<template>
  <div class="flex items-center gap-3">
    <input
      type="color"
      class="h-9 w-14 cursor-pointer rounded-md border border-input bg-transparent p-0.5"
      :value="parsed.hex"
      @input="emitValue(($event.target as HTMLInputElement).value, parsed.alpha)"
    />
    <template v-if="alpha">
      <input
        type="range"
        min="0.1"
        max="1"
        step="0.05"
        class="w-32 accent-primary"
        :value="parsed.alpha"
        @input="emitValue(parsed.hex, Number(($event.target as HTMLInputElement).value))"
      />
      <span class="w-10 text-xs tabular-nums text-muted-foreground"
        >{{ Math.round(parsed.alpha * 100) }} %</span
      >
    </template>
    <Input
      class="w-44 font-mono text-xs"
      :model-value="modelValue"
      @update:model-value="emit('update:modelValue', String($event))"
    />
    <span class="size-6 rounded-md border border-border" :style="{ background: modelValue }" />
  </div>
</template>
