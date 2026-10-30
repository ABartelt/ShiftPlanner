<script setup lang="ts">
import { reactive, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import PageHeader from '@/components/PageHeader.vue'
import FormField from '@/components/FormField.vue'
import DurationField from '@/components/DurationField.vue'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import type { UpdateAppSettingsRequest } from '@/api'
import { useSettingsStore } from '@/stores/settings'
import { useSaving } from '@/composables/useSaving'
import { durationToMinutes, minutesToDuration } from '@/domain/time'

const { t } = useI18n()
const store = useSettingsStore()
const { saving, error, run } = useSaving()

const form = reactive({
  title: '',
  subtitle: '',
  contact: '',
  greeting: '',
  memberName: '',
  start: 480,
  end: 1560,
})

function reset(): void {
  const s = store.settings
  Object.assign(form, {
    title: s.title,
    subtitle: s.subtitle,
    contact: s.contact,
    greeting: s.greeting,
    memberName: s.memberName,
    start: durationToMinutes(s.startTime),
    end: durationToMinutes(s.endTime),
  })
}
watch(() => store.settings, reset, { immediate: true, deep: true })

async function save(): Promise<void> {
  const body: UpdateAppSettingsRequest = {
    title: form.title,
    subtitle: form.subtitle,
    contact: form.contact,
    greeting: form.greeting,
    memberName: form.memberName,
    startTime: minutesToDuration(form.start),
    endTime: minutesToDuration(form.end),
  }
  await run(() => store.update(body))
}
</script>

<template>
  <div class="mx-auto max-w-xl">
    <PageHeader :title="t('admin_settings_title')" :subtitle="t('admin_settings_subtitle')" />
    <form class="space-y-4 rounded-xl border border-border bg-card p-4" @submit.prevent="save">
      <FormField :label="t('admin_settings_app_title')" for="s-title"
        ><Input id="s-title" v-model="form.title"
      /></FormField>
      <FormField :label="t('admin_settings_subtitle_field')" for="s-subtitle"
        ><Input id="s-subtitle" v-model="form.subtitle"
      /></FormField>
      <FormField :label="t('admin_settings_contact')" for="s-contact"
        ><Input id="s-contact" v-model="form.contact"
      /></FormField>
      <FormField :label="t('admin_settings_greeting')" for="s-greeting"
        ><Input id="s-greeting" v-model="form.greeting"
      /></FormField>
      <FormField :label="t('admin_settings_member_name')" for="s-member"
        ><Input id="s-member" v-model="form.memberName"
      /></FormField>
      <div class="grid grid-cols-2 gap-3">
        <FormField :label="t('admin_settings_start')"
          ><DurationField v-model="form.start" :step="30" :min="0" :max="1440"
        /></FormField>
        <FormField :label="t('admin_settings_end')" :hint="t('admin_settings_end_hint')"
          ><DurationField v-model="form.end" :step="30" :min="60" :max="2880"
        /></FormField>
      </div>
      <p v-if="error" class="text-sm text-destructive">{{ error }}</p>
      <div class="flex justify-end gap-2">
        <Button type="button" variant="ghost" @click="reset">{{ t('btn_cancel') }}</Button>
        <Button type="submit" :disabled="saving">{{ t('btn_save') }}</Button>
      </div>
    </form>
  </div>
</template>
