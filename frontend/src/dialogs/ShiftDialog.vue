<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import { toast } from 'vue-sonner'
import { Trash2 } from '@lucide/vue'
import { Button } from '@/components/ui/button'
import { Checkbox } from '@/components/ui/checkbox'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select'
import ResponsiveDialog from '@/components/ResponsiveDialog.vue'
import ConfirmDialog from '@/components/ConfirmDialog.vue'
import {
  ApiError,
  EMPTY_GUID,
  containersApi,
  employeesApi,
  shiftsApi,
  type ContainerResponse,
  type EmployeeResponse,
  type ShiftTypeResponse,
} from '@/api'
import { useAuth } from '@/auth/useAuth'
import { useSettingsStore } from '@/stores/settings'
import { useErrors } from '@/composables/useErrors'
import type { ShiftCard } from '@/domain/shifts'
import { durationToMinutes, formatDayLong, formatTime } from '@/domain/time'

const props = defineProps<{ card: ShiftCard | null; open: boolean }>()
const emit = defineEmits<{ 'update:open': [value: boolean]; saved: [] }>()

const { t } = useI18n()
const auth = useAuth()
const settings = useSettingsStore()
const { report, messageOf } = useErrors()

const loading = ref(false)
const saving = ref(false)
const container = ref<ContainerResponse | null>(null)
const types = ref<ShiftTypeResponse[]>([])
const employees = ref<EmployeeResponse[]>([])
const typeId = ref('')
const employeeId = ref(EMPTY_GUID)
const blocked = ref(false)
const blockReason = ref('')
const error = ref('')
const confirmDelete = ref(false)
const confirmUnblockDelete = ref(false)

const isNew = computed(() => !props.card?.shift)
const isMine = computed(
  () => !!props.card?.shift && props.card.shift.employeeId === auth.profile.value?.sub,
)
const isAdmin = computed(() => auth.isAdmin.value)
const canEdit = computed(() => isNew.value || isMine.value || isAdmin.value)
const selectedType = computed(
  () => types.value.find((x) => x.id === typeId.value) ?? props.card?.type ?? null,
)
const offset = computed(() => durationToMinutes(selectedType.value?.startingTimeShift))
const title = computed(() => (isNew.value ? t('shift_new') : t('shift_edit')))

watch(
  () => props.open,
  async (open) => {
    if (!open || !props.card) return
    error.value = ''
    blocked.value = props.card.status === 'blocked'
    blockReason.value = props.card.blockReason ?? ''
    employeeId.value =
      props.card.shift?.employeeId ??
      (blocked.value ? EMPTY_GUID : (auth.profile.value?.sub ?? EMPTY_GUID))
    typeId.value = props.card.type.id
    loading.value = true
    try {
      const [c, availability, people] = await Promise.all([
        containersApi.get(props.card.containerId),
        containersApi.availableTypes(props.card.containerId, props.card.start),
        isAdmin.value ? employeesApi.list() : Promise.resolve<EmployeeResponse[]>([]),
      ])
      container.value = c
      employees.value = people
      // types with a free position at this slot, plus the shift's own type
      const free = availability.filter((a) => a.availableCount > 0).map((a) => a.type)
      const list = free.filter((x) => isAdmin.value || !x.onlyAssignableByAdmin)
      if (props.card.shift?.type && !list.some((x) => x.id === props.card!.type.id))
        list.unshift(props.card.type)
      types.value = list
      if (!list.length) {
        error.value = t('shift_no_types')
      } else if (!list.some((x) => x.id === typeId.value)) {
        typeId.value = list[0]!.id
      }
    } catch (e) {
      await report(e)
      emit('update:open', false)
    } finally {
      loading.value = false
    }
  },
  { immediate: true },
)

function close(): void {
  emit('update:open', false)
}

async function save(): Promise<void> {
  const card = props.card
  if (!card || !container.value) return
  error.value = ''
  if (!canEdit.value) {
    error.value = t('shift_not_allowed')
    return
  }
  if (blocked.value) {
    if (!blockReason.value.trim()) {
      error.value = t('shift_block_reason_required')
      return
    }
    if (blockReason.value.trim().length > 50) {
      error.value = t('shift_block_reason_max')
      return
    }
  }
  const body = {
    employeeKeycloakId: blocked.value ? EMPTY_GUID : employeeId.value,
    shiftTypeId: typeId.value,
    start: card.start.toISOString(),
    blockReason: blocked.value ? blockReason.value.trim() : null,
  }
  if (!blocked.value && body.employeeKeycloakId === EMPTY_GUID) {
    if (isNew.value) {
      error.value = t('shift_person_required')
      return
    }
    confirmUnblockDelete.value = true
    return
  }
  saving.value = true
  try {
    if (isNew.value) {
      await containersApi.createShift(card.containerId, body)
      toast.success(t('shift_booked'))
    } else {
      await shiftsApi.update(card.shift!.id, body)
      toast.success(t('shift_updated'))
    }
    emit('saved')
    close()
  } catch (e) {
    if (e instanceof ApiError && e.status === 409) error.value = t('shift_conflict')
    else if (e instanceof ApiError && e.status < 500) error.value = messageOf(e)
    else await report(e)
  } finally {
    saving.value = false
  }
}

