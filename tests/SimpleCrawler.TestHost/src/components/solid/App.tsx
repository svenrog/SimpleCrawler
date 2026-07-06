import { createMemo, createSignal, onMount } from 'solid-js';
import { getPage, pages } from '../../shared/pages';
import { getCatalog, getFacets, bucketProducts, PAGE_SIZE } from '../../shared/catalog';
import { currentPath, start, subscribe } from '../../shared/router';
import Header from './Header';
import Sidebar from './Sidebar';
import Catalog from './Catalog';
import Footer from './Footer';
import '../../styles/global.css';

function CatalogPage(props: { path: string }) {
    const page = createMemo(() => getPage(props.path));
    const catalog = createMemo(() => getCatalog(pages.length * PAGE_SIZE));
    const facets = createMemo(() => getFacets(catalog()));
    const index = createMemo(() => pages.findIndex((entry) => entry.href === props.path));
    const products = createMemo(() => bucketProducts(catalog(), index()));

    return (
        <div class="page">
            <Header current={props.path} />
            <section class="hero" style={{ 'background-color': page().color }}>
                <div class="hero__inner">
                    <h1 class="hero__title" innerHTML={page().titleHtml} />
                    <p class="hero__body">{page().body}</p>
                </div>
            </section>
            <main class="shell">
                <Sidebar facets={facets()} />
                <Catalog products={products()} total={catalog().length} path={props.path} />
            </main>
            <Footer />
        </div>
    );
}

export default function App() {
    const [path, setPath] = createSignal(currentPath());

    onMount(() => {
        start();
        subscribe((next) => setPath((prev) => (prev === next ? prev : next)));
    });

    return <CatalogPage path={path()} />;
}
