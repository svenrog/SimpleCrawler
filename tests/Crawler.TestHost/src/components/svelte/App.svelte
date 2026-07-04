<script lang="ts">
import { onMount } from 'svelte';
import { getPage } from '../../shared/pages';
import { getCatalog, getFacets } from '../../shared/catalog';
import { currentPath, start, subscribe } from '../../shared/router';
import Header from './Header.svelte';
import Sidebar from './Sidebar.svelte';
import Catalog from './Catalog.svelte';
import Footer from './Footer.svelte';
import '../../styles/global.css';

let path = $state(currentPath());
const page = $derived(getPage(path));
const products = $derived(getCatalog(path));
const facets = $derived(getFacets(products));

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
        <Catalog {products} />
    </main>
    <Footer />
</div>
