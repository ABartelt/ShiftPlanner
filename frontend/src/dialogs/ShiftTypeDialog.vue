<script setup lang="ts">
import { ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import ResponsiveDialog from '@/components/ResponsiveDialog.vue'
import FormField from '@/components/FormField.vue'
import ColorField from '@/components/ColorField.vue'
import { Button } from '@/components/ui/button'
import { Checkbox } from '@/components/ui/checkbox'
import { Input } from '@/components/ui/input'
import { Textarea } from '@/components/ui/textarea'
import { shiftTypesApi, type ShiftTypeResponse } from '@/api'
import { useSaving } from '@/composables/useSaving'
import { durationToMinutes, minutesToDuration } from '@/domain/time'

const props = defineProps<{
  open: boolean
  shiftType: ShiftTypeResponse | null
  seasonId: string
}>()
const emit = defineEmits<{
  'update:open': [value: boolean]
  saved: [type: ShiftTypeResponse | null]
}>()
const { t } = useI18n()
const { saving, error, run } = useSaving()

const name = ref('')
const color = ref('#e11d48')
const offset = ref(0)
const adminOnly = ref(false)
const description = ref('')

watch(
  () => props.open,
  (open) => {
    if (!open) return
    error.value = ''
    name.value = props.shiftType?.name ?? ''
    color.value = props.shiftType?.color || '#e11d48'
    offset.value = durationToMinutes(props.shiftType?.startingTimeShift)
    adminOnly.value = props.shiftType?.onlyAssignableByAdmin ?? false
    description.value = props.shiftType?.description ?? ''
  },
)

async function save(): Promise<void> {
  if (!name.value.trim()) {
    error.value = t('admin_shift_type_name_required')
    return
  }
  if (!color.value) {
    error.value = t('admin_shift_type_color_required')
    return
  }
  const body = {
    name: name.value.trim(),
    color: color.value,
    startingTimeShift: minutesToDuration(Math.max(-180, Math.min(180, offset.value))),
    onlyAssignableByAdmin: adminOnly.value,
    seasonId: props.seasonId,
    description: description.value.trim() || null,
  }
  let created: ShiftTypeResponse | null = null
  const ok = await run(async () => {
    if (props.shiftType) await shiftTypesApi.update({ ...body, id: props.shiftType.id })
    else created = await shiftTypesApi.create(body)
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
    :title="shiftType ? t('btn_edit') : t('btn_create')"
    @update:open="emit('update:open', $event)"
  >
    <form class="space-y-4" @submit.prevent="save">
      <FormField :label="t('admin_shift_type_name')" for="st-name">
        <Input id="st-name" v-model="name" required />
      </FormField>
      <FormField :label="t('admin_shift_type_color')">
        <ColorField v-model="color" />
      </FormField>
      <FormField
        :label="t('admin_shift_type_offset')"
        for="st-offset"
        :hint="t('admin_shift_type_offset_hint')"
      >
        <Input
          id="st-offset"
          v-model.number="offset"
          type="number"
          min="-180"
          max="180"
          step="5"
          class="w-32"
        />
      </FormField>
      <label class="flex items-center gap-2 text-sm">
        <Checkbox :model-value="adminOnly" @update:model-value="adminOnly = $event === true" />
        {{ t('admin_shift_type_admin_only') }}
      </label>
      <FormField :label="t('admin_shift_type_description')" for="st-desc">
        <Textarea id="st-desc" v-model="description" rows="3" />
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
