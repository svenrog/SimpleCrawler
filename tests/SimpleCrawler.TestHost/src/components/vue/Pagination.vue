<script setup lang="ts">
import { computed } from 'vue';
import { pages } from '../../shared/pages';

const props = defineProps<{ path: string }>();
const index = computed(() => pages.findIndex((entry) => entry.href === props.path));
</script>

<template>
    <nav v-if="pages.length > 1" class="pagination" aria-label="Pagination">
        <a v-if="index > 0" class="pagination__link" :href="pages[index - 1].href" rel="prev">Previous</a>
        <span v-else class="pagination__link is-disabled" aria-disabled="true">Previous</span>
        <template v-for="(entry, i) in pages" :key="entry.href">
            <span v-if="i === index" class="pagination__link is-current" aria-current="page">{{ i + 1 }}</span>
            <a v-else class="pagination__link" :href="entry.href">{{ i + 1 }}</a>
        </template>
        <a v-if="index >= 0 && index < pages.length - 1" class="pagination__link" :href="pages[index + 1].href" rel="next">Next</a>
        <span v-else class="pagination__link is-disabled" aria-disabled="true">Next</span>
    </nav>
</template>
