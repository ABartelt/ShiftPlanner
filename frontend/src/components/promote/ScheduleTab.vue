<script setup lang="ts">
import { ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import FormField from '@/components/FormField.vue'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Switch } from '@/components/ui/switch'
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select'
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table'
import {
  promotionsApi,
  type DayOfWeek,
  type Duration,
  type PromotionLogResponse,
  type PromotionSchedule,
  type PromotionScheduleResponse,
} from '@/api'
import { useConfig } from '@/config'
import { useSeasonStore } from '@/stores/season'
import { useSaving } from '@/composables/useSaving'
import { useErrors } from '@/composables/useErrors'
import { formatDateTime } from '@/domain/time'

const { t } = useI18n()
const season = useSeasonStore()
const { saving, error, run } = useSaving()
const { report } = useErrors()

const schedule = ref<PromotionSchedule | null>(null)
const info = ref<PromotionScheduleResponse | null>(null)
const log = ref<PromotionLogResponse[]>([])

// Monday first, values are .NET DayOfWeek
const weekdays: DayOfWeek[] = [1, 2, 3, 4, 5, 6, 0]

/** "19:00:00" ↔ "19:00" for <input type="time"> */
const toClock = (d: Duration) => d.slice(0, 5)
const toDuration = (clock: string): Duration => `${clock || '00:00'}:00`

async function load(): Promise<void> {
  if (!season.currentId) return
  try {
    const [s, l] = await Promise.all([
      promotionsApi.schedule(season.currentId),
      promotionsApi.log(season.currentId),
    ])
    info.value = s
    log.value = l
    schedule.value = {
      ...s.schedule,
      timeZone: s.schedule.timeZone || useConfig().timeZone,
      publicBaseUrl: s.schedule.publicBaseUrl || window.location.origin,
    }
  } catch (e) {
    await report(e)
  }
}

watch(() => season.currentId, load, { immediate: true })

async function save(): Promise<void> {
  if (!season.currentId || !schedule.value) return
  const body = schedule.value
  if (await run(() => promotionsApi.updateSchedule(season.currentId!, body))) await load()
}
</script>

