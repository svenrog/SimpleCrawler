<script setup lang="ts">
import { pages } from '../../shared/pages';

defineProps<{ current: string }>();
</script>

<template>
    <header class="masthead">
        <div class="masthead__bar">
            <a class="masthead__logo" href="/">Fjällström Outfitters</a>
            <form class="masthead__search" role="search" @submit.prevent>
                <input type="search" placeholder="Search the range…" aria-label="Search" />
                <button type="submit">Go</button>
            </form>
        </div>
        <nav class="masthead__nav" aria-label="Primary">
            <ul>
                <li
                    v-for="entry in pages"
                    :key="entry.href"
                    :class="{ 'is-active': entry.href === current }"
                >
                    <a :href="entry.href" :aria-current="entry.href === current ? 'page' : undefined">
                        {{ entry.name }}
                    </a>
                </li>
            </ul>
        </nav>
    </header>
</template>
