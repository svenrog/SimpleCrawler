<script setup lang="ts">
import { computed } from 'vue';
import { pages, getPage } from '../../shared/pages';

const props = defineProps<{ path: string }>();
const page = computed(() => getPage(props.path));
</script>

<template>
    <div class="wrapper" :style="{ backgroundColor: page.color }">
        <nav class="container">
            <h1 class="title" v-html="page.titleHtml"></h1>
            <p class="description">{{ page.body }}</p>
            <ul v-if="page.found" class="nav">
                <li v-for="(entry, index) in pages" :key="index">
                    <a :href="entry.href">{{ entry.name }}</a>
                </li>
            </ul>
        </nav>
    </div>
</template>
