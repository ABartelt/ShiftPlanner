<script setup lang="ts">
import { ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import { toast } from 'vue-sonner'
import { Send } from '@lucide/vue'
import AdminTable from '@/components/AdminTable.vue'
import RowActions from '@/components/RowActions.vue'
import ConfirmDialog from '@/components/ConfirmDialog.vue'
import PromotionChannelDialog from '@/dialogs/PromotionChannelDialog.vue'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { promotionsApi, type PromotionChannelResponse } from '@/api'
import { useSeasonStore } from '@/stores/season'
import { useAdminList } from '@/composables/useAdminList'
import { useErrors } from '@/composables/useErrors'
import { formatDateTime } from '@/domain/time'

const { t } = useI18n()
const season = useSeasonStore()
const { report } = useErrors()
const list = useAdminList<PromotionChannelResponse>(
  () => promotionsApi.channels(season.currentId!),
  (id) => promotionsApi.removeChannel(id),
)
const editing = ref<PromotionChannelResponse | null>(null)
const dialogOpen = ref(false)
const testing = ref<string | null>(null)

const columns = [
  { key: 'name', label: t('promote_channel_name') },
  { key: 'kind', label: t('promote_channel_kind'), class: 'w-28' },
  { key: 'target', label: t('promote_channel_target') },
  { key: 'status', label: t('promote_channel_status'), class: 'w-56' },
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
function edit(x: PromotionChannelResponse): void {
  editing.value = x
  dialogOpen.value = true
}

async function test(channel: PromotionChannelResponse): Promise<void> {
  testing.value = channel.id
  try {
    const result = await promotionsApi.testChannel(channel.id)
    if (result.ok) toast.success(t('promote_test_ok', { name: channel.name }))
    else toast.error(`${channel.name}: ${result.message}`)
    await list.load()
  } catch (error) {
    await report(error)
  } finally {
    testing.value = null
  }
}
</script>

<template>
  <div>
    <p class="mb-3 text-sm text-muted-foreground">{{ t('promote_channels_intro') }}</p>
    <AdminTable
      :columns="columns"
      :rows="list.items.value"
      :loading="list.loading.value"
      @create="create"
      @reload="list.load"
    >
      <template #cell-name="{ row }">
        <span :class="row.enabled ? '' : 'text-muted-foreground line-through'">{{ row.name }}</span>
      </template>
      <template #cell-target="{ row }">{{
        row.kind === 'Slack' ? 'Webhook' : row.config.targetName || row.config.target
      }}</template>
      <template #cell-status="{ row }">
        <div class="flex flex-col gap-0.5">
          <Badge v-if="!row.enabled" variant="outline">{{ t('promote_channel_disabled') }}</Badge>
          <Badge v-else-if="row.lastTestOk === false" variant="destructive">{{
            t('promote_test_failed')
          }}</Badge>
          <Badge v-else-if="row.lastTestOk" variant="secondary">{{
            t('promote_test_passed')
          }}</Badge>
          <Badge v-else variant="outline">{{ t('promote_test_never') }}</Badge>
          <span
            v-if="row.lastTestAt"
            class="text-xs text-muted-foreground"
            :title="row.lastTestMessage ?? ''"
            >{{ formatDateTime(row.lastTestAt) }}</span
          >
        </div>
      </template>
      <template #actions="{ row }">
        <div class="flex items-center justify-end gap-1">
          <Button variant="ghost" size="sm" :disabled="testing === row.id" @click="test(row)"
            ><Send /> {{ t('promote_test') }}</Button
          >
          <RowActions can-delete @edit="edit(row)" @remove="list.pendingDelete.value = row" />
        </div>
      </template>
    </AdminTable>
    <PromotionChannelDialog
      v-if="season.currentId"
      v-model:open="dialogOpen"
      :channel="editing"
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
