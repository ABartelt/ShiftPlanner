<script setup lang="ts">
import { computed, onBeforeUnmount, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import { QrCode, Search } from '@lucide/vue'
import ResponsiveDialog from '@/components/ResponsiveDialog.vue'
import FormField from '@/components/FormField.vue'
import LocationFilter from '@/components/promote/LocationFilter.vue'
import { Button } from '@/components/ui/button'
import { Checkbox } from '@/components/ui/checkbox'
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
  promotionsApi,
  type PromotionChannelConfig,
  type PromotionChannelKind,
  type PromotionChannelResponse,
  type SignalGroupResponse,
  type TelegramChatResponse,
} from '@/api'
import { useSaving } from '@/composables/useSaving'
import { useErrors } from '@/composables/useErrors'

const props = defineProps<{
  open: boolean
  channel: PromotionChannelResponse | null
  seasonId: string
}>()
const emit = defineEmits<{
  'update:open': [value: boolean]
  saved: []
}>()
const { t } = useI18n()
const { saving, error, run } = useSaving()
const { messageOf } = useErrors()

const kinds: PromotionChannelKind[] = ['Signal', 'Telegram', 'Slack']
const kind = ref<PromotionChannelKind>('Signal')
const name = ref('')
const enabled = ref(true)
const config = ref<PromotionChannelConfig>(emptyConfig())
/** new secret; empty keeps the stored one */
const secret = ref('')
const removeSecret = ref(false)

const busy = ref(false)
const lookupError = ref('')
const telegramChats = ref<TelegramChatResponse[]>([])
const signalAccounts = ref<string[]>([])
const signalGroups = ref<SignalGroupResponse[]>([])
const qrUrl = ref<string | null>(null)

const hasStoredSecret = computed(() => !!props.channel?.hasSecret && !removeSecret.value)
const needsSecret = computed(() => kind.value !== 'Signal')

function emptyConfig(): PromotionChannelConfig {
  return { target: '', targetName: '', signalNumber: '', serviceUrl: '', locationIds: [] }
}

watch(
  () => props.open,
  (open) => {
    if (!open) return
    error.value = ''
    lookupError.value = ''
    kind.value = props.channel?.kind ?? 'Signal'
    name.value = props.channel?.name ?? ''
    enabled.value = props.channel?.enabled ?? true
    config.value = { ...emptyConfig(), ...props.channel?.config }
    secret.value = ''
    removeSecret.value = false
    telegramChats.value = []
    signalAccounts.value = []
    signalGroups.value = []
    clearQr()
  },
)

function clearQr(): void {
  if (qrUrl.value) URL.revokeObjectURL(qrUrl.value)
  qrUrl.value = null
}
onBeforeUnmount(clearQr)

async function lookup(action: () => Promise<void>): Promise<void> {
  busy.value = true
  lookupError.value = ''
  try {
    await action()
  } catch (e) {
    lookupError.value = messageOf(e)
  } finally {
    busy.value = false
  }
}

const findTelegramChats = () =>
  lookup(async () => {
    telegramChats.value = await promotionsApi.telegramChats(
      secret.value.trim() || null,
      props.channel?.id ?? null,
    )
    if (!telegramChats.value.length) lookupError.value = t('promote_telegram_no_chats')
  })

const showSignalQr = () =>
  lookup(async () => {
    clearQr()
    qrUrl.value = URL.createObjectURL(await promotionsApi.signalLinkQr(config.value.serviceUrl))
  })

const loadSignalAccounts = () =>
  lookup(async () => {
    signalAccounts.value = await promotionsApi.signalAccounts(config.value.serviceUrl)
    if (!signalAccounts.value.length) lookupError.value = t('promote_signal_no_accounts')
    else if (!config.value.signalNumber) config.value.signalNumber = signalAccounts.value[0]!
  })

const loadSignalGroups = () =>
  lookup(async () => {
    signalGroups.value = await promotionsApi.signalGroups(
      config.value.serviceUrl,
      config.value.signalNumber,
    )
    if (!signalGroups.value.length) lookupError.value = t('promote_signal_no_groups')
  })

function pickTarget(id: string, title: string): void {
  config.value.target = id
  config.value.targetName = title
  if (!name.value.trim()) name.value = title
}

async function save(): Promise<void> {
  if (!name.value.trim()) {
    error.value = t('field_required')
    return
  }
  const secretValue = removeSecret.value ? '' : secret.value.trim() || null
  const ok = await run(async () => {
    if (props.channel)
      await promotionsApi.updateChannel({
        id: props.channel.id,
        name: name.value.trim(),
        enabled: enabled.value,
        config: config.value,
        secret: secretValue,
      })
    else
      await promotionsApi.createChannel(props.seasonId, {
        name: name.value.trim(),
        kind: kind.value,
        enabled: enabled.value,
        config: config.value,
        secret: secretValue,
      })
  })
  if (ok) {
    emit('saved')
    emit('update:open', false)
  }
}
</script>

