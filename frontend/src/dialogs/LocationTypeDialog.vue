<script setup lang="ts">
import { ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import ResponsiveDialog from '@/components/ResponsiveDialog.vue'
import FormField from '@/components/FormField.vue'
import { Button } from '@/components/ui/button'
import { Checkbox } from '@/components/ui/checkbox'
import { Input } from '@/components/ui/input'
import { locationsApi, type LocationTypeResponse } from '@/api'
import { useSaving } from '@/composables/useSaving'

const props = defineProps<{ open: boolean }>()
const emit = defineEmits<{
  'update:open': [value: boolean]
  created: [type: LocationTypeResponse]
}>()
const { t } = useI18n()
const { saving, error, run } = useSaving()
const name = ref('')
const adminOnly = ref(false)

watch(
  () => props.open,
  (open) => {
    if (open) {
      name.value = ''
      adminOnly.value = false
      error.value = ''
    }
  },
)

async function save(): Promise<void> {
  if (!name.value.trim()) {
    error.value = t('field_required')
    return
  }
  let created: LocationTypeResponse | null = null
  const ok = await run(async () => {
    created = await locationsApi.createType({ name: name.value.trim(), adminOnly: adminOnly.value })
  })
  if (ok && created) {
    emit('created', created)
    emit('update:open', false)
  }
}
</script>

<template>
  <ResponsiveDialog
    :open="open"
    :title="t('admin_location_type_new')"
    @update:open="emit('update:open', $event)"
  >
    <form class="space-y-4" @submit.prevent="save">
      <FormField :label="t('admin_location_name')" for="lt-name">
        <Input id="lt-name" v-model="name" required />
      </FormField>
      <label class="flex items-center gap-2 text-sm">
        <Checkbox :model-value="adminOnly" @update:model-value="adminOnly = $event === true" />
        {{ t('admin_location_type_admin_only') }}
      </label>
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
