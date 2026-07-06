import { pages } from '../../shared/pages';

export default function Pagination({ path }: { path: string }) {
    if (pages.length <= 1) return null;

    const index = pages.findIndex((entry) => entry.href === path);

    return (
        <nav className="pagination" aria-label="Pagination">
            {index > 0 ? (
                <a className="pagination__link" href={pages[index - 1].href} rel="prev">
                    Previous
                </a>
            ) : (
                <span className="pagination__link is-disabled" aria-disabled="true">
                    Previous
                </span>
            )}
            {pages.map((entry, i) =>
                i === index ? (
                    <span key={entry.href} className="pagination__link is-current" aria-current="page">
                        {i + 1}
                    </span>
                ) : (
                    <a key={entry.href} className="pagination__link" href={entry.href}>
                        {i + 1}
                    </a>
                )
            )}
            {index >= 0 && index < pages.length - 1 ? (
                <a className="pagination__link" href={pages[index + 1].href} rel="next">
                    Next
                </a>
            ) : (
                <span className="pagination__link is-disabled" aria-disabled="true">
                    Next
                </span>
            )}
        </nav>
    );
}