async function remove(): Promise<void> {
  const shift = props.card?.shift
  if (!shift) return
  saving.value = true
  try {
    await shiftsApi.remove(shift.id)
    toast.success(t('shift_deleted'))
    emit('saved')
    close()
  } catch (e) {
    if (e instanceof ApiError && e.status === 423) {
      error.value = t('shift_too_late', {
        contact: settings.settings.contact || t('error_contact_fallback'),
      })
    } else if (e instanceof ApiError && e.status < 500) {
      error.value = messageOf(e)
    } else {
      await report(e)
    }
  } finally {
    saving.value = false
  }
}

function onBlockedChange(value: boolean | 'indeterminate'): void {
  blocked.value = value === true
  if (blocked.value) employeeId.value = EMPTY_GUID
}
</script>

<template>
  <ResponsiveDialog :open="open" :title="title" @update:open="emit('update:open', $event)">
    <div v-if="card" class="space-y-4">
      <div>
        <p class="text-sm text-muted-foreground">{{ formatDayLong(card.start) }}</p>
        <p class="font-display text-lg font-semibold">
          {{ formatTime(card.start, offset) }} – {{ formatTime(card.end, offset) }}
        </p>
        <p v-if="isAdmin" class="eyebrow mt-1">{{ t('shift_admin_mode') }}</p>
      </div>

      <div class="space-y-1.5">
        <Label>{{ t('shift_type') }}</Label>
        <Select v-if="canEdit && types.length" v-model="typeId" :disabled="loading">
          <SelectTrigger class="w-full"><SelectValue /></SelectTrigger>
          <SelectContent>
            <SelectItem v-for="x in types" :key="x.id" :value="x.id">
              <span
                class="mr-2 inline-block size-3 rounded-full align-middle"
                :style="{ background: x.color }"
              />{{ x.name }}
            </SelectItem>
          </SelectContent>
        </Select>
        <p v-else class="text-sm">{{ card.type.name }}</p>
        <p v-if="selectedType?.description" class="text-sm italic text-muted-foreground">
          {{ selectedType.description }}
        </p>
      </div>

      <div class="space-y-1.5">
        <Label>{{ t('shift_person') }}</Label>
        <Select v-if="isAdmin" v-model="employeeId" :disabled="blocked || loading">
          <SelectTrigger class="w-full"><SelectValue /></SelectTrigger>
          <SelectContent>
            <SelectItem :value="EMPTY_GUID">{{ t('shift_nobody') }}</SelectItem>
            <SelectItem v-for="e in employees" :key="e.id" :value="e.id">{{
              e.fullName
            }}</SelectItem>
          </SelectContent>
        </Select>
        <p v-else class="text-sm">
          {{ card.shift?.employeeFullName || auth.profile.value?.fullName }}
          <span v-if="isNew || isMine" class="text-muted-foreground">({{ t('shift_you') }})</span>
        </p>
      </div>

      <div v-if="isAdmin" class="space-y-2 rounded-lg border border-border p-3">
        <label class="flex items-center gap-2 text-sm">
          <Checkbox :model-value="blocked" @update:model-value="onBlockedChange" />
          {{ t('shift_block') }}
        </label>
        <div v-if="blocked" class="space-y-1.5">
          <Label for="block-reason">{{ t('shift_block_reason') }}</Label>
          <Input id="block-reason" v-model="blockReason" maxlength="50" autofocus />
        </div>
      </div>

      <p v-if="error" class="rounded-lg border border-destructive/50 bg-destructive/10 p-3 text-sm">
        {{ error }}
      </p>

      <div class="flex flex-wrap items-center justify-between gap-2 pt-2">
        <Button
          v-if="!isNew && canEdit"
          variant="destructive"
          size="sm"
          :disabled="saving"
          @click="confirmDelete = true"
        >
          <Trash2 /> {{ t('shift_delete') }}
        </Button>
        <span v-else />
        <div class="flex gap-2">
          <Button variant="ghost" @click="close">{{ t('btn_cancel') }}</Button>
          <Button :disabled="saving || loading || !types.length || !canEdit" @click="save">{{
            t('btn_ok')
          }}</Button>
        </div>
      </div>
    </div>

    <ConfirmDialog
      v-model:open="confirmDelete"
      :title="t('shift_delete')"
      :text="t('shift_delete_confirm')"
      destructive
      @confirm="remove"
    />
    <ConfirmDialog
      v-model:open="confirmUnblockDelete"
      :title="t('shift_delete')"
      :text="t('shift_unblock_delete_confirm')"
      destructive
      @confirm="remove"
    />
  </ResponsiveDialog>
</template>
