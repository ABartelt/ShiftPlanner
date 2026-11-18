<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import { useRoute, useRouter } from 'vue-router'
import { ArrowLeft, Printer } from '@lucide/vue'
import { format as dfFormat } from 'date-fns'
import { de } from 'date-fns/locale'
import QRCode from 'qrcode'
import BrandText from '@/components/BrandText.vue'
import { Button } from '@/components/ui/button'
import { Checkbox } from '@/components/ui/checkbox'
import { locationsApi, type LocationResponse, type ShiftResponse } from '@/api'
import { useConfig } from '@/config'
import { useSeasonStore } from '@/stores/season'
import { useSettingsStore } from '@/stores/settings'
import { useErrors } from '@/composables/useErrors'
import { usePageTitle } from '@/composables/usePageTitle'
import {
  buildPoster,
  chunkColumns,
  dayCutoffMinutes,
  locationSpan,
  type PosterColumn,
} from '@/domain/poster'
import { formatDateTime, formatTime, inZone } from '@/domain/time'

/* Printable notice of one location (A4 portrait), one page per day. A black-and-white table —
   rows are time slots, columns shift types — with full names of booked people. Rosa means one
   thing only: "still free". Always light, whatever the app theme is. */
const props = defineProps<{ id: string }>()
const { t } = useI18n()
const route = useRoute()
const router = useRouter()
const config = useConfig()
const season = useSeasonStore()
const settings = useSettingsStore()
const { report } = useErrors()
const page = usePageTitle()

const location = ref<LocationResponse | null>(null)
const shifts = ref<ShiftResponse[]>([])
const loading = ref(true)
const generatedAt = ref(new Date())

const upcomingOnly = computed(() => route.query.from === 'today')
const days = computed(() =>
  location.value
    ? buildPoster(location.value, shifts.value, {
        from: upcomingOnly.value ? generatedAt.value : undefined,
        dayCutoffMinutes: dayCutoffMinutes(settings.window),
      }).map((day) => ({ ...day, parts: chunkColumns(day, 6) }))
    : [],
)

const host = window.location.host
const signupHref = computed(() => `${window.location.origin}/locations/${props.id}`)
/* SVG markup generated locally from our own URL, safe for v-html */
const qrSvg = ref('')
watch(
  signupHref,
  async (href) => {
    try {
      qrSvg.value = await QRCode.toString(href, {
        type: 'svg',
        margin: 0,
        errorCorrectionLevel: 'M',
        color: { dark: '#000000', light: '#ffffff' },
      })
    } catch {
      qrSvg.value = ''
    }
  },
  { immediate: true },
)
const brandTitle = computed(() => settings.settings.title || config.brand.name)
/* the band is black: the white wordmark fits as is, the black one gets inverted */
const logo = computed(() =>
  config.brand.logoUrl
    ? { src: config.brand.logoUrl, invert: false }
    : config.brand.logoLightUrl
      ? { src: config.brand.logoLightUrl, invert: true }
      : null,
)

async function load(): Promise<void> {
  loading.value = true
  try {
    const loc = await locationsApi.get(props.id)
    const span = locationSpan(loc)
    shifts.value = span ? await locationsApi.shifts(loc.id, span.from, span.to) : []
    location.value = loc
    generatedAt.value = new Date()
    page.set(`${t('poster_title')} ${loc.name}`)
  } catch (error) {
    await report(error)
  } finally {
    loading.value = false
  }
}
watch(() => props.id, load, { immediate: true })

function setUpcomingOnly(value: boolean): void {
  void router.replace({ query: { ...route.query, from: value ? 'today' : undefined } })
}

function print(): void {
  window.print()
}

