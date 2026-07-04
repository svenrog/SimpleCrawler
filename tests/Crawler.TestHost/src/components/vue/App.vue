<script setup lang="ts">
import { computed, onMounted, ref } from 'vue';
import { getPage } from '../../shared/pages';
import { getCatalog, getFacets } from '../../shared/catalog';
import { currentPath, start, subscribe } from '../../shared/router';
import Header from './Header.vue';
import Sidebar from './Sidebar.vue';
import Catalog from './Catalog.vue';
import Footer from './Footer.vue';
import '../../styles/global.css';

const path = ref(currentPath());
const page = computed(() => getPage(path.value));
const products = computed(() => getCatalog(path.value));
const facets = computed(() => getFacets(products.value));

onMounted(() => {
    start();
    subscribe((next) => {
        if (path.value !== next) path.value = next;
    });
});
</script>

<template>
    <div class="page">
        <Header :current="path" />
        <section class="hero" :style="{ backgroundColor: page.color }">
            <div class="hero__inner">
                <h1 class="hero__title" v-html="page.titleHtml"></h1>
                <p class="hero__body">{{ page.body }}</p>
            </div>
        </section>
        <main class="shell">
            <Sidebar :facets="facets" />
            <Catalog :products="products" />
        </main>
        <Footer />
    </div>
</template>
