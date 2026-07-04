<script setup lang="ts">
import { pages } from '../../shared/pages';

const columns = [pages.slice(0, 6), pages.slice(6, 12), pages.slice(12, 18)];
const year = new Date().getFullYear();
</script>

<template>
    <footer class="footer">
        <div class="footer__columns">
            <nav
                v-for="(column, index) in columns"
                :key="index"
                class="footer__column"
                :aria-label="`Footer ${index + 1}`"
            >
                <ul>
                    <li v-for="entry in column" :key="entry.href">
                        <a :href="entry.href">{{ entry.name }}</a>
                    </li>
                </ul>
            </nav>
        </div>
        <p class="footer__legal">© {{ year }} Fjällström Outfitters — test fixture.</p>
    </footer>
</template>
