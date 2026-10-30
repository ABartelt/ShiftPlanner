<script setup lang="ts">
import { computed, onMounted, ref, watch, type Component } from 'vue'
import { useI18n } from 'vue-i18n'
import {
  ArrowLeft,
  ArrowRight,
  CalendarDays,
  Check,
  CircleCheck,
  ClipboardCheck,
  Info,
  Layers,
  LoaderCircle,
  Lock,
  MapPin,
  Plus,
  Sparkles,
  Tag,
  Trash2,
} from '@lucide/vue'
import { toast } from 'vue-sonner'
import PageHeader from '@/components/PageHeader.vue'
import FormField from '@/components/FormField.vue'
import ColorField from '@/components/ColorField.vue'
import DurationField from '@/components/DurationField.vue'
import NumberStepper from '@/components/NumberStepper.vue'
import SetupPreview, { type PreviewFocus } from '@/components/setup/SetupPreview.vue'
import { Button } from '@/components/ui/button'
import { Checkbox } from '@/components/ui/checkbox'
import { Input } from '@/components/ui/input'
import {
  ApiError,
  containersApi,
  frameworksApi,
  locationsApi,
  shiftTypesApi,
  type FrameworkResponse,
  type LocationTypeResponse,
  type ShiftTypeResponse,
} from '@/api'
import { useSeasonStore } from '@/stores/season'
import { useLocationsStore } from '@/stores/locations'
import { useErrors } from '@/composables/useErrors'
import {
  addDays,
  dayKey,
  formatDayShort,
  inZone,
  instantAt,
  minutesToClock,
  minutesToDuration,
  timeZone,
  weekStart,
} from '@/domain/time'
import { cn } from '@/lib/utils'

const { t } = useI18n()
const season = useSeasonStore()
const locationsStore = useLocationsStore()
const { report, messageOf } = useErrors()

const STORAGE_KEY = 'setup-wizard-draft'
interface Step {
  label: string
  question: string
  term?: string
  icon: Component
  focus: PreviewFocus
}
const steps = computed<Step[]>(() => [
  {
    label: t('wizard_step_types'),
    question: t('wizard_step_types_q'),
    term: t('nav_shift_types'),
    icon: Tag,
    focus: 'seat',
  },
  {
    label: t('wizard_step_framework'),
    question: t('wizard_step_framework_q'),
    term: t('nav_frameworks'),
    icon: Layers,
    focus: 'shift',
  },
  {
    label: t('wizard_step_location'),
    question: t('wizard_step_location_q'),
    icon: MapPin,
    focus: 'location',
  },
  {
    label: t('wizard_step_containers'),
    question: t('wizard_step_containers_q'),
    term: t('admin_location_containers'),
    icon: CalendarDays,
    focus: 'container',
  },
  {
    label: t('wizard_step_summary'),
    question: t('wizard_step_summary_q'),
    icon: ClipboardCheck,
    focus: null,
  },
])
const LAST = 4
const step = ref(0)
const current = computed(() => steps.value[step.value]!)

interface NewType {
  key: string
  name: string
  color: string
  offset: number
  adminOnly: boolean
  description: string
  createdId?: string
}
interface Draft {
  selectedTypeIds: string[]
  newTypes: NewType[]
  /** Seats per shift, keyed by existing type id or NewType.key. */
  counts: Record<string, number>
  existingFrameworkId: string
  frameworkName: string
  frameworkMinutes: number
  createdFrameworkId?: string
  locationName: string
  /** Existing location type id, or NEW_LOCATION_TYPE to create one. */
  locationTypeId: string
  newLocationTypeName: string
  newLocationTypeAdminOnly: boolean
  createdLocationTypeId?: string
  createdLocationId?: string
  days: string[]
  startMinutes: number
  totalShifts: number
  color: string
  createdContainerDays: string[]
}
const draft = ref<Draft>(emptyDraft())
function emptyDraft(): Draft {
  return {
    selectedTypeIds: [],
    newTypes: [],
    counts: {},
    existingFrameworkId: '',
    frameworkName: '',
    frameworkMinutes: 90,
    locationName: '',
    locationTypeId: '',
    newLocationTypeName: '',
    newLocationTypeAdminOnly: false,
    days: [],
    startMinutes: 8 * 60,
    totalShifts: 4,
    color: 'rgba(59, 130, 246, 0.30)',
    createdContainerDays: [],
  }
}
let keySeq = 0
const newKey = () => `new-${Date.now().toString(36)}-${keySeq++}`

const existingTypes = ref<ShiftTypeResponse[]>([])
const existingFrameworks = ref<FrameworkResponse[]>([])
const locationTypes = ref<LocationTypeResponse[]>([])

onMounted(async () => {
  try {
    const raw = sessionStorage.getItem(STORAGE_KEY)
    if (raw) {
      const saved = { ...emptyDraft(), ...(JSON.parse(raw) as Draft) }
      // drafts from before stable keys: give every new type one
      for (const nt of saved.newTypes) nt.key ||= newKey()
      draft.value = saved
    }
  } catch {
    /* ignore */
  }
  await loadLists()
})
watch(draft, (d) => sessionStorage.setItem(STORAGE_KEY, JSON.stringify(d)), { deep: true })

