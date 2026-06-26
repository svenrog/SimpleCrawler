import { createMemo, createSignal, onMount, For, Show } from 'solid-js';
import { pages, getPage } from '../../shared/pages';
import { currentPath, start, subscribe } from '../../shared/router';
import {
    enterClassName,
    exitClassName,
    initialClassName,
    pageTransitionDuration,
} from '../../shared/transition';
import '../../styles/global.css';

function PageView(props: { path: string }) {
    const page = createMemo(() => getPage(props.path));
    return (
        <div class="wrapper" style={{ 'background-color': page().color }}>
            <nav class="container">
                <h1 class="title" innerHTML={page().titleHtml} />
                <p class="description">{page().body}</p>
                <Show when={page().found}>
                    <ul class="nav">
                        <For each={pages}>
                            {(entry) => (
                                <li>
                                    <a href={entry.href}>{entry.name}</a>
                                </li>
                            )}
                        </For>
                    </ul>
                </Show>
            </nav>
        </div>
    );
}

export default function App() {
    const [path, setPath] = createSignal(currentPath());
    const [previous, setPrevious] = createSignal<string | null>(null);
    const [initial, setInitial] = createSignal(true);

    onMount(() => {
        start();
        subscribe((next) => {
            const prev = path();
            if (prev === next) return;
            setInitial(false);
            setPath(next);
            setPrevious(prev);
            window.setTimeout(
                () => setPrevious((current) => (current === prev ? null : current)),
                pageTransitionDuration
            );
        });
    });

    return (
        <div class="transition-group">
            <Show keyed when={path()}>
                {(value) => (
                    <div class={initial() ? initialClassName : enterClassName}>
                        <PageView path={value} />
                    </div>
                )}
            </Show>
            <Show keyed when={previous()}>
                {(value) => (
                    <div class={exitClassName}>
                        <PageView path={value} />
                    </div>
                )}
            </Show>
        </div>
    );
}
