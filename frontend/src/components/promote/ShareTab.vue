<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import { useDebounceFn } from '@vueuse/core'
import { toast } from 'vue-sonner'
import { Copy, MessageCircle, Send, Share2 } from '@lucide/vue'
import FormField from '@/components/FormField.vue'
import LocationFilter from './LocationFilter.vue'
import { Button } from '@/components/ui/button'
import { Checkbox } from '@/components/ui/checkbox'
import { Input } from '@/components/ui/input'
import { Skeleton } from '@/components/ui/skeleton'
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select'
import {
  promotionsApi,
  type PromotionChannelResponse,
  type PromotionFormat,
  type PromotionPreviewResponse,
} from '@/api'
import { useSeasonStore } from '@/stores/season'
import { useErrors } from '@/composables/useErrors'
import { addDays, todayKey } from '@/domain/time'

const { t } = useI18n()
const season = useSeasonStore()
const { report } = useErrors()

type Preset = 'tomorrow' | 'weekend' | 'week' | 'event'
const preset = ref<Preset | null>('tomorrow')
const from = ref(addDays(todayKey(), 1))
const to = ref(addDays(todayKey(), 1))
const locationIds = ref<string[]>([])
const format = ref<PromotionFormat>('WhatsApp')
const preview = ref<PromotionPreviewResponse | null>(null)
const loading = ref(false)

const channels = ref<PromotionChannelResponse[]>([])
const selectedChannels = ref<string[]>([])
const sending = ref(false)

const formats: PromotionFormat[] = ['WhatsApp', 'Signal', 'Telegram', 'Slack', 'Plain']
const canShare = typeof navigator !== 'undefined' && 'share' in navigator

function applyPreset(p: Preset): void {
  preset.value = p
  const today = todayKey()
  if (p === 'tomorrow') {
    from.value = to.value = addDays(today, 1)
  } else if (p === 'weekend') {
    // weekday of the event-local day: 0 = Sunday … 6 = Saturday
    const weekday = new Date(`${today}T12:00:00Z`).getUTCDay()
    const saturday = weekday === 0 ? addDays(today, -1) : addDays(today, 6 - weekday)
    from.value = weekday === 0 ? today : saturday
    to.value = addDays(saturday, 1)
  } else if (p === 'week') {
    from.value = today
    to.value = addDays(today, 6)
  } else if (season.current) {
    from.value = season.current.startDate.slice(0, 10)
    to.value = season.current.endDate.slice(0, 10)
  }
}

const load = useDebounceFn(async () => {
  if (!season.currentId || !from.value || !to.value || to.value < from.value) return
  loading.value = true
  try {
    preview.value = await promotionsApi.preview(
      season.currentId,
      { from: from.value, to: to.value },
      format.value,
      locationIds.value,
    )
  } catch (error) {
    await report(error)
  } finally {
    loading.value = false
  }
}, 250)

watch([() => season.currentId, from, to, format, locationIds], load, { immediate: true })

watch(
  () => season.currentId,
  async (id) => {
    if (!id) return
    try {
      channels.value = (await promotionsApi.channels(id)).filter((c) => c.enabled)
      selectedChannels.value = []
    } catch (error) {
      await report(error)
    }
  },
  { immediate: true },
)

const whatsAppUrl = computed(() =>
  preview.value ? `https://wa.me/?text=${encodeURIComponent(preview.value.text)}` : '#',
)

async function copy(): Promise<void> {
  if (!preview.value) return
  await navigator.clipboard.writeText(preview.value.text)
  toast.success(t('promote_copied'))
}

async function share(): Promise<void> {
  if (!preview.value) return
  try {
    await navigator.share({ text: preview.value.text })
  } catch {
    /* dismissed */
  }
}

function toggleChannel(id: string, on: boolean): void {
  selectedChannels.value = on
    ? [...selectedChannels.value, id]
    : selectedChannels.value.filter((x) => x !== id)
}

