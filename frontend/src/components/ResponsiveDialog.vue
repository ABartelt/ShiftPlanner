<script setup lang="ts">
import { useMediaQuery } from '@vueuse/core'
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog'
import {
  Sheet,
  SheetContent,
  SheetDescription,
  SheetHeader,
  SheetTitle,
} from '@/components/ui/sheet'

/** Dialog on desktop, bottom sheet on phones. */
defineProps<{ open: boolean; title: string; description?: string; wide?: boolean }>()
const emit = defineEmits<{ 'update:open': [value: boolean] }>()
const isDesktop = useMediaQuery('(min-width: 768px)')
</script>

<template>
  <Dialog v-if="isDesktop" :open="open" @update:open="emit('update:open', $event)">
    <DialogContent
      :class="wide ? 'sm:max-w-3xl' : 'sm:max-w-lg'"
      class="max-h-[90vh] overflow-y-auto"
    >
      <DialogHeader>
        <DialogTitle>{{ title }}</DialogTitle>
        <DialogDescription v-if="description">{{ description }}</DialogDescription>
        <DialogDescription v-else class="sr-only">{{ title }}</DialogDescription>
      </DialogHeader>
      <slot />
    </DialogContent>
  </Dialog>
  <Sheet v-else :open="open" @update:open="emit('update:open', $event)">
    <SheetContent side="bottom" class="max-h-[92dvh] overflow-y-auto rounded-t-2xl safe-bottom">
      <SheetHeader class="px-0">
        <SheetTitle>{{ title }}</SheetTitle>
        <SheetDescription v-if="description">{{ description }}</SheetDescription>
        <SheetDescription v-else class="sr-only">{{ title }}</SheetDescription>
      </SheetHeader>
      <div class="px-0 pb-2">
        <slot />
      </div>
    </SheetContent>
  </Sheet>
</template>
