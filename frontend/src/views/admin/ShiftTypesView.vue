<script setup lang="ts">
import { ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import PageHeader from '@/components/PageHeader.vue'
import AdminTable from '@/components/AdminTable.vue'
import RowActions from '@/components/RowActions.vue'
import ConfirmDialog from '@/components/ConfirmDialog.vue'
import ShiftTypeDialog from '@/dialogs/ShiftTypeDialog.vue'
import { shiftTypesApi, type ShiftTypeResponse } from '@/api'
import { useAuth } from '@/auth/useAuth'
import { useSeasonStore } from '@/stores/season'
import { useAdminList } from '@/composables/useAdminList'
import { durationToMinutes } from '@/domain/time'

const { t } = useI18n()
const auth = useAuth()
const season = useSeasonStore()
const list = useAdminList<ShiftTypeResponse>(
  () => shiftTypesApi.list(season.currentId!),
  (id) => shiftTypesApi.remove(id),
)
const editing = ref<ShiftTypeResponse | null>(null)
const dialogOpen = ref(false)

const columns = [
  { key: 'name', label: t('admin_shift_type_name') },
  { key: 'color', label: t('admin_shift_type_color'), class: 'w-24' },
  { key: 'startingTimeShift', label: t('admin_shift_type_offset'), class: 'w-40' },
  { key: 'onlyAssignableByAdmin', label: t('admin_shift_type_admin_only'), class: 'w-40' },
]

watch(
  () => season.currentId,
  (id) => id && list.load(),
  { immediate: true },
)

function create(): void {
  editing.value = null
  dialogOpen.value = true
}
function edit(x: ShiftTypeResponse): void {
  editing.value = x
  dialogOpen.value = true
}
</script>

<template>
  <div class="mx-auto max-w-5xl">
    <PageHeader :title="t('admin_shift_types_title')" :subtitle="t('admin_shift_types_subtitle')" />
    <AdminTable
      :columns="columns"
      :rows="list.items.value"
      :loading="list.loading.value"
      @create="create"
      @reload="list.load"
    >
      <template #cell-color="{ row }"
        ><span
          class="inline-block size-5 rounded-md border border-border"
          :style="{ background: String(row.color) }"
      /></template>
      <template #cell-startingTimeShift="{ row }"
        >{{ durationToMinutes(String(row.startingTimeShift)) }} {{ t('unit_minutes') }}</template
      >
      <template #cell-onlyAssignableByAdmin="{ row }">{{
        row.onlyAssignableByAdmin ? t('yes') : t('no')
      }}</template>
      <template #actions="{ row }">
        <RowActions
          :can-delete="auth.isSuperAdmin.value"
          @edit="edit(row)"
          @remove="list.pendingDelete.value = row"
        />
      </template>
    </AdminTable>
    <ShiftTypeDialog
      v-if="season.currentId"
      v-model:open="dialogOpen"
      :shift-type="editing"
      :season-id="season.currentId"
      @saved="list.load"
    />
    <ConfirmDialog
      :open="!!list.pendingDelete.value"
      :text="t('confirm_delete', { name: list.pendingDelete.value?.name ?? '' })"
      destructive
      @update:open="!$event && (list.pendingDelete.value = null)"
      @confirm="list.confirmDelete"
    />
  </div>
</template>