<template>
  <div v-if="schedule" class="grid gap-4 lg:grid-cols-2">
    <form class="space-y-4 rounded-xl border border-border bg-card p-4" @submit.prevent="save">
      <label class="flex items-center gap-2 text-sm font-medium">
        <Switch :model-value="schedule.enabled" @update:model-value="schedule.enabled = $event" />
        {{ t('promote_schedule_enabled') }}
      </label>
      <p class="text-xs text-muted-foreground">{{ t('promote_schedule_intro') }}</p>

      <fieldset class="space-y-3 rounded-lg border p-3">
        <label class="flex items-center gap-2 text-sm font-medium">
          <Switch
            :model-value="schedule.recruitingEnabled"
            @update:model-value="schedule.recruitingEnabled = $event"
          />
          {{ t('promote_recruiting') }}
        </label>
        <p class="text-xs text-muted-foreground">{{ t('promote_recruiting_hint') }}</p>
        <div v-if="schedule.recruitingEnabled" class="flex flex-wrap items-end gap-3">
          <FormField :label="t('promote_weekday')">
            <Select
              :model-value="String(schedule.recruitingDay)"
              @update:model-value="schedule.recruitingDay = Number($event) as DayOfWeek"
            >
              <SelectTrigger class="w-40"><SelectValue /></SelectTrigger>
              <SelectContent>
                <SelectItem v-for="d in weekdays" :key="d" :value="String(d)">{{
                  t(`promote_weekday_${d}`)
                }}</SelectItem>
              </SelectContent>
            </Select>
          </FormField>
          <FormField :label="t('promote_time')">
            <Input
              type="time"
              class="w-32"
              :model-value="toClock(schedule.recruitingSendTime)"
              @update:model-value="schedule.recruitingSendTime = toDuration(String($event))"
            />
          </FormField>
        </div>
      </fieldset>

      <fieldset class="space-y-3 rounded-lg border p-3">
        <label class="flex items-center gap-2 text-sm font-medium">
          <Switch
            :model-value="schedule.dailyEnabled"
            @update:model-value="schedule.dailyEnabled = $event"
          />
          {{ t('promote_daily') }}
        </label>
        <p class="text-xs text-muted-foreground">{{ t('promote_daily_hint') }}</p>
        <div v-if="schedule.dailyEnabled" class="flex flex-wrap items-end gap-3">
          <FormField :label="t('promote_time')">
            <Input
              type="time"
              class="w-32"
              :model-value="toClock(schedule.dailySendTime)"
              @update:model-value="schedule.dailySendTime = toDuration(String($event))"
            />
          </FormField>
          <FormField :label="t('promote_lookahead')">
            <Input
              v-model.number="schedule.dailyLookaheadDays"
              type="number"
              min="1"
              max="14"
              class="w-24"
            />
          </FormField>
        </div>
      </fieldset>

      <fieldset class="space-y-3 rounded-lg border p-3">
        <label class="flex items-center gap-2 text-sm font-medium">
          <Switch
            :model-value="schedule.lastCallEnabled"
            @update:model-value="schedule.lastCallEnabled = $event"
          />
          {{ t('promote_last_call') }}
        </label>
        <p class="text-xs text-muted-foreground">{{ t('promote_last_call_hint') }}</p>
        <div v-if="schedule.lastCallEnabled" class="flex flex-wrap items-end gap-3">
          <FormField :label="t('promote_last_call_hours')">
            <Input
              v-model.number="schedule.lastCallHoursBefore"
              type="number"
              min="1"
              max="48"
              class="w-24"
            />
          </FormField>
          <FormField :label="t('promote_quiet')">
            <div class="flex items-center gap-2">
              <Input
                type="time"
                class="w-32"
                :model-value="toClock(schedule.quietFrom)"
                @update:model-value="schedule.quietFrom = toDuration(String($event))"
              />
              <span class="text-muted-foreground">–</span>
              <Input
                type="time"
                class="w-32"
                :model-value="toClock(schedule.quietUntil)"
                @update:model-value="schedule.quietUntil = toDuration(String($event))"
              />
            </div>
          </FormField>
        </div>
      </fieldset>

      <FormField :label="t('promote_min_open')" for="ps-min" :hint="t('promote_min_open_hint')">
        <Input
          id="ps-min"
          v-model.number="schedule.minOpenShifts"
          type="number"
          min="1"
          class="w-24"
        />
      </FormField>
      <FormField :label="t('promote_base_url')" for="ps-url" :hint="t('promote_base_url_hint')">
        <Input id="ps-url" v-model="schedule.publicBaseUrl" type="url" />
      </FormField>
      <FormField :label="t('promote_time_zone')" for="ps-tz">
        <Input id="ps-tz" v-model="schedule.timeZone" class="w-56" />
      </FormField>

      <p v-if="error" class="text-sm text-destructive">{{ error }}</p>
      <div class="flex justify-end">
        <Button type="submit" :disabled="saving">{{ t('btn_save') }}</Button>
      </div>
    </form>

    <div class="space-y-4">
      <div class="rounded-xl border border-border bg-card p-4">
        <p class="eyebrow mb-2">{{ t('promote_next_runs') }}</p>
        <p v-if="!info?.schedule.enabled" class="text-sm text-muted-foreground">
          {{ t('promote_schedule_off') }}
        </p>
        <p v-else-if="!info.enabledChannels" class="text-sm text-muted-foreground">
          {{ t('promote_no_channels') }}
        </p>
        <ul v-else-if="info.nextRuns.length" class="space-y-1 text-sm">
          <li v-for="r in info.nextRuns" :key="r.sendAt">
            {{ formatDateTime(r.sendAt) }} · {{ t(`promote_kind_${r.kind}`) }} →
            {{ t('promote_channels_count', info.enabledChannels) }}
          </li>
        </ul>
        <p v-else class="text-sm text-muted-foreground">{{ t('promote_no_next_runs') }}</p>
      </div>

      <div class="rounded-xl border border-border bg-card p-4">
        <p class="eyebrow mb-2">{{ t('promote_log') }}</p>
        <p v-if="!log.length" class="text-sm text-muted-foreground">{{ t('promote_log_empty') }}</p>
        <Table v-else>
          <TableHeader>
            <TableRow>
              <TableHead>{{ t('promote_log_when') }}</TableHead>
              <TableHead>{{ t('promote_channel_name') }}</TableHead>
              <TableHead>{{ t('promote_log_kind') }}</TableHead>
              <TableHead>{{ t('promote_channel_status') }}</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            <TableRow v-for="l in log" :key="l.id">
              <TableCell class="whitespace-nowrap">{{ formatDateTime(l.sentAt) }}</TableCell>
              <TableCell>{{ l.channelName }}</TableCell>
              <TableCell>{{ t(`promote_kind_${l.runKind}`) }}</TableCell>
              <TableCell>
                <Badge
                  :variant="
                    l.status === 'Failed'
                      ? 'destructive'
                      : l.status === 'Sent'
                        ? 'secondary'
                        : 'outline'
                  "
                  :title="l.error ?? ''"
                  >{{ t(`promote_status_${l.status}`) }} · {{ l.openShifts }}</Badge
                >
                <p v-if="l.error" class="mt-0.5 text-xs text-muted-foreground">{{ l.error }}</p>
              </TableCell>
            </TableRow>
          </TableBody>
        </Table>
      </div>
    </div>
  </div>
</template>
