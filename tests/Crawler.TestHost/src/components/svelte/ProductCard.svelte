<script lang="ts">
import type { Product } from '../../shared/catalog';

let { product }: { product: Product } = $props();
const discounted = $derived(product.price < product.listPrice);
const stars = $derived(Array.from({ length: 5 }, (_, i) => i < Math.floor(product.rating)));
</script>

<article class="card" data-sku={product.id} data-in-stock={product.inStock}>
    <div class="card__media">
        <div class="card__thumb" style="background-color: #0b5" aria-hidden="true"></div>
        {#if discounted}<span class="card__badge">Sale</span>{/if}
        {#if !product.inStock}<span class="card__badge card__badge--muted">Backorder</span>{/if}
    </div>
    <div class="card__body">
        <p class="card__brand">{product.brand}</p>
        <h3 class="card__title"><a href={`#${product.id}`}>{product.name}</a></h3>
        <div class="card__meta">
            <span class="stars" aria-label={`${product.rating} out of 5`}>
                {#each stars as on, i (i)}
                    <span class={on ? 'star star--on' : 'star'}>★</span>
                {/each}
            </span>
            <span class="card__reviews">({product.reviews})</span>
        </div>
        <p class="card__blurb">{product.blurb}</p>
        <ul class="card__tags">
            {#each product.tags as tag (tag)}
                <li class="tag">{tag}</li>
            {/each}
        </ul>
        <div class="card__footer">
            <span class="card__price">
                {#if discounted}<s class="card__list-price">{product.listPrice} kr</s>{/if}
                <strong>{product.price} kr</strong>
            </span>
            <button type="button" class="card__buy" disabled={!product.inStock}>
                {product.inStock ? 'Add to cart' : 'Notify me'}
            </button>
        </div>
    </div>
</article>