async function loadLists(): Promise<void> {
  if (!season.currentId) return
  try {
    ;[existingTypes.value, existingFrameworks.value, locationTypes.value] = await Promise.all([
      shiftTypesApi.list(season.currentId),
      frameworksApi.list(season.currentId),
      locationsApi.types(),
    ])
    if (!draft.value.locationTypeId) draft.value.locationTypeId = defaultLocationTypeId()
  } catch (error) {
    await report(error)
  }
}

/* step 1: tasks (shift types) */
const PALETTE = ['#e11d48', '#2563eb', '#16a34a', '#d97706', '#7c3aed', '#0891b2', '#db2777']
function toggleType(id: string, on: boolean): void {
  const set = new Set(draft.value.selectedTypeIds)
  if (on) set.add(id)
  else set.delete(id)
  draft.value.selectedTypeIds = [...set]
}
function addNewType(name = ''): void {
  const used = existingTypes.value.length + draft.value.newTypes.length
  draft.value.newTypes.push({
    key: newKey(),
    name,
    color: PALETTE[used % PALETTE.length]!,
    offset: 0,
    adminOnly: false,
    description: '',
  })
}
function removeNewType(i: number): void {
  const [removed] = draft.value.newTypes.splice(i, 1)
  if (removed) delete draft.value.counts[removed.key]
}
const suggestions = computed(() => {
  const taken = new Set(
    [...existingTypes.value, ...draft.value.newTypes].map((x) => x.name.trim().toLowerCase()),
  )
  return t('wizard_types_suggestion_list')
    .split(',')
    .filter((s) => !taken.has(s.toLowerCase()))
})
const chosenTypes = computed(() => [
  ...existingTypes.value
    .filter((x) => draft.value.selectedTypeIds.includes(x.id))
    .map((x) => ({ key: x.id, name: x.name, color: x.color, isNew: false })),
  ...draft.value.newTypes
    .filter((x) => x.name.trim())
    .map((x) => ({ key: x.key, name: x.name.trim(), color: x.color, isNew: true })),
])

/* step 2: shift pattern (framework) */
const DURATION_PRESETS = [60, 90, 120, 180, 240]
const useExistingFramework = computed(() => !!draft.value.existingFrameworkId)
const selectedFramework = computed(() =>
  existingFrameworks.value.find((x) => x.id === draft.value.existingFrameworkId),
)
const seats = computed(() =>
  selectedFramework.value
    ? selectedFramework.value.shiftTypeCounts.map((c) => ({
        key: c.shiftType.id,
        name: c.shiftType.name,
        color: c.shiftType.color,
        count: c.count,
      }))
    : chosenTypes.value.map((x) => ({ ...x, count: draft.value.counts[x.key] ?? 1 })),
)
const slotCapacity = computed(() => seats.value.reduce((s, x) => s + x.count, 0))
const frameworkMinutes = computed(() =>
  selectedFramework.value
    ? selectedFramework.value.secondsPerShift / 60
    : draft.value.frameworkMinutes,
)
const suggestedFrameworkName = computed(() => {
  const names = chosenTypes.value.map((x) => x.name)
  const head = names.length > 3 ? `${names.slice(0, 3).join(', ')} …` : names.join(' & ')
  return `${head || t('wizard_step_framework')} · ${minutesToClock(draft.value.frameworkMinutes)} h`
})
const frameworkName = computed(() =>
  selectedFramework.value
    ? selectedFramework.value.name
    : draft.value.frameworkName.trim() || suggestedFrameworkName.value,
)
function frameworkSummary(f: FrameworkResponse): string {
  return f.shiftTypeCounts.map((c) => `${c.count}× ${c.shiftType.name}`).join(', ')
}

/* step 3: location */
const NEW_LOCATION_TYPE = '__new__'
const defaultLocationTypeId = () => locationTypes.value[0]?.id ?? NEW_LOCATION_TYPE
const isNewLocationType = computed(() => draft.value.locationTypeId === NEW_LOCATION_TYPE)
const locationTypeName = computed(() =>
  isNewLocationType.value
    ? draft.value.newLocationTypeName.trim()
    : locationTypes.value.find((x) => x.id === draft.value.locationTypeId)?.name,
)

