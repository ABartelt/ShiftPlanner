<script setup lang="ts" generic="T extends { id: string }">
import { useI18n } from 'vue-i18n'
import { Plus, RefreshCw } from '@lucide/vue'
import { Button } from '@/components/ui/button'
import { Skeleton } from '@/components/ui/skeleton'
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table'

defineProps<{
  columns: { key: string; label: string; class?: string }[]
  rows: T[]
  loading?: boolean
  createLabel?: string
  hideCreate?: boolean
}>()
const emit = defineEmits<{ create: []; reload: [] }>()
const { t } = useI18n()
function cellValue(row: T, key: string): unknown {
  return (row as Record<string, unknown>)[key]
}
</script>

<template>
  <div>
    <div class="mb-3 flex items-center gap-2">
      <Button v-if="!hideCreate" size="sm" @click="emit('create')"
        ><Plus /> {{ createLabel ?? t('btn_new') }}</Button
      >
      <Button
        variant="outline"
        size="icon-sm"
        :aria-label="t('btn_reload')"
        :disabled="loading"
        @click="emit('reload')"
        ><RefreshCw
      /></Button>
      <slot name="toolbar" />
    </div>
    <div class="overflow-hidden rounded-xl border border-border bg-card">
      <Table>
        <TableHeader>
          <TableRow>
            <TableHead v-for="c in columns" :key="c.key" :class="c.class">{{ c.label }}</TableHead>
            <TableHead class="w-28 text-right" />
          </TableRow>
        </TableHeader>
        <TableBody>
          <TableRow v-if="loading && !rows.length">
            <TableCell :colspan="columns.length + 1"><Skeleton class="h-6 w-full" /></TableCell>
          </TableRow>
          <TableRow v-else-if="!rows.length">
            <TableCell :colspan="columns.length + 1" class="text-center text-muted-foreground"
              >–</TableCell
            >
          </TableRow>
          <TableRow v-for="row in rows" :key="row.id">
            <TableCell v-for="c in columns" :key="c.key" :class="c.class">
              <slot :name="`cell-${c.key}`" :row="row">{{ cellValue(row, c.key) }}</slot>
            </TableCell>
            <TableCell class="text-right">
              <div class="flex justify-end gap-1">
                <slot name="actions" :row="row" />
              </div>
            </TableCell>
          </TableRow>
        </TableBody>
      </Table>
    </div>
  </div>
</template>
