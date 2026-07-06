import { For } from 'solid-js';
import { pages } from '../../shared/pages';

export default function Header(props: { current: string }) {
    return (
        <header class="masthead">
            <div class="masthead__bar">
                <a class="masthead__logo" href="/">
                    Fjällström Outfitters
                </a>
                <form class="masthead__search" role="search" onSubmit={(e) => e.preventDefault()}>
                    <input type="search" placeholder="Search the range…" aria-label="Search" />
                    <button type="submit">Go</button>
                </form>
            </div>
            <nav class="masthead__nav" aria-label="Primary">
                <ul>
                    <For each={pages}>
                        {(entry) => (
                            <li classList={{ 'is-active': entry.href === props.current }}>
                                <a
                                    href={entry.href}
                                    aria-current={entry.href === props.current ? 'page' : undefined}
                                >
                                    {entry.name}
                                </a>
                            </li>
                        )}
                    </For>
                </ul>
            </nav>
        </header>
    );
}
