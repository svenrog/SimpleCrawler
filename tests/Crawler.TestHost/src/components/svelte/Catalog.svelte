<script lang="ts">
import type { Product } from '../../shared/catalog';
import ProductCard from './ProductCard.svelte';

let { products }: { products: Product[] } = $props();
</script>

<section class="catalog" aria-label="Products">
    <div class="catalog__toolbar">
        <p class="catalog__count">{products.length} products</p>
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
</section>
