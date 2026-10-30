<script setup lang="ts">
import { useI18n } from 'vue-i18n'
import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
} from '@/components/ui/alert-dialog'
import { buttonVariants } from '@/components/ui/button'

defineProps<{
  open: boolean
  title?: string
  text: string
  destructive?: boolean
  confirmLabel?: string
}>()
const emit = defineEmits<{ 'update:open': [value: boolean]; confirm: [] }>()
const { t } = useI18n()
</script>

<template>
  <AlertDialog :open="open" @update:open="emit('update:open', $event)">
    <AlertDialogContent>
      <AlertDialogHeader>
        <AlertDialogTitle>{{ title ?? t('confirm_title') }}</AlertDialogTitle>
        <AlertDialogDescription>{{ text }}</AlertDialogDescription>
      </AlertDialogHeader>
      <AlertDialogFooter>
        <AlertDialogCancel>{{ t('btn_cancel') }}</AlertDialogCancel>
        <AlertDialogAction
          :class="destructive ? buttonVariants({ variant: 'destructive' }) : undefined"
          @click="emit('confirm')"
        >
          {{ confirmLabel ?? (destructive ? t('btn_delete') : t('btn_ok')) }}
        </AlertDialogAction>
      </AlertDialogFooter>
    </AlertDialogContent>
  </AlertDialog>
</template>
