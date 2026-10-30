<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { CircleCheck } from '@lucide/vue'
import { toast } from 'vue-sonner'
import PageHeader from '@/components/PageHeader.vue'
import AdminTable from '@/components/AdminTable.vue'
import RowActions from '@/components/RowActions.vue'
import SeasonDialog from '@/dialogs/SeasonDialog.vue'
import DeleteSeasonDialog from '@/dialogs/DeleteSeasonDialog.vue'
import { Button } from '@/components/ui/button'
import { seasonsApi, type SeasonResponse } from '@/api'
import { useAuth } from '@/auth/useAuth'
import { useSeasonStore } from '@/stores/season'
import { useAdminList } from '@/composables/useAdminList'
import { useErrors } from '@/composables/useErrors'
import { formatDate } from '@/domain/time'

const { t } = useI18n()
const auth = useAuth()
const seasonStore = useSeasonStore()
const { report } = useErrors()
const list = useAdminList<SeasonResponse>(() => seasonsApi.list())
const editing = ref<SeasonResponse | null>(null)
const dialogOpen = ref(false)
const deleting = ref<SeasonResponse | null>(null)
const deleteOpen = ref(false)

const columns = [
  { key: 'name', label: t('admin_season_name') },
  { key: 'startDate', label: t('admin_season_start') },
  { key: 'endDate', label: t('admin_season_end') },
  { key: 'current', label: '' },
]

async function reload(): Promise<void> {
  await list.load()
  await seasonStore.load().catch(report)
}
onMounted(reload)

function create(): void {
  editing.value = null
  dialogOpen.value = true
}
function edit(s: SeasonResponse): void {
  editing.value = s
  dialogOpen.value = true
}
function remove(s: SeasonResponse): void {
  deleting.value = s
  deleteOpen.value = true
}
async function setCurrent(s: SeasonResponse): Promise<void> {
  try {
    await seasonsApi.setCurrent(s.id)
    toast.success(t('saved'))
    await reload()
  } catch (error) {
    await report(error)
  }
}
</script>

<template>
  <div class="mx-auto max-w-5xl">
    <PageHeader :title="t('admin_seasons_title')" :subtitle="t('admin_seasons_subtitle')" />
    <AdminTable
      :columns="columns"
      :rows="list.items.value"
      :loading="list.loading.value"
      @create="create"
      @reload="reload"
    >
      <template #cell-startDate="{ row }">{{ formatDate(String(row.startDate)) }}</template>
      <template #cell-endDate="{ row }">{{ formatDate(String(row.endDate)) }}</template>
      <template #cell-current="{ row }">
        <Button
          variant="ghost"
          size="sm"
          :class="row.isSelectedAsCurrent ? 'text-status-green' : 'text-muted-foreground'"
          :title="t('admin_season_set_current')"
          @click="setCurrent(row)"
        >
          <CircleCheck />
          <span v-if="row.isSelectedAsCurrent">{{ t('admin_season_is_current') }}</span>
        </Button>
      </template>
      <template #actions="{ row }">
        <RowActions :can-delete="auth.isSuperAdmin.value" @edit="edit(row)" @remove="remove(row)" />
      </template>
    </AdminTable>
    <SeasonDialog
      v-model:open="dialogOpen"
      :season="editing"
      :seasons="list.items.value"
      @saved="reload"
    />
    <DeleteSeasonDialog v-model:open="deleteOpen" :season="deleting" @deleted="reload" />
  </div>
</template>
