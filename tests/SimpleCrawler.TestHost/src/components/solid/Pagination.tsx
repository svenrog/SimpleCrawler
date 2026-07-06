import { For, Show } from 'solid-js';
import { pages } from '../../shared/pages';

export default function Pagination(props: { path: string }) {
    const index = () => pages.findIndex((entry) => entry.href === props.path);

    return (
        <Show when={pages.length > 1}>
            <nav class="pagination" aria-label="Pagination">
                <Show
                    when={index() > 0}
                    fallback={
                        <span class="pagination__link is-disabled" aria-disabled="true">
                            Previous
                        </span>
                    }
                >
                    <a class="pagination__link" href={pages[index() - 1].href} rel="prev">
                        Previous
                    </a>
                </Show>
                <For each={pages}>
                    {(entry, i) => (
                        <Show
                            when={i() !== index()}
                            fallback={
                                <span class="pagination__link is-current" aria-current="page">
                                    {i() + 1}
                                </span>
                            }
                        >
                            <a class="pagination__link" href={entry.href}>
                                {i() + 1}
                            </a>
                        </Show>
                    )}
                </For>
                <Show
                    when={index() >= 0 && index() < pages.length - 1}
                    fallback={
                        <span class="pagination__link is-disabled" aria-disabled="true">
                            Next
                        </span>
                    }
                >
                    <a class="pagination__link" href={pages[index() + 1].href} rel="next">
                        Next
                    </a>
                </Show>
            </nav>
        </Show>
    );
}
