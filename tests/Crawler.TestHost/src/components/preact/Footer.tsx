import { pages } from '../../shared/pages';

export default function Footer() {
    const columns = [pages.slice(0, 6), pages.slice(6, 12), pages.slice(12, 18)];
    return (
        <footer className="footer">
            <div className="footer__columns">
                {columns.map((column, index) => (
                    <nav key={index} className="footer__column" aria-label={`Footer ${index + 1}`}>
                        <ul>
                            {column.map((entry) => (
                                <li key={entry.href}>
                                    <a href={entry.href}>{entry.name}</a>
                                </li>
                            ))}
                        </ul>
                    </nav>
                ))}
            </div>
            <p className="footer__legal">© {new Date().getFullYear()} Fjällström Outfitters — test fixture.</p>
        </footer>
    );
}
