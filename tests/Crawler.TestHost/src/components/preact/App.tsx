import { useEffect, useState } from 'preact/hooks';
import { pages, getPage } from '../../shared/pages';
import { currentPath, start, subscribe } from '../../shared/router';
import {
    enterClassName,
    exitClassName,
    initialClassName,
    pageTransitionDuration,
} from '../../shared/transition';
import '../../styles/global.css';

function PageView({ path }: { path: string }) {
    const page = getPage(path);
    return (
        <div className="wrapper" style={{ backgroundColor: page.color }}>
            <nav className="container">
                <h1 className="title" dangerouslySetInnerHTML={{ __html: page.titleHtml }} />
                <p className="description">{page.body}</p>
                {page.found && (
                    <ul className="nav">
                        {pages.map((entry, index) => (
                            <li key={index}>
                                <a href={entry.href}>{entry.name}</a>
                            </li>
                        ))}
                    </ul>
                )}
            </nav>
        </div>
    );
}

export default function App() {
    const [path, setPath] = useState(currentPath());
    const [previous, setPrevious] = useState<string | null>(null);
    const [initial, setInitial] = useState(true);

    useEffect(() => {
        start();
        return subscribe((next) => {
            setPath((prev) => {
                if (prev === next) return prev;
                setInitial(false);
                setPrevious(prev);
                window.setTimeout(
                    () => setPrevious((current) => (current === prev ? null : current)),
                    pageTransitionDuration
                );
                return next;
            });
        });
    }, []);

    return (
        <div className="transition-group">
            <div key={path} className={initial ? initialClassName : enterClassName}>
                <PageView path={path} />
            </div>
            {previous !== null && (
                <div key={previous} className={exitClassName}>
                    <PageView path={previous} />
                </div>
            )}
        </div>
    );
}
