<script setup lang="ts">
import { computed, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { toast } from 'vue-sonner'
import { ExternalLink, Link2, Unlink } from '@lucide/vue'
import PageHeader from '@/components/PageHeader.vue'
import ConfirmDialog from '@/components/ConfirmDialog.vue'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog'
import { Label } from '@/components/ui/label'
import { Skeleton } from '@/components/ui/skeleton'
import { Switch } from '@/components/ui/switch'
import {
  ApiError,
  notificationsApi,
  type CreateChannelLinkResponse,
  type GetNotificationSettingsResponse,
  type NotificationChannel,
} from '@/api'
import { useErrors } from '@/composables/useErrors'
import { useSaving } from '@/composables/useSaving'

type ReminderKey = 'remindDayBefore' | 'remindShortBefore'

const { t } = useI18n()
const { report, messageOf } = useErrors()
const { saving, error, run } = useSaving()

const loading = ref(true)
const settings = ref<GetNotificationSettingsResponse | null>(null)
/** Pending link of a messenger; the dialog shows its code */
const link = ref<CreateChannelLinkResponse | null>(null)
const unlinking = ref<NotificationChannel | null>(null)
const busy = ref<NotificationChannel | null>(null)

/** Every channel the installation offers, with the user's link if there is one */
const channels = computed(() =>
  (settings.value?.availableChannels ?? []).map((channel) => ({
    channel,
    link: settings.value?.channels.find((l) => l.channel === channel) ?? null,
  })),
)

function channelLabel(channel: NotificationChannel): string {
  return channel === 'Email' ? t('notifications_channel_email') : channel
}

function expiresAt(iso: string): string {
  return new Date(iso).toLocaleTimeString('de-DE', { hour: '2-digit', minute: '2-digit' })
}

async function load(): Promise<void> {
  loading.value = true
  try {
    settings.value = await notificationsApi.get()
  } catch (e) {
    await report(e)
  } finally {
    loading.value = false
  }
}
load()

async function setReminder(key: ReminderKey, value: boolean): Promise<void> {
  const current = settings.value
  if (!current) return
  const previous = current[key]
  current[key] = value
  const ok = await run(() =>
    notificationsApi.update({
      remindDayBefore: current.remindDayBefore,
      remindShortBefore: current.remindShortBefore,
    }),
  )
  if (!ok) current[key] = previous
}

async function startLink(channel: NotificationChannel): Promise<void> {
  busy.value = channel
  try {
    const result = await notificationsApi.link(channel)
    if (result.linked) {
      toast.success(result.instructions)
      await load()
    } else {
      link.value = result
    }
  } catch (e) {
    // 409: channel not offered or no verified e-mail; the message says which
    if (e instanceof ApiError && e.status < 500) toast.error(messageOf(e))
    else await report(e)
  } finally {
    busy.value = null
  }
}

/** The user comes back from the messenger, so the list may have a new link */
async function closeLink(): Promise<void> {
  link.value = null
  await load()
}

async function unlink(): Promise<void> {
  const channel = unlinking.value
  unlinking.value = null
  if (!channel) return
  if (await run(() => notificationsApi.unlink(channel), t('notifications_unlinked'))) await load()
}
</script>

<template>
  <div class="mx-auto max-w-xl">
    <PageHeader :title="t('notifications_title')" :subtitle="t('notifications_subtitle')" />

    <div v-if="loading" class="space-y-4">
      <Skeleton class="h-32" />
      <Skeleton class="h-40" />
    </div>

    <template v-else-if="settings">
      <section class="rounded-xl border border-border bg-card p-4">
        <h2 class="font-semibold">{{ t('notifications_reminders') }}</h2>
        <div class="mt-3 flex items-center justify-between gap-4">
          <div>
            <Label for="n-day-before">{{ t('notifications_day_before') }}</Label>
            <p class="text-sm text-muted-foreground">{{ t('notifications_day_before_hint') }}</p>
          </div>
          <Switch
            id="n-day-before"
            :model-value="settings.remindDayBefore"
            :disabled="saving"
            @update:model-value="setReminder('remindDayBefore', $event)"
          />
        </div>
        <div class="mt-4 flex items-center justify-between gap-4">
          <div>
            <Label for="n-short-before">{{ t('notifications_short_before') }}</Label>
            <p class="text-sm text-muted-foreground">{{ t('notifications_short_before_hint') }}</p>
          </div>
          <Switch
            id="n-short-before"
            :model-value="settings.remindShortBefore"
            :disabled="saving"
            @update:model-value="setReminder('remindShortBefore', $event)"
          />
        </div>
        <p v-if="error" class="mt-3 text-sm text-destructive">{{ error }}</p>
      </section>

      <section class="mt-4 rounded-xl border border-border bg-card p-4">
        <h2 class="font-semibold">{{ t('notifications_channels') }}</h2>
        <p v-if="!channels.length" class="mt-2 text-sm text-muted-foreground">
          {{ t('notifications_channels_none') }}
        </p>
        <ul v-else class="mt-1 divide-y divide-border">
          <li
            v-for="c in channels"
            :key="c.channel"
            class="flex items-center justify-between gap-3 py-3"
          >
            <div class="min-w-0">
              <p class="font-medium">{{ channelLabel(c.channel) }}</p>
              <p class="truncate text-sm text-muted-foreground">
                {{ c.link ? c.link.address : t('notifications_not_linked') }}
              </p>
            </div>
            <div class="flex shrink-0 items-center gap-2">
              <Badge v-if="c.link && !c.link.enabled" variant="outline">
                {{ t('notifications_paused') }}
              </Badge>
              <Button
                v-if="!c.link || !c.link.enabled"
                size="sm"
                :disabled="busy === c.channel"
                @click="startLink(c.channel)"
              >
                <Link2 /> {{ c.link ? t('notifications_relink') : t('notifications_link') }}
              </Button>
              <Button
                v-if="c.link"
                size="icon"
                variant="ghost"
                :title="t('notifications_unlink')"
                :aria-label="t('notifications_unlink')"
                @click="unlinking = c.channel"
              >
                <Unlink />
              </Button>
            </div>
          </li>
        </ul>
      </section>
    </template>

    <Dialog :open="link !== null" @update:open="(open) => !open && closeLink()">
      <DialogContent v-if="link">
        <DialogHeader>
          <DialogTitle>
            {{ t('notifications_link_title', { channel: channelLabel(link.channel) }) }}
          </DialogTitle>
          <DialogDescription>{{ link.instructions }}</DialogDescription>
        </DialogHeader>
        <p class="select-all text-center font-mono text-3xl tracking-[0.3em]">{{ link.code }}</p>
        <p v-if="link.expiresAt" class="text-center text-xs text-muted-foreground">
          {{ t('notifications_code_expires', { time: expiresAt(link.expiresAt) }) }}
        </p>
        <DialogFooter>
          <Button v-if="link.deepLink" as-child>
            <a :href="link.deepLink" target="_blank" rel="noopener">
              <ExternalLink />
              {{ t('notifications_open_app', { channel: channelLabel(link.channel) }) }}
            </a>
          </Button>
          <Button variant="outline" @click="closeLink">{{ t('notifications_done') }}</Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>

    <ConfirmDialog
      :open="unlinking !== null"
      :text="t('notifications_unlink_text', { channel: unlinking ? channelLabel(unlinking) : '' })"
      :confirm-label="t('notifications_unlink')"
      destructive
      @update:open="(open) => !open && (unlinking = null)"
      @confirm="unlink"
    />
  </div>
</template>
