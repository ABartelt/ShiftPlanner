<script setup lang="ts">
import { ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import { Boxes, Sparkles } from '@lucide/vue'
import PageHeader from '@/components/PageHeader.vue'
import AdminTable from '@/components/AdminTable.vue'
import RowActions from '@/components/RowActions.vue'
import ConfirmDialog from '@/components/ConfirmDialog.vue'
import LocationDialog from '@/dialogs/LocationDialog.vue'
import { Button } from '@/components/ui/button'
import { locationsApi, type LocationResponse } from '@/api'
import { useAuth } from '@/auth/useAuth'
import { useSeasonStore } from '@/stores/season'
import { useLocationsStore } from '@/stores/locations'
import { useAdminList } from '@/composables/useAdminList'

const { t } = useI18n()
const auth = useAuth()
const season = useSeasonStore()
const locationsStore = useLocationsStore()
const list = useAdminList<LocationResponse>(
  () => locationsApi.list(season.currentId!),
  (id) => locationsApi.remove(id),
)
const editing = ref<LocationResponse | null>(null)
const dialogOpen = ref(false)

const columns = [
  { key: 'name', label: t('admin_location_name') },
  { key: 'type', label: t('admin_location_type'), class: 'w-48' },
  { key: 'containers', label: t('admin_location_containers'), class: 'w-48' },
]
watch(
  () => season.currentId,
  (id) => id && list.load(),
  { immediate: true },
)

async function reload(): Promise<void> {
  await list.load()
  if (season.currentId) await locationsStore.load(season.currentId).catch(() => undefined)
}
function create(): void {
  editing.value = null
  dialogOpen.value = true
}
function edit(x: LocationResponse): void {
  editing.value = x
  dialogOpen.value = true
}
</script>

<template>
  <div class="mx-auto max-w-5xl">
    <PageHeader :title="t('admin_locations_title')" :subtitle="t('admin_locations_subtitle')">
      <template #actions>
        <Button variant="outline" size="sm" as-child
          ><RouterLink to="/admin/setup"><Sparkles /> {{ t('nav_setup') }}</RouterLink></Button
        >
      </template>
    </PageHeader>
    <AdminTable
      :columns="columns"
      :rows="list.items.value"
      :loading="list.loading.value"
      @create="create"
      @reload="reload"
    >
      <template #cell-type="{ row }">{{ row.type?.name }}</template>
      <template #cell-containers="{ row }">
        <Button variant="outline" size="sm" as-child>
          <RouterLink :to="`/admin/locations/${row.id}/containers`"
            ><Boxes /> {{ t('admin_location_containers_edit') }}</RouterLink
          >
        </Button>
      </template>
      <template #actions="{ row }">
        <RowActions
          :can-delete="auth.isSuperAdmin.value"
          @edit="edit(row)"
          @remove="list.pendingDelete.value = row"
        />
      </template>
    </AdminTable>
    <LocationDialog
      v-if="season.currentId"
      v-model:open="dialogOpen"
      :location="editing"
      :season-id="season.currentId"
      @saved="reload"
    />
    <ConfirmDialog
      :open="!!list.pendingDelete.value"
      :text="t('confirm_delete', { name: list.pendingDelete.value?.name ?? '' })"
      destructive
      @update:open="!$event && (list.pendingDelete.value = null)"
      @confirm="list.confirmDelete().then(reload)"
    />
  </div>
</template>
