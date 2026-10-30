<script setup lang="ts">
import { ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import ResponsiveDialog from '@/components/ResponsiveDialog.vue'
import FormField from '@/components/FormField.vue'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select'
import { seasonsApi, type SeasonResponse } from '@/api'
import { useSaving } from '@/composables/useSaving'

const props = defineProps<{
  open: boolean
  season: SeasonResponse | null
  seasons: SeasonResponse[]
}>()
const emit = defineEmits<{ 'update:open': [value: boolean]; saved: [] }>()
const { t } = useI18n()
const { saving, error, run } = useSaving()

const NONE = '__none__'
const name = ref('')
const startDate = ref('')
const endDate = ref('')
const copyFrom = ref(NONE)

watch(
  () => props.open,
  (open) => {
    if (!open) return
    error.value = ''
    const year = new Date().getFullYear()
    name.value = props.season?.name ?? ''
    startDate.value = props.season ? props.season.startDate.slice(0, 10) : `${year}-06-01`
    endDate.value = props.season ? props.season.endDate.slice(0, 10) : `${year}-06-30`
    copyFrom.value = NONE
  },
)

async function save(): Promise<void> {
  if (!name.value.trim()) {
    error.value = t('field_required')
    return
  }
  const body = {
    name: name.value.trim(),
    startDate: `${startDate.value}T00:00:00Z`,
    endDate: `${endDate.value}T00:00:00Z`,
  }
  const ok = await run(async () => {
    if (props.season) await seasonsApi.update({ ...body, id: props.season.id })
    else
      await seasonsApi.create({
        ...body,
        copyFromSeasonId: copyFrom.value === NONE ? null : copyFrom.value,
      })
  })
  if (ok) {
    emit('saved')
    emit('update:open', false)
  }
}
</script>

<template>
  <ResponsiveDialog
    :open="open"
    :title="season ? t('btn_edit') : t('btn_create')"
    @update:open="emit('update:open', $event)"
  >
    <form class="space-y-4" @submit.prevent="save">
      <FormField :label="t('admin_season_name')" for="season-name">
        <Input id="season-name" v-model="name" required />
      </FormField>
      <div class="grid grid-cols-2 gap-3">
        <FormField :label="t('admin_season_start')" for="season-start">
          <Input id="season-start" v-model="startDate" type="date" required />
        </FormField>
        <FormField :label="t('admin_season_end')" for="season-end">
          <Input id="season-end" v-model="endDate" type="date" required />
        </FormField>
      </div>
      <FormField
        v-if="!season"
        :label="t('admin_season_copy_from')"
        :hint="t('admin_season_copy_hint')"
      >
        <Select v-model="copyFrom">
          <SelectTrigger class="w-full"><SelectValue /></SelectTrigger>
          <SelectContent>
            <SelectItem :value="NONE">{{ t('admin_season_copy_none') }}</SelectItem>
            <SelectItem v-for="s in seasons" :key="s.id" :value="s.id">{{ s.name }}</SelectItem>
          </SelectContent>
        </Select>
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