/* step 4: days and times (containers) */
const seasonDays = computed(() => {
  if (!season.current) return []
  const start = dayKey(season.current.startDate)
  const end = dayKey(season.current.endDate)
  const out: string[] = []
  for (let d = start, i = 0; d <= end && i < 120; d = addDays(d, 1), i++) out.push(d)
  return out
})
/** Season days laid out Monday-first; days outside the season are null. */
const calendarWeeks = computed(() => {
  const days = seasonDays.value
  if (!days.length) return []
  const inSeason = new Set(days)
  const last = days[days.length - 1]!
  const weeks: (string | null)[][] = []
  for (let d = weekStart(days[0]!); d <= last;) {
    const week: (string | null)[] = []
    for (let i = 0; i < 7; i++, d = addDays(d, 1)) week.push(inSeason.has(d) ? d : null)
    weeks.push(week)
  }
  return weeks
})
const weekdays = computed(() => t('wizard_weekdays').split(','))
const monthFormat = new Intl.DateTimeFormat('de', { month: 'short', timeZone: timeZone() })
const weekdayOf = (d: string) => (inZone(instantAt(d, 720)).getDay() + 6) % 7
function toggleDay(d: string): void {
  const set = new Set(draft.value.days)
  if (set.has(d)) set.delete(d)
  else set.add(d)
  draft.value.days = [...set].sort()
}
function selectDays(which: 'all' | 'weekend' | 'none'): void {
  draft.value.days =
    which === 'all'
      ? [...seasonDays.value]
      : which === 'weekend'
        ? seasonDays.value.filter((d) => weekdayOf(d) >= 4)
        : []
}
const BLOCK_COLORS = [
  'rgba(59, 130, 246, 0.30)',
  'rgba(34, 197, 94, 0.30)',
  'rgba(245, 158, 11, 0.30)',
  'rgba(236, 72, 153, 0.30)',
  'rgba(139, 92, 246, 0.30)',
  'rgba(20, 184, 166, 0.30)',
]
const blockMinutes = computed(() => frameworkMinutes.value * draft.value.totalShifts)
const blockEnd = computed(() => minutesToClock(draft.value.startMinutes + blockMinutes.value))
const timeline = computed(() =>
  Array.from({ length: draft.value.totalShifts }, (_, i) =>
    minutesToClock(draft.value.startMinutes + i * frameworkMinutes.value),
  ),
)
const totalCapacity = computed(
  () => draft.value.days.length * draft.value.totalShifts * slotCapacity.value,
)

/* validation per step */
function errorFor(i: number): string {
  const d = draft.value
  switch (i) {
    case 0:
      return chosenTypes.value.length ? '' : t('wizard_types_required')
    case 2:
      return d.locationName.trim() && locationTypeName.value ? '' : t('wizard_location_required')
    case 3:
      return d.days.length ? '' : t('wizard_days_required')
    default:
      return ''
  }
}
const stepError = computed(() => errorFor(step.value))
const canReach = (i: number) => Array.from({ length: i }, (_, j) => errorFor(j)).every((e) => !e)

/* run */
type Phase = 'types' | 'framework' | 'location' | 'containers'
const running = ref(false)
const phase = ref<Phase | null>(null)
const progress = ref('')
const failure = ref('')
const done = ref(false)

async function runAll(): Promise<void> {
  if (!season.currentId) return
  running.value = true
  failure.value = ''
  const d = draft.value
  const seasonId = season.currentId
  try {
    phase.value = 'types'
    progress.value = t('wizard_progress_types')
    for (const nt of d.newTypes) {
      if (!nt.name.trim() || nt.createdId) continue
      const created = await shiftTypesApi.create({
        name: nt.name.trim(),
        color: nt.color,
        startingTimeShift: minutesToDuration(nt.offset),
        onlyAssignableByAdmin: nt.adminOnly,
        seasonId,
        description: nt.description.trim() || null,
      })
      nt.createdId = created.id
    }

    phase.value = 'framework'
    progress.value = t('wizard_progress_framework')
    let frameworkId = d.existingFrameworkId || d.createdFrameworkId
    if (!frameworkId) {
      const typeCounts = [
        ...d.selectedTypeIds.map((id) => ({ shiftTypeId: id, count: d.counts[id] ?? 1 })),
        ...d.newTypes
          .filter((x) => x.createdId)
          .map((x) => ({ shiftTypeId: x.createdId!, count: d.counts[x.key] ?? 1 })),
      ]
      const fw = await frameworksApi.create({
        name: frameworkName.value,
        secondsPerShift: d.frameworkMinutes * 60,
        seasonId,
        typeCounts,
      })
      frameworkId = fw.id
      d.createdFrameworkId = fw.id
    }

    phase.value = 'location'
    progress.value = t('wizard_progress_location')
    let locationId = d.createdLocationId
    if (!locationId) {
      let typeId = d.locationTypeId
      if (typeId === NEW_LOCATION_TYPE) {
        typeId = d.createdLocationTypeId ?? ''
        if (!typeId) {
          const type = await locationsApi.createType({
            name: d.newLocationTypeName.trim(),
            adminOnly: d.newLocationTypeAdminOnly,
          })
          typeId = type.id
          d.createdLocationTypeId = type.id
        }
      }
      const loc = await locationsApi.create({
        name: d.locationName.trim(),
        typeId,
        seasonId,
      })
      locationId = loc.id
      d.createdLocationId = loc.id
    }

    phase.value = 'containers'
    const pending = d.days.filter((x) => !d.createdContainerDays.includes(x))
    for (const [i, day] of pending.entries()) {
      progress.value = t('wizard_progress_containers', { done: i + 1, total: pending.length })
      await containersApi.create({
        frameworkId,
        locationId,
        start: instantAt(day, d.startMinutes).toISOString(),
        totalShifts: d.totalShifts,
        color: d.color,
      })
      d.createdContainerDays.push(day)
    }

    phase.value = null
    done.value = true
    toast.success(t('wizard_done'))
    if (season.currentId) await locationsStore.load(season.currentId).catch(() => undefined)
  } catch (error) {
    failure.value =
      t('wizard_failed', { step: progress.value }) +
      ' ' +
      (error instanceof ApiError ? messageOf(error) : '')
    if (!(error instanceof ApiError)) await report(error)
  } finally {
    running.value = false
  }
}