async function send(): Promise<void> {
  if (!season.currentId || !selectedChannels.value.length) return
  sending.value = true
  try {
    const results = await promotionsApi.send(
      season.currentId,
      { from: from.value, to: to.value },
      selectedChannels.value,
    )
    for (const r of results) {
      if (r.status === 'Sent') toast.success(t('promote_sent_to', { name: r.channelName }))
      else toast.error(`${r.channelName}: ${r.error ?? t('promote_status_Failed')}`)
    }
  } catch (error) {
    await report(error)
  } finally {
    sending.value = false
  }
}
</script>

<template>
  <div class="grid gap-4 lg:grid-cols-2">
    <div class="space-y-4 rounded-xl border border-border bg-card p-4">
      <FormField :label="t('promote_range')">
        <div class="flex flex-wrap gap-2">
          <Button
            v-for="p in ['tomorrow', 'weekend', 'week', 'event'] as const"
            :key="p"
            size="sm"
            :variant="preset === p ? 'default' : 'outline'"
            @click="applyPreset(p)"
            >{{ t(`promote_preset_${p}`) }}</Button
          >
        </div>
        <div class="flex items-center gap-2">
          <Input v-model="from" type="date" class="w-40" @input="preset = null" />
          <span class="text-muted-foreground">–</span>
          <Input v-model="to" type="date" class="w-40" @input="preset = null" />
        </div>
      </FormField>
      <FormField :label="t('promote_locations')">
        <LocationFilter v-model="locationIds" />
      </FormField>
      <FormField :label="t('promote_format')" :hint="t('promote_format_hint')">
        <Select v-model="format">
          <SelectTrigger class="w-48"><SelectValue /></SelectTrigger>
          <SelectContent>
            <SelectItem v-for="f in formats" :key="f" :value="f">{{
              t(`promote_format_${f}`)
            }}</SelectItem>
          </SelectContent>
        </Select>
      </FormField>
    </div>

    <div class="space-y-3">
      <div class="rounded-xl border border-border bg-card p-4">
        <div class="mb-2 flex items-center justify-between gap-2">
          <p class="eyebrow">{{ t('promote_preview') }}</p>
          <span v-if="preview" class="text-xs text-muted-foreground">{{
            t('promote_open_count', preview.totalOpen)
          }}</span>
        </div>
        <Skeleton v-if="loading && !preview" class="h-40 w-full" />
        <pre
          v-else-if="preview"
          class="max-h-96 overflow-auto font-sans text-sm break-words whitespace-pre-wrap"
          >{{ preview.text }}</pre>
      </div>
      <div class="flex flex-wrap gap-2">
        <Button v-if="preview" as="a" :href="whatsAppUrl" target="_blank" rel="noopener">
          <MessageCircle /> {{ t('promote_whatsapp') }}
        </Button>
        <Button v-if="canShare" variant="outline" :disabled="!preview" @click="share">
          <Share2 /> {{ t('promote_share') }}
        </Button>
        <Button variant="outline" :disabled="!preview" @click="copy">
          <Copy /> {{ t('promote_copy') }}
        </Button>
      </div>
      <p class="text-xs text-muted-foreground">{{ t('promote_share_hint') }}</p>

      <div v-if="channels.length" class="space-y-3 rounded-xl border border-border bg-card p-4">
        <p class="eyebrow">{{ t('promote_send_now') }}</p>
        <div class="flex flex-wrap gap-x-4 gap-y-2">
          <label v-for="c in channels" :key="c.id" class="flex items-center gap-2 text-sm">
            <Checkbox
              :model-value="selectedChannels.includes(c.id)"
              @update:model-value="toggleChannel(c.id, $event === true)"
            />
            {{ c.name }} <span class="text-xs text-muted-foreground">({{ c.kind }})</span>
          </label>
        </div>
        <p class="text-xs text-muted-foreground">{{ t('promote_send_now_hint') }}</p>
        <Button
          :disabled="!selectedChannels.length || sending || !preview?.totalOpen"
          @click="send"
        >
          <Send /> {{ t('promote_send') }}
        </Button>
      </div>
    </div>
  </div>
</template>
