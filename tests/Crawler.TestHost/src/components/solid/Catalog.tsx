import { For } from 'solid-js';
import type { Product } from '../../shared/catalog';
import ProductCard from './ProductCard';

export default function Catalog(props: { products: Product[] }) {
    return (
        <section class="catalog" aria-label="Products">
            <div class="catalog__toolbar">
                <p class="catalog__count">{props.products.length} products</p>
                <label class="catalog__sort">
                    Sort
                    <select value="popular">
                        <option value="popular">Most popular</option>
                        <option value="price-asc">Price, low to high</option>
                        <option value="price-desc">Price, high to low</option>
                        <option value="rating">Highest rated</option>
                    </select>
                </label>
            </div>
            <div class="catalog__grid">
                <For each={props.products}>{(product) => <ProductCard product={product} />}</For>
            </div>
        </section>
    );
}
