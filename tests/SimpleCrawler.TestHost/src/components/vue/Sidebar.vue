<script setup lang="ts">
import type { Facet } from '../../shared/catalog';

defineProps<{ facets: Facet[] }>();
</script>

<template>
    <aside class="filters" aria-label="Filters">
        <section v-for="facet in facets" :key="facet.name" class="filters__group">
            <h2 class="filters__heading">{{ facet.name }}</h2>
            <ul>
                <li v-for="value in facet.values" :key="value.label" class="filters__row">
                    <label>
                        <input type="checkbox" :name="facet.name" :value="value.label" />
                        <span class="filters__label">{{ value.label }}</span>
                        <span class="filters__count">{{ value.count }}</span>
                    </label>
                </li>
            </ul>
        </section>
    </aside>
</template>