function weekday(date: Date): string {
  return dfFormat(inZone(date), 'EEEE', { locale: de })
}
function date(date: Date): string {
  return dfFormat(inZone(date), 'dd.MM.yyyy', { locale: de })
}
function offsetLabel(col: PosterColumn): string {
  if (!col.offsetMinutes) return ''
  const min = Math.abs(col.offsetMinutes)
  return col.offsetMinutes < 0
    ? t('poster_offset_earlier', { min })
    : t('poster_offset_later', { min })
}
</script>

<template>
  <div class="poster-root">
    <div class="toolbar print:hidden">
      <Button variant="ghost" @click="router.push({ name: 'admin-export' })"
        ><ArrowLeft /> {{ t('poster_back') }}</Button
      >
      <label class="flex items-center gap-2 text-sm">
        <Checkbox
          :model-value="upcomingOnly"
          @update:model-value="setUpcomingOnly($event === true)"
        />
        {{ t('poster_upcoming_only') }}
      </label>
      <Button :disabled="loading || !days.length" @click="print"
        ><Printer /> {{ t('poster_print') }}</Button
      >
    </div>

    <p v-if="loading" class="status print:hidden">…</p>
    <p v-else-if="!days.length" class="status">{{ t('poster_empty') }}</p>

    <section v-for="day in days" :key="day.day" class="sheet">
      <header class="band">
        <div class="when">
          <div class="weekday headline"><BrandText :text="weekday(day.start)" /></div>
          <div class="date">{{ date(day.start) }}</div>
        </div>
        <div class="where">
          <img v-if="logo" :src="logo.src" alt="" :class="['logo', { invert: logo.invert }]" />
          <span v-else class="brand-name">{{ brandTitle }}</span>
          <span class="location">{{ location?.name }}</span>
          <span v-if="season.current" class="season">{{ season.current.name }}</span>
        </div>
        <div :class="['badge', day.freeCount ? 'is-free' : 'is-full']">
          <template v-if="day.freeCount">
            <span class="badge-count">{{ day.freeCount }}</span>
            {{ t('poster_free_count', day.freeCount) }}
          </template>
          <template v-else>{{ t('poster_full') }}</template>
        </div>
      </header>
      <div class="gradient" />

      <table
        v-for="(part, p) in day.parts"
        :key="p"
        :class="['roster', { dense: part.columns.length >= 5 }]"
      >
        <colgroup>
          <col class="col-time" />
          <col v-for="col in part.columns" :key="col.type.id" />
        </colgroup>
        <thead>
          <tr>
            <th class="th-time">{{ t('poster_col_time') }}</th>
            <th v-for="col in part.columns" :key="col.type.id">
              {{ col.type.name }}
              <span v-if="col.offsetMinutes" class="th-sub">{{ offsetLabel(col) }}</span>
            </th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="row in part.rows" :key="row.key">
            <td class="time">
              <span class="from">{{ formatTime(row.start) }}</span>
              <span class="to">{{ t('poster_until', { time: formatTime(row.end) }) }}</span>
            </td>
            <td
              v-for="(cell, i) in row.cells"
              :key="part.columns[i]!.type.id"
              :class="{ 'has-free': cell.free }"
            >
              <div v-for="(name, n) in cell.names" :key="n" class="name">{{ name }}</div>
              <span v-if="cell.free" class="free">{{
                t('poster_free_cell', { count: cell.free })
              }}</span>
              <span v-if="!cell.names.length && !cell.free" class="none">–</span>
            </td>
          </tr>
        </tbody>
      </table>

      <footer class="foot">
        <div class="legend">
          <span class="swatch" />
          {{ t('poster_legend') }}
        </div>
        <div class="cta">
          <div class="cta-text">
            <strong class="cta-title">{{ t('poster_signup_hint') }}</strong>
            <span class="manual"
              >{{ t('poster_signup_manual') }} <span class="url">{{ host }}</span> →
              {{ location?.name }}</span
            >
            <span class="asof">{{ t('poster_as_of', { date: formatDateTime(generatedAt) }) }}</span>
          </div>
          <!-- eslint-disable-next-line vue/no-v-html -->
          <div v-if="qrSvg" class="qr" role="img" :aria-label="signupHref" v-html="qrSvg" />
        </div>
      </footer>
    </section>
  </div>
