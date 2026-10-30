<script setup lang="ts">
import { ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import ResponsiveDialog from '@/components/ResponsiveDialog.vue'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { frameworksApi, locationsApi, seasonsApi, shiftTypesApi, type SeasonResponse } from '@/api'
import { useSaving } from '@/composables/useSaving'

const props = defineProps<{ open: boolean; season: SeasonResponse | null }>()
const emit = defineEmits<{ 'update:open': [value: boolean]; deleted: [] }>()
const { t } = useI18n()
const { saving, error, run } = useSaving()

const locations = ref<string[]>([])
const frameworks = ref<string[]>([])
const types = ref<string[]>([])
const typed = ref('')

watch(
  () => props.open,
  async (open) => {
    if (!open || !props.season) return
    typed.value = ''
    error.value = ''
    const id = props.season.id
    const [l, f, s] = await Promise.all([
      locationsApi.list(id),
      frameworksApi.list(id),
      shiftTypesApi.list(id),
    ])
    locations.value = l.map((x) => x.name)
    frameworks.value = f.map((x) => x.name)
    types.value = s.map((x) => x.name)
  },
)

async function remove(): Promise<void> {
  if (!props.season) return
  if (typed.value.trim() !== props.season.name) {
    error.value = t('admin_season_delete_mismatch')
    return
  }
  const ok = await run(() => seasonsApi.remove(props.season!.id), t('deleted'))
  if (ok) {
    emit('deleted')
    emit('update:open', false)
  }
}
</script>

<template>
  <ResponsiveDialog
    :open="open"
    :title="t('admin_season_delete_title')"
    @update:open="emit('update:open', $event)"
  >
    <div v-if="season" class="space-y-4 text-sm">
      <p>{{ t('admin_season_delete_text', { name: season.name }) }}</p>
      <div
        v-if="locations.length + frameworks.length + types.length"
        class="space-y-2 rounded-lg border border-destructive/40 bg-destructive/10 p-3"
      >
        <p>
          {{
            t('admin_season_delete_contents', {
              locations: locations.length,
              frameworks: frameworks.length,
              types: types.length,
            })
          }}
        </p>
        <p v-if="locations.length">
          <b>{{ t('nav_locations') }}:</b> {{ locations.join(', ') }}
        </p>
        <p v-if="frameworks.length">
          <b>{{ t('nav_frameworks') }}:</b> {{ frameworks.join(', ') }}
        </p>
        <p v-if="types.length">
          <b>{{ t('nav_shift_types') }}:</b> {{ types.join(', ') }}
        </p>
      </div>
      <p>{{ t('admin_season_delete_confirm', { name: season.name }) }}</p>
      <Input v-model="typed" :placeholder="season.name" />
      <p v-if="error" class="text-destructive">{{ error }}</p>
      <div class="flex justify-end gap-2">
        <Button variant="ghost" @click="emit('update:open', false)">{{ t('btn_cancel') }}</Button>
        <Button variant="destructive" :disabled="saving" @click="remove">{{
          t('btn_delete')
        }}</Button>
      </div>
    </div>
  </ResponsiveDialog>
</template>
