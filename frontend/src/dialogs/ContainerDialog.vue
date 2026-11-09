<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import { Trash2 } from '@lucide/vue'
import { toast } from 'vue-sonner'
import ResponsiveDialog from '@/components/ResponsiveDialog.vue'
import FormField from '@/components/FormField.vue'
import ColorField from '@/components/ColorField.vue'
import ConfirmDialog from '@/components/ConfirmDialog.vue'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select'
import {
  ApiError,
  containersApi,
  frameworksApi,
  type ContainerResponse,
  type FrameworkResponse,
} from '@/api'
import { useAuth } from '@/auth/useAuth'
import { useSaving } from '@/composables/useSaving'
import { useErrors } from '@/composables/useErrors'
import { dayKey, formatDateTime, instantAt, minutesIntoDay, minutesToClock } from '@/domain/time'

/**
 * Create (container = null, with repeat for subsequent days) or edit a container.
 * `shiftCount` is the number of booked shifts inside the container (informational, from the parent).
 */
const props = defineProps<{
  open: boolean
  container: ContainerResponse | null
  locationId: string
  seasonId: string
  initialStart?: Date | null
  shiftCount?: number
}>()
const emit = defineEmits<{ 'update:open': [value: boolean]; saved: [] }>()
const { t } = useI18n()
const auth = useAuth()
const { saving, error, run } = useSaving()
const { report } = useErrors()

const frameworks = ref<FrameworkResponse[]>([])
const frameworkId = ref('')
const day = ref('')
const startMinutes = ref(8 * 60)
const repeatDays = ref(0)
const totalShifts = ref(4)
const color = ref('rgba(59, 130, 246, 0.30)')
const confirmDelete = ref(false)

const framework = computed(() => frameworks.value.find((f) => f.id === frameworkId.value) ?? null)
const start = computed(() => instantAt(day.value, startMinutes.value))
const end = computed(() =>
  framework.value
    ? new Date(start.value.getTime() + framework.value.secondsPerShift * 1000 * totalShifts.value)
    : null,
)
const hasShifts = computed(() => (props.shiftCount ?? 0) > 0)

watch(
  () => props.open,
  async (open) => {
    if (!open) return
    error.value = ''
    frameworks.value = await frameworksApi.list(props.seasonId).catch(() => [])
    const c = props.container
    const initial = c ? new Date(c.start) : (props.initialStart ?? new Date())
    day.value = dayKey(initial)
    startMinutes.value = Math.max(0, minutesIntoDay(initial, day.value))
    frameworkId.value = c?.framework.id ?? frameworks.value[0]?.id ?? ''
    totalShifts.value = c?.totalShifts ?? 4
    color.value = c?.color || 'rgba(59, 130, 246, 0.30)'
    repeatDays.value = 0
  },
)

async function save(): Promise<void> {
  if (!frameworkId.value || !day.value) {
    error.value = t('field_required')
    return
  }
  const ok = await run(async () => {
    if (props.container) {
      await containersApi.update({
        id: props.container.id,
        frameworkId: frameworkId.value,
        start: start.value.toISOString(),
        totalShifts: totalShifts.value,
        color: color.value,
      })
    } else {
      for (let i = 0; i <= repeatDays.value; i++) {
        const s = instantAt(day.value, startMinutes.value + i * 1440)
        await containersApi.create({
          frameworkId: frameworkId.value,
          locationId: props.locationId,
          start: s.toISOString(),
          totalShifts: totalShifts.value,
          color: color.value,
        })
      }
    }
  })
  if (ok) {
    emit('saved')
    emit('update:open', false)
  }
}

async function remove(): Promise<void> {
  if (!props.container) return
  try {
    await containersApi.remove(props.container.id)
    toast.success(t('deleted'))
    emit('saved')
    emit('update:open', false)
  } catch (e) {
    if (e instanceof ApiError && e.status === 403) error.value = t('admin_container_has_shifts')
    else await report(e)
  }
}
</script>

<template>
  <ResponsiveDialog
    :open="open"
    :title="container ? t('admin_container_edit') : t('admin_container_new')"
    @update:open="emit('update:open', $event)"
  >
    <form class="space-y-4" @submit.prevent="save">
      <p
        v-if="container && hasShifts"
        class="rounded-lg border border-orange/50 bg-orange/10 p-2 text-sm"
      >
        {{ t('admin_container_affected', { count: shiftCount }) }}
      </p>
      <FormField :label="t('admin_container_framework')">
        <Select v-model="frameworkId">
          <SelectTrigger class="w-full"><SelectValue /></SelectTrigger>
          <SelectContent>
            <SelectItem v-for="f in frameworks" :key="f.id" :value="f.id"
              >{{ f.name }} ({{ minutesToClock(f.secondsPerShift / 60) }} h)</SelectItem
            >
          </SelectContent>
        </Select>
      </FormField>
      <div class="grid grid-cols-2 gap-3">
        <FormField :label="t('admin_container_start')" for="c-day">
          <Input id="c-day" v-model="day" type="date" required />
        </FormField>
        <FormField :label="t('shift_start')" for="c-time">
          <Input
            id="c-time"
            type="time"
            step="900"
            :model-value="minutesToClock(startMinutes)"
            @update:model-value="
              startMinutes =
                Number(String($event).split(':')[0]) * 60 +
                Number(String($event).split(':')[1] ?? 0)
            "
          />
        </FormField>
      </div>
      <div class="grid grid-cols-2 gap-3">
        <FormField :label="t('admin_container_count')" for="c-count">
          <Input id="c-count" v-model.number="totalShifts" type="number" min="1" max="48" />
        </FormField>
        <FormField
          v-if="!container"
          :label="t('admin_container_days')"
          for="c-repeat"
          :hint="t('admin_container_days_hint')"
        >
          <Input id="c-repeat" v-model.number="repeatDays" type="number" min="0" max="30" />
        </FormField>
        <FormField v-else :label="t('admin_container_end')">
          <p class="py-2 text-sm">{{ end ? formatDateTime(end) : '–' }}</p>
        </FormField>
      </div>
      <p v-if="!container && end" class="text-sm text-muted-foreground">
        {{ t('admin_container_end') }}: {{ formatDateTime(end) }}
      </p>
      <FormField :label="t('admin_container_color')" :hint="t('admin_container_color_hint')">
        <ColorField v-model="color" alpha />
      </FormField>
      <p v-if="error" class="text-sm text-destructive">{{ error }}</p>
      <div class="flex flex-wrap items-center justify-between gap-2">
        <Button
          v-if="container && (!hasShifts || auth.isSuperAdmin.value)"
          type="button"
          variant="destructive"
          size="sm"
          @click="confirmDelete = true"
          ><Trash2 /> {{ t('btn_delete') }}</Button
        >
        <span v-else />
        <div class="flex gap-2">
          <Button type="button" variant="ghost" @click="emit('update:open', false)">{{
            t('btn_cancel')
          }}</Button>
          <Button type="submit" :disabled="saving || !frameworkId">{{ t('btn_save') }}</Button>
        </div>
      </div>
    </form>
    <ConfirmDialog
      v-model:open="confirmDelete"
      :text="
        t('admin_container_delete_confirm', {
          name: container?.framework.name ?? '',
          start: container ? formatDateTime(container.start) : '',
        })
      "
      destructive
      @confirm="remove"
    />
  </ResponsiveDialog>
</template>
