import { For } from 'solid-js';
import type { Facet } from '../../shared/catalog';

export default function Sidebar(props: { facets: Facet[] }) {
    return (
        <aside class="filters" aria-label="Filters">
            <For each={props.facets}>
                {(facet) => (
                    <section class="filters__group">
                        <h2 class="filters__heading">{facet.name}</h2>
                        <ul>
                            <For each={facet.values}>
                                {(value) => (
                                    <li class="filters__row">
                                        <label>
                                            <input type="checkbox" name={facet.name} value={value.label} />
                                            <span class="filters__label">{value.label}</span>
                                            <span class="filters__count">{value.count}</span>
                                        </label>
                                    </li>
                                )}
                            </For>
                        </ul>
                    </section>
                )}
            </For>
        </aside>
    );
}