<template>
  <ResponsiveDialog
    :open="open"
    :title="channel ? t('btn_edit') : t('promote_channel_new')"
    @update:open="emit('update:open', $event)"
  >
    <form class="space-y-4" @submit.prevent="save">
      <FormField v-if="!channel" :label="t('promote_channel_kind')">
        <Select v-model="kind">
          <SelectTrigger class="w-full"><SelectValue /></SelectTrigger>
          <SelectContent>
            <SelectItem v-for="k in kinds" :key="k" :value="k">{{ k }}</SelectItem>
          </SelectContent>
        </Select>
      </FormField>
      <p class="rounded-lg bg-muted p-3 text-xs text-muted-foreground">
        {{ t(`promote_howto_${kind}`) }}
      </p>

      <!-- Telegram -->
      <template v-if="kind === 'Telegram'">
        <FormField
          :label="t('promote_telegram_token')"
          for="pc-secret"
          :hint="hasStoredSecret ? t('promote_secret_stored', { hint: channel?.secretHint }) : ''"
        >
          <Input
            id="pc-secret"
            v-model="secret"
            type="password"
            autocomplete="off"
            :placeholder="hasStoredSecret ? '••••••••' : '123456:ABC…'"
          />
        </FormField>
        <FormField :label="t('promote_telegram_chat')" for="pc-target">
          <div class="flex gap-2">
            <Input id="pc-target" v-model="config.target" placeholder="-100…" />
            <Button type="button" variant="outline" :disabled="busy" @click="findTelegramChats"
              ><Search /> {{ t('promote_telegram_find') }}</Button
            >
          </div>
          <div v-if="telegramChats.length" class="flex flex-wrap gap-2 pt-1">
            <Button
              v-for="c in telegramChats"
              :key="c.id"
              type="button"
              size="sm"
              :variant="config.target === c.id ? 'default' : 'outline'"
              @click="pickTarget(c.id, c.title)"
              >{{ c.title }}</Button
            >
          </div>
        </FormField>
      </template>

      <!-- Slack -->
      <FormField
        v-else-if="kind === 'Slack'"
        :label="t('promote_slack_webhook')"
        for="pc-secret"
        :hint="hasStoredSecret ? t('promote_secret_stored', { hint: channel?.secretHint }) : ''"
      >
        <Input
          id="pc-secret"
          v-model="secret"
          type="password"
          autocomplete="off"
          :placeholder="hasStoredSecret ? '••••••••' : 'https://hooks.slack.com/services/…'"
        />
      </FormField>

      <!-- Signal -->
      <template v-else>
        <FormField
          :label="t('promote_signal_service')"
          for="pc-service"
          :hint="t('promote_signal_service_hint')"
        >
          <Input id="pc-service" v-model="config.serviceUrl" placeholder="http://signal-api:8080" />
        </FormField>
        <div class="flex flex-wrap gap-2">
          <Button type="button" variant="outline" size="sm" :disabled="busy" @click="showSignalQr"
            ><QrCode /> {{ t('promote_signal_link') }}</Button
          >
          <Button
            type="button"
            variant="outline"
            size="sm"
            :disabled="busy"
            @click="loadSignalAccounts"
            >{{ t('promote_signal_load_accounts') }}</Button
          >
        </div>
        <div v-if="qrUrl" class="flex flex-col items-center gap-2 rounded-lg border p-3">
          <img :src="qrUrl" alt="Signal QR" class="size-48" />
          <p class="text-center text-xs text-muted-foreground">{{ t('promote_signal_qr_hint') }}</p>
        </div>
        <FormField :label="t('promote_signal_number')" for="pc-number">
          <Select v-if="signalAccounts.length" v-model="config.signalNumber">
            <SelectTrigger class="w-full"><SelectValue /></SelectTrigger>
            <SelectContent>
              <SelectItem v-for="a in signalAccounts" :key="a" :value="a">{{ a }}</SelectItem>
            </SelectContent>
          </Select>
          <Input v-else id="pc-number" v-model="config.signalNumber" placeholder="+49…" />
        </FormField>
        <FormField :label="t('promote_signal_group')">
          <div class="flex gap-2">
            <Input
              :model-value="config.targetName || config.target"
              readonly
              :placeholder="t('promote_signal_group_placeholder')"
            />
            <Button
              type="button"
              variant="outline"
              :disabled="busy || !config.signalNumber"
              @click="loadSignalGroups"
              ><Search /> {{ t('promote_signal_load_groups') }}</Button
            >
          </div>
          <div v-if="signalGroups.length" class="flex flex-wrap gap-2 pt-1">
            <Button
              v-for="g in signalGroups"
              :key="g.id"
              type="button"
              size="sm"
              :variant="config.target === g.id ? 'default' : 'outline'"
              @click="pickTarget(g.id, g.name)"
              >{{ g.name }}</Button
            >
          </div>
        </FormField>
      </template>

      <p v-if="lookupError" class="text-sm text-destructive">{{ lookupError }}</p>

      <label
        v-if="needsSecret && channel?.hasSecret"
        class="flex items-center gap-2 text-xs text-muted-foreground"
      >
        <Checkbox
          :model-value="removeSecret"
          @update:model-value="removeSecret = $event === true"
        />
        {{ t('promote_secret_remove') }}
      </label>

      <FormField :label="t('promote_channel_name')" for="pc-name">
        <Input id="pc-name" v-model="name" maxlength="100" required />
      </FormField>
      <FormField :label="t('promote_locations')">
        <LocationFilter v-model="config.locationIds" />
      </FormField>
      <label class="flex items-center gap-2 text-sm">
        <Switch :model-value="enabled" @update:model-value="enabled = $event" />
        {{ t('promote_channel_enabled') }}
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
