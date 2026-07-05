<script setup lang="ts">
import { computed, onMounted, ref } from 'vue';
import { getPage, pages } from '../../shared/pages';
import { getCatalog, getFacets, bucketProducts, PAGE_SIZE } from '../../shared/catalog';
import { currentPath, start, subscribe } from '../../shared/router';
import Header from './Header.vue';
import Sidebar from './Sidebar.vue';
import Catalog from './Catalog.vue';
import Footer from './Footer.vue';
import '../../styles/global.css';

const path = ref(currentPath());
const page = computed(() => getPage(path.value));
const catalog = getCatalog(pages.length * PAGE_SIZE);
const facets = getFacets(catalog);
const index = computed(() => pages.findIndex((entry) => entry.href === path.value));
const products = computed(() => bucketProducts(catalog, index.value));

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
            <Catalog :products="products" :total="catalog.length" :path="path" />
        </main>
        <Footer />
    </div>
</template>
