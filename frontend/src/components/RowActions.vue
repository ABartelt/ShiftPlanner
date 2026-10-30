<script setup lang="ts">
import { useI18n } from 'vue-i18n'
import { Pencil, Trash2 } from '@lucide/vue'
import { Button } from '@/components/ui/button'

defineProps<{ canDelete?: boolean }>()
const emit = defineEmits<{ edit: []; remove: [] }>()
const { t } = useI18n()
</script>

<template>
  <Button variant="ghost" size="icon-sm" :title="t('btn_edit')" @click="emit('edit')"
    ><Pencil
  /></Button>
  <Button
    v-if="canDelete !== false"
    variant="ghost"
    size="icon-sm"
    class="text-destructive"
    :title="t('btn_delete')"
    @click="emit('remove')"
    ><Trash2
  /></Button>
</template>
