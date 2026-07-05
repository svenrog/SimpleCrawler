<script lang="ts">
import { pages } from '../../shared/pages';

let { path }: { path: string } = $props();
const index = $derived(pages.findIndex((entry) => entry.href === path));
</script>

{#if pages.length > 1}
    <nav class="pagination" aria-label="Pagination">
        {#if index > 0}
            <a class="pagination__link" href={pages[index - 1].href} rel="prev">Previous</a>
        {:else}
            <span class="pagination__link is-disabled" aria-disabled="true">Previous</span>
        {/if}
        {#each pages as entry, i (entry.href)}
            {#if i === index}
                <span class="pagination__link is-current" aria-current="page">{i + 1}</span>
            {:else}
                <a class="pagination__link" href={entry.href}>{i + 1}</a>
            {/if}
        {/each}
        {#if index >= 0 && index < pages.length - 1}
            <a class="pagination__link" href={pages[index + 1].href} rel="next">Next</a>
        {:else}
            <span class="pagination__link is-disabled" aria-disabled="true">Next</span>
        {/if}
    </nav>
{/if}
