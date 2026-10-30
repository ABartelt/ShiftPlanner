<script setup lang="ts">
import { computed } from 'vue'

/* LIQUIDASI has no umlauts or ß; the manual asks to set the dots by hand. Umlauts are rendered
   as the base letter with two dots drawn above it, ß as "ss". */
const props = defineProps<{ text: string }>()

const BASE: Record<string, string> = { ä: 'a', ö: 'o', ü: 'u', Ä: 'A', Ö: 'O', Ü: 'U' }

const parts = computed(() =>
  props.text
    .replace(/ß/g, 'ss')
    .split(/([äöüÄÖÜ])/)
    .filter(Boolean)
    .map((part) => ({ text: BASE[part] ?? part, umlaut: part in BASE })),
)
</script>

<template>
  <span class="sr-only">{{ text }}</span>
  <span aria-hidden="true"
    ><template v-for="(part, i) in parts" :key="i"
      ><span v-if="part.umlaut" class="umlaut">{{ part.text }}</span
      ><template v-else>{{ part.text }}</template></template
    ></span
  >
</template>

<style scoped>
/* Two dots the size of LIQUIDASI's full stop (0.167em), just above its x-height (0.681em). */
.umlaut {
  position: relative;
}
.umlaut::before {
  content: '';
  position: absolute;
  bottom: 0.93em;
  left: calc(50% - 0.27em);
  width: 0.167em;
  height: 0.167em;
  border-radius: 0.05em;
  background: currentColor;
  box-shadow: 0.33em 0 0 currentColor;
}
</style>
