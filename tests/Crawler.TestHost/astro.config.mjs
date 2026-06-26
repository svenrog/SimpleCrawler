import { defineConfig } from 'astro/config';
import react from '@astrojs/react';
import preact from '@astrojs/preact';
import vue from '@astrojs/vue';
import svelte from '@astrojs/svelte';
import solid from '@astrojs/solid-js';

// Each framework's components live under src/components/<framework>/ so the three
// JSX renderers (react, preact, solid) can be disambiguated by include globs.
// Output goes to wwwroot/<framework>/ and shared chunks to wwwroot/_astro/.
// Asset filenames use hyphen+hash (no internal dots) because the test host's
// EmbeddedResourceRouteResolver turns every dot in a filename into a path separator.
export default defineConfig({
    outDir: './wwwroot',
    build: {
        assets: '_astro',
        inlineStylesheets: 'never',
    },
    integrations: [
        react({ include: ['**/components/react/**'] }),
        preact({ include: ['**/components/preact/**'] }),
        solid({ include: ['**/components/solid/**'] }),
        vue(),
        svelte(),
    ],
    vite: {
        build: {
            sourcemap: false,
            rollupOptions: {
                output: {
                    entryFileNames: '_astro/[name]-[hash].js',
                    chunkFileNames: '_astro/[name]-[hash].js',
                    assetFileNames: '_astro/[name]-[hash][extname]',
                },
            },
        },
    },
});