type ItemState = 'existing' | 'todo' | 'running' | 'done'
const planItems = computed(() => {
  const d = draft.value
  const state = (p: Phase, isDone: boolean, existing = false): ItemState =>
    existing
      ? 'existing'
      : isDone
        ? 'done'
        : running.value && phase.value === p
          ? 'running'
          : 'todo'
  const newNamed = d.newTypes.filter((x) => x.name.trim())
  return [
    {
      icon: Tag,
      label: t('wizard_step_types'),
      detail: chosenTypes.value.map((x) => x.name).join(', '),
      state: state(
        'types',
        newNamed.length > 0 && newNamed.every((x) => x.createdId),
        !newNamed.length,
      ),
    },
    {
      icon: Layers,
      label: t('wizard_step_framework'),
      // the suggested name already ends in the duration
      detail: frameworkName.value.includes(minutesToClock(frameworkMinutes.value))
        ? frameworkName.value
        : `${frameworkName.value} · ${minutesToClock(frameworkMinutes.value)} h`,
      state: state('framework', !!d.createdFrameworkId, useExistingFramework.value),
    },
    {
      icon: MapPin,
      label: t('wizard_step_location'),
      detail: [d.locationName.trim(), locationTypeName.value].filter(Boolean).join(' · '),
      state: state('location', !!d.createdLocationId),
    },
    {
      icon: CalendarDays,
      label: t('wizard_plan_containers', d.days.length),
      detail:
        running.value && phase.value === 'containers'
          ? progress.value
          : d.days.map((x) => formatDayShort(instantAt(x, 720))).join(', '),
      state: state(
        'containers',
        d.days.length > 0 && d.days.every((x) => d.createdContainerDays.includes(x)),
      ),
    },
  ]
})

function reset(): void {
  draft.value = emptyDraft()
  sessionStorage.removeItem(STORAGE_KEY)
  done.value = false
  failure.value = ''
  step.value = 0
  draft.value.locationTypeId = defaultLocationTypeId()
}

const choiceClass = (on: boolean) =>
  cn(
    'flex w-full items-start gap-3 rounded-lg border p-3 text-left transition-colors',
    on
      ? 'border-primary bg-primary/10'
      : 'border-border hover:border-muted-foreground/40 hover:bg-accent/50',
  )
const chipClass = (on: boolean) =>
  cn(
    'rounded-full border px-3 py-1 text-xs font-medium tabular-nums transition-colors',
    on
      ? 'border-primary bg-primary text-primary-foreground'
      : 'border-border text-muted-foreground hover:bg-accent hover:text-foreground',
  )
</script>

