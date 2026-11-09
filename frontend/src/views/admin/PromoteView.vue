<script setup lang="ts">
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import { useRoute, useRouter } from 'vue-router'
import PageHeader from '@/components/PageHeader.vue'
import ShareTab from '@/components/promote/ShareTab.vue'
import ChannelsTab from '@/components/promote/ChannelsTab.vue'
import ScheduleTab from '@/components/promote/ScheduleTab.vue'
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs'
import { useSeasonStore } from '@/stores/season'

const { t } = useI18n()
const route = useRoute()
const router = useRouter()
const season = useSeasonStore()

const tabs = ['share', 'channels', 'schedule'] as const
const tab = computed(() =>
  tabs.includes(route.query.tab as (typeof tabs)[number]) ? String(route.query.tab) : 'share',
)
</script>

<template>
  <div class="mx-auto max-w-6xl">
    <PageHeader
      :title="t('promote_title')"
      :subtitle="t('promote_subtitle', { season: season.current?.name ?? '' })"
    />
    <Tabs
      :model-value="tab"
      @update:model-value="router.replace({ query: { ...route.query, tab: String($event) } })"
    >
      <TabsList class="mb-4">
        <TabsTrigger v-for="x in tabs" :key="x" :value="x">{{ t(`promote_tab_${x}`) }}</TabsTrigger>
      </TabsList>
      <TabsContent value="share"><ShareTab /></TabsContent>
      <TabsContent value="channels"><ChannelsTab /></TabsContent>
      <TabsContent value="schedule"><ScheduleTab /></TabsContent>
    </Tabs>
  </div>
</template>