</template>

<style scoped>
.poster-root {
  --ink: #000;
  --paper: #fff;
  --secondary: #555;
  --free: var(--brand-rosa, #ff96e0);
  --free-tint: #ffe3f6;
  --rule: #000;
  --rule-soft: #c4c4c4;
  min-height: 100dvh;
  background: #d9d9d9;
  color: var(--ink);
  padding: 0 0 2rem;
  font-family: var(--font-sans);
  font-weight: 400;
  letter-spacing: 0.01em;
}
.toolbar {
  position: sticky;
  top: 0;
  z-index: 10;
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  justify-content: center;
  gap: 1rem;
  padding: 0.75rem 1rem;
  background: var(--background);
  color: var(--foreground);
  border-bottom: 1px solid var(--border);
}
.status {
  margin: 3rem auto;
  text-align: center;
  font-weight: 900;
}

.sheet {
  width: 210mm;
  min-height: 297mm;
  margin: 1.5rem auto 0;
  padding: 12mm;
  background: var(--paper);
  box-shadow: 0 4px 24px rgb(0 0 0 / 0.25);
  display: flex;
  flex-direction: column;
  print-color-adjust: exact;
  -webkit-print-color-adjust: exact;
}

/* ── band: the day is the biggest thing on the page ───────────────────── */
.band {
  display: grid;
  grid-template-columns: 1fr auto;
  grid-template-areas:
    'when where'
    'when badge';
  align-items: end;
  gap: 3mm 6mm;
  padding: 6mm 7mm 5mm;
  background: var(--ink);
  color: var(--paper);
  border-radius: 3mm 3mm 0 0;
}
.when {
  grid-area: when;
}
.weekday {
  font-size: 36pt;
  line-height: 1;
}
.date {
  margin-top: 2mm;
  font-size: 20pt;
  font-weight: 900;
  font-variant-numeric: tabular-nums;
}
.where {
  grid-area: where;
  display: flex;
  flex-direction: column;
  align-items: flex-end;
  gap: 1mm;
  text-align: right;
}
.logo {
  height: 8mm;
  width: auto;
  margin-bottom: 1mm;
}
.logo.invert {
  filter: invert(1);
}
.brand-name {
  font-size: 11pt;
  font-weight: 900;
}
.location {
  font-size: 15pt;
  font-weight: 900;
  text-transform: uppercase;
  letter-spacing: 0.04em;
}
.season {
  font-size: 11pt;
}
.badge {
  grid-area: badge;
  justify-self: end;
  display: inline-flex;
  align-items: center;
  gap: 2mm;
  padding: 1.5mm 4.5mm;
  border-radius: 99mm;
  font-size: 13pt;
  font-weight: 900;
  text-transform: uppercase;
  letter-spacing: 0.04em;
  white-space: nowrap;
}
.badge.is-free {
  background: var(--free);
  color: var(--ink);
}
.badge.is-full {
  border: 0.5mm solid var(--paper);
}
.badge-count {
  font-size: 22pt;
  line-height: 1;
}
.gradient {
  height: 2.5mm;
  margin-bottom: 5mm;
  border-radius: 0 0 2mm 2mm;
  background: var(--brand-gradient);
}

/* ── table: rows are slots, columns shift types ───────────────────────── */
.roster {
  width: 100%;
  border-collapse: collapse;
  table-layout: fixed;
}
.roster + .roster {
  margin-top: 6mm;
}
.col-time {
  width: 26mm;
}
th {
  padding: 0 2.5mm 2mm;
  text-align: left;
  vertical-align: bottom;
  font-size: 12pt;
  font-weight: 900;
  text-transform: uppercase;
  letter-spacing: 0.06em;
  border-bottom: 1mm solid var(--rule);
}
.th-time {
  padding-left: 0;
}
.th-sub {
  display: block;
  margin-top: 0.5mm;
  font-size: 11pt;
  font-weight: 400;
  text-transform: none;
  letter-spacing: 0.01em;
  color: var(--secondary);
}
tr {
  break-inside: avoid;
}
td {
  padding: 2.5mm;
  vertical-align: top;
  border-bottom: 0.35mm solid var(--rule);
  border-left: 0.25mm solid var(--rule-soft);
}
td.time {
  padding-left: 0;
  border-left: none;
}
td.has-free {
  background: var(--free-tint);
}
.time .from {
  display: block;
  font-size: 20pt;
  font-weight: 900;
  line-height: 1;
  font-variant-numeric: tabular-nums;
}
.time .to {
  display: block;
  margin-top: 1mm;
  font-size: 11pt;
  color: var(--secondary);
  font-variant-numeric: tabular-nums;
}
.name {
  font-size: 13pt;
  line-height: 1.3;
  hyphens: auto;
  overflow-wrap: break-word;
}
.free {
  display: inline-block;
  margin-top: 1mm;
  padding: 0.8mm 3mm;
  border: 0.6mm solid var(--ink);
  border-radius: 99mm;
  background: var(--free);
  font-size: 14pt;
  font-weight: 900;
  text-transform: uppercase;
  letter-spacing: 0.06em;
  white-space: nowrap;
}
.name + .free {
  margin-top: 2mm;
}
.none {
  font-size: 13pt;
  color: #9a9a9a;
}
.dense th {
  font-size: 11pt;
  letter-spacing: 0.03em;
}
.dense .name {
  font-size: 11.5pt;
}
.dense .free {
  font-size: 12pt;
  padding: 0.6mm 2.2mm;
}

/* ── footer ───────────────────────────────────────────────────────────── */
.foot {
  margin-top: auto;
  padding-top: 6mm;
  break-inside: avoid;
}
.legend {
  display: flex;
  align-items: center;
  gap: 2.5mm;
  font-size: 11pt;
}
.swatch {
  width: 6mm;
  height: 6mm;
  border-radius: 1.5mm;
  border: 0.5mm solid var(--ink);
  background: var(--free);
}
.cta {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 8mm;
  margin-top: 3mm;
  padding-top: 4mm;
  border-top: 1mm solid var(--rule);
}
.cta-text {
  display: flex;
  flex-direction: column;
  gap: 1.5mm;
  min-width: 0;
}
.cta-title {
  font-size: 18pt;
  font-weight: 900;
  line-height: 1.15;
}
.manual {
  font-size: 13pt;
}
.url {
  font-weight: 900;
  text-decoration: underline;
  text-underline-offset: 0.15em;
}
.asof {
  margin-top: 1mm;
  font-size: 11pt;
  color: var(--secondary);
}
/* 32 mm scans reliably from about 1 m; the white padding is the quiet zone */
.qr {
  flex: none;
  width: 36mm;
  height: 36mm;
  padding: 2mm;
  background: var(--paper);
  border: 0.6mm solid var(--ink);
  border-radius: 2mm;
}
.qr :deep(svg) {
  display: block;
  width: 100%;
  height: 100%;
}

@media print {
  /* scoped rules are unlayered and would beat Tailwind's print:hidden */
  .toolbar {
    display: none;
  }
  .poster-root {
    background: none;
    padding: 0;
    min-height: 0;
  }
  .sheet {
    width: auto;
    /* a bit below the printable height (273 mm), so the footer never spills onto a page alone */
    min-height: 265mm;
    margin: 0;
    padding: 0;
    box-shadow: none;
  }
  .sheet + .sheet {
    break-before: page;
  }
}
</style>

<style>
@media print {
  @page {
    size: A4 portrait;
    margin: 12mm;
  }
  html,
  body {
    background: #fff !important;
  }
}
</style>
