<script lang="ts">
import { pages, getPage } from '../../shared/pages';

let { path }: { path: string } = $props();
const page = $derived(getPage(path));
</script>

<div class="wrapper" style="background-color: {page.color}">
    <nav class="container">
        <h1 class="title">{@html page.titleHtml}</h1>
        <p class="description">{page.body}</p>
        {#if page.found}
            <ul class="nav">
                {#each pages as entry, index (index)}
                    <li><a href={entry.href}>{entry.name}</a></li>
                {/each}
            </ul>
        {/if}
    </nav>
</div>
