<script setup lang="ts">
import { ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import { Download } from '@lucide/vue'
import PageHeader from '@/components/PageHeader.vue'
import FormField from '@/components/FormField.vue'
import { Button } from '@/components/ui/button'
import { Checkbox } from '@/components/ui/checkbox'
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select'
import { locationsApi } from '@/api'
import { useLocationsStore } from '@/stores/locations'
import { useErrors } from '@/composables/useErrors'
import { downloadBlob, slug } from '@/domain/download'

const { t } = useI18n()
const locations = useLocationsStore()
const { report } = useErrors()
const locationId = ref('')
const anonymous = ref(false)
const busy = ref(false)

watch(
  () => locations.items,
  (items) => {
    if (!locationId.value && items[0]) locationId.value = items[0].id
  },
  { immediate: true },
)

async function download(): Promise<void> {
  const loc = locations.byId.get(locationId.value)
  if (!loc) return
  busy.value = true
  try {
    const blob = await locationsApi.exportXlsx(loc.id, anonymous.value)
    downloadBlob(blob, `${anonymous.value ? 'anonymous-' : ''}${slug(loc.name)}.xlsx`)
  } catch (error) {
    await report(error)
  } finally {
    busy.value = false
  }
}
</script>

<template>
  <div class="mx-auto max-w-xl">
    <PageHeader :title="t('admin_export_title')" />
    <div class="space-y-4 rounded-xl border border-border bg-card p-4">
      <FormField :label="t('admin_export_location')">
        <Select v-model="locationId">
          <SelectTrigger class="w-full"><SelectValue /></SelectTrigger>
          <SelectContent>
            <SelectItem v-for="l in locations.items" :key="l.id" :value="l.id">{{
              l.name
            }}</SelectItem>
          </SelectContent>
        </Select>
      </FormField>
      <label class="flex items-center gap-2 text-sm">
        <Checkbox :model-value="anonymous" @update:model-value="anonymous = $event === true" />
        {{ t('admin_export_anonymous') }}
      </label>
      <Button :disabled="!locationId || busy" @click="download"
        ><Download /> {{ t('admin_export_download') }}</Button
      >
    </div>
  </div>
</template>
