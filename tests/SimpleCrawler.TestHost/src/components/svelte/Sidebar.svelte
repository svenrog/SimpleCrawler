<script lang="ts">
import type { Facet } from '../../shared/catalog';

let { facets }: { facets: Facet[] } = $props();
</script>

<aside class="filters" aria-label="Filters">
    {#each facets as facet (facet.name)}
        <section class="filters__group">
            <h2 class="filters__heading">{facet.name}</h2>
            <ul>
                {#each facet.values as value (value.label)}
                    <li class="filters__row">
                        <label>
                            <input type="checkbox" name={facet.name} value={value.label} />
                            <span class="filters__label">{value.label}</span>
                            <span class="filters__count">{value.count}</span>
                        </label>
                    </li>
                {/each}
            </ul>
        </section>
    {/each}
</aside>
