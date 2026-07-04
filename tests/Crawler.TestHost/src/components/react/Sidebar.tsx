import type { Facet } from '../../shared/catalog';

export default function Sidebar({ facets }: { facets: Facet[] }) {
    return (
        <aside className="filters" aria-label="Filters">
            {facets.map((facet) => (
                <section key={facet.name} className="filters__group">
                    <h2 className="filters__heading">{facet.name}</h2>
                    <ul>
                        {facet.values.map((value) => (
                            <li key={value.label} className="filters__row">
                                <label>
                                    <input type="checkbox" name={facet.name} value={value.label} />
                                    <span className="filters__label">{value.label}</span>
                                    <span className="filters__count">{value.count}</span>
                                </label>
                            </li>
                        ))}
                    </ul>
                </section>
            ))}
        </aside>
    );
}
