import type { Product } from '../../shared/catalog';
import ProductCard from './ProductCard';

export default function Catalog({ products }: { products: Product[] }) {
    return (
        <section className="catalog" aria-label="Products">
            <div className="catalog__toolbar">
                <p className="catalog__count">{products.length} products</p>
                <label className="catalog__sort">
                    Sort
                    <select defaultValue="popular">
                        <option value="popular">Most popular</option>
                        <option value="price-asc">Price, low to high</option>
                        <option value="price-desc">Price, high to low</option>
                        <option value="rating">Highest rated</option>
                    </select>
                </label>
            </div>
            <div className="catalog__grid">
                {products.map((product) => (
                    <ProductCard key={product.id} product={product} />
                ))}
            </div>
        </section>
    );
}
