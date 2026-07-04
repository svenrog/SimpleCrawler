<script lang="ts">
import { pages } from '../../shared/pages';

let { current }: { current: string } = $props();
</script>

<header class="masthead">
    <div class="masthead__bar">
        <a class="masthead__logo" href="/">Fjällström Outfitters</a>
        <form class="masthead__search" role="search" onsubmit={(e) => e.preventDefault()}>
            <input type="search" placeholder="Search the range…" aria-label="Search" />
            <button type="submit">Go</button>
        </form>
    </div>
    <nav class="masthead__nav" aria-label="Primary">
        <ul>
            {#each pages as entry (entry.href)}
                <li class:is-active={entry.href === current}>
                    <a href={entry.href} aria-current={entry.href === current ? 'page' : undefined}>
                        {entry.name}
                    </a>
                </li>
            {/each}
        </ul>
    </nav>
</header>
