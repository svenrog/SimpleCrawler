<script lang="ts">
import { onMount } from 'svelte';
import PageView from './PageView.svelte';
import { currentPath, start, subscribe } from '../../shared/router';
import {
    enterClassName,
    exitClassName,
    initialClassName,
    pageTransitionDuration,
} from '../../shared/transition';
import '../../styles/global.css';

let path = $state(currentPath());
let previous = $state<string | null>(null);
let initial = $state(true);

onMount(() => {
    start();
    return subscribe((next) => {
        const prev = path;
        if (prev === next) return;
        initial = false;
        path = next;
        previous = prev;
        window.setTimeout(() => {
            if (previous === prev) previous = null;
        }, pageTransitionDuration);
    });
});
</script>

<div class="transition-group">
    {#key path}
        <div class={initial ? initialClassName : enterClassName}>
            <PageView {path} />
        </div>
    {/key}
    {#if previous !== null}
        {#key previous}
            <div class={exitClassName}>
                <PageView path={previous} />
            </div>
        {/key}
    {/if}
</div>
