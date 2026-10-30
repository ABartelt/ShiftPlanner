<script setup lang="ts">
import { ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import { Plus } from '@lucide/vue'
import ResponsiveDialog from '@/components/ResponsiveDialog.vue'
import FormField from '@/components/FormField.vue'
import LocationTypeDialog from './LocationTypeDialog.vue'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select'
import { locationsApi, type LocationResponse, type LocationTypeResponse } from '@/api'
import { useSaving } from '@/composables/useSaving'

const props = defineProps<{ open: boolean; location: LocationResponse | null; seasonId: string }>()
const emit = defineEmits<{
  'update:open': [value: boolean]
  saved: [location: LocationResponse | null]
}>()
const { t } = useI18n()
const { saving, error, run } = useSaving()
const name = ref('')
const typeId = ref('')
const types = ref<LocationTypeResponse[]>([])
const typeDialog = ref(false)

watch(
  () => props.open,
  async (open) => {
    if (!open) return
    error.value = ''
    name.value = props.location?.name ?? ''
    types.value = await locationsApi.types().catch(() => [])
    typeId.value = props.location?.type.id ?? types.value[0]?.id ?? ''
  },
)

function onTypeCreated(type: LocationTypeResponse): void {
  types.value = [...types.value, type]
  typeId.value = type.id
}

async function save(): Promise<void> {
  if (!name.value.trim() || !typeId.value) {
    error.value = t('field_required')
    return
  }
  const body = { name: name.value.trim(), typeId: typeId.value, seasonId: props.seasonId }
  let created: LocationResponse | null = null
  const ok = await run(async () => {
    if (props.location) await locationsApi.update({ ...body, id: props.location.id })
    else created = await locationsApi.create(body)
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
    :title="location ? t('btn_edit') : t('btn_create')"
    @update:open="emit('update:open', $event)"
  >
    <form class="space-y-4" @submit.prevent="save">
      <FormField :label="t('admin_location_name')" for="loc-name">
        <Input id="loc-name" v-model="name" required />
      </FormField>
      <FormField :label="t('admin_location_type')">
        <div class="flex gap-2">
          <Select v-model="typeId">
            <SelectTrigger class="flex-1"><SelectValue /></SelectTrigger>
            <SelectContent>
              <SelectItem v-for="x in types" :key="x.id" :value="x.id"
                >{{ x.name
                }}<span v-if="x.adminOnly" class="ml-1 text-muted-foreground"
                  >(Admin)</span
                ></SelectItem
              >
            </SelectContent>
          </Select>
          <Button
            type="button"
            variant="outline"
            size="icon"
            :title="t('admin_location_type_new')"
            @click="typeDialog = true"
            ><Plus
          /></Button>
        </div>
      </FormField>
      <p v-if="error" class="text-sm text-destructive">{{ error }}</p>
      <div class="flex justify-end gap-2">
        <Button type="button" variant="ghost" @click="emit('update:open', false)">{{
          t('btn_cancel')
        }}</Button>
        <Button type="submit" :disabled="saving || !typeId">{{ t('btn_save') }}</Button>
      </div>
    </form>
    <LocationTypeDialog v-model:open="typeDialog" @created="onTypeCreated" />
  </ResponsiveDialog>
</template>