<template>
  <div class="mx-auto max-w-6xl">
    <PageHeader :title="t('wizard_title')" :subtitle="t('wizard_subtitle')" />

    <!-- stepper -->
    <nav class="mb-6" :aria-label="t('wizard_title')">
      <ol class="grid grid-cols-5 gap-1.5 sm:gap-2">
        <li v-for="(s, i) in steps" :key="i">
          <button
            type="button"
            :disabled="running || done || !canReach(i)"
            :aria-current="i === step ? 'step' : undefined"
            :class="
              cn(
                'group flex w-full flex-col gap-2 rounded-lg text-left disabled:cursor-not-allowed',
                i !== step && canReach(i) && !running && !done && 'cursor-pointer',
              )
            "
            @click="step = i"
          >
            <span
              :class="
                cn(
                  'h-1 w-full rounded-full transition-colors',
                  i <= step
                    ? 'bg-primary'
                    : 'bg-border group-enabled:group-hover:bg-muted-foreground/40',
                )
              "
            />
            <span class="flex items-center gap-2">
              <span
                :class="
                  cn(
                    'flex size-6 shrink-0 items-center justify-center rounded-full text-xs font-semibold transition-colors',
                    i === step
                      ? 'bg-primary text-primary-foreground'
                      : i < step
                        ? 'bg-primary/15 text-primary'
                        : 'bg-surface-raised text-muted-foreground',
                  )
                "
              >
                <Check v-if="i < step" class="size-3.5" />
                <template v-else>{{ i + 1 }}</template>
              </span>
              <span
                :class="
                  cn(
                    'hidden truncate text-sm font-medium sm:block',
                    i === step ? 'text-foreground' : 'text-muted-foreground',
                  )
                "
                >{{ s.label }}</span
              >
            </span>
          </button>
        </li>
      </ol>
    </nav>

    <div class="grid items-start gap-6 lg:grid-cols-[minmax(0,1fr)_19rem]">
      <div class="rounded-xl border border-border bg-card">
        <!-- step heading -->
        <header class="border-b border-border p-5 sm:p-6">
          <p class="eyebrow flex items-center gap-2">
            <component :is="current.icon" class="size-3.5" />
            {{ t('wizard_step_of', { n: step + 1, total: steps.length }) }} · {{ current.label }}
          </p>
          <h2 class="mt-2 text-xl font-semibold sm:text-2xl">{{ current.question }}</h2>
          <p v-if="step === 0" class="mt-2 max-w-2xl text-sm text-muted-foreground">
            {{ t('wizard_types_intro') }}
          </p>
          <p v-else-if="step === 1" class="mt-2 max-w-2xl text-sm text-muted-foreground">
            {{ t('wizard_framework_intro') }}
          </p>
          <p v-else-if="step === 2" class="mt-2 max-w-2xl text-sm text-muted-foreground">
            {{ t('wizard_location_intro') }}
          </p>
          <p v-else-if="step === 3" class="mt-2 max-w-2xl text-sm text-muted-foreground">
            {{ t('wizard_containers_intro') }}
          </p>
          <p
            v-if="current.term"
            class="mt-3 inline-flex items-center gap-1.5 rounded-full bg-surface-raised px-2.5 py-1 text-xs text-muted-foreground"
          >
            <Info class="size-3.5" /> {{ t('wizard_term', { term: current.term }) }}
          </p>
        </header>

        <div class="space-y-6 p-5 sm:p-6">
          <!-- 1 tasks -->
          <template v-if="step === 0">
            <section class="space-y-3">
              <h3 class="text-sm font-semibold">{{ t('wizard_types_existing') }}</h3>
              <div v-if="existingTypes.length" class="grid gap-2 sm:grid-cols-2">
                <label
                  v-for="x in existingTypes"
                  :key="x.id"
                  :class="cn(choiceClass(draft.selectedTypeIds.includes(x.id)), 'cursor-pointer')"
                >
                  <Checkbox
                    class="mt-0.5"
                    :model-value="draft.selectedTypeIds.includes(x.id)"
                    @update:model-value="toggleType(x.id, $event === true)"
                  />
                  <span class="min-w-0 flex-1">
                    <span class="flex items-center gap-2 text-sm font-medium">
                      <span class="size-3 shrink-0 rounded-full" :style="{ background: x.color }" />
                      <span class="truncate">{{ x.name }}</span>
                      <Lock
                        v-if="x.onlyAssignableByAdmin"
                        class="size-3.5 shrink-0 text-muted-foreground"
                        :aria-label="t('admin_shift_type_admin_only')"
                      />
                    </span>
                    <span
                      v-if="x.description"
                      class="mt-0.5 line-clamp-2 block text-xs text-muted-foreground"
                      >{{ x.description }}</span
                    >
                  </span>
                </label>
              </div>
              <p
                v-else
                class="rounded-lg border border-dashed border-border px-3 py-4 text-sm text-muted-foreground"
              >
                {{ t('wizard_types_existing_empty') }}
              </p>
            </section>

            <section class="space-y-3">
              <h3 class="text-sm font-semibold">{{ t('wizard_types_new') }}</h3>
              <div
                v-for="(nt, i) in draft.newTypes"
                :key="nt.key"
                class="rounded-lg border border-border bg-background/40 p-3"
              >
                <div class="flex flex-wrap items-end gap-3">
                  <FormField class="min-w-48 flex-1" :label="t('admin_shift_type_name')">
                    <Input
                      v-model="nt.name"
                      :placeholder="t('wizard_types_name_placeholder')"
                      :disabled="!!nt.createdId"
                    />
                  </FormField>
                  <FormField :label="t('admin_shift_type_color')"
                    ><ColorField v-model="nt.color"
                  /></FormField>
                  <Button
                    type="button"
                    variant="ghost"
                    size="icon"
                    :disabled="!!nt.createdId"
                    :aria-label="t('wizard_types_remove')"
                    :title="t('wizard_types_remove')"
                    @click="removeNewType(i)"
                    ><Trash2
                  /></Button>
                </div>
                <details class="group mt-3">
                  <summary
                    class="cursor-pointer list-none text-xs font-medium text-muted-foreground select-none hover:text-foreground [&::-webkit-details-marker]:hidden"
                  >
                    <span class="inline-block transition-transform group-open:rotate-90">›</span>
                    {{ t('wizard_types_more') }}
                  </summary>
                  <div class="mt-3 grid gap-4 md:grid-cols-2">
                    <FormField class="md:col-span-2" :label="t('admin_shift_type_description')">
                      <Input
                        v-model="nt.description"
                        :placeholder="t('wizard_types_description_placeholder')"
                      />
                    </FormField>
                    <FormField
                      :label="t('admin_shift_type_offset')"
                      :hint="t('admin_shift_type_offset_hint')"
                      ><Input
                        v-model.number="nt.offset"
                        type="number"
                        min="-180"
                        max="180"
                        step="5"
                        class="w-28"
                    /></FormField>
                    <div class="space-y-1">
                      <label class="flex items-center gap-2 pt-6 text-sm font-medium">
                        <Checkbox
                          :model-value="nt.adminOnly"
                          @update:model-value="nt.adminOnly = $event === true"
                        />{{ t('admin_shift_type_admin_only') }}
                      </label>
                      <p class="text-xs text-muted-foreground">
                        {{ t('wizard_types_admin_only_hint') }}
                      </p>
                    </div>
                  </div>
                </details>
              </div>

              <div class="flex flex-wrap items-center gap-2">
                <Button type="button" variant="outline" size="sm" @click="addNewType()"
                  ><Plus /> {{ t('wizard_types_add') }}</Button
                >
                <template v-if="suggestions.length">
                  <span class="ml-1 text-xs text-muted-foreground"
                    >{{ t('wizard_types_suggestions') }}:</span
                  >
                  <button
                    v-for="s in suggestions"
                    :key="s"
                    type="button"
                    :class="chipClass(false)"
                    @click="addNewType(s)"
                  >
                    + {{ s }}
                  </button>
                </template>
              </div>
            </section>
          </template>

          <!-- 2 shift pattern -->
          <template v-else-if="step === 1">
            <div class="grid gap-2 sm:grid-cols-2">
              <button
                type="button"
                :class="choiceClass(!useExistingFramework)"
                @click="draft.existingFrameworkId = ''"
              >
                <Sparkles class="mt-0.5 size-4 shrink-0 text-primary" />
                <span>
                  <span class="block text-sm font-medium">{{ t('wizard_framework_new') }}</span>
                  <span class="block text-xs text-muted-foreground">{{
                    t('wizard_framework_new_hint')
                  }}</span>
                </span>
              </button>
              <button
                v-for="f in existingFrameworks"
                :key="f.id"
                type="button"
                :class="choiceClass(draft.existingFrameworkId === f.id)"
                @click="draft.existingFrameworkId = f.id"
              >
                <Layers class="mt-0.5 size-4 shrink-0 text-muted-foreground" />
                <span class="min-w-0">
                  <span class="block truncate text-sm font-medium">{{ f.name }}</span>
                  <span class="block text-xs text-muted-foreground"
                    >{{ minutesToClock(f.secondsPerShift / 60) }} h ·
                    {{ frameworkSummary(f) }}</span
                  >
                </span>
              </button>
            </div>

            <p
              v-if="useExistingFramework"
              class="flex items-start gap-2 text-sm text-muted-foreground"
            >
              <Info class="mt-0.5 size-4 shrink-0" /> {{ t('wizard_framework_existing_hint') }}
            </p>

            <template v-else>
              <section class="space-y-2">
                <h3 class="text-sm font-semibold">{{ t('wizard_framework_duration') }}</h3>
                <div class="flex flex-wrap items-center gap-2">
                  <button
                    v-for="m in DURATION_PRESETS"
                    :key="m"
                    type="button"
                    :class="chipClass(draft.frameworkMinutes === m)"
                    @click="draft.frameworkMinutes = m"
                  >
                    {{ minutesToClock(m) }} h
                  </button>
                  <DurationField v-model="draft.frameworkMinutes" :step="5" :min="5" />
                </div>
                <p class="text-xs text-muted-foreground">
                  {{ t('wizard_framework_duration_hint') }}
                </p>
              </section>

              <section class="space-y-2">
                <h3 class="text-sm font-semibold">{{ t('wizard_framework_counts') }}</h3>
                <ul class="divide-y divide-border rounded-lg border border-border">
                  <li
                    v-for="x in chosenTypes"
                    :key="x.key"
                    class="flex items-center gap-3 px-3 py-2"
                  >
                    <span class="size-3 shrink-0 rounded-full" :style="{ background: x.color }" />
                    <span class="flex-1 truncate text-sm">{{ x.name }}</span>
                    <NumberStepper
                      :model-value="draft.counts[x.key] ?? 1"
                      :min="1"
                      :max="99"
                      @update:model-value="draft.counts[x.key] = $event"
                    />
                  </li>
                </ul>
                <p class="text-xs text-muted-foreground">
                  {{ t('wizard_framework_counts_hint') }}
                  <strong class="font-semibold text-foreground">{{
                    t('wizard_per_shift', { people: t('wizard_people', slotCapacity) })
                  }}</strong>
                </p>
              </section>

              <FormField
                :label="t('wizard_framework_name')"
                :hint="t('wizard_framework_name_hint')"
              >
                <Input v-model="draft.frameworkName" :placeholder="suggestedFrameworkName" />
              </FormField>
            </template>
          </template>

          <!-- 3 location -->
          <template v-else-if="step === 2">
            <FormField :label="t('wizard_location_name')">
              <Input
                v-model="draft.locationName"
                class="h-11 text-base"
                :placeholder="t('wizard_location_name_placeholder')"
              />
            </FormField>
            <section class="space-y-2">
              <h3 class="text-sm font-semibold">{{ t('wizard_location_type') }}</h3>
              <div class="flex flex-wrap gap-2">
                <button
                  v-for="x in locationTypes"
                  :key="x.id"
                  type="button"
                  :class="
                    cn(
                      chipClass(draft.locationTypeId === x.id),
                      'inline-flex items-center gap-1.5 px-3.5 py-1.5 text-sm',
                    )
                  "
                  @click="draft.locationTypeId = x.id"
                >
                  <Lock v-if="x.adminOnly" class="size-3.5" />{{ x.name }}
                </button>
                <button
                  type="button"
                  :class="
                    cn(
                      chipClass(isNewLocationType),
                      'inline-flex items-center gap-1.5 border-dashed px-3.5 py-1.5 text-sm',
                    )
                  "
                  @click="draft.locationTypeId = NEW_LOCATION_TYPE"
                >
                  <Plus class="size-3.5" />{{ t('wizard_location_type_new') }}
                </button>
              </div>
              <div
                v-if="isNewLocationType"
                class="flex flex-wrap items-end gap-4 rounded-lg border border-border bg-background/40 p-3"
              >
                <FormField class="min-w-48 flex-1" :label="t('wizard_location_type_name')">
                  <Input
                    v-model="draft.newLocationTypeName"
                    :placeholder="t('wizard_location_type_placeholder')"
                    :disabled="!!draft.createdLocationTypeId"
                  />
                </FormField>
                <label class="flex items-center gap-2 pb-2 text-sm font-medium">
                  <Checkbox
                    :model-value="draft.newLocationTypeAdminOnly"
                    :disabled="!!draft.createdLocationTypeId"
                    @update:model-value="draft.newLocationTypeAdminOnly = $event === true"
                  /><Lock class="size-3.5 text-muted-foreground" />
                  {{ t('admin_location_type_admin_only') }}
                </label>
              </div>
              <p class="text-xs text-muted-foreground">{{ t('wizard_location_type_hint') }}</p>
            </section>
          </template>

          <!-- 4 days and times -->
          <template v-else-if="step === 3">
            <section class="space-y-3">
              <div class="flex flex-wrap items-center justify-between gap-2">
                <h3 class="text-sm font-semibold">{{ t('wizard_days') }}</h3>
                <div class="flex gap-1.5">
                  <button type="button" :class="chipClass(false)" @click="selectDays('all')">
                    {{ t('wizard_days_all') }}
                  </button>
                  <button type="button" :class="chipClass(false)" @click="selectDays('weekend')">
                    {{ t('wizard_days_weekend') }}
                  </button>
                  <button type="button" :class="chipClass(false)" @click="selectDays('none')">
                    {{ t('wizard_days_none') }}
                  </button>
                </div>
              </div>
              <div class="max-w-md">
                <div
                  class="grid grid-cols-7 gap-1 pb-1 text-center text-[0.6875rem] font-medium text-muted-foreground"
                >
                  <span v-for="w in weekdays" :key="w">{{ w }}</span>
                </div>
                <div
                  v-for="(week, wi) in calendarWeeks"
                  :key="wi"
                  class="grid grid-cols-7 gap-1 pb-1"
                >
                  <template v-for="(d, di) in week" :key="d ?? `${wi}-${di}`">
                    <button
                      v-if="d"
                      type="button"
                      :aria-pressed="draft.days.includes(d)"
                      :class="
                        cn(
                          'flex aspect-square flex-col items-center justify-center rounded-lg border text-sm font-semibold tabular-nums transition-colors',
                          draft.days.includes(d)
                            ? 'border-primary bg-primary text-primary-foreground'
                            : 'border-border hover:bg-accent',
                        )
                      "
                      @click="toggleDay(d)"
                    >
                      {{ Number(d.slice(8)) }}
                      <span class="text-[0.625rem] font-normal opacity-70">{{
                        monthFormat.format(instantAt(d, 720))
                      }}</span>
                    </button>
                    <span v-else />
                  </template>
                </div>
              </div>
              <p class="text-xs text-muted-foreground">
                {{ t('wizard_days_selected', draft.days.length) }}
              </p>
            </section>

            <section class="grid gap-4 sm:grid-cols-2">
              <FormField :label="t('wizard_start')">
                <Input
                  type="time"
                  step="900"
                  class="w-32"
                  :model-value="minutesToClock(draft.startMinutes)"
                  @update:model-value="
                    draft.startMinutes =
                      Number(String($event).split(':')[0]) * 60 +
                      Number(String($event).split(':')[1] ?? 0)
                  "
                />
              </FormField>
              <FormField :label="t('wizard_shift_count')">
                <NumberStepper v-model="draft.totalShifts" :min="1" :max="48" />
              </FormField>
              <FormField
                class="sm:col-span-2"
                :label="t('wizard_block_color')"
                :hint="t('admin_container_color_hint')"
              >
                <div class="flex flex-wrap items-center gap-3">
                  <div class="flex gap-1.5">
                    <button
                      v-for="c in BLOCK_COLORS"
                      :key="c"
                      type="button"
                      :aria-pressed="draft.color === c"
                      :class="
                        cn(
                          'size-8 rounded-full border border-white/20 transition-shadow',
                          draft.color === c && 'ring-2 ring-primary ring-offset-2 ring-offset-card',
                        )
                      "
                      :style="{ background: c }"
                      @click="draft.color = c"
                    />
                  </div>
                  <ColorField v-model="draft.color" alpha />
                </div>
              </FormField>
            </section>

            <!-- timeline of one block -->
            <section class="space-y-2">
              <div
                class="flex overflow-hidden rounded-lg border border-white/10 p-1"
                :style="{ background: draft.color }"
              >
                <div
                  v-for="(start, i) in timeline"
                  :key="i"
                  class="min-w-0 flex-1 border-r border-background/40 bg-background/70 px-1.5 py-2 text-center text-[0.6875rem] font-medium tabular-nums last:border-r-0 first:rounded-l-md last:rounded-r-md"
                  :title="start"
                >
                  <span v-if="draft.totalShifts <= 8">{{ start }}</span>
                </div>
              </div>
              <p class="text-sm">
                {{
                  t('wizard_block_result', {
                    start: minutesToClock(draft.startMinutes),
                    end: blockEnd,
                    duration: minutesToClock(blockMinutes),
                  })
                }}
              </p>
            </section>
          </template>

          <!-- 5 review -->
          <template v-else>
            <p class="text-base leading-relaxed">
              <i18n-t keypath="wizard_summary_sentence" scope="global">
                <template #location
                  ><strong>{{ draft.locationName.trim() }}</strong></template
                >
                <template #days
                  ><strong>{{ t('wizard_summary_days', draft.days.length) }}</strong></template
                >
                <template #shifts
                  ><strong>{{ t('wizard_summary_shifts', draft.totalShifts) }}</strong></template
                >
                <template #start
                  ><strong>{{ minutesToClock(draft.startMinutes) }}</strong></template
                >
                <template #end
                  ><strong>{{ blockEnd }}</strong></template
                >
                <template #people
                  ><strong>{{
                    seats.map((s) => `${s.count}× ${s.name}`).join(', ')
                  }}</strong></template
                >
              </i18n-t>
            </p>

            <div
              class="flex items-baseline gap-3 rounded-lg border border-primary/30 bg-primary/10 px-4 py-3"
            >
              <span class="font-display text-3xl font-semibold tabular-nums text-primary">{{
                totalCapacity
              }}</span>
              <span class="text-sm">{{ t('wizard_capacity', totalCapacity) }}</span>
            </div>

            <section class="space-y-2">
              <h3 class="text-sm font-semibold">{{ t('wizard_plan_title') }}</h3>
              <ul class="divide-y divide-border rounded-lg border border-border">
                <li v-for="item in planItems" :key="item.label" class="flex items-start gap-3 p-3">
                  <component :is="item.icon" class="mt-0.5 size-4 shrink-0 text-muted-foreground" />
                  <div class="min-w-0 flex-1">
                    <p class="flex items-center gap-2 text-sm font-medium">
                      {{ item.label }}
                      <span
                        :class="
                          cn(
                            'rounded-full px-1.5 py-px text-[0.625rem] font-semibold tracking-wide uppercase',
                            item.state === 'existing'
                              ? 'bg-surface-raised text-muted-foreground'
                              : 'bg-primary/15 text-primary',
                          )
                        "
                        >{{
                          item.state === 'existing'
                            ? t('wizard_badge_existing')
                            : t('wizard_badge_new')
                        }}</span
                      >
                    </p>
                    <p class="truncate text-xs text-muted-foreground">{{ item.detail }}</p>
                  </div>
                  <LoaderCircle
                    v-if="item.state === 'running'"
                    class="size-4 shrink-0 animate-spin text-primary"
                  />
                  <CircleCheck
                    v-else-if="item.state === 'done'"
                    class="size-4 shrink-0 text-status-green"
                  />
                </li>
              </ul>
            </section>

            <p
              v-if="failure"
              class="rounded-lg border border-destructive/50 bg-destructive/10 p-3 text-sm"
            >
              {{ failure }}
            </p>
            <div
              v-if="done"
              class="space-y-3 rounded-lg border border-status-green/50 bg-status-green/10 p-4"
            >
              <div class="flex items-start gap-3">
                <CircleCheck class="mt-0.5 size-5 shrink-0 text-status-green" />
                <div>
                  <p class="font-semibold">{{ t('wizard_done') }}</p>
                  <p class="text-sm text-muted-foreground">{{ t('wizard_done_hint') }}</p>
                </div>
              </div>
              <div class="flex flex-wrap gap-2">
                <Button size="sm" as-child
                  ><RouterLink :to="`/locations/${draft.createdLocationId}`">{{
                    t('wizard_open_location')
                  }}</RouterLink></Button
                >
                <Button size="sm" variant="outline" as-child
                  ><RouterLink :to="`/admin/locations/${draft.createdLocationId}/containers`">{{
                    t('wizard_open_admin')
                  }}</RouterLink></Button
                >
                <Button size="sm" variant="ghost" @click="reset"
                  ><Plus /> {{ t('wizard_restart') }}</Button
                >
              </div>
            </div>
          </template>
        </div>

        <!-- navigation -->
        <footer
          v-if="!done"
          class="flex flex-wrap items-center justify-between gap-3 border-t border-border px-5 py-4 sm:px-6"
        >
          <Button variant="ghost" :disabled="step === 0 || running" @click="step--"
            ><ArrowLeft /> {{ t('btn_back') }}</Button
          >
          <div class="flex flex-wrap items-center justify-end gap-3">
            <p v-if="stepError" class="flex items-center gap-1.5 text-xs text-muted-foreground">
              <Info class="size-3.5 shrink-0" /> {{ stepError }}
            </p>
            <Button v-if="step < LAST" :disabled="!!stepError" @click="step++"
              >{{ t('btn_next') }} <ArrowRight
            /></Button>
            <Button v-else size="lg" :disabled="running" @click="runAll">
              <LoaderCircle v-if="running" class="animate-spin" />
              <Sparkles v-else />
              {{ running ? t('wizard_running') : failure ? t('btn_retry') : t('wizard_run') }}
            </Button>
          </div>
        </footer>
      </div>

      <aside class="space-y-3 lg:sticky lg:top-4">
        <SetupPreview
          :location-name="draft.locationName"
          :location-type-name="locationTypeName"
          :days="draft.days"
          :start-minutes="draft.startMinutes"
          :shift-minutes="frameworkMinutes"
          :total-shifts="draft.totalShifts"
          :color="draft.color"
          :seats="seats"
          :capacity="totalCapacity"
          :focus="current.focus"
        />
        <p class="px-1 text-xs text-muted-foreground">{{ t('wizard_draft_hint') }}</p>
      </aside>
    </div>
  </div>
</template>
