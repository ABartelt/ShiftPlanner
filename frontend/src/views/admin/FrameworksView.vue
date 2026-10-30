<script setup lang="ts">
import { ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import PageHeader from '@/components/PageHeader.vue'
import AdminTable from '@/components/AdminTable.vue'
import RowActions from '@/components/RowActions.vue'
import ConfirmDialog from '@/components/ConfirmDialog.vue'
import FrameworkDialog from '@/dialogs/FrameworkDialog.vue'
import { frameworksApi, type FrameworkResponse } from '@/api'
import { useAuth } from '@/auth/useAuth'
import { useSeasonStore } from '@/stores/season'
import { useAdminList } from '@/composables/useAdminList'
import { minutesToClock } from '@/domain/time'

const { t } = useI18n()
const auth = useAuth()
const season = useSeasonStore()
const list = useAdminList<FrameworkResponse>(
  () => frameworksApi.list(season.currentId!),
  (id) => frameworksApi.remove(id),
)
const editing = ref<FrameworkResponse | null>(null)
const dialogOpen = ref(false)

const columns = [
  { key: 'name', label: t('admin_framework_name') },
  { key: 'secondsPerShift', label: t('admin_framework_duration'), class: 'w-32' },
  { key: 'shiftTypeCounts', label: t('admin_framework_counts') },
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
function edit(x: FrameworkResponse): void {
  editing.value = x
  dialogOpen.value = true
}
</script>

<template>
  <div class="mx-auto max-w-5xl">
    <PageHeader :title="t('admin_frameworks_title')" :subtitle="t('admin_frameworks_subtitle')" />
    <AdminTable
      :columns="columns"
      :rows="list.items.value"
      :loading="list.loading.value"
      @create="create"
      @reload="list.load"
    >
      <template #cell-secondsPerShift="{ row }"
        >{{ minutesToClock(Number(row.secondsPerShift) / 60) }} h</template
      >
      <template #cell-shiftTypeCounts="{ row }">
        <span
          v-for="c in row.shiftTypeCounts"
          :key="c.shiftType.id"
          class="mr-2 inline-flex items-center gap-1 text-sm"
        >
          <span
            class="inline-block size-2.5 rounded-full"
            :style="{ background: c.shiftType.color }"
          />{{ c.count }}× {{ c.shiftType.name }}
        </span>
      </template>
      <template #actions="{ row }">
        <RowActions
          :can-delete="auth.isSuperAdmin.value"
          @edit="edit(row)"
          @remove="list.pendingDelete.value = row"
        />
      </template>
    </AdminTable>
    <FrameworkDialog
      v-if="season.currentId"
      v-model:open="dialogOpen"
      :framework="editing"
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
