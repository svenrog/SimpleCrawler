<script lang="ts">
import type { Product } from '../../shared/catalog';
import ProductCard from './ProductCard.svelte';
import Pagination from './Pagination.svelte';

let { products, total, path }: { products: Product[]; total: number; path: string } = $props();
</script>

<section class="catalog" aria-label="Products">
    <div class="catalog__toolbar">
        <p class="catalog__count">{total} products</p>
        <label class="catalog__sort">
            Sort
            <select>
                <option value="popular">Most popular</option>
                <option value="price-asc">Price, low to high</option>
                <option value="price-desc">Price, high to low</option>
                <option value="rating">Highest rated</option>
            </select>
        </label>
    </div>
    <div class="catalog__grid">
        {#each products as product (product.id)}
            <ProductCard {product} />
        {/each}
    </div>
    <Pagination {path} />
</section>
