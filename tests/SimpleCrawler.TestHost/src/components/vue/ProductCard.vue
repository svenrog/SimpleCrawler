<script setup lang="ts">
import { computed } from 'vue';
import type { Product } from '../../shared/catalog';

const props = defineProps<{ product: Product }>();
const discounted = computed(() => props.product.price < props.product.listPrice);
const stars = computed(() =>
    Array.from({ length: 5 }, (_, i) => i < Math.floor(props.product.rating))
);
</script>

<template>
    <article class="card" :data-sku="product.id" :data-in-stock="product.inStock">
        <div class="card__media">
            <div class="card__thumb" :style="{ backgroundColor: '#0b5' }" aria-hidden="true"></div>
            <span v-if="discounted" class="card__badge">Sale</span>
            <span v-if="!product.inStock" class="card__badge card__badge--muted">Backorder</span>
        </div>
        <div class="card__body">
            <p class="card__brand">{{ product.brand }}</p>
            <h3 class="card__title">
                <a :href="`#${product.id}`">{{ product.name }}</a>
            </h3>
            <div class="card__meta">
                <span class="stars" :aria-label="`${product.rating} out of 5`">
                    <span v-for="(on, i) in stars" :key="i" :class="on ? 'star star--on' : 'star'">★</span>
                </span>
                <span class="card__reviews">({{ product.reviews }})</span>
            </div>
            <p class="card__blurb">{{ product.blurb }}</p>
            <ul class="card__tags">
                <li v-for="tag in product.tags" :key="tag" class="tag">{{ tag }}</li>
            </ul>
            <div class="card__footer">
                <span class="card__price">
                    <s v-if="discounted" class="card__list-price">{{ product.listPrice }} kr</s>
                    <strong>{{ product.price }} kr</strong>
                </span>
                <button type="button" class="card__buy" :disabled="!product.inStock">
                    {{ product.inStock ? 'Add to cart' : 'Notify me' }}
                </button>
            </div>
        </div>
    </article>
</template>
