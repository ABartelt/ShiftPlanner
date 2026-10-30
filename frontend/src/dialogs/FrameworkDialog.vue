<script setup lang="ts">
import { ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import { Plus, X } from '@lucide/vue'
import ResponsiveDialog from '@/components/ResponsiveDialog.vue'
import FormField from '@/components/FormField.vue'
import DurationField from '@/components/DurationField.vue'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select'
import { frameworksApi, shiftTypesApi, type FrameworkResponse, type ShiftTypeResponse } from '@/api'
import { useSaving } from '@/composables/useSaving'

const props = defineProps<{
  open: boolean
  framework: FrameworkResponse | null
  seasonId: string
}>()
const emit = defineEmits<{
  'update:open': [value: boolean]
  saved: [framework: FrameworkResponse | null]
}>()
const { t } = useI18n()
const { saving, error, run } = useSaving()

const name = ref('')
const minutes = ref(90)
const rows = ref<{ typeId: string; count: number }[]>([])
const types = ref<ShiftTypeResponse[]>([])

watch(
  () => props.open,
  async (open) => {
    if (!open) return
    error.value = ''
    name.value = props.framework?.name ?? ''
    minutes.value = props.framework ? Math.round(props.framework.secondsPerShift / 60) : 90
    rows.value = props.framework?.shiftTypeCounts.map((c) => ({
      typeId: c.shiftType.id,
      count: c.count,
    })) ?? [{ typeId: '', count: 1 }]
    types.value = await shiftTypesApi.list(props.seasonId).catch(() => [])
  },
)

async function save(): Promise<void> {
  const typeCounts = rows.value
    .filter((r) => r.typeId && r.count > 0)
    .map((r) => ({ shiftTypeId: r.typeId, count: r.count }))
  if (!name.value.trim()) {
    error.value = t('field_required')
    return
  }
  if (!typeCounts.length) {
    error.value = t('admin_framework_counts_required')
    return
  }
  const body = {
    name: name.value.trim(),
    secondsPerShift: minutes.value * 60,
    seasonId: props.seasonId,
    typeCounts,
  }
  let created: FrameworkResponse | null = null
  const ok = await run(async () => {
    if (props.framework) await frameworksApi.update({ ...body, id: props.framework.id })
    else created = await frameworksApi.create(body)
  })
  if (ok) {
    emit('saved', created)
    emit('update:open', false)
  }
}
</script>

<template>
  <ResponsiveDialog
    :open="open"
    :title="framework ? t('btn_edit') : t('btn_create')"
    @update:open="emit('update:open', $event)"
  >
    <form class="space-y-4" @submit.prevent="save">
      <FormField :label="t('admin_framework_name')" for="fw-name">
        <Input id="fw-name" v-model="name" required />
      </FormField>
      <FormField :label="t('admin_framework_duration')">
        <DurationField v-model="minutes" :step="5" :min="5" />
      </FormField>
      <FormField :label="t('admin_framework_counts')">
        <div class="space-y-2">
          <div v-for="(row, i) in rows" :key="i" class="flex items-center gap-2">
            <Select v-model="row.typeId">
              <SelectTrigger class="flex-1"
                ><SelectValue :placeholder="t('shift_type')"
              /></SelectTrigger>
              <SelectContent>
                <SelectItem v-for="x in types" :key="x.id" :value="x.id">{{ x.name }}</SelectItem>
              </SelectContent>
            </Select>
            <Input
              v-model.number="row.count"
              type="number"
              min="1"
              max="99"
              class="w-20"
              :aria-label="t('admin_framework_count')"
            />
            <Button type="button" variant="ghost" size="icon-sm" @click="rows.splice(i, 1)"
              ><X
            /></Button>
          </div>
          <Button
            type="button"
            variant="outline"
            size="sm"
            @click="rows.push({ typeId: '', count: 1 })"
            ><Plus /> {{ t('admin_framework_add_type') }}</Button
          >
        </div>
      </FormField>
      <p v-if="error" class="text-sm text-destructive">{{ error }}</p>
      <div class="flex justify-end gap-2">
        <Button type="button" variant="ghost" @click="emit('update:open', false)">{{
          t('btn_cancel')
        }}</Button>
        <Button type="submit" :disabled="saving">{{ t('btn_save') }}</Button>
      </div>
    </form>
  </ResponsiveDialog>
</template>
