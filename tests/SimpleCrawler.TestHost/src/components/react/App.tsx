import { useEffect, useMemo, useState } from 'react';
import { getPage, pages } from '../../shared/pages';
import { getCatalog, getFacets, bucketProducts, PAGE_SIZE } from '../../shared/catalog';
import { currentPath, start, subscribe } from '../../shared/router';
import Header from './Header';
import Sidebar from './Sidebar';
import Catalog from './Catalog';
import Footer from './Footer';
import '../../styles/global.css';

function CatalogPage({ path }: { path: string }) {
    const page = getPage(path);
    const catalog = useMemo(() => getCatalog(pages.length * PAGE_SIZE), []);
    const facets = useMemo(() => getFacets(catalog), [catalog]);
    const index = pages.findIndex((entry) => entry.href === path);
    const products = useMemo(() => bucketProducts(catalog, index), [catalog, index]);

    return (
        <div className="page">
            <Header current={path} />
            <section className="hero" style={{ backgroundColor: page.color }}>
                <div className="hero__inner">
                    <h1 className="hero__title" dangerouslySetInnerHTML={{ __html: page.titleHtml }} />
                    <p className="hero__body">{page.body}</p>
                </div>
            </section>
            <main className="shell">
                <Sidebar facets={facets} />
                <Catalog products={products} total={catalog.length} path={path} />
            </main>
            <Footer />
        </div>
    );
}

export default function App() {
    const [path, setPath] = useState(currentPath());

    useEffect(() => {
        start();
        return subscribe((next) => setPath((prev) => (prev === next ? prev : next)));
    }, []);

    return <CatalogPage path={path} />;
}
