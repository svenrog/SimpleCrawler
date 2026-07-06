<script lang="ts">
import { onMount } from 'svelte';
import { getPage, pages } from '../../shared/pages';
import { getCatalog, getFacets, bucketProducts, PAGE_SIZE } from '../../shared/catalog';
import { currentPath, start, subscribe } from '../../shared/router';
import Header from './Header.svelte';
import Sidebar from './Sidebar.svelte';
import Catalog from './Catalog.svelte';
import Footer from './Footer.svelte';
import '../../styles/global.css';

let path = $state(currentPath());
const page = $derived(getPage(path));
const catalog = getCatalog(pages.length * PAGE_SIZE);
const facets = getFacets(catalog);
const index = $derived(pages.findIndex((entry) => entry.href === path));
const products = $derived(bucketProducts(catalog, index));

onMount(() => {
    start();
    return subscribe((next) => {
        if (path !== next) path = next;
    });
});
</script>

<div class="page">
    <Header current={path} />
    <section class="hero" style="background-color: {page.color}">
        <div class="hero__inner">
            <h1 class="hero__title">{@html page.titleHtml}</h1>
            <p class="hero__body">{page.body}</p>
        </div>
    </section>
    <main class="shell">
        <Sidebar {facets} />
        <Catalog products={products} total={catalog.length} {path} />
    </main>
    <Footer />
</div>
