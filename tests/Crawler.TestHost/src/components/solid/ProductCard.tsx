import { For, Show } from 'solid-js';
import type { Product } from '../../shared/catalog';

function Stars(props: { rating: number }) {
    const full = () => Math.floor(props.rating);
    return (
        <span class="stars" aria-label={`${props.rating} out of 5`}>
            <For each={Array.from({ length: 5 }, (_, i) => i)}>
                {(i) => <span class={i < full() ? 'star star--on' : 'star'}>★</span>}
            </For>
        </span>
    );
}

export default function ProductCard(props: { product: Product }) {
    const discounted = () => props.product.price < props.product.listPrice;
    return (
        <article class="card" data-sku={props.product.id} data-in-stock={props.product.inStock}>
            <div class="card__media">
                <div class="card__thumb" style={{ 'background-color': '#0b5' }} aria-hidden="true" />
                <Show when={discounted()}>
                    <span class="card__badge">Sale</span>
                </Show>
                <Show when={!props.product.inStock}>
                    <span class="card__badge card__badge--muted">Backorder</span>
                </Show>
            </div>
            <div class="card__body">
                <p class="card__brand">{props.product.brand}</p>
                <h3 class="card__title">
                    <a href={`#${props.product.id}`}>{props.product.name}</a>
                </h3>
                <div class="card__meta">
                    <Stars rating={props.product.rating} />
                    <span class="card__reviews">({props.product.reviews})</span>
                </div>
                <p class="card__blurb">{props.product.blurb}</p>
                <ul class="card__tags">
                    <For each={props.product.tags}>{(tag) => <li class="tag">{tag}</li>}</For>
                </ul>
                <div class="card__footer">
                    <span class="card__price">
                        <Show when={discounted()}>
                            <s class="card__list-price">{props.product.listPrice} kr</s>
                        </Show>
                        <strong>{props.product.price} kr</strong>
                    </span>
                    <button type="button" class="card__buy" disabled={!props.product.inStock}>
                        {props.product.inStock ? 'Add to cart' : 'Notify me'}
                    </button>
                </div>
            </div>
        </article>
    );
}
