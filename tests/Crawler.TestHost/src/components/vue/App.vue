<script setup lang="ts">
import { onMounted, ref } from 'vue';
import PageView from './PageView.vue';
import { currentPath, start, subscribe } from '../../shared/router';
import {
    enterClassName,
    exitClassName,
    initialClassName,
    pageTransitionDuration,
} from '../../shared/transition';
import '../../styles/global.css';

const path = ref(currentPath());
const previous = ref<string | null>(null);
const initial = ref(true);

onMounted(() => {
    start();
    subscribe((next) => {
        const prev = path.value;
        if (prev === next) return;
        initial.value = false;
        path.value = next;
        previous.value = prev;
        window.setTimeout(() => {
            if (previous.value === prev) previous.value = null;
        }, pageTransitionDuration);
    });
});
</script>

<template>
    <div class="transition-group">
        <div :key="path" :class="initial ? initialClassName : enterClassName">
            <PageView :path="path" />
        </div>
        <div v-if="previous !== null" :key="previous" :class="exitClassName">
            <PageView :path="previous" />
        </div>
    </div>
</template>
