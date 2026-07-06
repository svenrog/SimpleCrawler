import { pages } from '../../shared/pages';

export default function Header({ current }: { current: string }) {
    return (
        <header className="masthead">
            <div className="masthead__bar">
                <a className="masthead__logo" href="/">
                    Fjällström Outfitters
                </a>
                <form className="masthead__search" role="search" onSubmit={(e) => e.preventDefault()}>
                    <input type="search" placeholder="Search the range…" aria-label="Search" />
                    <button type="submit">Go</button>
                </form>
            </div>
            <nav className="masthead__nav" aria-label="Primary">
                <ul>
                    {pages.map((entry) => (
                        <li key={entry.href} className={entry.href === current ? 'is-active' : undefined}>
                            <a href={entry.href} aria-current={entry.href === current ? 'page' : undefined}>
                                {entry.name}
                            </a>
                        </li>
                    ))}
                </ul>
            </nav>
        </header>
    );
}
